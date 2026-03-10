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

    public enum HideBehavior
    {
        Freeze,
        FleeToOcclusion
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAIController : MonoBehaviour
    {
        [Header("References")]
        public VisionSensor vision;
        public HearingSensor hearing;
        public ThreatMemory memory;

        [Header("State: Patrol (Common)")]
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

        [Tooltip("Avoid picking random patrol points visible from the player's main camera (stealthier patrol).")]
        public bool avoidPlayerLoSInRandomPatrol = true;

        [Header("Investigate/Search")]
        public float investigateDuration = 3f;
        public float searchRadius = 5f;
        public float searchDuration = 6f;

        [Header("Chase")]
        public float repathInterval = 0.2f;
        public float giveUpAfterSeconds = 6f;

        // --- Prediction / Suspicion ---
        [Header("Prediction / Suspicion")]
        [Tooltip("Distance at which prediction lead scales up (m).")]
        public float predictionScaleDistance = 10f;

        [Tooltip("Max prediction lead scale (multiplies ThreatMemory.predictionLeadTime).")]
        public float maxPredictionLeadScale = 1.5f;

        [Tooltip("When searching/patrolling, consider strongest suspicion node within this distance.")]
        public float suspicionQueryDistance = 20f;

        [Tooltip("Minimum suspicion value to act on.")]
        public float suspicionThreshold = 0.75f;

        // --- Hide From Player ---
        [Header("Hide From Player (Bracken-like)")]
        [Tooltip("Enable hiding behavior when the player is watching.")]
        public bool hideFromPlayer = true;

        [Tooltip("What to do when watched.")]
        public HideBehavior hideBehavior = HideBehavior.FleeToOcclusion;

        [Tooltip("Player eye Transform (camera or head). If null, uses Camera.main automatically.")]
        public Transform playerEye;

        [Tooltip("Player horizontal FOV degrees (used to decide if 'watched').")]
        [Range(30f, 160f)]
        public float playerFOV = 90f;

        [Tooltip("Distance within which watching matters.")]
        public float watchedDistance = 25f;

        [Tooltip("Cooldown before picking a new hide point (s).")]
        public float hideRepathCooldown = 1.0f;

        [Tooltip("Attempts to sample an occluded hide point per repath.")]
        public int hideSampleAttempts = 12;

        [Tooltip("Radius around current enemy position to search for hide points.")]
        public float hideSearchRadius = 12f;

        [Tooltip("LayerMask for occlusion against the player (usually same as Vision occluders).")]
        public LayerMask playerOccluderMask;

        [Header("Gizmos / Debug")]
        [Tooltip("Draw the random patrol area disc when Patrol Mode = Random.")]
        public bool gizmoShowRandomPatrolArea = true;

        [Tooltip("Draw a small sphere on the last random destination chosen.")]
        public bool gizmoShowRandomLastPoint = true;

        [Tooltip("Draw waypoint markers and lines when Patrol Mode = Waypoints.")]
        public bool gizmoShowWaypoints = true;

        public event Action<AIState, AIState> OnStateChanged;

        private NavMeshAgent _agent;
        private AIState _state;
        private float _stateTimer;
        private int _patrolIndex;
        private float _nextRepathTime;
        private float _currentWaitTarget; // dynamic wait for random patrol
        private Vector3 _lastRandomPoint; // for gizmos
        private float _nextHideRepathTime;
        private float _prevDetection;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoBraking = true;
            _agent.updateRotation = true;

            if (vision == null) vision = GetComponent<VisionSensor>();
            if (hearing == null) hearing = GetComponent<HearingSensor>();
            if (memory == null) memory = GetComponent<ThreatMemory>();

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
            bool canPatrol =
                (patrolMode == PatrolMode.Waypoints && patrolPoints != null && patrolPoints.Length > 0)
                || (patrolMode == PatrolMode.Random);

            SetState(canPatrol ? AIState.Patrol : AIState.Idle);
        }

        private void Update()
        {
            memory?.Tick();

            // Capture previous detection and raise suspicion on big drop
            if (vision != null)
            {
                float curr = vision.CurrentDetection;
                if (_prevDetection >= 0.5f && curr < 0.2f && memory.HasLastKnownPosition)
                {
                    SuspicionSystem.Instance?.Raise(memory.LastKnownPosition, amount: 0.6f, radius: 5.5f, decayPerSecond: 0.9f);
                }
                _prevDetection = curr;
            }

            // Pre-emptive hide: runs regardless of state (supports "hide before even being seen")
            if (hideFromPlayer)
            {
                HandleHideFromPlayer();
            }

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
            if (!_agent.hasPath || (!_agent.pathPending && _agent.remainingDistance <= waypointTolerance))
            {
                _stateTimer += Time.deltaTime;

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
                        // If we fail to find a point, wait a tad and try again
                        _stateTimer = 0f;
                        _currentWaitTarget = 0.2f;
                    }
                }
            }
        }

        private bool TryGetRandomGroundPoint(out Vector3 result)
        {
            Vector3 center = GetRandomPatrolCenter();
            Transform cam = (Camera.main != null) ? Camera.main.transform : null;

            for (int attempt = 0; attempt < randomMaxAttempts; attempt++)
            {
                Vector2 rnd2 = UnityEngine.Random.insideUnitCircle * randomPatrolRadius;
                Vector3 candidate = new Vector3(center.x + rnd2.x, center.y + 2.5f, center.z + rnd2.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 2.5f, NavMesh.AllAreas))
                {
                    Vector3 onNav = navHit.position + Vector3.up * 0.1f;

                    if (TryProjectToGround(onNav, groundRaycastDepth, out Vector3 groundPoint))
                    {
                        // Optional: avoid being in the player's LoS for "hide before seen" vibe
                        if (avoidPlayerLoSInRandomPatrol && cam != null && playerOccluderMask.value != 0)
                        {
                            if (IsVisibleFrom(cam.position, groundPoint))
                            {
                                // try another point
                                continue;
                            }
                        }

                        // Final validation: ensure on NavMesh
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

            if (vision != null && vision.CurrentTarget != null)
            {
                if (Time.time >= _nextRepathTime)
                {
                    _nextRepathTime = Time.time + repathInterval;

                    Vector3 currPos = vision.CurrentTarget.position;
                    Vector3 lastPos = memory.LastKnownPosition;
                    Vector3 vel = (currPos - lastPos) / Mathf.Max(0.01f, repathInterval);

                    memory.SetLastKnown(currPos, vel);
                    memory.Touch();

                    float d = Vector3.Distance(transform.position, currPos);
                    float scale = Mathf.Clamp01(d / Mathf.Max(0.01f, predictionScaleDistance));
                    scale = Mathf.Lerp(0.5f, maxPredictionLeadScale, scale);

                    Vector3 predicted = memory.GetPredictedPosition(scale);
                    MoveTo(predicted);
                }
            }
            else
            {
                if (_stateTimer >= giveUpAfterSeconds)
                {
                    SetState(AIState.Search);
                }
            }
        }

        private void UpdateSearch()
        {
            Vector3 basePos;
            bool hasBase = memory.HasLastKnownPosition;
            if (hasBase)
            {
                basePos = memory.LastKnownPosition;
            }
            else
            {
                basePos = transform.position;
                if (SuspicionSystem.Instance != null
                    && SuspicionSystem.Instance.TryGetStrongestNear(transform.position, suspicionQueryDistance, out var node)
                    && node.value >= suspicionThreshold)
                {
                    basePos = node.position;
                }
                else
                {
                    bool canPatrol =
                        (patrolMode == PatrolMode.Waypoints && patrolPoints != null && patrolPoints.Length > 0)
                        || (patrolMode == PatrolMode.Random);

                    SetState(canPatrol ? AIState.Patrol : AIState.Idle);
                    return;
                }
            }

            _stateTimer += Time.deltaTime;

            if (!_agent.hasPath || _agent.remainingDistance <= waypointTolerance)
            {
                Vector3 rnd = UnityEngine.Random.insideUnitSphere * searchRadius;
                rnd.y = 0f;
                Vector3 dest = basePos + rnd;

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
            SuspicionSystem.Instance?.Raise(evt.worldPosition, amount: Mathf.Clamp(evt.loudness, 0.25f, 2f), radius: 5f, decayPerSecond: 0.8f);
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
                    if (TryGetRandomGroundPoint(out Vector3 dest))
                    {
                        _lastRandomPoint = dest;
                        MoveTo(dest);
                    }
                }
            }
        }

        // --- Hide-from-player (pre-emptive) ---
        private void HandleHideFromPlayer()
        {
            // Always prefer Camera.main per your request
            Transform eye = (Camera.main != null) ? Camera.main.transform : playerEye;
            if (eye == null)
                return;

            Vector3 toEnemy = transform.position - eye.position;
            float dist = toEnemy.magnitude;
            if (dist > watchedDistance)
                return;

            Vector3 fwd = new Vector3(eye.forward.x, 0f, eye.forward.z).normalized;
            Vector3 dir = new Vector3(toEnemy.x, 0f, toEnemy.z).normalized;
            float ang = Vector3.Angle(fwd, dir);
            if (ang > playerFOV * 0.5f)
                return;

            if (!HasPlayerLineOfSight(eye.position, transform.position))
                return;

            // Watched → act immediately, regardless of current AI state
            if (hideBehavior == HideBehavior.Freeze)
            {
                if (_agent.hasPath)
                    _agent.ResetPath();
                return;
            }

            if (Time.time >= _nextHideRepathTime)
            {
                _nextHideRepathTime = Time.time + hideRepathCooldown;

                if (TrySampleOccludedHidePoint(eye.position, out Vector3 hidePoint))
                {
                    MoveTo(hidePoint);
                }
                else
                {
                    // Fallback: move opposite from the player horizontally
                    Vector3 away = transform.position + (dir.normalized * hideSearchRadius);
                    if (NavMesh.SamplePosition(away, out NavMeshHit hit, hideSearchRadius, NavMesh.AllAreas))
                    {
                        MoveTo(hit.position);
                    }
                }
            }
        }

        private bool HasPlayerLineOfSight(Vector3 playerEyePos, Vector3 enemyPos)
        {
            if (playerOccluderMask.value == 0)
                return true;

            Vector3 d = enemyPos - playerEyePos;
            float dist = d.magnitude;
            d /= Mathf.Max(0.001f, dist);
            return !Physics.Raycast(playerEyePos, d, dist, playerOccluderMask, QueryTriggerInteraction.Ignore);
        }

        private bool IsVisibleFrom(Vector3 observerPos, Vector3 point)
        {
            if (playerOccluderMask.value == 0)
                return true;

            Vector3 d = point - observerPos;
            float dist = d.magnitude;
            d /= Mathf.Max(0.001f, dist);
            return !Physics.Raycast(observerPos, d, dist, playerOccluderMask, QueryTriggerInteraction.Ignore);
        }

        private bool TrySampleOccludedHidePoint(Vector3 playerEyePos, out Vector3 result)
        {
            for (int i = 0; i < hideSampleAttempts; i++)
            {
                Vector2 r2 = UnityEngine.Random.insideUnitCircle * hideSearchRadius;
                Vector3 candidate = new Vector3(transform.position.x + r2.x, transform.position.y + 1.0f, transform.position.z + r2.y);

                if (NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 2.0f, NavMesh.AllAreas))
                {
                    Vector3 pt = navHit.position + Vector3.up * 0.2f;

                    // Check occlusion from player
                    if (!IsVisibleFrom(playerEyePos, pt))
                    {
                        result = navHit.position;
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (patrolMode == PatrolMode.Random)
            {
                if (gizmoShowRandomPatrolArea)
                {
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

                        Gizmos.DrawSphere(p.position, 0.2f);

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