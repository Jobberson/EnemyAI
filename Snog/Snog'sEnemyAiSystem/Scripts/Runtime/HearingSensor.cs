using System;
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

        [Header("Budgeting")]
        [Tooltip("Clamp of how many sounds to process per frame for this sensor. 0 = unlimited.")]
        public int maxEventsPerFrame = 0;

        public event Action<SoundEvent> OnHeardSound;

        [Header("Gizmos / Debug")]
        [Tooltip("Draw the base hearing radius disc in the Scene view when selected.")]
        public bool gizmoShowHearingRadius = true;

        private int _processedThisFrame;

        private void OnEnable()
        {
            SoundSystem.OnSound += HandleSound;
        }

        private void OnDisable()
        {
            SoundSystem.OnSound -= HandleSound;
        }

        private void LateUpdate()
        {
            _processedThisFrame = 0;
        }

        private void HandleSound(SoundEvent evt)
        {
            if (maxEventsPerFrame > 0 && _processedThisFrame >= maxEventsPerFrame)
                return;

            // Distance check using loudness-scaled radius
            float finalRadius = baseHearingRadius * Mathf.Max(1f, evt.loudness);
            float dist = Vector3.Distance(transform.position, evt.worldPosition);
            if (dist > Mathf.Min(finalRadius, evt.maxRange))
                return;

            // Occlusion test (optional)
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

            if (perceived >= minLoudness)
            {
                _processedThisFrame++;
                OnHeardSound?.Invoke(evt);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!gizmoShowHearingRadius)
                return;

            // Use baseHearingRadius as the reference disc; loudness-scaled radius varies at runtime
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