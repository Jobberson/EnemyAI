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

    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public class EnemyAIController : MonoBehaviour
    {
        [Header("References")]
        public VisionSensor vision;
        public HearingSensor hearing;
        public ThreatMemory memory;

        [Header("Patrol")]
        public Transform[] patrolPoints;
        public float waypointTolerance = 0.8f;
        public float waitAtWaypoint = 1.5f;

        [Header("Investigate/Search")]
        public float investigateDuration = 3f;
        public float searchRadius = 5f;
        public float searchDuration = 6f;

        [Header("Chase")]
        public float repathInterval = 0.2f;
        public float giveUpAfterSeconds = 6f;

        public event Action<AIState, AIState> OnStateChanged;

        private NavMeshAgent _agent;
        private AIState _state;
        private float _stateTimer;
        private int _patrolIndex;
        private float _nextRepathTime;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.autoBraking = true;
            _agent.updateRotation = true;

            if (vision == null) vision = GetComponent<VisionSensor>();
            if (hearing == null) hearing = GetComponent<HearingSensor>();
            if (memory == null) memory = GetComponent<ThreatMemory>();
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
            SetState(patrolPoints != null && patrolPoints.Length > 0 ? AIState.Patrol : AIState.Idle);
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
            // Could add idle animation, look around, etc.
        }

        private void UpdatePatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            if (!_agent.pathPending && _agent.remainingDistance <= waypointTolerance)
            {
                _stateTimer += Time.deltaTime;
                if (_stateTimer >= waitAtWaypoint)
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
            // Wander around last known position
            if (!memory.HasLastKnownPosition)
            {
                SetState(patrolPoints != null && patrolPoints.Length > 0 ? AIState.Patrol : AIState.Idle);
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
                SetState(patrolPoints != null && patrolPoints.Length > 0 ? AIState.Patrol : AIState.Idle);
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

            if (_state == AIState.Patrol && patrolPoints != null && patrolPoints.Length > 0)
            {
                MoveTo(patrolPoints[_patrolIndex].position);
            }
        }
    }
}