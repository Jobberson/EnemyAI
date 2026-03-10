#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace SnogTools.AI.Editor
{
    public class StealthSetupWizard : EditorWindow
    {
        private const string MenuPath = "SnogTools/AI/Stealth Setup Wizard";
        private const string PrefPrefix = "SnogTools.AI.SetupWizard.";

        // Desired project items
        private string _groundLayerName = "Ground";
        private string _obstacleLayerName = "Obstacles";
        private string _targetLayerName = "PerceptionTarget";
        private string _playerTagName = "Player";

        // Scene selections
        private GameObject _playerGO;
        private bool _addPerceptionTargetToPlayer = true;
        private bool _addSoundEmitterToPlayer = true;
        private bool _addFootstepTesterToPlayer = true;

        // Enemy setup
        private Object[] _enemyObjects = new Object[0];
        private bool _configureEnemyMasks = true;
        private bool _ensureCoreComponents = true;

        // Patrol defaults applied when creating EnemyAIController
        private bool _avoidPlayerLoSInRandomPatrol = true;

        // Validation cache
        private int _groundLayerIndex = -1;
        private int _obstacleLayerIndex = -1;
        private int _targetLayerIndex = -1;

        private Vector2 _scroll;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var wnd = GetWindow<StealthSetupWizard>("Stealth Setup");
            wnd.minSize = new Vector2(480, 520);
            wnd.Show();
        }

        private void OnEnable()
        {
            _groundLayerName = EditorPrefs.GetString(PrefPrefix + "groundLayer", "Ground");
            _obstacleLayerName = EditorPrefs.GetString(PrefPrefix + "obstacleLayer", "Obstacles");
            _targetLayerName = EditorPrefs.GetString(PrefPrefix + "targetLayer", "PerceptionTarget");
            _playerTagName = EditorPrefs.GetString(PrefPrefix + "playerTag", "Player");

            _addPerceptionTargetToPlayer = EditorPrefs.GetBool(PrefPrefix + "addPT", true);
            _addSoundEmitterToPlayer = EditorPrefs.GetBool(PrefPrefix + "addSE", true);
            _addFootstepTesterToPlayer = EditorPrefs.GetBool(PrefPrefix + "addFT", true);

            _configureEnemyMasks = EditorPrefs.GetBool(PrefPrefix + "cfgMasks", true);
            _ensureCoreComponents = EditorPrefs.GetBool(PrefPrefix + "ensComp", true);
            _avoidPlayerLoSInRandomPatrol = EditorPrefs.GetBool(PrefPrefix + "avoidLOS", true);
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefPrefix + "groundLayer", _groundLayerName);
            EditorPrefs.SetString(PrefPrefix + "obstacleLayer", _obstacleLayerName);
            EditorPrefs.SetString(PrefPrefix + "targetLayer", _targetLayerName);
            EditorPrefs.SetString(PrefPrefix + "playerTag", _playerTagName);

            EditorPrefs.SetBool(PrefPrefix + "addPT", _addPerceptionTargetToPlayer);
            EditorPrefs.SetBool(PrefPrefix + "addSE", _addSoundEmitterToPlayer);
            EditorPrefs.SetBool(PrefPrefix + "addFT", _addFootstepTesterToPlayer);

            EditorPrefs.SetBool(PrefPrefix + "cfgMasks", _configureEnemyMasks);
            EditorPrefs.SetBool(PrefPrefix + "ensComp", _ensureCoreComponents);
            EditorPrefs.SetBool(PrefPrefix + "avoidLOS", _avoidPlayerLoSInRandomPatrol);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();

            DrawProjectSetup();
            EditorGUILayout.Space(6);

            DrawSceneSetup();
            EditorGUILayout.Space(6);

            DrawEnemySetup();
            EditorGUILayout.Space(6);

            DrawNavigationHelp();
            EditorGUILayout.Space(6);

            DrawValidationSummary();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var title = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14
                };
                GUILayout.Label("Stealth Setup Wizard", title);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Docs", GUILayout.Width(64)))
                {
                    Application.OpenURL("https://your-docs-url-here"); // replace with your docs URL
                }
            }

            EditorGUILayout.HelpBox("Create layers/tags, mark your Player, drop a SuspicionSystem, and configure Enemies with one click. Works with your NavMesh-based AI and inspectors.", MessageType.Info);
        }

        private void DrawProjectSetup()
        {
            EditorGUILayout.LabelField("1) Project Setup (Layers & Tags)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _groundLayerName = EditorGUILayout.TextField(new GUIContent("Ground Layer"), _groundLayerName);
                _obstacleLayerName = EditorGUILayout.TextField(new GUIContent("Occluders Layer"), _obstacleLayerName);
                _targetLayerName = EditorGUILayout.TextField(new GUIContent("Target Layer"), _targetLayerName);
                _playerTagName = EditorGUILayout.TextField(new GUIContent("Player Tag"), _playerTagName);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Create / Ensure Layers & Tag", GUILayout.Height(24)))
                    {
                        EnsureProjectItems();
                    }

                    if (GUILayout.Button("Validate", GUILayout.Width(90)))
                    {
                        ValidateLayers();
                    }
                }

                // Live validation badges
                ValidateLayers();
                DrawLayerBadge("Ground", _groundLayerName, _groundLayerIndex);
                DrawLayerBadge("Occluders", _obstacleLayerName, _obstacleLayerIndex);
                DrawLayerBadge("PerceptionTarget", _targetLayerName, _targetLayerIndex);

                bool tagOK = TagLayerUtility.TagExists(_playerTagName);
                DrawBadge("Player Tag", _playerTagName, tagOK);
            }
        }

        private void DrawSceneSetup()
        {
            EditorGUILayout.LabelField("2) Scene Setup (Player & Systems)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _playerGO = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Player Object"), _playerGO, typeof(GameObject), true);

                _addPerceptionTargetToPlayer = EditorGUILayout.ToggleLeft("Add PerceptionTarget", _addPerceptionTargetToPlayer);
                _addSoundEmitterToPlayer = EditorGUILayout.ToggleLeft("Add SoundEmitter (for footsteps)", _addSoundEmitterToPlayer);
                _addFootstepTesterToPlayer = EditorGUILayout.ToggleLeft("Add FootstepTestEmitter (press Space to emit)", _addFootstepTesterToPlayer);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = _playerGO != null;
                    if (GUILayout.Button("Mark Player & Add Components", GUILayout.Height(24)))
                    {
                        SetupPlayer(_playerGO);
                    }
                    GUI.enabled = true;

                    if (GUILayout.Button("Create SuspicionSystem (Scene)", GUILayout.Height(24)))
                    {
                        CreateSuspicionSystem();
                    }
                }

                // tiny hint
                EditorGUILayout.LabelField("Tip: Select your Player object in the Hierarchy and click the button above.", EditorStyles.miniLabel);
            }
        }

        private void DrawEnemySetup()
        {
            EditorGUILayout.LabelField("3) Enemy Setup (Select AI Instances or Prefabs)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox("Drag enemy instances or prefabs below. The wizard will add core components (if missing) and configure masks and layers.", MessageType.None);

                int newCount = Mathf.Max(0, EditorGUILayout.IntField("Size", _enemyObjects.Length));
                if (newCount != _enemyObjects.Length)
                {
                    System.Array.Resize(ref _enemyObjects, newCount);
                }

                for (int i = 0; i < _enemyObjects.Length; i++)
                {
                    _enemyObjects[i] = EditorGUILayout.ObjectField($"Enemy [{i}]", _enemyObjects[i], typeof(GameObject), true);
                }

                EditorGUILayout.Space(4);
                _ensureCoreComponents = EditorGUILayout.ToggleLeft("Ensure core components (NavMeshAgent, EnemyAIController, Vision/Hearing/Memory)", _ensureCoreComponents);
                _configureEnemyMasks = EditorGUILayout.ToggleLeft("Configure Vision/Hearing/Controller masks", _configureEnemyMasks);

                EditorGUILayout.Space(4);
                _avoidPlayerLoSInRandomPatrol = EditorGUILayout.ToggleLeft("Random Patrol: Avoid Player LoS (default on new controllers)", _avoidPlayerLoSInRandomPatrol);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Configure Selected Enemies", GUILayout.Height(24)))
                    {
                        ConfigureEnemies();
                    }

                    if (GUILayout.Button("Create Minimal Enemy In Scene", GUILayout.Height(24)))
                    {
                        CreateMinimalEnemy();
                    }
                }
            }
        }

        private void DrawNavigationHelp()
        {
            EditorGUILayout.LabelField("4) Navigation (Bake NavMesh)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("You need a baked NavMesh for agents to move.", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("• Mark walkable level geometry with the Ground layer.", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Add NavMeshAgent to Enemies (wizard can do it).", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("• Open the Navigation window and Bake.", EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Open Navigation Window", GUILayout.Height(22)))
                    {
                        EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
                    }

                    if (GUILayout.Button("Select All NavMeshAgents In Scene", GUILayout.Height(22)))
                    {
                        var agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
                        var list = new List<Object>();
                        foreach (var a in agents)
                        {
                            list.Add(a.gameObject);
                        }
                        Selection.objects = list.ToArray();
                    }
                }
            }
        }

        private void DrawValidationSummary()
        {
            EditorGUILayout.LabelField("5) Validation", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                ValidateLayers();
                bool okLayers = _groundLayerIndex >= 0 && _obstacleLayerIndex >= 0 && _targetLayerIndex >= 0;
                DrawBadge("Layers", okLayers ? "All present" : "Missing required layers", okLayers);

                bool hasSuspicion = FindObjectOfType<SuspicionSystem>() != null;
                DrawBadge("SuspicionSystem in Scene", hasSuspicion ? "Present" : "Missing", hasSuspicion);

                bool hasAnyAgent = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Length > 0;
                DrawBadge("NavMeshAgents in Scene", hasAnyAgent ? "Found" : "None", hasAnyAgent);

                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox("If anything is missing, use the buttons above to create/configure it.", MessageType.Info);
            }
        }

        // ----- Actions -----

        private void EnsureProjectItems()
        {
            Undo.IncrementCurrentGroup();

            _groundLayerIndex = TagLayerUtility.EnsureLayer(_groundLayerName);
            _obstacleLayerIndex = TagLayerUtility.EnsureLayer(_obstacleLayerName);
            _targetLayerIndex = TagLayerUtility.EnsureLayer(_targetLayerName);
            TagLayerUtility.EnsureTag(_playerTagName);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidateLayers();
        }

        private void ValidateLayers()
        {
            _groundLayerIndex = LayerMask.NameToLayer(_groundLayerName);
            _obstacleLayerIndex = LayerMask.NameToLayer(_obstacleLayerName);
            _targetLayerIndex = LayerMask.NameToLayer(_targetLayerName);
        }

        private void SetupPlayer(GameObject go)
        {
            if (go == null)
                return;

            Undo.RecordObject(go, "Setup Player");

            // Tag & Layer (layer optional—typically keep player on Default)
            if (!TagLayerUtility.TagExists(_playerTagName))
            {
                TagLayerUtility.EnsureTag(_playerTagName);
            }
            go.tag = _playerTagName;

            if (_addPerceptionTargetToPlayer)
            {
                if (go.GetComponent<PerceptionTarget>() == null)
                {
                    Undo.AddComponent<PerceptionTarget>(go);
                }
            }

            if (_addSoundEmitterToPlayer)
            {
                if (go.GetComponent<SoundEmitter>() == null)
                {
                    Undo.AddComponent<SoundEmitter>(go);
                }
            }

            if (_addFootstepTesterToPlayer)
            {
                if (go.GetComponent<FootstepTestEmitter>() == null)
                {
                    Undo.AddComponent<FootstepTestEmitter>(go);
                }
            }

            // Optionally set the layer so vision sensors can find it via LayerMask
            if (_targetLayerIndex >= 0)
            {
                go.layer = _targetLayerIndex;
            }

            EditorGUIUtility.PingObject(go);
        }

        private void CreateSuspicionSystem()
        {
            var existing = FindObjectOfType<SuspicionSystem>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorUtility.DisplayDialog("SuspicionSystem", "A SuspicionSystem already exists in this scene.", "OK");
                return;
            }

            var go = new GameObject("SuspicionSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create SuspicionSystem");
            go.AddComponent<SuspicionSystem>();
            Selection.activeObject = go;
        }

        private void ConfigureEnemies()
        {
            ValidateLayers();

            foreach (var obj in _enemyObjects)
            {
                var go = obj as GameObject;
                if (go == null)
                    continue;

                Undo.RegisterFullObjectHierarchyUndo(go, "Configure Enemy");

                // Ensure core components
                if (_ensureCoreComponents)
                {
                    if (go.GetComponent<NavMeshAgent>() == null)
                    {
                        go.AddComponent<NavMeshAgent>();
                    }
                    if (go.GetComponent<ThreatMemory>() == null)
                    {
                        go.AddComponent<ThreatMemory>();
                    }
                    if (go.GetComponent<VisionSensor>() == null)
                    {
                        go.AddComponent<VisionSensor>();
                    }
                    if (go.GetComponent<HearingSensor>() == null)
                    {
                        go.AddComponent<HearingSensor>();
                    }
                    if (go.GetComponent<EnemyAIController>() == null)
                    {
                        go.AddComponent<EnemyAIController>();
                    }
                }

                // Configure masks
                if (_configureEnemyMasks)
                {
                    var vs = go.GetComponent<VisionSensor>();
                    var hs = go.GetComponent<HearingSensor>();
                    var ctrl = go.GetComponent<EnemyAIController>();

                    int targetMask = SafeMask(_targetLayerName);
                    int obstacleMask = SafeMask(_obstacleLayerName);
                    int groundMask = SafeMask(_groundLayerName);

                    if (vs != null)
                    {
                        Undo.RecordObject(vs, "Configure VisionSensor");
                        vs.targetMask = targetMask;
                        vs.occluderMask = obstacleMask;
                        EditorUtility.SetDirty(vs);
                    }

                    if (hs != null)
                    {
                        Undo.RecordObject(hs, "Configure HearingSensor");
                        hs.occluderMask = obstacleMask;
                        EditorUtility.SetDirty(hs);
                    }

                    if (ctrl != null)
                    {
                        Undo.RecordObject(ctrl, "Configure EnemyAIController");
                        ctrl.playerOccluderMask = obstacleMask;
                        ctrl.groundLayer = groundMask;
                        ctrl.hideFromPlayer = true; // default helps demos
                        ctrl.avoidPlayerLoSInRandomPatrol = _avoidPlayerLoSInRandomPatrol;
                        EditorUtility.SetDirty(ctrl);
                    }
                }

                EditorGUIUtility.PingObject(go);
            }
        }

        private int SafeMask(string layerName)
        {
            int li = LayerMask.NameToLayer(layerName);
            if (li < 0)
                return 0;
            return 1 << li;
        }

        private void CreateMinimalEnemy()
        {
            ValidateLayers();

            var go = new GameObject("Enemy");
            Undo.RegisterCreatedObjectUndo(go, "Create Enemy");

            var agent = go.AddComponent<NavMeshAgent>();
            var mem = go.AddComponent<ThreatMemory>();
            var vis = go.AddComponent<VisionSensor>();
            var hear = go.AddComponent<HearingSensor>();
            var ctrl = go.AddComponent<EnemyAIController>();

            // Masks
            int targetMask = SafeMask(_targetLayerName);
            int obstacleMask = SafeMask(_obstacleLayerName);
            int groundMask = SafeMask(_groundLayerName);

            vis.targetMask = targetMask;
            vis.occluderMask = obstacleMask;

            hear.occluderMask = obstacleMask;

            ctrl.playerOccluderMask = obstacleMask;
            ctrl.groundLayer = groundMask;
            ctrl.hideFromPlayer = true;
            ctrl.avoidPlayerLoSInRandomPatrol = _avoidPlayerLoSInRandomPatrol;

            // Place near scene view camera
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                go.transform.position = sv.pivot + sv.rotation * Vector3.forward * 4f;
            }

            Selection.activeObject = go;
        }

        // ----- UI helpers -----

        private void DrawLayerBadge(string label, string name, int index)
        {
            bool ok = index >= 0;
            DrawBadge(label, $"{name} {(ok ? $"(#{index})" : "(missing)")}", ok);
        }

        private void DrawBadge(string label, string message, bool ok)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var col = GUI.color;
                GUI.color = ok ? new Color(0.6f, 0.9f, 0.6f) : new Color(1f, 0.7f, 0.7f);
                GUILayout.Label(ok ? "●" : "○", GUILayout.Width(20));
                GUI.color = col;

                GUILayout.Label(label, GUILayout.Width(160));
                GUILayout.Label(message);
            }
        }
    }
}
#endif