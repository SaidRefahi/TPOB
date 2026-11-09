using System.IO;
using Game.Gameplay.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor.LevelEditor
{
    public static class LevelSceneTemplateGenerator
    {
        private const string ScenesDir = "Assets/_Project/Scenes/Levels";

        [MenuItem("TPOB/Tools/Create New Level Scene", priority = 100)]
        public static void CreateNewLevelScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Level Root
            var levelRoot = new GameObject("Level_Root");
            var levelDef = levelRoot.AddComponent<LevelDefinition>();

            // 2. Ensure full playable hierarchy (Spawns, TPOBPlayerSpawner, RobotCoordinator, Camera, VContainer, Networking)
            LevelBuilderWindow.EnsureScenePlayableHierarchy(scene, new Vector3(-2f, 0.1f, -4f), new Vector3(2f, 0.1f, -4f));

            // 3. Initial Modular Floor Geometry (Ground.prefab instead of primitive cube)
            var groundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Props/Ground.prefab");
            if (groundPrefab != null)
            {
                var floorGroup = new GameObject("Floor_StartingArea");
                floorGroup.transform.SetParent(levelRoot.transform);
                for (int x = -2; x < 2; x++)
                {
                    for (int z = -2; z < 2; z++)
                    {
                        var tile = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab, floorGroup.transform);
                        tile.transform.position = new Vector3((x + 0.5f) * 4f, 0f, (z + 0.5f) * 4f);
                        tile.transform.localScale = new Vector3(4f, 1f, 4f);
                    }
                }
            }

            // 4. Generate Subterranean Deadzone
            levelDef.SetGroundY(0f);
            levelDef.SetDeadzoneY(-4f);
            levelDef.GenerateSubterraneanDeadzone();
            levelDef.AutoDiscoverElements();

            // 5. Save Scene
            if (!Directory.Exists(ScenesDir))
            {
                Directory.CreateDirectory(ScenesDir);
            }

            string scenePath = $"{ScenesDir}/Room_NewLevel.unity";
            int counter = 1;
            while (File.Exists(scenePath))
            {
                scenePath = $"{ScenesDir}/Room_NewLevel_{counter++}.unity";
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Selection.activeGameObject = levelRoot;
            Debug.Log($"<color=#50fa7b><b>[NUEVA ESCENA CREADA]</b></color> Escena guardada en '{scenePath}'. ¡Todo configurado para construir y dar Play!");
        }
    }
}
