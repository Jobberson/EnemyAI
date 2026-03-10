using System.Collections.Generic;
using UnityEngine;

namespace SnogTools.AI
{
    [System.Serializable]
    public class SuspicionNode
    {
        public Vector3 position;
        public float radius;
        public float value;
        public float decayPerSecond;
        public float createdAt;
    }

    public class SuspicionSystem : MonoBehaviour
    {
        public static SuspicionSystem Instance { get; private set; }

        [Tooltip("Max number of active suspicion nodes.")]
        public int maxNodes = 32;

        private readonly List<SuspicionNode> _nodes = new List<SuspicionNode>(32);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                var n = _nodes[i];
                n.value -= n.decayPerSecond * dt;
                if (n.value <= 0f)
                {
                    _nodes.RemoveAt(i);
                    continue;
                }
                _nodes[i] = n;
            }
        }

        public void Raise(Vector3 pos, float amount, float radius = 6f, float decayPerSecond = 1f)
        {
            // Merge with nearest node if close
            int idx = FindNodeWithin(pos, radius * 0.5f);
            if (idx >= 0)
            {
                var n = _nodes[idx];
                n.value += amount;
                n.radius = Mathf.Max(n.radius, radius);
                n.decayPerSecond = Mathf.Max(n.decayPerSecond, decayPerSecond * 0.8f);
                _nodes[idx] = n;
                return;
            }

            if (_nodes.Count >= maxNodes)
                return;

            _nodes.Add(new SuspicionNode
            {
                position = pos,
                radius = radius,
                value = amount,
                decayPerSecond = decayPerSecond,
                createdAt = Time.time
            });
        }

        public bool TryGetStrongestNear(Vector3 pos, float maxDistance, out SuspicionNode node)
        {
            node = null;
            float best = 0f;
            for (int i = 0; i < _nodes.Count; i++)
            {
                float d = Vector3.Distance(pos, _nodes[i].position);
                if (d <= maxDistance && _nodes[i].value > best)
                {
                    best = _nodes[i].value;
                    node = _nodes[i];
                }
            }
            return node != null;
        }

        private int FindNodeWithin(Vector3 pos, float dist)
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (Vector3.Distance(pos, _nodes[i].position) <= dist)
                    return i;
            }
            return -1;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Instance != this)
                return;

            if (!Application.isPlaying)
                return;

            foreach (var n in _nodes)
            {
                float a = Mathf.Clamp01(n.value / 5f);
                Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.15f + 0.35f * a);
                UnityEditor.Handles.DrawSolidDisc(n.position, Vector3.up, n.radius);
                Gizmos.color = new Color(1f, 0.2f, 0.4f, 0.9f);
                UnityEditor.Handles.DrawWireDisc(n.position, Vector3.up, n.radius);
            }
        }
#endif
    }
}