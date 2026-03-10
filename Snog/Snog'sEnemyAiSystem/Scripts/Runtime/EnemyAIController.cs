using System;
using UnityEngine;
using UnityEngine.AI;

namespace SnogTools.AI
{
    public enum AIState
    {
        Idle,
        Patrol,
        Investigate,
        Chase,
        Search
    }

    public enum PatrolMode
    {
        Waypoints,
        Random
    }

    public enum RandomPatrolCenterMode
    {
        Self,
        Transform,
        StaticPoint
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAIController : MonoBehaviour
    {
        [Header("References")]
        public VisionSensor vision;
        public HearingSensor hearing;
        public ThreatMemory memory;

        [Header("Patrolling")]
        public PatrolMode patrolMode = PatrolMode.Waypoints;
        [Tooltip("How close the agent must be to consider the patrol destination reached.")]
        public float waypointTolerance = 0.8f;
        [Tooltip("Base wait time at a patrol destination (waypoint or random point).")]
        public float waitAtDestination = 1.5f;

        [Header("Patrol: Waypoints Mode")]
        [Tooltip("Ordered patrol points. Used when Patrol Mode = Waypoints.")]
        public Transform[] patrolPoints;

        [Header("Patrol: Random Mode")]
        [Tooltip("How we choose the center of the random patrol area.")]
        public RandomPatrolCenterMode randomCenterMode = RandomPatrolCenterMode.Self;
        [Tooltip("Center Transform, used only when Random Center Mode = Transform.")]
        public Transform randomCenterTransform;
        [Tooltip("Static world position, used only when Random Center Mode = StaticPoint.")]
        public Vector3 randomCenterPoint;
        [Tooltip("Radius of the random patrol area around the chosen center.")]
        [Min(0.1f)]
        public float randomPatrolRadius = 12f;
        [Tooltip("Minimum wait time at a random patrol point.")]
        [Min(0f)]
        public float randomWaitMin = 0.8f;
        [Tooltip("Maximum wait time at a random patrol point.")]
        [Min(0f)]
        public float randomWaitMax = 2.0f;
        [Tooltip("Maximum attempts to find a valid random ground point per selection.")]
        [Min(1)]
        public int randomMaxAttempts = 8;
        [Tooltip("How far down to raycast to find ground beneath the sampled NavMesh point.")]
        [Min(0.1f)]
        public float groundRaycastDepth = 8f;
        [Tooltip("LayerMask for what counts as 'ground'. The random patrol point will be clamped onto this layer via downward raycast.")]
        public LayerMask groundLayer;

        [Header("Investigate/Search")]
        public float investigateDuration = 3f;
        public float searchRadius = 5f;
        public float searchDuration = 6f;

        [Header("Chase")]
        public float repathInterval = 0.2f;
        public float giveUpAfterSeconds = 6f;

        public event Action<AIState, AIState> OnStateChanged;

        [Header("Gizmos / Debug")]
        [Tooltip("Draw the random patrol area disc when Patrol Mode = Random.")]
        public bool gizmoShowRandomPatrolArea = true;
        [Tooltip("Draw a small sphere on the last random destination chosen.")]
        public bool gizmoShowRandomLastPoint = true;
        [Tooltip("Draw waypoint markers and lines when Patrol Mode = Waypoints.")]
        public bool gizmoShowWaypoints = true;

        private NavMeshAgent _agent;
        private AIState _state;
        private float _stateTimer;
        private int _patrolIndex;
        private float _nextRepathTime;
        private float _currentWaitTarget;
        private Vector3 _lastRandomPoint;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoBraking = true;
            _agent.updateRotation = true;

            if (vision == null) vision = GetComponent<VisionSensor>();
            if (hearing == null) hearing = GetComponent<HearingSensor>();
            if (memory == null) memory = GetComponent<ThreatMemory>();

            // Clamp wait range correctness
            if (randomWaitMax < randomWaitMin)
            {
                randomWaitMax = randomWaitMin;
            }
        }

        private void OnEnable()
        {
            if (vision != null)
            {
                vision.OnTargetSpotted += HandleTargetSpotted;
                vision.OnTargetLost += HandleTargetLost;
            }
            if (hearing != null)
            {
                hearing.OnHeardSound += HandleHeardSound;
            }
        }

        private void OnDisable()
        {
            if (vision != null)
            {
                vision.OnTargetSpotted -= HandleTargetSpotted;
                vision.OnTargetLost -= HandleTargetLost;
            }
            if (hearing != null)
            {
                hearing.OnHeardSound -= HandleHeardSound;
            }
        }

        private void Start()
        {
            // Start state: Patrol if configured; otherwise Idle
            bool canPatrol =
                (patrolMode == PatrolMode.Waypoints && patrolPoints != null && patrolPoints.Length > 0)
                || (patrolMode == PatrolMode.Random);

            SetState(canPatrol ? AIState.Patrol : AIState.Idle);
        }

