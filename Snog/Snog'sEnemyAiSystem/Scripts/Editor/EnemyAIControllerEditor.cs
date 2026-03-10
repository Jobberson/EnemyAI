#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace SnogTools.AI.Editor
{
    [CustomEditor(typeof(EnemyAIController))]
    public class EnemyAIControllerEditor : UnityEditor.Editor
    {
        private bool _foldRefs;
        private bool _foldPatrol;
        private bool _foldInvestigate;
        private bool _foldChase;
        private bool _foldPredSusp;
        private bool _foldHide;
        private bool _foldGizmos;

        private const string KEY_PREFIX = "SnogTools.AI.EnemyAIController.";
        private EnemyAIController _c;

        // Cached props (existing)
        private SerializedProperty propVision;
        private SerializedProperty propHearing;
        private SerializedProperty propMemory;

        private SerializedProperty propPatrolMode;
        private SerializedProperty propWaypointTolerance;
        private SerializedProperty propWaitAtDestination;

        private SerializedProperty propPatrolPoints;

        private SerializedProperty propRandomCenterMode;
        private SerializedProperty propRandomCenterTransform;
        private SerializedProperty propRandomCenterPoint;
        private SerializedProperty propRandomRadius;
        private SerializedProperty propRandomWaitMin;
        private SerializedProperty propRandomWaitMax;
        private SerializedProperty propRandomMaxAttempts;
        private SerializedProperty propGroundRaycastDepth;
        private SerializedProperty propGroundLayer;
        private SerializedProperty propAvoidPlayerLoS;

        private SerializedProperty propInvestigateDuration;
        private SerializedProperty propSearchRadius;
        private SerializedProperty propSearchDuration;

        private SerializedProperty propRepathInterval;
        private SerializedProperty propGiveUpAfterSeconds;

        // New: Prediction / Suspicion
        private SerializedProperty propPredScaleDist;
        private SerializedProperty propPredMaxScale;
        private SerializedProperty propSuspQueryDist;
        private SerializedProperty propSuspThreshold;

        // New: Hide From Player
        private SerializedProperty propHideFromPlayer;
        private SerializedProperty propHideBehavior;
        private SerializedProperty propPlayerEye;
        private SerializedProperty propPlayerFOV;
        private SerializedProperty propWatchedDist;
        private SerializedProperty propHideRepathCooldown;
        private SerializedProperty propHideAttempts;
        private SerializedProperty propHideRadius;
        private SerializedProperty propPlayerOccluderMask;

        // Gizmos
        private SerializedProperty propGizmoShowRandomArea;
        private SerializedProperty propGizmoShowRandomLast;
        private SerializedProperty propGizmoShowWaypoints;

        private void OnEnable()
        {
            _c = (EnemyAIController)target;

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
            propAvoidPlayerLoS = serializedObject.FindProperty("avoidPlayerLoSInRandomPatrol");

            propInvestigateDuration = serializedObject.FindProperty("investigateDuration");
            propSearchRadius = serializedObject.FindProperty("searchRadius");
            propSearchDuration = serializedObject.FindProperty("searchDuration");

            propRepathInterval = serializedObject.FindProperty("repathInterval");
            propGiveUpAfterSeconds = serializedObject.FindProperty("giveUpAfterSeconds");

            // Prediction / Suspicion
            propPredScaleDist = serializedObject.FindProperty("predictionScaleDistance");
            propPredMaxScale = serializedObject.FindProperty("maxPredictionLeadScale");
            propSuspQueryDist = serializedObject.FindProperty("suspicionQueryDistance");
            propSuspThreshold = serializedObject.FindProperty("suspicionThreshold");

            // Hide From Player
            propHideFromPlayer = serializedObject.FindProperty("hideFromPlayer");
            propHideBehavior = serializedObject.FindProperty("hideBehavior");
            propPlayerEye = serializedObject.FindProperty("playerEye");
            propPlayerFOV = serializedObject.FindProperty("playerFOV");
            propWatchedDist = serializedObject.FindProperty("watchedDistance");
            propHideRepathCooldown = serializedObject.FindProperty("hideRepathCooldown");
            propHideAttempts = serializedObject.FindProperty("hideSampleAttempts");
            propHideRadius = serializedObject.FindProperty("hideSearchRadius");
            propPlayerOccluderMask = serializedObject.FindProperty("playerOccluderMask");

            // Gizmos
            propGizmoShowRandomArea = serializedObject.FindProperty("gizmoShowRandomPatrolArea");
            propGizmoShowRandomLast = serializedObject.FindProperty("gizmoShowRandomLastPoint");
            propGizmoShowWaypoints = serializedObject.FindProperty("gizmoShowWaypoints");

            _foldRefs = EditorPrefs.GetBool(KEY_PREFIX + "foldRefs", true);
            _foldPatrol = EditorPrefs.GetBool(KEY_PREFIX + "foldPatrol", true);
            _foldInvestigate = EditorPrefs.GetBool(KEY_PREFIX + "foldInvestigate", true);
            _foldChase = EditorPrefs.GetBool(KEY_PREFIX + "foldChase", true);
            _foldPredSusp = EditorPrefs.GetBool(KEY_PREFIX + "foldPredSusp", true);
            _foldHide = EditorPrefs.GetBool(KEY_PREFIX + "foldHide", true);
            _foldGizmos = EditorPrefs.GetBool(KEY_PREFIX + "foldGizmos", true);
        }

        private void OnDisable()
        {
            EditorPrefs.SetBool(KEY_PREFIX + "foldRefs", _foldRefs);
            EditorPrefs.SetBool(KEY_PREFIX + "foldPatrol", _foldPatrol);
            EditorPrefs.SetBool(KEY_PREFIX + "foldInvestigate", _foldInvestigate);
            EditorPrefs.SetBool(KEY_PREFIX + "foldChase", _foldChase);
            EditorPrefs.SetBool(KEY_PREFIX + "foldPredSusp", _foldPredSusp);
            EditorPrefs.SetBool(KEY_PREFIX + "foldHide", _foldHide);
            EditorPrefs.SetBool(KEY_PREFIX + "foldGizmos", _foldGizmos);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawHeader();

            // References
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

            EditorGUILayout.Space(4f);

            // Patrol
            _foldPatrol = EditorGUILayout.BeginFoldoutHeaderGroup(_foldPatrol, "State: Patrol");
            if (_foldPatrol)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propPatrolMode);
                EditorGUILayout.PropertyField(propWaypointTolerance, new GUIContent("Arrival Tolerance"));
                EditorGUILayout.PropertyField(propWaitAtDestination, new GUIContent("Wait At Destination"));

                EditorGUILayout.Space(3f);
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
                                propRandomCenterPoint.vector3Value = _c.transform.position;
                            }
                        }

                        EditorGUILayout.PropertyField(propRandomRadius, new GUIContent("Radius"));
                        EditorGUILayout.Slider(propRandomWaitMin, 0f, 10f, new GUIContent("Wait Min"));
                        EditorGUILayout.Slider(propRandomWaitMax, 0f, 10f, new GUIContent("Wait Max"));
                        if (propRandomWaitMax.floatValue < propRandomWaitMin.floatValue)
                        {
                            propRandomWaitMax.floatValue = propRandomWaitMin.floatValue;
                        }

                        EditorGUILayout.PropertyField(propRandomMaxAttempts, new GUIContent("Max Attempts"));
                        EditorGUILayout.PropertyField(propGroundRaycastDepth, new GUIContent("Ground Raycast Depth"));
                        EditorGUILayout.PropertyField(propGroundLayer, new GUIContent("Ground Layer"));

                        EditorGUILayout.PropertyField(propAvoidPlayerLoS, new GUIContent("Avoid Player LoS (Random Patrol)"));
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4f);

            // Investigate / Search
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

            EditorGUILayout.Space(4f);

            // Chase
            _foldChase = EditorGUILayout.BeginFoldoutHeaderGroup(_foldChase, "State: Chase");
            if (_foldChase)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propRepathInterval);
                EditorGUILayout.PropertyField(propGiveUpAfterSeconds);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4f);

            // Prediction / Suspicion
            _foldPredSusp = EditorGUILayout.BeginFoldoutHeaderGroup(_foldPredSusp, "Prediction / Suspicion");
            if (_foldPredSusp)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propPredScaleDist, new GUIContent("Prediction Scale Distance"));
                EditorGUILayout.PropertyField(propPredMaxScale, new GUIContent("Max Prediction Lead Scale"));
                EditorGUILayout.PropertyField(propSuspQueryDist, new GUIContent("Suspicion Query Distance"));
                EditorGUILayout.PropertyField(propSuspThreshold, new GUIContent("Suspicion Threshold"));
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4f);

            // Hide From Player
            _foldHide = EditorGUILayout.BeginFoldoutHeaderGroup(_foldHide, "Hide From Player (Bracken-like)");
            if (_foldHide)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propHideFromPlayer, new GUIContent("Enable"));
                EditorGUILayout.PropertyField(propHideBehavior, new GUIContent("Behavior"));

                // Info: We use Camera.main automatically
                EditorGUILayout.HelpBox("Uses Camera.main as the player's eye. You can assign Player Eye to override if needed.", MessageType.None);
                EditorGUILayout.PropertyField(propPlayerEye, new GUIContent("Player Eye (optional override)"));

                EditorGUILayout.PropertyField(propPlayerFOV, new GUIContent("Player FOV (°)"));
                EditorGUILayout.PropertyField(propWatchedDist, new GUIContent("Watched Distance"));
                EditorGUILayout.PropertyField(propHideRepathCooldown, new GUIContent("Repath Cooldown (s)"));
                EditorGUILayout.PropertyField(propHideAttempts, new GUIContent("Hide Sample Attempts"));
                EditorGUILayout.PropertyField(propHideRadius, new GUIContent("Hide Search Radius"));
                EditorGUILayout.PropertyField(propPlayerOccluderMask, new GUIContent("Player Occluder Mask"));

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.Space(4f);

            // Gizmos
            _foldGizmos = EditorGUILayout.BeginFoldoutHeaderGroup(_foldGizmos, "Gizmos / Debug");
            if (_foldGizmos)
            {
                EditorGUI.indentLevel++;
                var mode = (PatrolMode)propPatrolMode.enumValueIndex;
                if (mode == PatrolMode.Random)
                {
                    EditorGUILayout.PropertyField(propGizmoShowRandomArea, new GUIContent("Show Random Patrol Area"));
                    EditorGUILayout.PropertyField(propGizmoShowRandomLast, new GUIContent("Show Last Random Point"));
                }
                else
                {
                    EditorGUILayout.PropertyField(propGizmoShowWaypoints, new GUIContent("Show Waypoints"));
                }

                if (GUILayout.Button("Refresh Scene Gizmos"))
                {
                    SceneView.RepaintAll();
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
                    Application.OpenURL("https://your-docs-url-here");
                }
            }

            EditorGUILayout.Space(2f);
        }
    }
}
#endif