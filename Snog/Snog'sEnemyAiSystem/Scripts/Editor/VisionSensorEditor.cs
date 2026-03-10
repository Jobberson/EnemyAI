#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SnogTools.AI.Editor
{
    [CustomEditor(typeof(VisionSensor))]
    public class VisionSensorEditor : UnityEditor.Editor
    {
        private bool _foldTargetsFov;
        private bool _foldDetection;
        private bool _foldBudget;
        private bool _foldGizmos;
        private bool _foldRuntime;

        private const string KEY = "SnogTools.AI.VisionSensor.";
        private VisionSensor _sensor;

        // Cached serialized props
        private SerializedProperty propTargetMask;
        private SerializedProperty propOccluderMask;

        private SerializedProperty propFieldOfView;
        private SerializedProperty propViewDistance;
        private SerializedProperty propEyeHeight;

        private SerializedProperty propTimeToFullDetect;
        private SerializedProperty propDetectionDecay;
        private SerializedProperty propAngleWeight;
        private SerializedProperty propDistanceWeight;

        private SerializedProperty propScanInterval;
        private SerializedProperty propLosSamples;

        private SerializedProperty propGizmoShowFOV;
        private SerializedProperty propGizmoShowEyeRay;

        // Editor-only test helper
        private Transform _testTarget;

        private void OnEnable()
        {
            _sensor = (VisionSensor)target;

            propTargetMask = serializedObject.FindProperty("targetMask");
            propOccluderMask = serializedObject.FindProperty("occluderMask");

            propFieldOfView = serializedObject.FindProperty("fieldOfView");
            propViewDistance = serializedObject.FindProperty("viewDistance");
            propEyeHeight = serializedObject.FindProperty("eyeHeight");

            propTimeToFullDetect = serializedObject.FindProperty("timeToFullDetect");
            propDetectionDecay = serializedObject.FindProperty("detectionDecayPerSecond");
            propAngleWeight = serializedObject.FindProperty("angleWeight");
            propDistanceWeight = serializedObject.FindProperty("distanceWeight");

            propScanInterval = serializedObject.FindProperty("scanInterval");
            propLosSamples = serializedObject.FindProperty("losSamplePoints");

            propGizmoShowFOV = serializedObject.FindProperty("gizmoShowFOV");
            propGizmoShowEyeRay = serializedObject.FindProperty("gizmoShowEyeRay");

            // Load foldout states
            _foldTargetsFov = EditorPrefs.GetBool(KEY + "foldTargetsFov", true);
            _foldDetection = EditorPrefs.GetBool(KEY + "foldDetection", true);
            _foldBudget = EditorPrefs.GetBool(KEY + "foldBudget", true);
            _foldGizmos = EditorPrefs.GetBool(KEY + "foldGizmos", true);
            _foldRuntime = EditorPrefs.GetBool(KEY + "foldRuntime", true);
        }

        private void OnDisable()
        {
            // Persist foldout states
            EditorPrefs.SetBool(KEY + "foldTargetsFov", _foldTargetsFov);
            EditorPrefs.SetBool(KEY + "foldDetection", _foldDetection);
            EditorPrefs.SetBool(KEY + "foldBudget", _foldBudget);
            EditorPrefs.SetBool(KEY + "foldGizmos", _foldGizmos);
            EditorPrefs.SetBool(KEY + "foldRuntime", _foldRuntime);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();

            // Targets & FOV
            _foldTargetsFov = EditorGUILayout.BeginFoldoutHeaderGroup(_foldTargetsFov, "Targets & FOV");
            if (_foldTargetsFov)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propTargetMask, new GUIContent("Target Mask"));
                EditorGUILayout.PropertyField(propOccluderMask, new GUIContent("Occluder Mask"));

                EditorGUILayout.Space(2f);
                EditorGUILayout.Slider(propFieldOfView, 1f, 180f, new GUIContent("Field of View (°)"));
                EditorGUILayout.PropertyField(propViewDistance, new GUIContent("View Distance"));
                EditorGUILayout.PropertyField(propEyeHeight, new GUIContent("Eye Height"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            // Detection
            _foldDetection = EditorGUILayout.BeginFoldoutHeaderGroup(_foldDetection, "Detection");
            if (_foldDetection)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propTimeToFullDetect, new GUIContent("Time To Full Detect (s)"));
                EditorGUILayout.PropertyField(propDetectionDecay, new GUIContent("Decay Per Second"));
                EditorGUILayout.PropertyField(propAngleWeight, new GUIContent("Angle Weight Curve"));
                EditorGUILayout.PropertyField(propDistanceWeight, new GUIContent("Distance Weight Curve"));

                EditorGUILayout.Space(2f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reset Curves to Defaults"))
                    {
                        foreach (var t in targets)
                        {
                            var s = t as VisionSensor;
                            if (s == null) continue;
                            Undo.RecordObject(s, "Reset Vision Curves");
                            s.angleWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);
                            s.distanceWeight = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);
                            EditorUtility.SetDirty(s);
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            // Budget/Performance
            _foldBudget = EditorGUILayout.BeginFoldoutHeaderGroup(_foldBudget, "Budget / Performance");
            if (_foldBudget)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propScanInterval, new GUIContent("Scan Interval (s)"));
                EditorGUILayout.IntSlider(propLosSamples, 1, 3, new GUIContent("LOS Sample Points"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            // Gizmos / Debug
            _foldGizmos = EditorGUILayout.BeginFoldoutHeaderGroup(_foldGizmos, "Gizmos / Debug");
            if (_foldGizmos)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propGizmoShowFOV, new GUIContent("Show FOV Cone"));
                EditorGUILayout.PropertyField(propGizmoShowEyeRay, new GUIContent("Show Forward Ray"));

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Scene Gizmos"))
                    {
                        SceneView.RepaintAll();
                    }
                    if (GUILayout.Button("Frame Eye"))
                    {
                        var eye = _sensor.transform.position + Vector3.up * _sensor.eyeHeight;
                        SceneView.lastActiveSceneView?.Frame(new Bounds(eye, Vector3.one), false);
                    }
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            // Runtime Info + Quick LOS Test
            _foldRuntime = EditorGUILayout.BeginFoldoutHeaderGroup(_foldRuntime, "Runtime");
            if (_foldRuntime)
            {
                EditorGUI.indentLevel++;

                if (Application.isPlaying)
                {
                    // Detection progress bar
                    float pct = Mathf.Clamp01(_sensor.CurrentDetection);
                    Rect r = GUILayoutUtility.GetRect(18, 18);
                    EditorGUI.ProgressBar(r, pct, $"Detection: {(int)(pct * 100f)}%");

                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField("Current Target", _sensor.CurrentTarget ? _sensor.CurrentTarget.name : "(none)");

                    EditorGUILayout.Space(4f);
                    _testTarget = (Transform)EditorGUILayout.ObjectField("Test LOS Target", _testTarget, typeof(Transform), true);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = _testTarget != null;
                        if (GUILayout.Button("Test Line of Sight"))
                        {
                            bool has = _sensor.HasLineOfSight(_testTarget);
                            EditorUtility.DisplayDialog("VisionSensor • LOS Test", has ? "Has Line of Sight ✅" : "No Line of Sight ❌", "OK");
                        }
                        GUI.enabled = true;

                        if (GUILayout.Button("Ping Current Target") && _sensor.CurrentTarget != null)
                        {
                            EditorGUIUtility.PingObject(_sensor.CurrentTarget.gameObject);
                            Selection.activeObject = _sensor.CurrentTarget.gameObject;
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to see live detection and run LOS tests.", MessageType.Info);
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
                GUILayout.Label("Vision Sensor", title);

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