        private void Update()
        {
            memory?.Tick();

            switch (_state)
            {
                case AIState.Idle:
                    UpdateIdle();
                    break;
                case AIState.Patrol:
                    UpdatePatrol();
                    break;
                case AIState.Investigate:
                    UpdateInvestigate();
                    break;
                case AIState.Chase:
                    UpdateChase();
                    break;
                case AIState.Search:
                    UpdateSearch();
                    break;
            }
        }

        private void UpdateIdle()
        {
            // reserved for idle behaviors/animations
        }

        private void UpdatePatrol()
        {
            if (patrolMode == PatrolMode.Waypoints)
            {
                UpdatePatrol_Waypoints();
            }
            else
            {
                UpdatePatrol_Random();
            }
        }

        #region Waypoints Patrol
        private void UpdatePatrol_Waypoints()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                // Fallback: switch to random if configured that way
                if (patrolMode == PatrolMode.Random)
                {
                    UpdatePatrol_Random();
                }
                return;
            }

            if (!_agent.pathPending && _agent.remainingDistance <= waypointTolerance)
            {
                _stateTimer += Time.deltaTime;

                if (_stateTimer >= waitAtDestination)
                {
                    _stateTimer = 0f;
                    _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                    MoveTo(patrolPoints[_patrolIndex].position);
                }
            }
            else if (!_agent.hasPath)
            {
                MoveTo(patrolPoints[_patrolIndex].position);
            }
        }
        #endregion

        #region Random Patrol
        private void UpdatePatrol_Random()
        {
            // If we reached / have no path, handle waiting then pick another random point on ground.
            if (!_agent.hasPath || (!_agent.pathPending && _agent.remainingDistance <= waypointTolerance))
            {
                _stateTimer += Time.deltaTime;

                // If we just arrived, create a wait window (randomized for natural behavior)
                if (_currentWaitTarget <= 0f)
                {
                    _currentWaitTarget = UnityEngine.Random.Range(randomWaitMin, randomWaitMax);
                }

                if (_stateTimer >= _currentWaitTarget)
                {
                    _stateTimer = 0f;
                    _currentWaitTarget = 0f;

                    if (TryGetRandomGroundPoint(out Vector3 dest))
                    {
                        _lastRandomPoint = dest;
                        MoveTo(dest);
                    }
                    else
                    {
                        // If we fail to find a point, wait a tad and try again next frame
                        _stateTimer = 0f;
                        _currentWaitTarget = 0.2f;
                    }
                }
            }
        }

        private bool TryGetRandomGroundPoint(out Vector3 result)
        {
            Vector3 center = GetRandomPatrolCenter();

            for (int attempt = 0; attempt < randomMaxAttempts; attempt++)
            {
                // Sample a random horizontal direction in the radius
                Vector2 rnd2 = UnityEngine.Random.insideUnitCircle * randomPatrolRadius;
                Vector3 candidate = new Vector3(center.x + rnd2.x, center.y + 2.5f, center.z + rnd2.y); // lift slightly for ray

                // First ensure candidate is on the NavMesh (broad validation)
                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas))
                {
                    Vector3 onNav = navHit.position + Vector3.up * 0.1f;

                    // Then ensure there's actual GROUND below (prevents selecting top of props if desired)
                    if (TryProjectToGround(onNav, groundRaycastDepth, out Vector3 groundPoint))
                    {
                        // Final validation: ensure projected ground point is still within walkable NavMesh
                        if (NavMesh.SamplePosition(groundPoint, out NavMeshHit finalHit, 0.5f, NavMesh.AllAreas))
                        {
                            result = finalHit.position;
                            return true;
                        }
                    }
                }
            }

            result = default;
            return false;
        }

        private Vector3 GetRandomPatrolCenter()
        {
            switch (randomCenterMode)
            {
                case RandomPatrolCenterMode.Transform:
                    return randomCenterTransform != null ? randomCenterTransform.position : transform.position;
                case RandomPatrolCenterMode.StaticPoint:
                    return randomCenterPoint;
                case RandomPatrolCenterMode.Self:
                default:
                    return transform.position;
            }
        }

        private bool TryProjectToGround(Vector3 startAbove, float maxDownDistance, out Vector3 ground)
        {
            Vector3 origin = startAbove;
            Vector3 dir = Vector3.down;
            float dist = Mathf.Max(0.01f, maxDownDistance);

            if (Physics.Raycast(origin, dir, out RaycastHit hit, dist, groundLayer, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point;
                return true;
            }

            ground = default;
            return false;
        }
        #endregion

        private void UpdateInvestigate()
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer >= investigateDuration)
            {
                SetState(AIState.Search);
                return;
            }
        }

        private void UpdateChase()
        {
            _stateTimer += Time.deltaTime;

            if (vision.CurrentTarget != null)
            {
                if (Time.time >= _nextRepathTime)
                {
                    _nextRepathTime = Time.time + repathInterval;
                    MoveTo(vision.CurrentTarget.position);
                    memory.SetLastKnownPosition(vision.CurrentTarget.position);
                    memory.Touch();
                }
            }
            else
            {
                // No target visible; give up after grace period -> Search last known
                if (_stateTimer >= giveUpAfterSeconds)
                {
                    SetState(AIState.Search);
                }
            }
        }

        private void UpdateSearch()
        {
            if (!memory.HasLastKnownPosition)
            {
                // Return to patrol mode requested by user
                bool canPatrol =
                    (patrolMode == PatrolMode.Waypoints && patrolPoints != null && patrolPoints.Length > 0)
                    || (patrolMode == PatrolMode.Random);

                SetState(canPatrol ? AIState.Patrol : AIState.Idle);
                return;
            }

            _stateTimer += Time.deltaTime;

            if (!_agent.hasPath || _agent.remainingDistance <= waypointTolerance)
            {
                Vector3 rnd = UnityEngine.Random.insideUnitSphere * searchRadius;
                rnd.y = 0f;
                Vector3 dest = memory.LastKnownPosition + rnd;

                if (NavMesh.SamplePosition(dest, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
                {
                    MoveTo(hit.position);
                }
            }

            if (_stateTimer >= searchDuration)
            {
                memory.ClearPosition();

                bool canPatrol =
                    (patrolMode == PatrolMode.Waypoints && patrolPoints != null && patrolPoints.Length > 0)
                    || (patrolMode == PatrolMode.Random);

                SetState(canPatrol ? AIState.Patrol : AIState.Idle);
            }
        }

        private void MoveTo(Vector3 position)
        {
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.SetDestination(position);
            }
        }

        private void HandleTargetSpotted(Transform target)
        {
            SetState(AIState.Chase);
        }

        private void HandleTargetLost()
        {
            _stateTimer = 0f; // start grace timer in Chase
        }

        private void HandleHeardSound(SoundEvent evt)
        {
            // Only react if we don't currently see the target
            if (vision != null && vision.CurrentTarget != null)
                return;

            memory.SetLastKnownPosition(evt.worldPosition);
            SetState(AIState.Investigate);
            MoveTo(evt.worldPosition);
        }

        public void ForceInvestigate(Vector3 position)
        {
            memory.SetLastKnownPosition(position);
            SetState(AIState.Investigate);
            MoveTo(position);
        }

        public void ForceChase(Transform target)
        {
            SetState(AIState.Chase);
            MoveTo(target.position);
        }

        public void Abort()
        {
            _agent.ResetPath();
            SetState(AIState.Idle);
        }

        private void SetState(AIState next)
        {
            if (_state == next)
                return;

            AIState prev = _state;
            _state = next;
            _stateTimer = 0f;

            OnStateChanged?.Invoke(prev, next);

            if (_state == AIState.Patrol)
            {
                if (patrolMode == PatrolMode.Waypoints)
                {
                    if (patrolPoints != null && patrolPoints.Length > 0)
                    {
                        MoveTo(patrolPoints[_patrolIndex].position);
                    }
                }
                else
                {
                    // Random patrol: pick an initial point immediately
                    if (TryGetRandomGroundPoint(out Vector3 dest))
                    {
                        _lastRandomPoint = dest;
                        MoveTo(dest);
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (patrolMode == PatrolMode.Random)
            {
                if (gizmoShowRandomPatrolArea)
                {
                    // Determine center non-destructively (works in edit mode as well)
                    Vector3 center = Application.isPlaying ? GetRandomPatrolCenter() :
                        (randomCenterMode == RandomPatrolCenterMode.Transform && randomCenterTransform != null
                            ? randomCenterTransform.position
                            : (randomCenterMode == RandomPatrolCenterMode.StaticPoint ? randomCenterPoint : transform.position));

                    UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 0.15f);
                    UnityEditor.Handles.DrawSolidDisc(center, Vector3.up, randomPatrolRadius);

                    UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
                    UnityEditor.Handles.DrawWireDisc(center, Vector3.up, randomPatrolRadius);
                }

                if (gizmoShowRandomLastPoint)
                {
                    Vector3 marker = _lastRandomPoint == default ? transform.position : _lastRandomPoint;
                    Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
                    Gizmos.DrawSphere(marker, 0.2f);
                }
            }
            else if (patrolMode == PatrolMode.Waypoints && gizmoShowWaypoints)
            {
                if (patrolPoints != null)
                {
                    Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);

                    for (int i = 0; i < patrolPoints.Length; i++)
                    {
                        var p = patrolPoints[i];
                        if (p == null)
                            continue;

                        // point marker
                        Gizmos.DrawSphere(p.position, 0.2f);

                        // path line to next
                        int j = (i + 1) % patrolPoints.Length;
                        var next = patrolPoints[j];
                        if (next != null)
                        {
                            Gizmos.DrawLine(p.position, next.position);
                        }
                    }
                }
            }
        }
#endif
    }
}