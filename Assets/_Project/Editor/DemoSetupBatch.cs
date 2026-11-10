using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class DemoSetupBatch
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string Room1ScenePath = "Assets/_Project/Scenes/Rooms/Room_1.unity";
        private const string VictoryScenePath = "Assets/_Project/Scenes/Victory.unity";

        [MenuItem("TPOB/Demo/0. Preparar Todo (Victory, Prefabs y UI)", priority = 1)]
        public static void ExecuteAll()
        {
            Debug.Log("<color=yellow>[DemoSetupBatch] Iniciando preparación integral de la demo...</color>");

            // 1. Ensure UI Art assets
            UIAssetGenerator.EnsureUIAssets();

            // 2. Build Victory Scene
            VictorySceneBuilder.BuildVictoryScene();

            // 3. Generate Prefabs with adjusted sizes and LevelSelectModal
            MainMenuBuilder.GenerateUIPrefabs();
            LobbyBuilder.GenerateLobbyPrefab();
            PauseMenuBuilder.GeneratePauseMenuPrefab();

            // 4. Update Boot scene UI instances
            if (File.Exists(BootScenePath))
            {
                var bootScene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
                LobbyBuilder.InstantiateFullUIInScene();
                EditorSceneManager.SaveScene(bootScene);
                Debug.Log("<color=green>[DemoSetupBatch] Escena Boot actualizada con los nuevos prefabs e instancias.</color>");
            }

            // 5. Configure EditorBuildSettings in exact order
            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(Room1ScenePath, true),
                new EditorBuildSettingsScene(VictoryScenePath, true)
            };

            EditorBuildSettings.scenes = buildScenes.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[DemoSetupBatch] ¡Preparación integral de la demo finalizada con éxito!</color>");
        }
    }
}
