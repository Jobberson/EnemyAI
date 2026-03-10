#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SnogTools.AI.Editor
{
    [CustomEditor(typeof(HearingSensor))]
    public class HearingSensorEditor : UnityEditor.Editor
    {
        private bool _foldHearing;
        private bool _foldBudget;
        private bool _foldGizmos;
        private bool _foldRuntime;

        private const string KEY = "SnogTools.AI.HearingSensor.";
        private HearingSensor _sensor;

        // Serialized props
        private SerializedProperty propBaseRadius;
        private SerializedProperty propMinLoudness;
        private SerializedProperty propOccluderMask;
        private SerializedProperty propOcclusionAttn;

        private SerializedProperty propMaxEvents;

        private SerializedProperty propGizmoShowRadius;

        // Test-emit controls (editor-only)
        private float _testLoudness = 1.2f;
        private float _testMaxRange = 12f;
        private SoundType _testSoundType = SoundType.Footstep;

        // Live last-heard info (captured via subscription in play mode)
        private string _lastType = "(none)";
        private float _lastLoudness = 0f;
        private float _lastDistance = 0f;
        private double _lastHeardTime = -1;

        private void OnEnable()
        {
            _sensor = (HearingSensor)target;

            propBaseRadius = serializedObject.FindProperty("baseHearingRadius");
            propMinLoudness = serializedObject.FindProperty("minLoudness");
            propOccluderMask = serializedObject.FindProperty("occluderMask");
            propOcclusionAttn = serializedObject.FindProperty("occlusionAttenuation");

            propMaxEvents = serializedObject.FindProperty("maxEventsPerFrame");

            propGizmoShowRadius = serializedObject.FindProperty("gizmoShowHearingRadius");

            _foldHearing = EditorPrefs.GetBool(KEY + "foldHearing", true);
            _foldBudget = EditorPrefs.GetBool(KEY + "foldBudget", true);
            _foldGizmos = EditorPrefs.GetBool(KEY + "foldGizmos", true);
            _foldRuntime = EditorPrefs.GetBool(KEY + "foldRuntime", true);

            // Subscribe to live hearing feed only for single-object editing to avoid ambiguity
            if (Application.isPlaying && targets.Length == 1 && _sensor != null)
            {
                _sensor.OnHeardSound += HandleHeardSound;
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(KEY + "foldHearing", _foldHearing);
            EditorPrefs.SetBool(KEY + "foldBudget", _foldBudget);
            EditorPrefs.SetBool(KEY + "foldGizmos", _foldGizmos);
            EditorPrefs.SetBool(KEY + "foldRuntime", _foldRuntime);

            if (Application.isPlaying && targets.Length == 1 && _sensor != null)
            {
                _sensor.OnHeardSound -= HandleHeardSound;
            }
        }

        private void HandleHeardSound(SoundEvent evt)
        {
            _lastType = evt.type.ToString();
            _lastLoudness = evt.loudness;
            _lastDistance = Vector3.Distance(_sensor.transform.position, evt.worldPosition);
            _lastHeardTime = EditorApplication.timeSinceStartup;

            // Keep inspector repainting so info stays fresh
            Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();

            // Hearing settings
            _foldHearing = EditorGUILayout.BeginFoldoutHeaderGroup(_foldHearing, "Hearing");
            if (_foldHearing)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propBaseRadius, new GUIContent("Base Radius (loudness=1)"));
                EditorGUILayout.Slider(propMinLoudness, 0f, 5f, new GUIContent("Min Loudness"));

                EditorGUILayout.Space(2f);
                EditorGUILayout.PropertyField(propOccluderMask, new GUIContent("Occluder Mask"));
                EditorGUILayout.Slider(propOcclusionAttn, 0f, 1f, new GUIContent("Occlusion Attenuation"));

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            // Budgeting
            _foldBudget = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBudget, "Budgeting");
            if (_foldBudget)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propMaxEvents, new GUIContent("Max Events Per Frame (0 = unlimited)"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            // Gizmos
            _foldGizmos = EditorGUILayout.BeginFoldoutHeaderGroup(_foldGizmos, "Gizmos / Debug");
            if (_foldGizmos)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propGizmoShowRadius, new GUIContent("Show Hearing Radius"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Scene Gizmos"))
                    {
                        SceneView.RepaintAll();
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            // Runtime
            _foldRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(_foldRuntime, "Runtime");
            if (_foldRuntime)
            {
                EditorGUI.indentLevel++;

                if (Application.isPlaying)
                {
                    // Emit test sound buttons
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Emit Test Sound", EditorStyles.boldLabel);
                        _testSoundType = (SoundType)EditorGUILayout.EnumPopup("Type", _testSoundType);
                        _testLoudness = EditorGUILayout.Slider("Loudness", _testLoudness, 0f, 10f);
                        _testMaxRange = EditorGUILayout.Slider("Max Range", _testMaxRange, 0f, 50f);

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button("Emit At Sensor Position"))
                            {
                                var evt = new SoundEvent(_sensor.transform.position, _testLoudness, _testMaxRange, _testSoundType, _sensor);
                                SoundSystem.Raise(evt);
                            }

                            if (GUILayout.Button("Emit At Random Nearby"))
                            {
                                Vector3 around = _sensor.transform.position + (Random.insideUnitSphere * (_sensor.baseHearingRadius * 0.75f));
                                around.y = _sensor.transform.position.y;
                                var evt = new SoundEvent(around, _testLoudness, _testMaxRange, _testSoundType, _sensor);
                                SoundSystem.Raise(evt);
                            }
                        }
                    }

                    // Last heard info
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Last Heard", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Type", _lastType);
                        EditorGUILayout.LabelField("Loudness", _lastLoudness.ToString("0.00"));
                        EditorGUILayout.LabelField("Distance", _lastDistance.ToString("0.00") + " m");

                        if (_lastHeardTime >= 0)
                        {
                            double age = EditorApplication.timeSinceStartup - _lastHeardTime;
                            EditorGUILayout.LabelField("Age", age.ToString("0.00") + " s ago");
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Age", "—");
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to emit test sounds and see live events.", MessageType.Info);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var title = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };
                GUILayout.Label("Hearing Sensor", title);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Docs", GUILayout.Width(56)))
                {
                    Application.OpenURL("https://example.com"); // Replace with your docs URL
                }
            }
            EditorGUILayout.Space(2f);
        }
    }
}
#endif