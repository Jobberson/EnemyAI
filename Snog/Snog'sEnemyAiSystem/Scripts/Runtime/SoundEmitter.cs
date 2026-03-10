using System;
using UnityEngine;

namespace SnogTools.AI
{
    public enum SoundType
    {
        Footstep,
        Gunshot,
        Impact,
        Voice,
        Custom
    }

    [Serializable]
    public struct SoundEvent
    {
        public Vector3 worldPosition;
        public float loudness;    // base strength at the source (e.g., 1..10)
        public float maxRange;    // hard cap distance (computed final range = f(loudness))
        public SoundType type;
        public UnityEngine.Object source; // optional

        public SoundEvent(Vector3 pos, float loudness, float maxRange, SoundType type, UnityEngine.Object source = null)
        {
            this.worldPosition = pos;
            this.loudness = Mathf.Max(0f, loudness);
            this.maxRange = Mathf.Max(0f, maxRange);
            this.type = type;
            this.source = source;
        }
    }

    public static class SoundSystem
    {
        public static event Action<SoundEvent> OnSound;

        public static void Raise(SoundEvent evt)
        {
            OnSound?.Invoke(evt);
        }
    }

    /// <summary>
    /// Attach to anything that should emit audible events (footsteps, doors, etc.).
    /// Call Emit() to broadcast.
    /// </summary>
    public class SoundEmitter : MonoBehaviour
    {
        public void Emit(float loudness, float maxRange, SoundType type = SoundType.Custom)
        {
            var evt = new SoundEvent(transform.position, loudness, maxRange, type, this);
            SoundSystem.Raise(evt);
        }
    }
}