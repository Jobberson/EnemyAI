#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SnogTools.AI.Editor
{
    [CustomEditor(typeof(EnemyAIController))]
    public class EnemyAIControllerEditor : UnityEditor.Editor
    {
        // Foldout states (persisted via EditorPrefs keys)
        private bool _foldRefs;
        private bool _foldPatrol;
        private bool _foldInvestigate;
        private bool _foldChase;
        private bool _foldGizmos;

        // Property cache
        private SerializedProperty propVision;
        private SerializedProperty propHearing;
        private SerializedProperty propMemory;

        private SerializedProperty propPatrolMode;
        private SerializedProperty propWaypointTolerance;
        private SerializedProperty propWaitAtDestination;

        // Waypoints
        private SerializedProperty propPatrolPoints;

        // Random patrol
        private SerializedProperty propRandomCenterMode;
        private SerializedProperty propRandomCenterTransform;
        private SerializedProperty propRandomCenterPoint;
        private SerializedProperty propRandomRadius;
        private SerializedProperty propRandomWaitMin;
        private SerializedProperty propRandomWaitMax;
        private SerializedProperty propRandomMaxAttempts;
        private SerializedProperty propGroundRaycastDepth;
        private SerializedProperty propGroundLayer;

        // Investigate/Search
        private SerializedProperty propInvestigateDuration;
        private SerializedProperty propSearchRadius;
        private SerializedProperty propSearchDuration;

        // Chase
        private SerializedProperty propRepathInterval;
        private SerializedProperty propGiveUpAfterSeconds;

        // Gizmos
        private SerializedProperty propGizmoShowRandomArea;
        private SerializedProperty propGizmoShowRandomLast;
        private SerializedProperty propGizmoShowWaypoints;

        private const string KEY_PREFIX = "SnogTools.AI.EnemyAIController.";
        private EnemyAIController _controller;

        private void OnEnable()
        {
            _controller = (EnemyAIController)target;

            // Cache properties
            propVision = serializedObject.FindProperty("vision");
            propHearing = serializedObject.FindProperty("hearing");
            propMemory = serializedObject.FindProperty("memory");

            propPatrolMode = serializedObject.FindProperty("patrolMode");
            propWaypointTolerance = serializedObject.FindProperty("waypointTolerance");
            propWaitAtDestination = serializedObject.FindProperty("waitAtDestination");

            propPatrolPoints = serializedObject.FindProperty("patrolPoints");

            propRandomCenterMode = serializedObject.FindProperty("randomCenterMode");
            propRandomCenterTransform = serializedObject.FindProperty("randomCenterTransform");
            propRandomCenterPoint = serializedObject.FindProperty("randomCenterPoint");
            propRandomRadius = serializedObject.FindProperty("randomPatrolRadius");
            propRandomWaitMin = serializedObject.FindProperty("randomWaitMin");
            propRandomWaitMax = serializedObject.FindProperty("randomWaitMax");
            propRandomMaxAttempts = serializedObject.FindProperty("randomMaxAttempts");
            propGroundRaycastDepth = serializedObject.FindProperty("groundRaycastDepth");
            propGroundLayer = serializedObject.FindProperty("groundLayer");

            propInvestigateDuration = serializedObject.FindProperty("investigateDuration");
            propSearchRadius = serializedObject.FindProperty("searchRadius");
            propSearchDuration = serializedObject.FindProperty("searchDuration");

            propRepathInterval = serializedObject.FindProperty("repathInterval");
            propGiveUpAfterSeconds = serializedObject.FindProperty("giveUpAfterSeconds");

            propGizmoShowRandomArea = serializedObject.FindProperty("gizmoShowRandomPatrolArea");
            propGizmoShowRandomLast = serializedObject.FindProperty("gizmoShowRandomLastPoint");
            propGizmoShowWaypoints = serializedObject.FindProperty("gizmoShowWaypoints");

            // Load foldout prefs
            _foldRefs = EditorPrefs.GetBool(KEY_PREFIX + "foldRefs", true);
            _foldPatrol = EditorPrefs.GetBool(KEY_PREFIX + "foldPatrol", true);
            _foldInvestigate = EditorPrefs.GetBool(KEY_PREFIX + "foldInvestigate", true);
            _foldChase = EditorPrefs.GetBool(KEY_PREFIX + "foldChase", true);
            _foldGizmos = EditorPrefs.GetBool(KEY_PREFIX + "foldGizmos", true);
        }

        private void OnDisable()
        {
            // Save foldout prefs
            EditorPrefs.SetBool(KEY_PREFIX + "foldRefs", _foldRefs);
            EditorPrefs.SetBool(KEY_PREFIX + "foldPatrol", _foldPatrol);
            EditorPrefs.SetBool(KEY_PREFIX + "foldInvestigate", _foldInvestigate);
            EditorPrefs.SetBool(KEY_PREFIX + "foldChase", _foldChase);
            EditorPrefs.SetBool(KEY_PREFIX + "foldGizmos", _foldGizmos);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();

            _foldRefs = EditorGUILayout.BeginFoldoutHeaderGroup(_foldRefs, "References");
            if (_foldRefs)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propVision);
                EditorGUILayout.PropertyField(propHearing);
                EditorGUILayout.PropertyField(propMemory);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            _foldPatrol = EditorGUILayout.BeginFoldoutHeaderGroup(_foldPatrol, "State: Patrol");
            if (_foldPatrol)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(propPatrolMode);

                // Shared patrol settings
                EditorGUILayout.PropertyField(propWaypointTolerance, new GUIContent("Arrival Tolerance"));
                EditorGUILayout.PropertyField(propWaitAtDestination, new GUIContent("Wait At Destination"));

                EditorGUILayout.Space(4f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var mode = (PatrolMode)propPatrolMode.enumValueIndex;

                    if (mode == PatrolMode.Waypoints)
                    {
                        EditorGUILayout.LabelField("Waypoints Mode", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(propPatrolPoints, true);
                        EditorGUI.indentLevel--;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Random Mode", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;

                        EditorGUILayout.PropertyField(propRandomCenterMode);

                        var centerMode = (RandomPatrolCenterMode)propRandomCenterMode.enumValueIndex;
                        if (centerMode == RandomPatrolCenterMode.Transform)
                        {
                            EditorGUILayout.PropertyField(propRandomCenterTransform);
                            if (propRandomCenterTransform.objectReferenceValue == null)
                            {
                                EditorGUILayout.HelpBox("Assign a Transform to use as patrol center.", MessageType.Info);
                            }
                        }
                        else if (centerMode == RandomPatrolCenterMode.StaticPoint)
                        {
                            EditorGUILayout.PropertyField(propRandomCenterPoint);
                            if (GUILayout.Button("Use Current Position As Static Point"))
                            {
                                propRandomCenterPoint.vector3Value = _controller.transform.position;
                            }
                        }

                        EditorGUILayout.PropertyField(propRandomRadius, new GUIContent("Radius"));
                        EditorGUILayout.Slider(propRandomWaitMin, 0f, 10f, new GUIContent("Wait Min"));
                        EditorGUILayout.Slider(propRandomWaitMax, 0f, 10f, new GUIContent("Wait Max"));

                        // ensure min <= max visually
                        if (propRandomWaitMax.floatValue < propRandomWaitMin.floatValue)
                        {
                            propRandomWaitMax.floatValue = propRandomWaitMin.floatValue;
                        }

                        EditorGUILayout.PropertyField(propRandomMaxAttempts, new GUIContent("Max Attempts"));
                        EditorGUILayout.PropertyField(propGroundRaycastDepth, new GUIContent("Ground Raycast Depth"));
                        EditorGUILayout.PropertyField(propGroundLayer, new GUIContent("Ground Layer"));

                        EditorGUI.indentLevel--;
                    }
                }

                // Runtime helper buttons (safe in edit mode)
                EditorGUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Go Idle"))
                    {
                        foreach (var t in targets)
                        {
                            var c = t as EnemyAIController;
                            c?.Abort();
                        }
                    }

                    if (GUILayout.Button("Force Patrol"))
                    {
                        foreach (var t in targets)
                        {
                            var c = t as EnemyAIController;
                            if (c == null) continue;

                            // Re-enter patrol state to trigger immediate destination selection
                            var so = new SerializedObject(c);
                            var modeProp = so.FindProperty("patrolMode");
                            var pointsProp = so.FindProperty("patrolPoints");

                            bool canPatrol =
                                ((PatrolMode)modeProp.enumValueIndex == PatrolMode.Waypoints && pointsProp != null && pointsProp.arraySize > 0)
                                || ((PatrolMode)modeProp.enumValueIndex == PatrolMode.Random);

                            if (canPatrol)
                            {
                                // simulate SetState to Patrol via runtime
                                // Note: We do not expose SetState publicly; entering by toggling fields is sufficient in playmode.
                                if (Application.isPlaying)
                                {
                                    // Nudge the agent by reassigning the same mode to trigger UpdatePatrol flow
                                    c.enabled = false;
                                    c.enabled = true;
                                }
                            }
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            _foldInvestigate = EditorGUILayout.BeginFoldoutHeaderGroup(_foldInvestigate, "State: Investigate / Search");
            if (_foldInvestigate)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propInvestigateDuration);
                EditorGUILayout.PropertyField(propSearchRadius);
                EditorGUILayout.PropertyField(propSearchDuration);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            _foldChase = EditorGUILayout.BeginFoldoutHeaderGroup(_foldChase, "State: Chase");
            if (_foldChase)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propRepathInterval);
                EditorGUILayout.PropertyField(propGiveUpAfterSeconds);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(3f);

            _foldGizmos = EditorGUILayout.BeginFoldoutHeaderGroup(_foldGizmos, "Gizmos / Debug");
            if (_foldGizmos)
            {
                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var mode = (PatrolMode)propPatrolMode.enumValueIndex;

                    if (mode == PatrolMode.Random)
                    {
                        EditorGUILayout.PropertyField(propGizmoShowRandomArea, new GUIContent("Show Random Patrol Area"));
                        EditorGUILayout.PropertyField(propGizmoShowRandomLast, new GUIContent("Show Last Random Point"));
                    }
                    else if (mode == PatrolMode.Waypoints)
                    {
                        EditorGUILayout.PropertyField(propGizmoShowWaypoints, new GUIContent("Show Waypoints"));
                    }

                    // Info & scene refresh
                    if (GUILayout.Button("Refresh Scene Gizmos"))
                    {
                        SceneView.RepaintAll();
                    }
                }

                // Runtime quick info
                if (Application.isPlaying && _controller != null)
                {
                    EditorGUILayout.Space(3f);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("State", _controller.name);
                        // Could show more runtime info if you expose it, e.g., current target, memory, etc.
                    }
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
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };

                GUILayout.Label("Enemy AI Controller", titleStyle);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Docs", GUILayout.Width(56)))
                {
                    Application.OpenURL("https://example.com"); // Replace with your docs URL
                }

                if (GUILayout.Button("Samples", GUILayout.Width(70)))
                {
                    // Optional: ping sample folder
                    // EditorUtility.DisplayDialog("Samples", "Open Samples~/BasicDemo/", "OK");
                }
            }

            EditorGUILayout.Space(2f);
        }
    }
}
#endif