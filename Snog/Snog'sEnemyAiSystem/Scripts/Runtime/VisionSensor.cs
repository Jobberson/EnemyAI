using System;
using UnityEngine;

namespace SnogTools.AI
{
    public class VisionSensor : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("LayerMask for detectable targets (e.g., Player)")]
        public LayerMask targetMask;

        [Tooltip("LayerMask for occluders (e.g., Obstacles)")]
        public LayerMask occluderMask;

        [Header("FOV")]
        [Range(1f, 180f)]
        public float fieldOfView = 90f;

        [Min(0.1f)]
        public float viewDistance = 20f;

        [Tooltip("Height offset from this transform used as eye position")]
        public float eyeHeight = 1.7f;

        [Header("Detection")]
        [Tooltip("Seconds to fully detect when target is centered and unobstructed.")]
        public float timeToFullDetect = 0.8f;

        [Tooltip("How quickly detection falls when losing sight.")]
        public float detectionDecayPerSecond = 1.2f;

        [Tooltip("Weight curve by angle (x: normalized angle 0=on-axis..1=at edge; y: weight 0..1)")]
        public AnimationCurve angleWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);

        [Tooltip("Weight curve by distance (x: normalized distance 0=near..1=far; y: weight 0..1)")]
        public AnimationCurve distanceWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);

        [Header("Budgeting")]
        [Tooltip("Seconds between sensor sweeps")]
        public float scanInterval = 0.1f;

        [Tooltip("How many LOS rays to try (head/torso). 1 or 2 recommended.")]
        [Range(1, 3)]
        public int losSamplePoints = 2;

        public event Action<Transform> OnTargetSpotted;
        public event Action OnTargetLost;

        public Transform CurrentTarget { get; private set; }
        public float CurrentDetection { get; private set; }
        
        [Header("Gizmos / Debug")]
        [Tooltip("Draw the FOV cone in the Scene view when selected.")]
        public bool gizmoShowFOV = true;

        [Tooltip("Draw a short forward ray from the eye position when selected.")]
        public bool gizmoShowEyeRay = true;

        private float _nextScanTime;
        private Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        private const float DETECT_THRESHOLD = 1f;
        private const float LOST_THRESHOLD = 0.05f;

        private void Reset()
        {
            angleWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);
            distanceWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);
        }

        private void Update()
        {
            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + scanInterval;
                Scan();
            }

            if (CurrentTarget == null && CurrentDetection > 0f)
            {
                CurrentDetection = Mathf.Max(0f, CurrentDetection - detectionDecayPerSecond * Time.deltaTime);
                if (CurrentDetection <= LOST_THRESHOLD)
                {
                    OnTargetLost?.Invoke();
                }
            }
        }

        private void Scan()
        {
            // Find all candidate colliders in range with a cheap overlap
            Collider[] hits = Physics.OverlapSphere(EyePosition, viewDistance, targetMask, QueryTriggerInteraction.Ignore);
            Transform best = null;
            float bestScore = 0f;

            for (int i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponentInParent<PerceptionTarget>();
                if (target == null)
                    continue;

                var point = target.GetPoint();
                Vector3 toTarget = point.position - EyePosition;
                float dist = toTarget.magnitude;

                // Check FOV
                float angle = Vector3.Angle(transform.forward, toTarget);
                if (angle > fieldOfView * 0.5f)
                    continue;

                // LOS with one or two sample points (head & torso)
                bool hasLoS = HasLineOfSight(point.position);
                if (!hasLoS && losSamplePoints > 1)
                {
                    // Torso guess: a bit lower
                    hasLoS = HasLineOfSight(point.position + Vector3.down * 0.4f);
                }
                if (!hasLoS)
                    continue;

                // Produce a score using curves
                float angleNorm = Mathf.InverseLerp(0f, fieldOfView * 0.5f, angle);
                float distNorm = Mathf.InverseLerp(0f, viewDistance, dist);
                float score = angleWeight.Evaluate(angleNorm) * distanceWeight.Evaluate(distNorm);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = point;
                }
            }

            if (best != null)
            {
                // Accumulate detection by score and time
                float rate = Mathf.Max(0.2f, bestScore) * (1f / Mathf.Max(0.1f, timeToFullDetect));
                CurrentDetection = Mathf.Clamp01(CurrentDetection + rate * scanInterval);

                if (CurrentDetection >= DETECT_THRESHOLD && CurrentTarget != best.root)
                {
                    CurrentTarget = best.root;
                    OnTargetSpotted?.Invoke(CurrentTarget);
                }
            }
            else
            {
                // No valid target in sight; decay
                CurrentDetection = Mathf.Max(0f, CurrentDetection - detectionDecayPerSecond * scanInterval);
                if (CurrentTarget != null && CurrentDetection <= LOST_THRESHOLD)
                {
                    CurrentTarget = null;
                    OnTargetLost?.Invoke();
                }
            }
        }

        public bool HasLineOfSight(Transform target)
        {
            return HasLineOfSight(target.position);
        }

        public bool HasLineOfSight(Vector3 worldPoint)
        {
            Vector3 origin = EyePosition;
            Vector3 dir = (worldPoint - origin);
            float dist = dir.magnitude;
            dir /= Mathf.Max(0.001f, dist);

            if (Physics.Raycast(origin, dir, dist, occluderMask, QueryTriggerInteraction.Ignore))
                return false;

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (gizmoShowFOV)
            {
                UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.25f);
                UnityEditor.Handles.DrawSolidArc(
                    EyePosition,
                    Vector3.up,
                    Quaternion.Euler(0, -fieldOfView * 0.5f, 0) * transform.forward,
                    fieldOfView,
                    viewDistance
                );
            }

            if (gizmoShowEyeRay)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(EyePosition, EyePosition + transform.forward * Mathf.Min(0.5f * viewDistance, 2f));
            }

            // Optional: when running, show a line to current target if any
            if (Application.isPlaying && CurrentTarget != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.95f);
                Gizmos.DrawLine(EyePosition, CurrentTarget.position);
            }
        }
#endif
    }
}
