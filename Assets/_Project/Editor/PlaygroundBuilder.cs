using System.IO;
using Game.Core.Enums;
using Game.Gameplay.Interactables;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
using Game.Gameplay.Player.Commands;
using Game.Gameplay.Rooms;
using Game.Gameplay.Rooms.Conditions;
using Game.Gameplay.Spawning;
using Game.Gameplay.Checkpoints;
using Game.Gameplay.Hazards;
using Game.Gameplay.Player.Death;
using PurrNet;
using PurrNet.Transports;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Gameplay.Camera;
using Game.Network.Events;
using Game.Network.Scopes;
using Game.Network.Services;
using Game.Network.UI;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class PlaygroundBuilder
    {
        private const string PrefabsDir = "Assets/_Project/Prefabs";
        private const string MaterialsDir = "Assets/_Project/Art/Materials";
        private const string ScenePath = "Assets/_Project/Scenes/Rooms/Room_01.unity";

        static PlaygroundBuilder()
        {
            EditorApplication.delayCall += EnsureNetworkingInCurrentScene;

            string triggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "BuildPlayground.trigger");
            if (File.Exists(triggerPath))
            {
                try
                {
                    File.Delete(triggerPath);
                }
                catch
                {
                }

                EditorApplication.delayCall += () =>
                {
                    BuildAll();
                };
            }

            string campaignTriggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "BuildCampaign.trigger");
            if (File.Exists(campaignTriggerPath))
            {
                try
                {
                    File.Delete(campaignTriggerPath);
                }
                catch
                {
                }

                EditorApplication.delayCall += () =>
                {
                    RoomContentBuilder.BuildAllCampaignRooms();
                };
            }
        }

        [MenuItem("TPOB/1. Generar Prefabs y Playground")]
        public static void BuildAll()
        {
            EnsureDirectories();
            var materials = CreateMaterials();
            var prefabs = CreatePrefabs(materials);
            BuildPlaygroundScene(prefabs, materials);
            Debug.Log("<color=green>[TPOB] Playground y Prefabs generados con éxito en Room_01.unity!</color>");
        }


        [MenuItem("TPOB/2. Configurar Networking en Escena Actual")]
        public static void EnsureNetworkingInCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.isLoaded) return;

            // 1. Find or create --- NETWORKING --- root
            GameObject netGroup = GameObject.Find("--- NETWORKING ---");
            if (netGroup == null)
            {
                netGroup = new GameObject("--- NETWORKING ---");
                Undo.RegisterCreatedObjectUndo(netGroup, "Create --- NETWORKING ---");
            }

            // 2. Find or create [NETWORK_MANAGER]
            NetworkManager netManager = Object.FindFirstObjectByType<NetworkManager>();
            GameObject netManagerGo;
            if (netManager == null)
            {
                netManagerGo = new GameObject("[NETWORK_MANAGER]");
                netManager = netManagerGo.AddComponent<NetworkManager>();
                netManagerGo.AddComponent<UDPTransport>();
                Undo.RegisterCreatedObjectUndo(netManagerGo, "Create [NETWORK_MANAGER]");
            }
            else
            {
                netManagerGo = netManager.gameObject;
                if (netManagerGo.GetComponent<UDPTransport>() == null)
                {
                    netManagerGo.AddComponent<UDPTransport>();
                }
                if (netManagerGo.transform.parent != null)
                {
                    netManagerGo.transform.SetParent(null);
                }
            }

            var rules = AssetDatabase.LoadAssetAtPath<NetworkRules>("Assets/PurrNet/Defaults/NetworkRules/ServerStrict.asset");
            var visibility = AssetDatabase.LoadAssetAtPath<NetworkVisibilityRuleSet>("Assets/PurrNet/Defaults/VisibilitySets/AlwaysVisible.asset");
            var netPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/_Project/Network/NetworkPrefabs.asset");

            var netManagerSo = new SerializedObject(netManager);
            if (rules != null && netManagerSo.FindProperty("_networkRules").objectReferenceValue == null)
            {
                netManagerSo.FindProperty("_networkRules").objectReferenceValue = rules;
            }
            if (visibility != null && netManagerSo.FindProperty("_visibilityRules").objectReferenceValue == null)
            {
                netManagerSo.FindProperty("_visibilityRules").objectReferenceValue = visibility;
            }
            if (netPrefabs != null && netManagerSo.FindProperty("_networkPrefabs").objectReferenceValue == null)
            {
                netManagerSo.FindProperty("_networkPrefabs").objectReferenceValue = netPrefabs;
            }
            netManagerSo.FindProperty("_dontDestroyOnLoad").boolValue = true;
            netManagerSo.ApplyModifiedPropertiesWithoutUndo();

            // 3. Find or create [NETWORK_EVENT_RELAY]
            NetworkEventRelay relay = Object.FindFirstObjectByType<NetworkEventRelay>();
            GameObject relayGo;
            if (relay == null)
            {
                relayGo = new GameObject("[NETWORK_EVENT_RELAY]");
                relayGo.transform.SetParent(netGroup.transform);
                relay = relayGo.AddComponent<NetworkEventRelay>();
                Undo.RegisterCreatedObjectUndo(relayGo, "Create [NETWORK_EVENT_RELAY]");
            }
            else
            {
                relayGo = relay.gameObject;
                if (relayGo.transform.parent == null)
                {
                    relayGo.transform.SetParent(netGroup.transform);
                }
            }

            // 4. Find or create [GAME_LIFETIME_SCOPE]
            GameLifetimeScope gameScope = Object.FindFirstObjectByType<GameLifetimeScope>();
            GameObject gameScopeGo;
            if (gameScope == null)
            {
                gameScopeGo = new GameObject("[GAME_LIFETIME_SCOPE]");
                gameScopeGo.transform.SetParent(netGroup.transform);
                gameScope = gameScopeGo.AddComponent<GameLifetimeScope>();
                Undo.RegisterCreatedObjectUndo(gameScopeGo, "Create [GAME_LIFETIME_SCOPE]");
            }
            else
            {
                gameScopeGo = gameScope.gameObject;
                if (gameScopeGo.transform.parent == null)
                {
                    gameScopeGo.transform.SetParent(netGroup.transform);
                }
            }

            // 5. Find or create LevelManager
            LevelManager levelManager = Object.FindFirstObjectByType<LevelManager>();
            if (levelManager == null)
            {
                levelManager = gameScopeGo.AddComponent<LevelManager>();
            }

            // 6. Find or create Bootstrapper
            Bootstrapper bootstrapper = Object.FindFirstObjectByType<Bootstrapper>();
            if (bootstrapper == null)
            {
                var bootstrapperGo = new GameObject("[BOOTSTRAPPER]");
                bootstrapperGo.transform.SetParent(netGroup.transform);
                bootstrapper = bootstrapperGo.AddComponent<Bootstrapper>();
                Undo.RegisterCreatedObjectUndo(bootstrapperGo, "Create [BOOTSTRAPPER]");
            }

            // 7. Find or create [CONNECTION_HUD]
            ConnectionHUD hud = Object.FindFirstObjectByType<ConnectionHUD>();
            if (hud == null)
            {
                var hudGo = new GameObject("[CONNECTION_HUD]");
                hudGo.transform.SetParent(netGroup.transform);
                hud = hudGo.AddComponent<ConnectionHUD>();
                Undo.RegisterCreatedObjectUndo(hudGo, "Create [CONNECTION_HUD]");
            }
            else
            {
                if (hud.transform.parent == null)
                {
                    hud.transform.SetParent(netGroup.transform);
                }
            }

            // Wire GameScope
            var gameScopeSo = new SerializedObject(gameScope);
            gameScopeSo.FindProperty("_networkManager").objectReferenceValue = netManager;
            gameScopeSo.FindProperty("_levelManager").objectReferenceValue = levelManager;
            gameScopeSo.ApplyModifiedPropertiesWithoutUndo();

            // Find or create [NETWORK_AUDIO_RELAY]
            var audioRelay = Object.FindFirstObjectByType<Game.Network.Audio.NetworkAudioRelay>();
            if (audioRelay == null)
            {
                var audioRelayGo = new GameObject("[NETWORK_AUDIO_RELAY]");
                audioRelayGo.transform.SetParent(netGroup.transform);
                audioRelay = audioRelayGo.AddComponent<Game.Network.Audio.NetworkAudioRelay>();
                Undo.RegisterCreatedObjectUndo(audioRelayGo, "Create [NETWORK_AUDIO_RELAY]");
            }
            else
            {
                if (audioRelay.transform.parent == null)
                {
                    audioRelay.transform.SetParent(netGroup.transform);
                }
            }

            // Wire RoomLifetimeScope
            RoomLifetimeScope roomScope = Object.FindFirstObjectByType<RoomLifetimeScope>();
            if (roomScope != null)
            {
                var roomScopeSo = new SerializedObject(roomScope);
                roomScopeSo.FindProperty("_networkEventRelay").objectReferenceValue = relay;
                roomScopeSo.FindProperty("_networkAudioRelay").objectReferenceValue = audioRelay;
                roomScopeSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(roomScope);
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;

            EditorUtility.SetDirty(netManager);
            EditorUtility.SetDirty(gameScope);
            EditorUtility.SetDirty(netGroup);
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("<color=cyan>[TPOB] Networking estructurado y configurado en escena: --- NETWORKING ---, NetworkManager, NetworkEventRelay, GameLifetimeScope, LevelManager, Bootstrapper!</color>");
        }

        public static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);
            if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);
            AssetDatabase.ImportAsset("Assets/_Project/Gameplay/Hazards/KillVolume.cs", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh();
        }

        public struct MaterialSet
        {
            public Material Floor;
            public Material Wall;
            public Material Ramp;
            public Material Legs;
            public Material Torso;
            public Material Grabbable;
            public Material Magnetic;
            public Material Kickable;
            public Material LeverBase;
            public Material LeverHandle;
            public Material DoorFrame;
            public Material DoorLeaf;
            public Material ButtonBase;
            public Material ButtonFace;
            public Material WeightPlate;
            public Material BreakableWall;
            public Material PushableBox;
            public Material PuzzleSocket;
            public Material CheckpointBase;
            public Material CheckpointIndicator;
            public Material KillVolume;
        }

        public struct PrefabSet
        {
            public GameObject Legs;
            public GameObject Torso;
            public GameObject GrabbableBox;
            public GameObject MagneticKey;
            public GameObject KickableObstacle;
            public GameObject LeverSwitch;
            public GameObject SlidingDoor;
            public GameObject TargetButton;
            public GameObject WeightPlatform;
            public GameObject BreakableWall;
            public GameObject PushableBox;
            public GameObject PuzzleSocket;
            public GameObject Checkpoint;
            public GameObject KillVolume;
            public GameObject SeeSawPlatform;
        }

        public static MaterialSet CreateMaterials()
        {
            MaterialSet set = new MaterialSet();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            set.Floor = GetOrCreateMaterial("Mat_Floor", new Color(0.18f, 0.2f, 0.25f), shader);
            set.Wall = GetOrCreateMaterial("Mat_Wall", new Color(0.38f, 0.4f, 0.45f), shader);
            set.Ramp = GetOrCreateMaterial("Mat_Ramp", new Color(0.28f, 0.32f, 0.38f), shader);
            set.Legs = GetOrCreateMaterial("Mat_Legs", new Color(0f, 0.75f, 0.95f), shader);
            set.Torso = GetOrCreateMaterial("Mat_Torso", new Color(1f, 0.48f, 0.05f), shader);
            set.Grabbable = GetOrCreateMaterial("Mat_GrabbableBox", new Color(0.95f, 0.75f, 0.2f), shader);
            set.Magnetic = GetOrCreateMaterial("Mat_MagneticKey", new Color(0.1f, 0.88f, 0.75f), shader, 0.85f, 0.8f);
            set.Kickable = GetOrCreateMaterial("Mat_KickableObstacle", new Color(0.85f, 0.15f, 0.15f), shader);
            set.LeverBase = GetOrCreateMaterial("Mat_LeverBase", new Color(0.12f, 0.15f, 0.22f), shader);
            set.LeverHandle = GetOrCreateMaterial("Mat_LeverHandle", new Color(0.15f, 0.95f, 0.35f), shader);
            set.DoorFrame = GetOrCreateMaterial("Mat_DoorFrame", new Color(0.2f, 0.22f, 0.28f), shader, 0.7f, 0.6f);
            set.DoorLeaf = GetOrCreateMaterial("Mat_DoorLeaf", new Color(0.15f, 0.55f, 0.85f), shader, 0.8f, 0.7f);
            set.ButtonBase = GetOrCreateMaterial("Mat_ButtonBase", new Color(0.25f, 0.25f, 0.28f), shader);
            set.ButtonFace = GetOrCreateMaterial("Mat_ButtonFace", new Color(0.9f, 0.2f, 0.2f), shader, 0.2f, 0.8f);
            set.WeightPlate = GetOrCreateMaterial("Mat_WeightPlate", new Color(0.85f, 0.7f, 0.1f), shader, 0.5f, 0.5f);
            set.BreakableWall = GetOrCreateMaterial("Mat_BreakableWall", new Color(0.45f, 0.35f, 0.3f), shader, 0.1f, 0.2f);
            set.PushableBox = GetOrCreateMaterial("Mat_PushableBox", new Color(0.3f, 0.65f, 0.45f), shader, 0.3f, 0.5f);
            set.PuzzleSocket = GetOrCreateMaterial("Mat_PuzzleSocket", new Color(0.15f, 0.75f, 0.85f), shader, 0.8f, 0.7f);
            set.CheckpointBase = GetOrCreateMaterial("Mat_CheckpointBase", new Color(0.2f, 0.22f, 0.28f), shader, 0.5f, 0.6f);
            set.CheckpointIndicator = GetOrCreateMaterial("Mat_CheckpointIndicator", new Color(0.85f, 0.45f, 0.1f), shader, 0.8f, 0.8f);
            set.KillVolume = GetOrCreateMaterial("Mat_KillVolume", new Color(0.85f, 0.15f, 0.15f, 0.3f), shader);

            AssetDatabase.SaveAssets();
            return set;
        }

        private static Material GetOrCreateMaterial(string name, Color color, Shader shader, float metallic = 0f, float smoothness = 0.5f)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static PrefabSet CreatePrefabs(MaterialSet mats)
        {
            PrefabSet prefabs = new PrefabSet();
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");

            // 1. LegsPlayer Prefab
            prefabs.Legs = CreateLegsPrefab(mats.Legs, inputActions);

            // 2. TorsoPlayer Prefab
            prefabs.Torso = CreateTorsoPrefab(mats.Torso, inputActions);

            // 3. GrabbableBox Prefab
            prefabs.GrabbableBox = CreateGrabbableBoxPrefab(mats.Grabbable);

            // 4. MagneticKey Prefab
            prefabs.MagneticKey = CreateMagneticKeyPrefab(mats.Magnetic);

            // 5. KickableObstacle Prefab
            prefabs.KickableObstacle = CreateKickableObstaclePrefab(mats.Kickable);

            // 6. LeverSwitch Prefab
            prefabs.LeverSwitch = CreateLeverPrefab(mats.LeverBase, mats.LeverHandle);

            // 7. SlidingDoor Prefab
            prefabs.SlidingDoor = CreateSlidingDoorPrefab(mats.DoorFrame, mats.DoorLeaf);

            // 8. TargetButton Prefab
            prefabs.TargetButton = CreateTargetButtonPrefab(mats.ButtonBase, mats.ButtonFace);

            // 9. WeightPlatform Prefab
            prefabs.WeightPlatform = CreateWeightPlatformPrefab(mats.Floor, mats.WeightPlate);

            // 10. BreakableWall Prefab
            prefabs.BreakableWall = CreateBreakableWallPrefab(mats.BreakableWall, mats.BreakableWall);

            // 11. PushableBox Prefab
            prefabs.PushableBox = CreatePushableBoxPrefab(mats.PushableBox);

            // 12. PuzzleSocket Prefab
            prefabs.PuzzleSocket = CreatePuzzleSocketPrefab(mats.DoorFrame, mats.PuzzleSocket);

            // 13. Checkpoint Prefab
            prefabs.Checkpoint = CreateCheckpointPrefab(mats.CheckpointBase, mats.CheckpointIndicator);

            // 14. KillVolume Prefab
            prefabs.KillVolume = CreateKillVolumePrefab();

            // 15. SeeSawPlatform Prefab
            prefabs.SeeSawPlatform = CreateSeeSawPrefab(mats.Floor);

            AssetDatabase.SaveAssets();
            return prefabs;
        }

        public static NetworkTransform AddServerAuthoritativeTransform(GameObject root)
        {
            var nt = root.AddComponent<NetworkTransform>();
            var ntSo = new SerializedObject(nt);
            var ownerAuthProp = ntSo.FindProperty("_ownerAuth");
            if (ownerAuthProp != null) ownerAuthProp.boolValue = false;
            var syncPosProp = ntSo.FindProperty("_syncPosition");
            if (syncPosProp != null) syncPosProp.enumValueIndex = (int)SyncMode.World;
            var syncRotProp = ntSo.FindProperty("_syncRotation");
            if (syncRotProp != null) syncRotProp.enumValueIndex = (int)SyncMode.World;
            var syncScaleProp = ntSo.FindProperty("_syncScale");
            if (syncScaleProp != null) syncScaleProp.boolValue = false;
            var syncParentProp = ntSo.FindProperty("_syncParent");
            if (syncParentProp != null) syncParentProp.boolValue = false;
            ntSo.ApplyModifiedPropertiesWithoutUndo();
            return nt;
        }

        private static GameObject CreateLegsPrefab(Material mat, InputActionAsset inputActions)
        {
            string path = $"{PrefabsDir}/LegsPlayer.prefab";
            GameObject root = new GameObject("LegsPlayer");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 70f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.9f, 0f);
            col.height = 1.8f;
            col.radius = 0.45f;

            AddServerAuthoritativeTransform(root);

            var controller = root.AddComponent<LegsController>();
            var reader = root.AddComponent<LegsInputReader>();
            var invoker = root.AddComponent<CommandInvoker>();

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            mesh.name = "LegsMesh";
            mesh.transform.SetParent(root.transform);
            mesh.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            mesh.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            mesh.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(mesh.GetComponent<Collider>());

            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(root.transform);
            groundCheck.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var kickPoint = new GameObject("KickPoint");
            kickPoint.transform.SetParent(root.transform);
            kickPoint.transform.localPosition = new Vector3(0f, 0.6f, 0.75f);

            var readerSo = new SerializedObject(reader);
            readerSo.FindProperty("_inputActions").objectReferenceValue = inputActions;
            readerSo.ApplyModifiedPropertiesWithoutUndo();

            var socketGo = new GameObject("FusionSocket");
            socketGo.transform.SetParent(root.transform);
            socketGo.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            var fusionSocket = socketGo.AddComponent<Game.Gameplay.Player.Robot.FusionSocket>();

            var socketVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socketVisual.name = "SocketVisual";
            socketVisual.transform.SetParent(socketGo.transform);
            socketVisual.transform.localPosition = Vector3.zero;
            socketVisual.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            socketVisual.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(socketVisual.GetComponent<Collider>());

            var dockImpulse = socketGo.AddComponent<CinemachineImpulseSource>();
            var socketSo = new SerializedObject(fusionSocket);
            socketSo.FindProperty("_attachPoint").objectReferenceValue = socketGo.transform;
            socketSo.FindProperty("_visualJoint").objectReferenceValue = socketVisual.transform;
            socketSo.FindProperty("_dockImpulseSource").objectReferenceValue = dockImpulse;
            socketSo.ApplyModifiedPropertiesWithoutUndo();

            var kickImpulse = root.AddComponent<CinemachineImpulseSource>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            controllerSo.FindProperty("_fusionSocket").objectReferenceValue = fusionSocket;
            controllerSo.FindProperty("_kickImpulseSource").objectReferenceValue = kickImpulse;
            controllerSo.FindProperty("_inputReader").objectReferenceValue = reader;
            controllerSo.FindProperty("_commandInvoker").objectReferenceValue = invoker;
            controllerSo.FindProperty("_groundCheckPoint").objectReferenceValue = groundCheck.transform;
            controllerSo.FindProperty("_kickPoint").objectReferenceValue = kickPoint.transform;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            var deathHandler = root.AddComponent<PlayerDeathHandler>();
            var deathSo = new SerializedObject(deathHandler);
            deathSo.FindProperty("_role").enumValueIndex = (int)PlayerRole.Legs;
            deathSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            deathSo.FindProperty("_visualModel").objectReferenceValue = mesh;
            deathSo.FindProperty("_impulseSource").objectReferenceValue = kickImpulse;
            deathSo.ApplyModifiedPropertiesWithoutUndo();

            col.sharedMaterial = Game.Gameplay.PhysicsJuice.PhysicsMaterialFactory.ComedicNormal;

            var impactFeedback = root.AddComponent<Game.Gameplay.Player.Juice.ImpactFeedbackSystem>();
            var tumble = root.AddComponent<Game.Gameplay.Player.Juice.TumbleController>();

            var impactSo = new SerializedObject(impactFeedback);
            impactSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            impactSo.FindProperty("_visualRoot").objectReferenceValue = mesh.transform;
            impactSo.FindProperty("_impulseSource").objectReferenceValue = kickImpulse;
            impactSo.ApplyModifiedPropertiesWithoutUndo();

            var tumbleSo = new SerializedObject(tumble);
            tumbleSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            tumbleSo.FindProperty("_visualTransform").objectReferenceValue = mesh.transform;
            tumbleSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateTorsoPrefab(Material mat, InputActionAsset inputActions)
        {
            string path = $"{PrefabsDir}/TorsoPlayer.prefab";
            GameObject root = new GameObject("TorsoPlayer");

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 40f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(0.9f, 1f, 0.9f);

            AddServerAuthoritativeTransform(root);

            var controller = root.AddComponent<TorsoController>();
            var reader = root.AddComponent<TorsoInputReader>();
            var invoker = root.AddComponent<CommandInvoker>();

            var aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(root.transform);
            aimPivot.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            var torsoMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torsoMesh.name = "TorsoMesh";
            torsoMesh.transform.SetParent(aimPivot.transform);
            torsoMesh.transform.localPosition = Vector3.zero;
            torsoMesh.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
            torsoMesh.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(torsoMesh.GetComponent<Collider>());

            var holdSocket = new GameObject("HoldSocket");
            holdSocket.transform.SetParent(aimPivot.transform);
            holdSocket.transform.localPosition = new Vector3(0f, 0f, 1.1f);

            var magnetOrigin = new GameObject("MagnetOrigin");
            magnetOrigin.transform.SetParent(aimPivot.transform);
            magnetOrigin.transform.localPosition = new Vector3(0f, 0f, 0.9f);

            var readerSo = new SerializedObject(reader);
            readerSo.FindProperty("_inputActions").objectReferenceValue = inputActions;
            readerSo.ApplyModifiedPropertiesWithoutUndo();

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            controllerSo.FindProperty("_inputReader").objectReferenceValue = reader;
            controllerSo.FindProperty("_commandInvoker").objectReferenceValue = invoker;
            controllerSo.FindProperty("_aimPivot").objectReferenceValue = aimPivot.transform;
            controllerSo.FindProperty("_holdSocket").objectReferenceValue = holdSocket.transform;
            controllerSo.FindProperty("_magnetOrigin").objectReferenceValue = magnetOrigin.transform;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            var deathImpulse = root.AddComponent<CinemachineImpulseSource>();
            var deathHandler = root.AddComponent<PlayerDeathHandler>();
            var deathSo = new SerializedObject(deathHandler);
            deathSo.FindProperty("_role").enumValueIndex = (int)PlayerRole.Torso;
            deathSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            deathSo.FindProperty("_visualModel").objectReferenceValue = torsoMesh;
            deathSo.FindProperty("_impulseSource").objectReferenceValue = deathImpulse;
            deathSo.ApplyModifiedPropertiesWithoutUndo();

            col.sharedMaterial = Game.Gameplay.PhysicsJuice.PhysicsMaterialFactory.ComedicNormal;

            var impactFeedback = root.AddComponent<Game.Gameplay.Player.Juice.ImpactFeedbackSystem>();
            var tumble = root.AddComponent<Game.Gameplay.Player.Juice.TumbleController>();

            var impactSo = new SerializedObject(impactFeedback);
            impactSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            impactSo.FindProperty("_visualRoot").objectReferenceValue = torsoMesh.transform;
            impactSo.FindProperty("_impulseSource").objectReferenceValue = deathImpulse;
            impactSo.ApplyModifiedPropertiesWithoutUndo();

            var tumbleSo = new SerializedObject(tumble);
            tumbleSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            tumbleSo.FindProperty("_visualTransform").objectReferenceValue = torsoMesh.transform;
            tumbleSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateGrabbableBoxPrefab(Material mat)
        {
            string path = $"{PrefabsDir}/GrabbableBox.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "GrabbableBox";
            root.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            root.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 2f;

            if (root.TryGetComponent<Collider>(out var boxCol))
            {
                boxCol.sharedMaterial = Game.Gameplay.PhysicsJuice.PhysicsMaterialFactory.ComedicNormal;
            }

            AddServerAuthoritativeTransform(root);
            root.AddComponent<GrabbableObject>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateMagneticKeyPrefab(Material mat)
        {
            string path = $"{PrefabsDir}/MagneticKey.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "MagneticKey";
            root.transform.localScale = new Vector3(0.4f, 0.2f, 0.4f);
            root.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.5f;

            AddServerAuthoritativeTransform(root);
            root.AddComponent<MagneticKey>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateKickableObstaclePrefab(Material mat)
        {
            string path = $"{PrefabsDir}/KickableObstacle.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "KickableObstacle";
            root.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
            root.GetComponent<Renderer>().sharedMaterial = mat;

            if (root.TryGetComponent<Collider>(out var kickCol))
            {
                kickCol.sharedMaterial = Game.Gameplay.PhysicsJuice.PhysicsMaterialFactory.BouncyRubber;
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f;

            AddServerAuthoritativeTransform(root);
            root.AddComponent<KickableTestObject>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateLeverPrefab(Material baseMat, Material handleMat)
        {
            string path = $"{PrefabsDir}/LeverSwitch.prefab";
            GameObject root = new GameObject("LeverSwitch");

            var triggerCol = root.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(1.4f, 1.2f, 1.4f);
            triggerCol.center = new Vector3(0f, 0.6f, 0f);

            var baseBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseBox.name = "Base";
            baseBox.transform.SetParent(root.transform);
            baseBox.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            baseBox.transform.localScale = new Vector3(0.7f, 0.2f, 0.7f);
            baseBox.GetComponent<Renderer>().sharedMaterial = baseMat;

            var handlePivot = new GameObject("HandlePivot");
            handlePivot.transform.SetParent(root.transform);
            handlePivot.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            handlePivot.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);

            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "Shaft";
            shaft.transform.SetParent(handlePivot.transform);
            shaft.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            shaft.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
            shaft.GetComponent<Renderer>().sharedMaterial = handleMat;
            Object.DestroyImmediate(shaft.GetComponent<Collider>());

            var knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            knob.name = "Knob";
            knob.transform.SetParent(handlePivot.transform);
            knob.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            knob.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            knob.GetComponent<Renderer>().sharedMaterial = handleMat;
            Object.DestroyImmediate(knob.GetComponent<Collider>());

            var lever = root.AddComponent<LeverMechanism>();

            var leverSo = new SerializedObject(lever);
            leverSo.FindProperty("_handleTransform").objectReferenceValue = handlePivot.transform;
            leverSo.FindProperty("_indicatorRenderer").objectReferenceValue = knob.GetComponent<Renderer>();
            leverSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSlidingDoorPrefab(Material frameMat, Material leafMat)
        {
            string path = $"{PrefabsDir}/SlidingDoor.prefab";
            GameObject root = new GameObject("SlidingDoor");

            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPost.name = "LeftPost";
            leftPost.transform.SetParent(root.transform);
            leftPost.transform.localPosition = new Vector3(-1.8f, 1.75f, 0f);
            leftPost.transform.localScale = new Vector3(0.4f, 3.5f, 0.6f);
            leftPost.GetComponent<Renderer>().sharedMaterial = frameMat;

            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPost.name = "RightPost";
            rightPost.transform.SetParent(root.transform);
            rightPost.transform.localPosition = new Vector3(1.8f, 1.75f, 0f);
            rightPost.transform.localScale = new Vector3(0.4f, 3.5f, 0.6f);
            rightPost.GetComponent<Renderer>().sharedMaterial = frameMat;

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Lintel";
            lintel.transform.SetParent(root.transform);
            lintel.transform.localPosition = new Vector3(0f, 3.65f, 0f);
            lintel.transform.localScale = new Vector3(4f, 0.4f, 0.6f);
            lintel.GetComponent<Renderer>().sharedMaterial = frameMat;

            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "DoorLeaf";
            leaf.transform.SetParent(root.transform);
            leaf.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            leaf.transform.localScale = new Vector3(3.2f, 3.2f, 0.25f);
            leaf.GetComponent<Renderer>().sharedMaterial = leafMat;

            var door = root.AddComponent<SlidingDoor>();
            var impulse = root.AddComponent<CinemachineImpulseSource>();

            var doorSo = new SerializedObject(door);
            doorSo.FindProperty("_doorLeaf").objectReferenceValue = leaf.transform;
            doorSo.FindProperty("_openOffset").vector3Value = new Vector3(0f, 3.3f, 0f);
            doorSo.FindProperty("_impulseSource").objectReferenceValue = impulse;
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateTargetButtonPrefab(Material baseMat, Material faceMat)
        {
            string path = $"{PrefabsDir}/TargetButton.prefab";
            GameObject root = new GameObject("TargetButton");

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0f, 0.1f);
            col.size = new Vector3(1.2f, 1.2f, 0.3f);

            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "BackingPlate";
            backing.transform.SetParent(root.transform);
            backing.transform.localPosition = new Vector3(0f, 0f, 0.05f);
            backing.transform.localScale = new Vector3(1.2f, 1.2f, 0.1f);
            backing.GetComponent<Renderer>().sharedMaterial = baseMat;
            Object.DestroyImmediate(backing.GetComponent<Collider>());

            var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            face.name = "ButtonFace";
            face.transform.SetParent(root.transform);
            face.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            face.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);
            var faceRenderer = face.GetComponent<Renderer>();
            faceRenderer.sharedMaterial = faceMat;
            Object.DestroyImmediate(face.GetComponent<Collider>());

            var btn = root.AddComponent<TargetButton>();

            var btnSo = new SerializedObject(btn);
            btnSo.FindProperty("_buttonFace").objectReferenceValue = face.transform;
            btnSo.FindProperty("_pressOffset").vector3Value = new Vector3(0f, 0f, -0.15f);
            btnSo.FindProperty("_indicatorRenderer").objectReferenceValue = faceRenderer;
            btnSo.FindProperty("_isToggle").boolValue = true;
            btnSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateWeightPlatformPrefab(Material baseMat, Material plateMat)
        {
            string path = $"{PrefabsDir}/WeightPlatform.prefab";
            GameObject root = new GameObject("WeightPlatform");

            var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "BaseRim";
            rim.transform.SetParent(root.transform);
            rim.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            rim.transform.localScale = new Vector3(2.6f, 0.1f, 2.6f);
            rim.GetComponent<Renderer>().sharedMaterial = baseMat;

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "PressurePlate";
            plate.transform.SetParent(root.transform);
            plate.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            plate.transform.localScale = new Vector3(2.2f, 0.15f, 2.2f);
            var plateRenderer = plate.GetComponent<Renderer>();
            plateRenderer.sharedMaterial = plateMat;

            var wp = root.AddComponent<WeightPlatform>();

            var wpSo = new SerializedObject(wp);
            wpSo.FindProperty("_platformPlate").objectReferenceValue = plate.transform;
            wpSo.FindProperty("_indicatorRenderer").objectReferenceValue = plateRenderer;
            wpSo.FindProperty("_sinkOffset").vector3Value = new Vector3(0f, -0.18f, 0f);
            wpSo.FindProperty("_detectionBoxHalfExtents").vector3Value = new Vector3(1.6f, 1.5f, 1.6f);
            wpSo.FindProperty("_detectionBoxOffset").vector3Value = new Vector3(0f, 1.5f, 0f);
            wpSo.FindProperty("_requiredMass").floatValue = 80f;
            wpSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBreakableWallPrefab(Material wallMat, Material chunkMat)
        {
            string path = $"{PrefabsDir}/BreakableWall.prefab";
            GameObject root = new GameObject("BreakableWall");

            var solidCol = root.AddComponent<BoxCollider>();
            solidCol.size = new Vector3(3f, 3f, 0.6f);
            solidCol.center = new Vector3(0f, 1.5f, 0f);

            var intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intact.name = "IntactWall";
            intact.transform.SetParent(root.transform);
            intact.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            intact.transform.localScale = new Vector3(3f, 3f, 0.6f);
            intact.GetComponent<Renderer>().sharedMaterial = wallMat;
            Object.DestroyImmediate(intact.GetComponent<Collider>());

            var brokenRoot = new GameObject("BrokenPieces");
            brokenRoot.transform.SetParent(root.transform);
            brokenRoot.transform.localPosition = Vector3.zero;

            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    chunk.name = $"Chunk_{x}_{y}";
                    chunk.transform.SetParent(brokenRoot.transform);
                    chunk.transform.localPosition = new Vector3(-1.125f + x * 0.75f, 0.75f + y * 1.5f, 0f);
                    chunk.transform.localScale = new Vector3(0.7f, 1.4f, 0.5f);
                    chunk.GetComponent<Renderer>().sharedMaterial = chunkMat;

                    var rb = chunk.AddComponent<Rigidbody>();
                    rb.mass = 2f;
                    rb.isKinematic = true;
                }
            }

            brokenRoot.SetActive(false);

            var bw = root.AddComponent<BreakableWall>();
            var impulse = root.AddComponent<CinemachineImpulseSource>();

            var bwSo = new SerializedObject(bw);
            bwSo.FindProperty("_intactVisual").objectReferenceValue = intact;
            bwSo.FindProperty("_brokenPiecesRoot").objectReferenceValue = brokenRoot;
            bwSo.FindProperty("_solidCollider").objectReferenceValue = solidCol;
            bwSo.FindProperty("_impulseSource").objectReferenceValue = impulse;
            bwSo.FindProperty("_minBreakForce").floatValue = 4f;
            bwSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePushableBoxPrefab(Material mat)
        {
            string path = $"{PrefabsDir}/PushableBox.prefab";
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "PushableBox";
            root.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            root.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 50f;

            AddServerAuthoritativeTransform(root);
            root.AddComponent<PushableBox>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePuzzleSocketPrefab(Material baseMat, Material ringMat)
        {
            string path = $"{PrefabsDir}/PuzzleSocket.prefab";
            GameObject root = new GameObject("PuzzleSocket");

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(root.transform);
            pedestal.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            pedestal.transform.localScale = new Vector3(0.9f, 0.4f, 0.9f);
            pedestal.GetComponent<Renderer>().sharedMaterial = baseMat;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "ReceptorRing";
            ring.transform.SetParent(root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            ring.transform.localScale = new Vector3(0.6f, 0.08f, 0.6f);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            var snap = new GameObject("SnapPoint");
            snap.transform.SetParent(root.transform);
            snap.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            var socket = root.AddComponent<PuzzleSocket>();

            var socketSo = new SerializedObject(socket);
            socketSo.FindProperty("_snapPoint").objectReferenceValue = snap.transform;
            socketSo.FindProperty("_detectionRadius").floatValue = 1.5f;
            socketSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCheckpointPrefab(Material baseMat, Material indicatorMat)
        {
            string path = $"{PrefabsDir}/Checkpoint.prefab";
            GameObject root = new GameObject("Checkpoint");

            var triggerCol = root.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(3f, 1.2f, 3f);
            triggerCol.center = new Vector3(0f, 0.6f, 0f);

            var basePlatform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePlatform.name = "BasePlatform";
            basePlatform.transform.SetParent(root.transform);
            basePlatform.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            basePlatform.transform.localScale = new Vector3(2.8f, 0.08f, 2.8f);
            basePlatform.GetComponent<Renderer>().sharedMaterial = baseMat;
            Object.DestroyImmediate(basePlatform.GetComponent<Collider>());

            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "IndicatorRing";
            indicator.transform.SetParent(root.transform);
            indicator.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            indicator.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);
            var indicatorRenderer = indicator.GetComponent<Renderer>();
            indicatorRenderer.sharedMaterial = indicatorMat;
            Object.DestroyImmediate(indicator.GetComponent<Collider>());

            var spLegs = new GameObject("SpawnPoint_Legs");
            spLegs.transform.SetParent(root.transform);
            spLegs.transform.localPosition = new Vector3(-0.75f, 0.15f, 0f);

            var spTorso = new GameObject("SpawnPoint_Torso");
            spTorso.transform.SetParent(root.transform);
            spTorso.transform.localPosition = new Vector3(0.75f, 0.15f, 0f);

            var impulse = root.AddComponent<CinemachineImpulseSource>();
            var checkpoint = root.AddComponent<Checkpoint>();

            var cpSo = new SerializedObject(checkpoint);
            cpSo.FindProperty("_legsSpawnPoint").objectReferenceValue = spLegs.transform;
            cpSo.FindProperty("_torsoSpawnPoint").objectReferenceValue = spTorso.transform;
            cpSo.FindProperty("_indicatorRenderer").objectReferenceValue = indicatorRenderer;
            cpSo.FindProperty("_impulseSource").objectReferenceValue = impulse;
            cpSo.FindProperty("_priority").intValue = 10;
            cpSo.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateKillVolumePrefab()
        {
            string path = $"{PrefabsDir}/KillVolume.prefab";
            GameObject root = new GameObject("KillVolume");

            var triggerCol = root.AddComponent<BoxCollider>();
            triggerCol.isTrigger = true;
            triggerCol.size = new Vector3(10f, 2f, 10f);
            triggerCol.center = new Vector3(0f, 0f, 0f);

            root.AddComponent<Game.Gameplay.Hazards.KillVolume>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject CreateSeeSawPrefab(Material mat)
        {
            string path = $"{PrefabsDir}/SeeSawPlatform.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject root = new GameObject("SeeSawPlatform");
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(8f, 0.4f, 3f);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 300f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

            var hinge = root.AddComponent<HingeJoint>();
            hinge.axis = Vector3.forward;
            hinge.anchor = Vector3.zero;

            root.AddComponent<Game.Gameplay.Interactables.SeeSawPlatform>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Plank";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(8f, 0.4f, 3f);
            visual.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var fulcrum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fulcrum.name = "Fulcrum";
            fulcrum.transform.SetParent(root.transform);
            fulcrum.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            fulcrum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            fulcrum.transform.localScale = new Vector3(0.8f, 1.5f, 0.8f);
            fulcrum.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(fulcrum.GetComponent<Collider>());

            AddServerAuthoritativeTransform(root);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildPlaygroundScene(PrefabSet prefabs, MaterialSet mats)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Clean previous objects
            var rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                Object.DestroyImmediate(rootObjects[i]);
            }

            // 1. ENVIRONMENT
            var envGroup = new GameObject("--- ENVIRONMENT ---");

            // Arena Floor (32x1x32)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ArenaFloor";
            floor.transform.SetParent(envGroup.transform);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(32f, 1f, 32f);
            floor.GetComponent<Renderer>().sharedMaterial = mats.Floor;

            // Walls with Exit Gateway opening (center gap for SlidingDoor)
            CreateWall("Wall_North_Left", new Vector3(-9f, 1.5f, 16f), new Vector3(14f, 3f, 1f), envGroup.transform, mats.Wall);
            CreateWall("Wall_North_Right", new Vector3(9f, 1.5f, 16f), new Vector3(14f, 3f, 1f), envGroup.transform, mats.Wall);
            CreateWall("Wall_North_Lintel", new Vector3(0f, 3.25f, 16f), new Vector3(4f, 0.5f, 1f), envGroup.transform, mats.Wall);
            CreateWall("Wall_South", new Vector3(0f, 1.5f, -16f), new Vector3(32f, 3f, 1f), envGroup.transform, mats.Wall);
            CreateWall("Wall_East", new Vector3(16f, 1.5f, 0f), new Vector3(1f, 3f, 32f), envGroup.transform, mats.Wall);
            CreateWall("Wall_West", new Vector3(-16f, 1.5f, 0f), new Vector3(1f, 3f, 32f), envGroup.transform, mats.Wall);

            // Secret Alcove Wall (entrance blocked by BreakableWall)
            CreateWall("Alcove_Wall_Back", new Vector3(11.5f, 1.5f, 10f), new Vector3(8f, 3f, 1f), envGroup.transform, mats.Wall);
            CreateWall("Alcove_Wall_Side", new Vector3(7.5f, 1.5f, 8f), new Vector3(1f, 3f, 5f), envGroup.transform, mats.Wall);

            // Raised Platform
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "RaisedPlatform";
            platform.transform.SetParent(envGroup.transform);
            platform.transform.position = new Vector3(0f, 1f, 9f);
            platform.transform.localScale = new Vector3(8f, 2f, 8f);
            platform.GetComponent<Renderer>().sharedMaterial = mats.Ramp;

            // Ramp up to platform
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "PlatformRamp";
            ramp.transform.SetParent(envGroup.transform);
            ramp.transform.position = new Vector3(-5.5f, 1f, 5.5f);
            ramp.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            ramp.transform.localScale = new Vector3(4f, 0.4f, 6.5f);
            ramp.GetComponent<Renderer>().sharedMaterial = mats.Ramp;

            // Kill Volume (underneath arena floor to catch falls)
            var killVolume = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KillVolume, envGroup.transform);
            killVolume.transform.position = new Vector3(0f, -4f, 0f);
            killVolume.transform.localScale = new Vector3(6f, 1f, 6f);

            // 2. INTERACTABLES
            var interactGroup = new GameObject("--- INTERACTABLES ---");

            // Mid-Room Checkpoint
            var checkpoint = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Checkpoint, interactGroup.transform);
            checkpoint.transform.position = new Vector3(0f, 0f, 4f);

            // Grabbable Boxes
            PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, interactGroup.transform);
            var box1 = GameObject.Find("GrabbableBox");
            if (box1) box1.transform.position = new Vector3(0f, 0.5f, -2f);

            var box2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, interactGroup.transform);
            box2.transform.position = new Vector3(-2f, 0.5f, 0f);

            var box3 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, interactGroup.transform);
            box3.transform.position = new Vector3(2f, 0.5f, 0f);

            // Pushable Box (for WeightPlatform)
            var pushBox = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PushableBox, interactGroup.transform);
            pushBox.transform.position = new Vector3(-7f, 0.6f, -4f);

            // Kickable Obstacles
            var kick1 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KickableObstacle, interactGroup.transform);
            kick1.transform.position = new Vector3(-4f, 0.6f, 3f);

            var kick2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KickableObstacle, interactGroup.transform);
            kick2.transform.position = new Vector3(-6f, 0.6f, 3f);

            // Breakable Wall blocking access to Alcove
            var breakableWall = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.BreakableWall, interactGroup.transform);
            breakableWall.transform.position = new Vector3(11.5f, 0f, 5.5f);

            // Target Button inside Alcove (rotated towards arena)
            var targetBtn = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, interactGroup.transform);
            targetBtn.transform.position = new Vector3(11.5f, 1.5f, 9.45f);
            targetBtn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Weight Platform
            var weightPlatform = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, interactGroup.transform);
            weightPlatform.transform.position = new Vector3(-7f, 0f, 0f);

            // Puzzle Socket
            var puzzleSocket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, interactGroup.transform);
            puzzleSocket.transform.position = new Vector3(5f, 0f, 0f);

            // Magnetic Key on elevated platform
            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, interactGroup.transform);
            key.transform.position = new Vector3(0f, 2.2f, 9f);

            // Lever Switch
            var lever = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, interactGroup.transform);
            lever.transform.position = new Vector3(5f, 0f, 3f);

            // Sliding Door at Exit Gateway
            var exitDoor = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.SlidingDoor, interactGroup.transform);
            exitDoor.transform.position = new Vector3(0f, 0f, 16f);

            // 3. MANAGEMENT
            var mgmtGroup = new GameObject("--- MANAGEMENT ---");

            var roomSystem = new GameObject("[ROOM_SYSTEM]");
            roomSystem.transform.SetParent(mgmtGroup.transform);
            var roomController = roomSystem.AddComponent<RoomController>();
            var roomScope = roomSystem.AddComponent<RoomLifetimeScope>();
            var roomCompletion = roomSystem.AddComponent<RoomCompletion>();

            // Setup Composite Condition Tree
            var conditionsGroup = new GameObject("[PUZZLE_CONDITIONS]");
            conditionsGroup.transform.SetParent(roomSystem.transform);

            var andCondGo = new GameObject("Root_AndCondition");
            andCondGo.transform.SetParent(conditionsGroup.transform);
            var andCondition = andCondGo.AddComponent<AndCondition>();

            // Condition 1: WeightPlatform activated
            var weightCondGo = new GameObject("Cond_WeightPlatform");
            weightCondGo.transform.SetParent(andCondGo.transform);
            var weightCondition = weightCondGo.AddComponent<MechanismCondition>();
            weightCondition.Configure(weightPlatform.GetComponent<WeightPlatform>(), true);

            // Condition 2: OR Condition (TargetButton hit OR Key slotted in PuzzleSocket)
            var orCondGo = new GameObject("Cond_Or_ButtonOrSocket");
            orCondGo.transform.SetParent(andCondGo.transform);
            var orCondition = orCondGo.AddComponent<OrCondition>();

            var btnCondGo = new GameObject("SubCond_TargetButton");
            btnCondGo.transform.SetParent(orCondGo.transform);
            var btnCondition = btnCondGo.AddComponent<MechanismCondition>();
            btnCondition.Configure(targetBtn.GetComponent<TargetButton>(), true);

            var socketCondGo = new GameObject("SubCond_PuzzleSocket");
            socketCondGo.transform.SetParent(orCondGo.transform);
            var socketCondition = socketCondGo.AddComponent<SocketCondition>();
            socketCondition.Configure(puzzleSocket.GetComponent<PuzzleSocket>());

            var leverCondGo = new GameObject("SubCond_LeverSwitch");
            leverCondGo.transform.SetParent(orCondGo.transform);
            var leverCondition = leverCondGo.AddComponent<MechanismCondition>();
            leverCondition.Configure(lever.GetComponent<LeverMechanism>(), true);

            orCondition.SetConditions(new ConditionBase[] { btnCondition, socketCondition, leverCondition });
            andCondition.SetConditions(new ConditionBase[] { weightCondition, orCondition });

            roomCompletion.Configure(andCondition, exitDoor.GetComponent<SlidingDoor>(), roomController);

            var spawnGroup = new GameObject("[SPAWN_POINTS]");
            spawnGroup.transform.SetParent(mgmtGroup.transform);
            var spawnManager = spawnGroup.AddComponent<SpawnPointManager>();

            var spLegs = new GameObject("SpawnPoint_Legs");
            spLegs.transform.SetParent(spawnGroup.transform);
            spLegs.transform.position = new Vector3(-3f, 0f, -7f);
            var spLegsComp = spLegs.AddComponent<Game.Gameplay.Spawning.SpawnPoint>();
            var spLegsSo = new SerializedObject(spLegsComp);
            spLegsSo.FindProperty("_targetRole").enumValueIndex = (int)PlayerRole.Legs;
            spLegsSo.ApplyModifiedPropertiesWithoutUndo();

            var spTorso = new GameObject("SpawnPoint_Torso");
            spTorso.transform.SetParent(spawnGroup.transform);
            spTorso.transform.position = new Vector3(3f, 0f, -7f);
            var spTorsoComp = spTorso.AddComponent<Game.Gameplay.Spawning.SpawnPoint>();
            var spTorsoSo = new SerializedObject(spTorsoComp);
            spTorsoSo.FindProperty("_targetRole").enumValueIndex = (int)PlayerRole.Torso;
            spTorsoSo.ApplyModifiedPropertiesWithoutUndo();

            // Player Spawner
            var spawnerGo = new GameObject("[PLAYER_SPAWNER]");
            spawnerGo.transform.SetParent(mgmtGroup.transform);
            var playerSpawner = spawnerGo.AddComponent<TPOBPlayerSpawner>();

            var spawnerSo = new SerializedObject(playerSpawner);
            spawnerSo.FindProperty("_legsPrefab").objectReferenceValue = prefabs.Legs;
            spawnerSo.FindProperty("_torsoPrefab").objectReferenceValue = prefabs.Torso;
            spawnerSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnManager;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            // Robot Coordinator
            var coordinatorGo = new GameObject("[ROBOT_COORDINATOR]");
            coordinatorGo.transform.SetParent(mgmtGroup.transform);
            var coordinator = coordinatorGo.AddComponent<Game.Gameplay.Player.Robot.RobotCoordinator>();

            // Checkpoint System & Respawn Coordinator
            var cpSystemGo = new GameObject("[CHECKPOINT_SYSTEM]");
            cpSystemGo.transform.SetParent(mgmtGroup.transform);
            var checkpointSystem = cpSystemGo.AddComponent<CheckpointSystem>();
            var cpSystemSo = new SerializedObject(checkpointSystem);
            cpSystemSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnManager;
            cpSystemSo.ApplyModifiedPropertiesWithoutUndo();

            var respawnCoordGo = new GameObject("[RESPAWN_COORDINATOR]");
            respawnCoordGo.transform.SetParent(mgmtGroup.transform);
            var respawnCoord = respawnCoordGo.AddComponent<RespawnCoordinator>();
            var respawnCoordSo = new SerializedObject(respawnCoord);
            respawnCoordSo.FindProperty("_checkpointSystem").objectReferenceValue = checkpointSystem;
            respawnCoordSo.ApplyModifiedPropertiesWithoutUndo();

            // NETWORKING (NetworkManager stays at root for DontDestroyOnLoad compatibility)
            var netGroup = new GameObject("--- NETWORKING ---");

            var netManagerGo = new GameObject("[NETWORK_MANAGER]");
            var netManager = netManagerGo.AddComponent<NetworkManager>();
            netManagerGo.AddComponent<UDPTransport>();

            var defaultRules = AssetDatabase.LoadAssetAtPath<NetworkRules>("Assets/PurrNet/Defaults/NetworkRules/ServerStrict.asset");
            var defaultVisibility = AssetDatabase.LoadAssetAtPath<NetworkVisibilityRuleSet>("Assets/PurrNet/Defaults/VisibilitySets/AlwaysVisible.asset");
            var defaultPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/_Project/Network/NetworkPrefabs.asset");

            var netManagerBuildSo = new SerializedObject(netManager);
            netManagerBuildSo.FindProperty("_dontDestroyOnLoad").boolValue = true;
            if (defaultRules != null) netManagerBuildSo.FindProperty("_networkRules").objectReferenceValue = defaultRules;
            if (defaultVisibility != null) netManagerBuildSo.FindProperty("_visibilityRules").objectReferenceValue = defaultVisibility;
            if (defaultPrefabs != null) netManagerBuildSo.FindProperty("_networkPrefabs").objectReferenceValue = defaultPrefabs;
            netManagerBuildSo.ApplyModifiedPropertiesWithoutUndo();

            var relayGo = new GameObject("[NETWORK_EVENT_RELAY]");
            relayGo.transform.SetParent(netGroup.transform);
            var relay = relayGo.AddComponent<NetworkEventRelay>();

            var gameScopeGo = new GameObject("[GAME_LIFETIME_SCOPE]");
            gameScopeGo.transform.SetParent(netGroup.transform);
            var gameScope = gameScopeGo.AddComponent<GameLifetimeScope>();
            var levelManager = gameScopeGo.AddComponent<LevelManager>();

            var bootstrapperGo = new GameObject("[BOOTSTRAPPER]");
            bootstrapperGo.transform.SetParent(netGroup.transform);
            var bootstrapper = bootstrapperGo.AddComponent<Bootstrapper>();

            var gameScopeSo = new SerializedObject(gameScope);
            gameScopeSo.FindProperty("_networkManager").objectReferenceValue = netManager;
            gameScopeSo.FindProperty("_levelManager").objectReferenceValue = levelManager;
            gameScopeSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire RoomScope
            var roomScopeSo = new SerializedObject(roomScope);
            roomScopeSo.FindProperty("_roomController").objectReferenceValue = roomController;
            roomScopeSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnManager;
            roomScopeSo.FindProperty("_playerSpawner").objectReferenceValue = playerSpawner;
            roomScopeSo.FindProperty("_robotCoordinator").objectReferenceValue = coordinator;
            roomScopeSo.FindProperty("_networkEventRelay").objectReferenceValue = relay;
            roomScopeSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. LIGHTING & CAMERA
            var lightingGroup = new GameObject("--- LIGHTING & CAMERA ---");

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(lightingGroup.transform);
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.2f;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(lightingGroup.transform);
            camGo.transform.position = new Vector3(0f, 18f, -22f);
            camGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            // 5. CAMERA RIG (Cinemachine 3.x)
            var cameraRigGo = new GameObject("[CAMERA_RIG]");
            cameraRigGo.transform.SetParent(mgmtGroup.transform);

            var targetGroupGo = new GameObject("CM_TargetGroup");
            targetGroupGo.transform.SetParent(cameraRigGo.transform);
            var targetGroup = targetGroupGo.AddComponent<CinemachineTargetGroup>();
            var targetGroupCoord = targetGroupGo.AddComponent<TargetGroupCoordinator>();

            var cmCamGo = new GameObject("CM_AdaptiveCamera");
            cmCamGo.transform.SetParent(cameraRigGo.transform);
            cmCamGo.transform.position = new Vector3(0f, 18f, -22f);
            cmCamGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            var cmCam = cmCamGo.AddComponent<CinemachineCamera>();
            cmCam.Target.TrackingTarget = targetGroupGo.transform;
            cmCam.Target.LookAtTarget = targetGroupGo.transform;
            cmCam.Lens.FieldOfView = 55f;

            var groupFraming = cmCamGo.AddComponent<CinemachineGroupFraming>();
            groupFraming.FramingMode = CinemachineGroupFraming.FramingModes.HorizontalAndVertical;
            groupFraming.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.DollyThenZoom;
            groupFraming.FramingSize = 0.52f;
            groupFraming.Damping = 2f;

            cmCamGo.AddComponent<CinemachineImpulseListener>();
            var impulseSource = cmCamGo.AddComponent<CinemachineImpulseSource>();

            var cameraController = cameraRigGo.AddComponent<CameraController>();
            var camCtrlSo = new SerializedObject(cameraController);
            camCtrlSo.FindProperty("_cinemachineCamera").objectReferenceValue = cmCam;
            camCtrlSo.FindProperty("_groupFraming").objectReferenceValue = groupFraming;
            camCtrlSo.FindProperty("_targetCoordinator").objectReferenceValue = targetGroupCoord;
            camCtrlSo.FindProperty("_impulseSource").objectReferenceValue = impulseSource;
            camCtrlSo.ApplyModifiedPropertiesWithoutUndo();

            var targetCoordSo = new SerializedObject(targetGroupCoord);
            targetCoordSo.FindProperty("_targetGroup").objectReferenceValue = targetGroup;
            targetCoordSo.ApplyModifiedPropertiesWithoutUndo();

            roomScopeSo.FindProperty("_cameraController").objectReferenceValue = cameraController;
            roomScopeSo.ApplyModifiedPropertiesWithoutUndo();

            EnsureNetworkingInCurrentScene();

            EditorSceneManager.SaveScene(scene);
        }

        public static void CreateWall(string name, Vector3 pos, Vector3 scale, Transform parent, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
