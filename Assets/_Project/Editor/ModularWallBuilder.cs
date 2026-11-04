using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Interactables;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class ModularWallBuilder
    {
        static ModularWallBuilder()
        {
            EditorApplication.delayCall += CheckTrigger;
        }

        private static void CheckTrigger()
        {
            string triggerPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "MigrateModularWalls.trigger");
            if (File.Exists(triggerPath))
            {
                try
                {
                    File.Delete(triggerPath);
                }
                catch
                {
                }

                MigrateAllScenes();
            }

            string audioTrigger = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "ConfigureAudio.trigger");
            if (File.Exists(audioTrigger))
            {
                try
                {
                    File.Delete(audioTrigger);
                }
                catch
                {
                }

                ConfigureAudioInBootScene();
            }
        }

        public const string WallsAssetDir = "Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Walls";
        public const string WallPrefabPath = WallsAssetDir + "/Wall.prefab";
        public const string WallCornerPrefabPath = WallsAssetDir + "/WallCorner.prefab";
        public const string WallDoorLPrefabPath = WallsAssetDir + "/WallDoorL.prefab";
        public const string WallDoorRPrefabPath = WallsAssetDir + "/WallDoorR.prefab";
        public const string SlidingDoorPrefabPath = "Assets/_Project/Prefabs/SlidingDoor.prefab";
        public const string RoomsDirectory = "Assets/_Project/Scenes/Rooms";

        public const float BaseWallWidth = 4.0f;
        public const float BaseWallHeight = 2.329f;
        public const float BaseWallThickness = 0.394f;
        public const float BaseCornerSize = 0.698f;
        public const float DefaultHeightScale = 1.6f;

        [MenuItem("TPOB/Paredes/1. Configurar Prefabs Base (Colliders y Compuerta Doble)", false, 20)]
        public static void SetupPrefabs()
        {
            EnsureBasePrefabsHaveColliders();
            UpdateSlidingDoorPrefab();
            Debug.Log("<color=green>[TPOB] Prefabs de paredes y compuerta doble configurados exitosamente.</color>");
        }

        [MenuItem("TPOB/Paredes/2. Actualizar Sala Abierta con Nuevas Paredes y Puertas", false, 21)]
        public static void MigrateActiveScene()
        {
            EnsureBasePrefabsHaveColliders();
            UpdateSlidingDoorPrefab();

            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.isLoaded)
            {
                Debug.LogWarning("[TPOB] No hay ninguna escena abierta.");
                return;
            }

            MigrateScene(activeScene);
            Debug.Log($"<color=green>[TPOB] Sala activa '{activeScene.name}' actualizada con paredes modulares y compuertas dobles.</color>");
        }

        [MenuItem("TPOB/Paredes/3. Reemplazar Paredes y Puertas en TODAS las Salas (01 a 10)", false, 22)]
        public static void MigrateAllScenes()
        {
            EnsureBasePrefabsHaveColliders();
            UpdateSlidingDoorPrefab();

            string[] roomFiles = Directory.GetFiles(RoomsDirectory, "*.unity", SearchOption.TopDirectoryOnly);
            int updatedCount = 0;

            foreach (var scenePath in roomFiles)
            {
                string normPath = scenePath.Replace('\\', '/');
                var scene = EditorSceneManager.OpenScene(normPath, OpenSceneMode.Single);
                if (scene.isLoaded)
                {
                    MigrateScene(scene);
                    EditorSceneManager.SaveScene(scene);
                    updatedCount++;
                }
            }

            // Return to Room 01
            string room01Path = $"{RoomsDirectory}/Room_01.unity";
            if (File.Exists(room01Path))
            {
                EditorSceneManager.OpenScene(room01Path, OpenSceneMode.Single);
            }

            Debug.Log($"<color=green>[TPOB] ¡Se actualizaron con éxito {updatedCount} salas de campaña con las nuevas paredes y compuertas dobles!</color>");
        }

        public static void EnsureBasePrefabsHaveColliders()
        {
            AddBoxColliderIfMissing(WallPrefabPath, new Vector3(0f, 1.1645f, 0f), new Vector3(4f, 2.329f, 0.394f));
            AddBoxColliderIfMissing(WallCornerPrefabPath, new Vector3(0f, 1.1645f, 0f), new Vector3(0.698f, 2.329f, 0.695f));
            AddBoxColliderIfMissing(WallDoorLPrefabPath, new Vector3(0f, 0.858f, 0f), new Vector3(0.078f, 1.716f, 2.055f));
            AddBoxColliderIfMissing(WallDoorRPrefabPath, new Vector3(0f, 0.858f, 0f), new Vector3(0.078f, 1.716f, 2.053f));
            AssetDatabase.SaveAssets();
        }

        private static void AddBoxColliderIfMissing(string prefabPath, Vector3 center, Vector3 size)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[TPOB] No se encontró el prefab en: {prefabPath}");
                return;
            }

            var col = prefab.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = prefab.AddComponent<BoxCollider>();
                col.center = center;
                col.size = size;
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        public static void UpdateSlidingDoorPrefab()
        {
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlidingDoorPrefabPath);
            if (doorPrefab == null)
            {
                Debug.LogWarning($"[TPOB] No se encontró SlidingDoor.prefab en: {SlidingDoorPrefabPath}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
            ConfigureSlidingDoorInstance(instance);
            PrefabUtility.SaveAsPrefabAssetAndConnect(instance, SlidingDoorPrefabPath, InteractionMode.AutomatedAction);
            Object.DestroyImmediate(instance);
        }

        public static void ConfigureSlidingDoorInstance(GameObject doorRoot)
        {
            var wallDoorLPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallDoorLPrefabPath);
            var wallDoorRPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallDoorRPrefabPath);

            var slidingDoor = doorRoot.GetComponent<SlidingDoor>();
            if (slidingDoor == null)
            {
                slidingDoor = doorRoot.AddComponent<SlidingDoor>();
            }

            var impulse = doorRoot.GetComponent<CinemachineImpulseSource>();
            if (impulse == null)
            {
                impulse = doorRoot.AddComponent<CinemachineImpulseSource>();
            }

            // Remove previous primitive or obsolete child leaves/posts
            List<GameObject> toDestroy = new List<GameObject>();
            for (int i = 0; i < doorRoot.transform.childCount; i++)
            {
                var child = doorRoot.transform.GetChild(i).gameObject;
                if (child.name.StartsWith("DoorLeaf") || child.name.StartsWith("LeftPost") || child.name.StartsWith("RightPost") || child.name.StartsWith("Lintel"))
                {
                    toDestroy.Add(child);
                }
            }
            foreach (var go in toDestroy)
            {
                Object.DestroyImmediate(go);
            }

            // Frame Posts and Lintel
            Material frameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_DoorFrame.mat");
            if (frameMat == null)
            {
                frameMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Material/M_ColorPalette.mat");
            }

            var leftPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftPost.name = "LeftPost";
            leftPost.transform.SetParent(doorRoot.transform, false);
            leftPost.transform.localPosition = new Vector3(-2.2f, 1.8f, 0f);
            leftPost.transform.localScale = new Vector3(0.35f, 3.6f, 0.5f);
            if (frameMat != null) leftPost.GetComponent<Renderer>().sharedMaterial = frameMat;

            var rightPost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightPost.name = "RightPost";
            rightPost.transform.SetParent(doorRoot.transform, false);
            rightPost.transform.localPosition = new Vector3(2.2f, 1.8f, 0f);
            rightPost.transform.localScale = new Vector3(0.35f, 3.6f, 0.5f);
            if (frameMat != null) rightPost.GetComponent<Renderer>().sharedMaterial = frameMat;

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "Lintel";
            lintel.transform.SetParent(doorRoot.transform, false);
            lintel.transform.localPosition = new Vector3(0f, 3.7f, 0f);
            lintel.transform.localScale = new Vector3(4.75f, 0.35f, 0.5f);
            if (frameMat != null) lintel.GetComponent<Renderer>().sharedMaterial = frameMat;

            // Left Leaf (WallDoorL)
            GameObject leftLeaf;
            if (wallDoorLPrefab != null)
            {
                leftLeaf = (GameObject)PrefabUtility.InstantiatePrefab(wallDoorLPrefab, doorRoot.transform);
            }
            else
            {
                leftLeaf = new GameObject("WallDoorL");
                leftLeaf.transform.SetParent(doorRoot.transform);
            }
            leftLeaf.name = "DoorLeaf_Left";
            leftLeaf.transform.localPosition = new Vector3(-1.025f, 0f, 0f);
            leftLeaf.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            leftLeaf.transform.localScale = new Vector3(1f, DefaultHeightScale, 1f);

            var colL = leftLeaf.GetComponent<BoxCollider>();
            if (colL == null) colL = leftLeaf.AddComponent<BoxCollider>();
            colL.center = new Vector3(0f, 0.858f, 0f);
            colL.size = new Vector3(0.15f, 1.716f, 2.055f);

            // Right Leaf (WallDoorR)
            GameObject rightLeaf;
            if (wallDoorRPrefab != null)
            {
                rightLeaf = (GameObject)PrefabUtility.InstantiatePrefab(wallDoorRPrefab, doorRoot.transform);
            }
            else
            {
                rightLeaf = new GameObject("WallDoorR");
                rightLeaf.transform.SetParent(doorRoot.transform);
            }
            rightLeaf.name = "DoorLeaf_Right";
            rightLeaf.transform.localPosition = new Vector3(1.025f, 0f, 0f);
            rightLeaf.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            rightLeaf.transform.localScale = new Vector3(1f, DefaultHeightScale, 1f);

            var colR = rightLeaf.GetComponent<BoxCollider>();
            if (colR == null) colR = rightLeaf.AddComponent<BoxCollider>();
            colR.center = new Vector3(0f, 0.858f, 0f);
            colR.size = new Vector3(0.15f, 1.716f, 2.053f);

            // Wire Serialized Properties
            var so = new SerializedObject(slidingDoor);
            so.FindProperty("_doorLeaf").objectReferenceValue = leftLeaf.transform;
            so.FindProperty("_doorLeafRight").objectReferenceValue = rightLeaf.transform;
            so.FindProperty("_openOffset").vector3Value = new Vector3(-2.15f, 0f, 0f);
            so.FindProperty("_openOffsetRight").vector3Value = new Vector3(2.15f, 0f, 0f);
            so.FindProperty("_impulseSource").objectReferenceValue = impulse;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void MigrateScene(Scene scene)
        {
            var rootObjects = scene.GetRootGameObjects();
            GameObject envGroup = null;
            GameObject floor = null;
            List<SlidingDoor> doors = new List<SlidingDoor>();

            foreach (var root in rootObjects)
            {
                if (root.name == "--- ENVIRONMENT ---")
                {
                    envGroup = root;
                }
                doors.AddRange(root.GetComponentsInChildren<SlidingDoor>(true));
            }

            if (envGroup == null)
            {
                Debug.LogWarning($"[TPOB] No se encontró --- ENVIRONMENT --- en {scene.name}");
                return;
            }

            // Find floor to know room bounds
            Transform floorT = envGroup.transform.Find("Floor");
            Vector3 floorScale = new Vector3(20f, 1f, 26f);
            Vector3 floorPos = Vector3.zero;

            if (floorT != null)
            {
                floor = floorT.gameObject;
                floorScale = floorT.localScale;
                floorPos = floorT.position;
            }

            // Remove old primitive walls
            List<GameObject> oldWalls = new List<GameObject>();
            for (int i = 0; i < envGroup.transform.childCount; i++)
            {
                var child = envGroup.transform.GetChild(i).gameObject;
                string n = child.name;
                if (n.StartsWith("Wall_") || n.StartsWith("DividingWall_") || n.StartsWith("Modular_Walls"))
                {
                    oldWalls.Add(child);
                }
            }

            // Detect any dividing wall z-positions
            List<float> dividingWallZPositions = new List<float>();
            foreach (var w in oldWalls)
            {
                if (w.name.StartsWith("DividingWall_Lintel_"))
                {
                    string suffix = w.name.Substring("DividingWall_Lintel_".Length);
                    if (float.TryParse(suffix, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float zVal))
                    {
                        dividingWallZPositions.Add(zVal);
                    }
                    else
                    {
                        dividingWallZPositions.Add(w.transform.position.z);
                    }
                }
                Undo.DestroyObjectImmediate(w);
            }

            // Build new modular walls under ENVIRONMENT
            GameObject wallsRoot = new GameObject("Modular_Walls");
            wallsRoot.transform.SetParent(envGroup.transform, false);
            Undo.RegisterCreatedObjectUndo(wallsRoot, "Create Modular Walls");

            var wallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabPath);
            var cornerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallCornerPrefabPath);

            BuildPerimeterWalls(wallsRoot.transform, floorPos, floorScale, wallPrefab, cornerPrefab);

            foreach (float divZ in dividingWallZPositions)
            {
                BuildDividingWall(wallsRoot.transform, floorPos, floorScale.x, divZ, wallPrefab);
            }

            // Update all SlidingDoor instances in the scene
            foreach (var door in doors)
            {
                ConfigureSlidingDoorInstance(door.gameObject);
                EditorUtility.SetDirty(door.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void BuildPerimeterWalls(Transform parent, Vector3 floorPos, Vector3 floorScale, GameObject wallPrefab, GameObject cornerPrefab, float heightScale = DefaultHeightScale)
        {
            float halfX = floorScale.x * 0.5f;
            float halfZ = floorScale.z * 0.5f;
            float cornerOffset = 0.35f;

            // 1. Four Corners
            if (cornerPrefab != null)
            {
                InstantiateCorner(parent, cornerPrefab, new Vector3(floorPos.x - halfX, floorPos.y, floorPos.z - halfZ), 0f, heightScale, "Corner_SW");
                InstantiateCorner(parent, cornerPrefab, new Vector3(floorPos.x + halfX, floorPos.y, floorPos.z - halfZ), -90f, heightScale, "Corner_SE");
                InstantiateCorner(parent, cornerPrefab, new Vector3(floorPos.x + halfX, floorPos.y, floorPos.z + halfZ), 180f, heightScale, "Corner_NE");
                InstantiateCorner(parent, cornerPrefab, new Vector3(floorPos.x - halfX, floorPos.y, floorPos.z + halfZ), 90f, heightScale, "Corner_NW");
            }

            // 2. South Wall (Z = floorPos.z - halfZ)
            float southSpan = floorScale.x - (cornerOffset * 2f);
            BuildLinearWallSegment(parent, wallPrefab,
                new Vector3(floorPos.x - halfX + cornerOffset, floorPos.y, floorPos.z - halfZ),
                new Vector3(floorPos.x + halfX - cornerOffset, floorPos.y, floorPos.z - halfZ),
                0f, heightScale, "Wall_South");

            // 3. West Wall (X = floorPos.x - halfX)
            float westSpan = floorScale.z - (cornerOffset * 2f);
            BuildLinearWallSegment(parent, wallPrefab,
                new Vector3(floorPos.x - halfX, floorPos.y, floorPos.z - halfZ + cornerOffset),
                new Vector3(floorPos.x - halfX, floorPos.y, floorPos.z + halfZ - cornerOffset),
                90f, heightScale, "Wall_West");

            // 4. East Wall (X = floorPos.x + halfX)
            BuildLinearWallSegment(parent, wallPrefab,
                new Vector3(floorPos.x + halfX, floorPos.y, floorPos.z - halfZ + cornerOffset),
                new Vector3(floorPos.x + halfX, floorPos.y, floorPos.z + halfZ - cornerOffset),
                -90f, heightScale, "Wall_East");

            // 5. North Wall with Door Gap (door at x = 0, gap = 4.2m -> x from -2.1 to +2.1)
            float doorGapHalf = 2.1f;

            // North Left
            BuildLinearWallSegment(parent, wallPrefab,
                new Vector3(floorPos.x - halfX + cornerOffset, floorPos.y, floorPos.z + halfZ),
                new Vector3(floorPos.x - doorGapHalf, floorPos.y, floorPos.z + halfZ),
                180f, heightScale, "Wall_North_Left");

            // North Right
            BuildLinearWallSegment(parent, wallPrefab,
                new Vector3(floorPos.x + doorGapHalf, floorPos.y, floorPos.z + halfZ),
                new Vector3(floorPos.x + halfX - cornerOffset, floorPos.y, floorPos.z + halfZ),
                180f, heightScale, "Wall_North_Right");

            // North Lintel beam above doorway
            CreateWallLintel(parent, new Vector3(floorPos.x, floorPos.y + (BaseWallHeight * heightScale) - 0.2f, floorPos.z + halfZ), 4.5f);
        }

        public static void BuildDividingWall(Transform parent, Vector3 floorPos, float floorWidth, float zPos, GameObject wallPrefab, float heightScale = DefaultHeightScale)
        {
            float halfX = floorWidth * 0.5f;
            float cornerOffset = 0.35f;
            float doorGapHalf = 2.1f;

            // Left segment
            BuildLinearWallSegment(parent, wallPrefab,
                new Vector3(floorPos.x - halfX + cornerOffset, floorPos.y, zPos),
                new Vector3(floorPos.x - doorGapHalf, floorPos.y, zPos),
                0f, heightScale, $"DividingWall_L_{zPos}");

            // Right segment
            BuildLinearWallSegment(parent, wallPrefab,
                new Vector3(floorPos.x + doorGapHalf, floorPos.y, zPos),
                new Vector3(floorPos.x + halfX - cornerOffset, floorPos.y, zPos),
                0f, heightScale, $"DividingWall_R_{zPos}");

            // Lintel
            CreateWallLintel(parent, new Vector3(floorPos.x, floorPos.y + (BaseWallHeight * heightScale) - 0.2f, zPos), 4.5f);
        }

        private static void BuildLinearWallSegment(Transform parent, GameObject wallPrefab, Vector3 start, Vector3 end, float rotationY, float heightScale, string groupName)
        {
            if (wallPrefab == null) return;

            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.1f) return;

            int count = Mathf.Max(1, Mathf.RoundToInt(length / BaseWallWidth));
            float segmentLen = length / count;
            float scaleX = segmentLen / BaseWallWidth;
            Vector3 dir = delta / length;

            GameObject group = new GameObject(groupName);
            group.transform.SetParent(parent, false);

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = start + dir * ((i + 0.5f) * segmentLen);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, group.transform);
                instance.name = $"{groupName}_Mod_{i}";
                instance.transform.position = pos;
                instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
                instance.transform.localScale = new Vector3(scaleX, heightScale, 1f);
            }
        }

        private static void InstantiateCorner(Transform parent, GameObject cornerPrefab, Vector3 pos, float rotationY, float heightScale, string name)
        {
            GameObject corner = (GameObject)PrefabUtility.InstantiatePrefab(cornerPrefab, parent);
            corner.name = name;
            corner.transform.position = pos;
            corner.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            corner.transform.localScale = new Vector3(1f, heightScale, 1f);
        }

        private static void CreateWallLintel(Transform parent, Vector3 pos, float width)
        {
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = $"Lintel_{pos.z}";
            lintel.transform.SetParent(parent, false);
            lintel.transform.position = pos;
            lintel.transform.localScale = new Vector3(width, 0.4f, BaseWallThickness);

            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Material/M_ColorPalette.mat");
            if (mat != null) lintel.GetComponent<Renderer>().sharedMaterial = mat;
        }

        [MenuItem("TPOB/Audio/Configurar y Actualizar Sistema de Música", false, 30)]
        public static void ConfigureAudioInBootScene()
        {
            PauseMenuBuilder.GenerateAllUIPrefabs();

            string bootPath = "Assets/_Project/Scenes/Boot.unity";
            if (File.Exists(bootPath))
            {
                var scene = EditorSceneManager.OpenScene(bootPath, OpenSceneMode.Single);
                PlaygroundBuilder.EnsureNetworkingInCurrentScene();
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("<color=green>[TPOB] ¡Sistema de música configurado exitosamente en Boot.unity y UI Prefabs!</color>");
        }
    }
}
