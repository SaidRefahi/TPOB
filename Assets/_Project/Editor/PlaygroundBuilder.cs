using System.IO;
using Game.Core.Enums;
using Game.Gameplay.Interactables;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
using Game.Gameplay.Rooms;
using Game.Gameplay.Spawning;
using PurrNet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabsDir)) Directory.CreateDirectory(PrefabsDir);
            if (!Directory.Exists(MaterialsDir)) Directory.CreateDirectory(MaterialsDir);
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
        }

        public struct PrefabSet
        {
            public GameObject Legs;
            public GameObject Torso;
            public GameObject GrabbableBox;
            public GameObject MagneticKey;
            public GameObject KickableObstacle;
            public GameObject LeverSwitch;
        }

        private static MaterialSet CreateMaterials()
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

        private static PrefabSet CreatePrefabs(MaterialSet mats)
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

            AssetDatabase.SaveAssets();
            return prefabs;
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

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<NetworkTransform>();

            var controller = root.AddComponent<LegsController>();
            var reader = root.AddComponent<LegsInputReader>();

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

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("_rigidbody").objectReferenceValue = rb;
            controllerSo.FindProperty("_inputReader").objectReferenceValue = reader;
            controllerSo.FindProperty("_groundCheckPoint").objectReferenceValue = groundCheck.transform;
            controllerSo.FindProperty("_kickPoint").objectReferenceValue = kickPoint.transform;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

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

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<NetworkTransform>();

            var controller = root.AddComponent<TorsoController>();
            var reader = root.AddComponent<TorsoInputReader>();

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
            controllerSo.FindProperty("_aimPivot").objectReferenceValue = aimPivot.transform;
            controllerSo.FindProperty("_holdSocket").objectReferenceValue = holdSocket.transform;
            controllerSo.FindProperty("_magnetOrigin").objectReferenceValue = magnetOrigin.transform;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

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

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<NetworkTransform>();
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

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<NetworkTransform>();
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

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f;

            root.AddComponent<NetworkIdentity>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<KickableTestObject>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateLeverPrefab(Material baseMat, Material handleMat)
        {
            string path = $"{PrefabsDir}/LeverSwitch.prefab";
            GameObject root = new GameObject("LeverSwitch");

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

            root.AddComponent<NetworkIdentity>();
            var lever = root.AddComponent<LeverMechanism>();

            var leverSo = new SerializedObject(lever);
            leverSo.FindProperty("_handleTransform").objectReferenceValue = handlePivot.transform;
            leverSo.ApplyModifiedPropertiesWithoutUndo();

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

            // Walls
            CreateWall("Wall_North", new Vector3(0f, 1.5f, 16f), new Vector3(32f, 3f, 1f), envGroup.transform, mats.Wall);
            CreateWall("Wall_South", new Vector3(0f, 1.5f, -16f), new Vector3(32f, 3f, 1f), envGroup.transform, mats.Wall);
            CreateWall("Wall_East", new Vector3(16f, 1.5f, 0f), new Vector3(1f, 3f, 32f), envGroup.transform, mats.Wall);
            CreateWall("Wall_West", new Vector3(-16f, 1.5f, 0f), new Vector3(1f, 3f, 32f), envGroup.transform, mats.Wall);

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

            // 2. MANAGEMENT
            var mgmtGroup = new GameObject("--- MANAGEMENT ---");

            var roomSystem = new GameObject("[ROOM_SYSTEM]");
            roomSystem.transform.SetParent(mgmtGroup.transform);
            var roomController = roomSystem.AddComponent<RoomController>();
            var roomScope = roomSystem.AddComponent<RoomLifetimeScope>();

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

            // Wire RoomScope
            var roomScopeSo = new SerializedObject(roomScope);
            roomScopeSo.FindProperty("_roomController").objectReferenceValue = roomController;
            roomScopeSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnManager;
            roomScopeSo.FindProperty("_playerSpawner").objectReferenceValue = playerSpawner;
            roomScopeSo.ApplyModifiedPropertiesWithoutUndo();

            // 3. INTERACTABLES
            var interactGroup = new GameObject("--- INTERACTABLES ---");

            // Grabbable Boxes
            PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, interactGroup.transform);
            var box1 = GameObject.Find("GrabbableBox");
            if (box1) box1.transform.position = new Vector3(0f, 0.5f, -2f);

            var box2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, interactGroup.transform);
            box2.transform.position = new Vector3(-2f, 0.5f, 0f);

            var box3 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, interactGroup.transform);
            box3.transform.position = new Vector3(2f, 0.5f, 0f);

            // Kickable Obstacles
            var kick1 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KickableObstacle, interactGroup.transform);
            kick1.transform.position = new Vector3(-4f, 0.6f, 3f);

            var kick2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KickableObstacle, interactGroup.transform);
            kick2.transform.position = new Vector3(-6f, 0.6f, 3f);

            // Magnetic Key on elevated platform
            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, interactGroup.transform);
            key.transform.position = new Vector3(0f, 2.2f, 9f);

            // Lever Switch
            var lever = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, interactGroup.transform);
            lever.transform.position = new Vector3(5f, 0f, 2f);

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
            camGo.transform.position = new Vector3(0f, 15f, -17f);
            camGo.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();

            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateWall(string name, Vector3 pos, Vector3 scale, Transform parent, Material mat)
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
