using UnityEngine;

namespace SnogTools.AI
{
    public class FootstepTestEmitter : MonoBehaviour
    {
        [Tooltip("If null, will try to use a SoundEmitter on this GameObject.")]
        public SoundEmitter emitter;

        [Header("Test Sound")]
        public float loudness = 1.2f;
        public float maxRange = 12f;
        public SoundType type = SoundType.Footstep;
        public KeyCode key = KeyCode.Space;

        private void Reset()
        {
            emitter = GetComponent<SoundEmitter>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(key))
            {
                if (emitter == null)
                    emitter = GetComponent<SoundEmitter>();

                if (emitter != null)
                {
                    emitter.Emit(loudness, maxRange, type);
                }
            }
        }
    }
}