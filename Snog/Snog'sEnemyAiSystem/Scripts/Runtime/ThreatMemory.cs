using UnityEngine;

namespace SnogTools.AI
{
    /// <summary>
    /// Stores last seen/heard data and suspicion values.
    /// </summary>
    public class ThreatMemory : MonoBehaviour
    {
        [Header("Decay")]
        [Tooltip("Seconds to fully forget last known position when nothing reinforces it.")]
        public float forgetAfterSeconds = 8f;

        [Header("Prediction")]
        [Tooltip("Lead time (seconds) for predicted intercept when chasing.")]
        public float predictionLeadTime = 0.5f;

        public Vector3 LastKnownPosition { get; private set; }
        public Vector3 LastKnownVelocity { get; private set; }
        public bool HasLastKnownPosition { get; private set; }

        private float _lastUpdateTime;

        public void SetLastKnown(Vector3 pos, Vector3 velocity)
        {
            LastKnownPosition = pos;
            LastKnownVelocity = velocity;
            HasLastKnownPosition = true;
            _lastUpdateTime = Time.time;
        }

        public void SetLastKnownPosition(Vector3 pos)
        {
            LastKnownPosition = pos;
            HasLastKnownPosition = true;
            _lastUpdateTime = Time.time;
        }

        public void ClearPosition()
        {
            HasLastKnownPosition = false;
        }

        public void Touch()
        {
            _lastUpdateTime = Time.time;
        }

        public void Tick()
        {
            if (HasLastKnownPosition)
            {
                if (Time.time - _lastUpdateTime >= forgetAfterSeconds)
                {
                    HasLastKnownPosition = false;
                }
            }
        }

        public Vector3 GetPredictedPosition(float leadTimeScale = 1f)
        {
            float lt = Mathf.Max(0f, predictionLeadTime) * Mathf.Max(0f, leadTimeScale);
            return LastKnownPosition + LastKnownVelocity * lt;
        }
    }
}