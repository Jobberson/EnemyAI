using System;
using UnityEngine;

namespace SnogTools.AI
{
    public enum LosMode
    {
        SingleRay,
        MultiRay,
        Capsule
    }

    public class VisionSensor : MonoBehaviour
    {
        [Header("Targets")]
        [Tooltip("LayerMask for detectable targets (e.g., Player)")]
        public LayerMask targetMask;

        [Tooltip("LayerMask for occluders (e.g., Obstacles)")]
        public LayerMask occluderMask;

        [Header("FOV Zones")]
        [Tooltip("Inner cone (fast detection).")]
        [Range(1f, 180f)]
        public float focusFOV = 50f;

        [Tooltip("Outer cone (slower detection). Must be >= focusFOV.")]
        [Range(1f, 180f)]
        public float peripheralFOV = 110f;

        [Min(0.1f)]
        public float viewDistance = 20f;

        [Tooltip("Height offset from this transform used as eye position")]
        public float eyeHeight = 1.7f;

        [Header("Detection (Focus Zone)")]
        [Tooltip("Seconds to fully detect in the focus cone when centered and unobstructed.")]
        public float focusTimeToFullDetect = 0.6f;

        [Tooltip("Angle weighting for focus cone (0=axis..1=edge) → weight 0..1")]
        public AnimationCurve focusAngleWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.25f);

