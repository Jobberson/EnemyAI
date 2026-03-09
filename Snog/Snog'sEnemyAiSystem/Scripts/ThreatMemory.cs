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

        public Vector3 LastKnownPosition { get; private set; }
        public bool HasLastKnownPosition { get; private set; }

        private float _lastUpdateTime;

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
    }
}