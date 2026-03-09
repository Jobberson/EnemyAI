using UnityEngine;

namespace SnogTools.AI
{
    /// <summary>
    /// Marks an object as detectable by AI. Optionally points to a 'aim' Transform (e.g., head).
    /// </summary>
    public class PerceptionTarget : MonoBehaviour
    {
        [Tooltip("Primary point for vision LOS checks (e.g., head). If null, uses this transform.")]
        public Transform targetPoint;

        public Transform GetPoint()
        {
            return targetPoint != null ? targetPoint : transform;
        }
    }
}