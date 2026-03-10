using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnogTools.AI
{
    public class HearingSensor : MonoBehaviour
    {
        [Header("Hearing")]
        [Tooltip("Base hearing radius used when loudness=1. Final radius scales by loudness.")]
        public float baseHearingRadius = 8f;

        [Tooltip("Minimum loudness required to be considered.")]
        public float minLoudness = 0.05f;

        [Tooltip("LayerMask used to check if sound is occluded.")]
        public LayerMask occluderMask;

        [Tooltip("Extra loss applied if occluded (0..1 multiplies perceived loudness).")]
        [Range(0f, 1f)]
        public float occlusionAttenuation = 0.5f;

        [Header("Prioritization")]
        [Tooltip("Max events processed per frame after prioritization (0 = unlimited).")]
        public int maxEventsPerFrame = 4;

        [Tooltip("Per-type weight (1 = neutral).")]
        public float footstepWeight = 1.0f;
        public float gunshotWeight = 2.0f;
        public float impactWeight = 1.2f;
        public float voiceWeight = 1.1f;
        public float customWeight = 1.0f;

        [Header("Cooldowns (seconds)")]
        public float footstepCooldown = 0.25f;
        public float gunshotCooldown = 0.05f;
        public float impactCooldown = 0.15f;
        public float voiceCooldown = 0.2f;
        public float customCooldown = 0.1f;

        [Header("Gizmos / Debug")]
        [Tooltip("Draw the base hearing radius disc in the Scene view when selected.")]
        public bool gizmoShowHearingRadius = true;

        public event Action<SoundEvent> OnHeardSound;

        private struct Pending
        {
            public SoundEvent evt;
            public float score;
        }

        private readonly List<Pending> _pending = new List<Pending>(16);
        private readonly Dictionary<SoundType, float> _nextAllowed = new Dictionary<SoundType, float>(8);

        private void OnEnable()
        {
            SoundSystem.OnSound += HandleSoundIncoming;
        }

        private void OnDisable()
        {
            SoundSystem.OnSound -= HandleSoundIncoming;
        }

        private void LateUpdate()
        {
            if (_pending.Count == 0)
                return;

            // Sort by score descending
            _pending.Sort((a, b) => b.score.CompareTo(a.score));

            int processed = 0;
            for (int i = 0; i < _pending.Count; i++)
            {
                if (maxEventsPerFrame > 0 && processed >= maxEventsPerFrame)
                    break;

                var p = _pending[i];

                // cooldown check
                float now = Time.time;
                if (_nextAllowed.TryGetValue(p.evt.type, out float t) && now < t)
                    continue;

                OnHeardSound?.Invoke(p.evt);
                processed++;

                // schedule next allowed time per type
                float cd = GetCooldown(p.evt.type);
                _nextAllowed[p.evt.type] = now + cd;
            }

            _pending.Clear();
        }

        private void HandleSoundIncoming(SoundEvent evt)
        {
            // Perception check (distance & occlusion) → compute perceived loudness
            float dist = Vector3.Distance(transform.position, evt.worldPosition);

            float maxR = Mathf.Min(evt.maxRange, baseHearingRadius * Mathf.Max(1f, evt.loudness));
            if (dist > maxR)
                return;

            float perceived = evt.loudness;

            if (occluderMask.value != 0)
            {
                Vector3 dir = (evt.worldPosition - transform.position);
                float d = dir.magnitude;
                dir /= Mathf.Max(0.001f, d);

                if (Physics.Raycast(transform.position, dir, d, occluderMask, QueryTriggerInteraction.Ignore))
                {
                    perceived *= occlusionAttenuation;
                }
            }

            if (perceived < minLoudness)
                return;

            // Priority score
            float w = GetTypeWeight(evt.type);
            float score = (perceived * w) / (dist + 0.1f);

            _pending.Add(new Pending
            {
                evt = evt,
                score = score
            });
        }

        private float GetTypeWeight(SoundType type)
        {
            switch (type)
            {
                case SoundType.Footstep: return footstepWeight;
                case SoundType.Gunshot:  return gunshotWeight;
                case SoundType.Impact:   return impactWeight;
                case SoundType.Voice:    return voiceWeight;
                default:                 return customWeight;
            }
        }

        private float GetCooldown(SoundType type)
        {
            switch (type)
            {
                case SoundType.Footstep: return footstepCooldown;
                case SoundType.Gunshot:  return gunshotCooldown;
                case SoundType.Impact:   return impactCooldown;
                case SoundType.Voice:    return voiceCooldown;
                default:                 return customCooldown;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!gizmoShowHearingRadius)
                return;

            float r = Mathf.Max(0.01f, baseHearingRadius);
            var pos = transform.position;

            UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            UnityEditor.Handles.DrawSolidDisc(pos, Vector3.up, r);

            UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.up, r);
        }
#endif
    }
}