        [Tooltip("Distance weighting for focus cone (0=near..1=far) → weight 0..1")]
        public AnimationCurve focusDistanceWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.25f);

        [Header("Detection (Peripheral Zone)")]
        [Tooltip("Seconds to fully detect in the peripheral cone when centered and unobstructed (slower).")]
        public float peripheralTimeToFullDetect = 1.2f;

        [Tooltip("Angle weighting for peripheral cone (0=axis..1=edge) → weight 0..1")]
        public AnimationCurve peripheralAngleWeight = AnimationCurve.EaseInOut(0, 0.8f, 1, 0.15f);

        [Tooltip("Distance weighting for peripheral cone (0=near..1=far) → weight 0..1")]
        public AnimationCurve peripheralDistanceWeight = AnimationCurve.EaseInOut(0, 0.8f, 1, 0.15f);

        [Header("Decay / Budget")]
        [Tooltip("How quickly detection falls when losing sight (per second).")]
        public float detectionDecayPerSecond = 1.2f;

        [Tooltip("Seconds between sensor sweeps")]
        public float scanInterval = 0.1f;

        [Header("Occlusion Robustness")]
        public LosMode losMode = LosMode.MultiRay;

        [Tooltip("How many rays to sample in MultiRay (1..5).")]
        [Range(1, 5)]
        public int losSamples = 3;

        [Tooltip("Jitter radius for MultiRay samples (meters).")]
        [Min(0f)]
        public float losSampleRadius = 0.15f;

        [Tooltip("Capsule radius (for Capsule LOS).")]
        [Min(0.01f)]
        public float losCapsuleRadius = 0.2f;

        [Header("Gizmos / Debug")]
        [Tooltip("Draw the FOV cones in the Scene view when selected.")]
        public bool gizmoShowFOV = true;

        [Tooltip("Draw a short forward ray from the eye position when selected.")]
        public bool gizmoShowEyeRay = true;

        public event Action<Transform> OnTargetSpotted;
        public event Action OnTargetLost;

        public Transform CurrentTarget { get; private set; }
        public float CurrentDetection { get; private set; }

        private float _nextScanTime;
        private Vector3 EyePosition => transform.position + Vector3.up * eyeHeight;

        private const float DETECT_THRESHOLD = 1f;
        private const float LOST_THRESHOLD = 0.05f;

        private void Reset()
        {
            focusAngleWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.25f);
            focusDistanceWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.25f);
            peripheralAngleWeight = AnimationCurve.EaseInOut(0, 0.8f, 1, 0.15f);
            peripheralDistanceWeight = AnimationCurve.EaseInOut(0, 0.8f, 1, 0.15f);
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
            // Broadphase
            Collider[] hits = Physics.OverlapSphere(EyePosition, viewDistance, targetMask, QueryTriggerInteraction.Ignore);

            Transform best = null;
            float bestRate = 0f;

            for (int i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponentInParent<PerceptionTarget>();
                if (target == null)
                    continue;

                var point = target.GetPoint();
                Vector3 toTarget = point.position - EyePosition;
                float dist = toTarget.magnitude;

                float ang = Vector3.Angle(transform.forward, toTarget);
                if (ang > peripheralFOV * 0.5f)
                    continue; // outside the widest cone

                // LOS
                if (!HasLineOfSightRobust(EyePosition, point.position))
                    continue;

                // Determine zone & compute accumulation rate
                bool inFocus = ang <= focusFOV * 0.5f;
                float angleNorm = inFocus
                    ? Mathf.InverseLerp(0f, focusFOV * 0.5f, ang)
                    : Mathf.InverseLerp(focusFOV * 0.5f, peripheralFOV * 0.5f, ang);

                float distNorm = Mathf.InverseLerp(0f, viewDistance, dist);

                float weight =
                    (inFocus
                        ? focusAngleWeight.Evaluate(angleNorm) * focusDistanceWeight.Evaluate(distNorm)
                        : peripheralAngleWeight.Evaluate(angleNorm) * peripheralDistanceWeight.Evaluate(distNorm));

                float tToFull = Mathf.Max(0.1f, inFocus ? focusTimeToFullDetect : peripheralTimeToFullDetect);
                float rate = Mathf.Max(0.1f, weight) * (1f / tToFull); // accumulation per second baseline

                if (rate > bestRate)
                {
                    bestRate = rate;
                    best = point;
                }
            }

            if (best != null)
            {
                // Accumulate by rate scaled by scanInterval
                CurrentDetection = Mathf.Clamp01(CurrentDetection + bestRate * scanInterval);

                if (CurrentDetection >= DETECT_THRESHOLD && (CurrentTarget == null || CurrentTarget != best.root))
                {
                    CurrentTarget = best.root;
                    OnTargetSpotted?.Invoke(CurrentTarget);
                }
            }
            else
            {
                // No valid target in sight; decay with scan cadence
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

        public bool HasLineOfSightRobust(Vector3 origin, Vector3 targetPoint)
        {
            if (losMode == LosMode.SingleRay)
            {
                return HasLineOfSight(targetPoint);
            }

            Vector3 dir = targetPoint - origin;
            float dist = dir.magnitude;
            if (dist <= 0.001f)
                return true;

            if (losMode == LosMode.Capsule)
            {
                // Capsule from slightly offset above origin to slightly below target; oriented along dir
                Vector3 n = dir / dist;
                Vector3 p0 = origin + n * 0.05f;
                Vector3 p1 = targetPoint - n * 0.05f;

                // Physics doesn't have direct CapsuleCast for overlap between points; we cast a capsule along dir with distance 0
                // Use a tiny SphereCast chain as approximation (two spherecasts)
                if (Physics.SphereCast(origin, losCapsuleRadius, n, out RaycastHit hit0, dist, occluderMask, QueryTriggerInteraction.Ignore))
                    return false;

                if (Physics.SphereCast(origin + Vector3.up * losCapsuleRadius, losCapsuleRadius, n, out RaycastHit hit1, dist, occluderMask, QueryTriggerInteraction.Ignore))
                    return false;

                return true;
            }

            // MultiRay
            if (losSamples <= 1 && losSampleRadius <= 0f)
            {
                return HasLineOfSight(targetPoint);
            }

            // Always include the center ray
            if (Physics.Raycast(origin, dir.normalized, dist, occluderMask, QueryTriggerInteraction.Ignore))
                return false;

            // Jitter around target point
            for (int i = 1; i < losSamples; i++)
            {
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * losSampleRadius;
                Vector3 tp = targetPoint + jitter;
                Vector3 d = tp - origin;
                float dd = d.magnitude;
                d /= Mathf.Max(0.001f, dd);

                if (Physics.Raycast(origin, d, dd, occluderMask, QueryTriggerInteraction.Ignore))
                    return false;
            }

            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (gizmoShowFOV)
            {
                // Peripheral cone
                UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.15f);
                UnityEditor.Handles.DrawSolidArc(
                    EyePosition,
                    Vector3.up,
                    Quaternion.Euler(0, -peripheralFOV * 0.5f, 0) * transform.forward,
                    peripheralFOV,
                    viewDistance
                );

                // Focus cone (draw on top)
                UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.25f);
                UnityEditor.Handles.DrawSolidArc(
                    EyePosition,
                    Vector3.up,
                    Quaternion.Euler(0, -focusFOV * 0.5f, 0) * transform.forward,
                    focusFOV,
                    viewDistance * 0.95f
                );
            }

            if (gizmoShowEyeRay)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(EyePosition, EyePosition + transform.forward * Mathf.Min(0.5f * viewDistance, 2f));
            }

            if (Application.isPlaying && CurrentTarget != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.95f);
                Gizmos.DrawLine(EyePosition, CurrentTarget.position);
            }
        }
#endif
    }
}