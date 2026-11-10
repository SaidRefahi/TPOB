using System.IO;
using Game.Gameplay.UI;
using Game.Network.Audio;
using Game.Network.Events;
using Game.Network.Scopes;
using Game.Network.Services;
using PurrNet;
using PurrNet.Transports;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Editor
{
    [InitializeOnLoad]
    public static class PauseMenuBuilder
    {
        private const string PrefabsDirectory = "Assets/_Project/Prefabs/UI";
        private const string PauseMenuPrefabPath = PrefabsDirectory + "/Canvas_PauseMenu.prefab";
        private const string SettingsPrefabPath = PrefabsDirectory + "/Dialog_Settings.prefab";
        private const string MainMenuPrefabPath = PrefabsDirectory + "/Canvas_MainMenu.prefab";
        private const string LobbyPrefabPath = PrefabsDirectory + "/Canvas_Lobby.prefab";
        private const string TriggerFilePath = "Temp/RebuildUI.trigger";

        static PauseMenuBuilder()
        {
            EditorApplication.delayCall += CheckAutoRebuildTrigger;
        }

        private static void CheckAutoRebuildTrigger()
        {
            if (!File.Exists(TriggerFilePath))
            {
                return;
            }

            File.Delete(TriggerFilePath);
            ConfigureCompleteBootScene();
        }

        [MenuItem("TPOB/UI/★ RECONSTRUIR Y GENERAR TODO EL SISTEMA UI (Prefabs y Boot) ★", priority = -100)]
        public static void ConfigureCompleteBootScene()
        {
            // 1. Asegurar generación de todos los prefabs y arte 2D de UI
            GenerateAllUIPrefabs();

            // 2. Abrir o crear la escena Boot.unity
            const string bootPath = "Assets/_Project/Scenes/Boot.unity";
            EnsureDirectory(Path.GetDirectoryName(bootPath));
            Scene bootScene;
            if (File.Exists(bootPath))
            {
                bootScene = EditorSceneManager.OpenScene(bootPath, OpenSceneMode.Single);
            }
            else
            {
                bootScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            }

            // 3. Infraestructura Persistente de Red (DDOL)
            var netGroup = GameObject.Find("--- NETWORKING ---");
            if (netGroup == null)
            {
                netGroup = new GameObject("--- NETWORKING ---");
                Undo.RegisterCreatedObjectUndo(netGroup, "Create --- NETWORKING ---");
            }

            var netManager = Object.FindFirstObjectByType<NetworkManager>();
            GameObject netManagerGo;
            if (netManager == null)
            {
                netManagerGo = new GameObject("[NETWORK_MANAGER]");
                netManager = netManagerGo.AddComponent<NetworkManager>();
                Undo.RegisterCreatedObjectUndo(netManagerGo, "Create [NETWORK_MANAGER]");
            }
            else
            {
                netManagerGo = netManager.gameObject;
            }

            if (netManagerGo.transform.parent != null)
            {
                netManagerGo.transform.SetParent(null);
            }

            // Eliminar transportes UDP duplicados o erróneos para garantizar PurrTransport
            var oldUdps = netManagerGo.GetComponents<UDPTransport>();
            for (int i = 0; i < oldUdps.Length; i++)
            {
                Object.DestroyImmediate(oldUdps[i]);
            }

            var purrTransports = netManagerGo.GetComponents<PurrTransport>();
            PurrTransport mainTransport;
            if (purrTransports.Length == 0)
            {
                mainTransport = netManagerGo.AddComponent<PurrTransport>();
            }
            else
            {
                mainTransport = purrTransports[0];
                for (int i = 1; i < purrTransports.Length; i++)
                {
                    Object.DestroyImmediate(purrTransports[i]);
                }
            }

            mainTransport.masterServer = "https://purrtransport.purrservers.com/";
            mainTransport.attemptDirectConnection = true;

            var rules = AssetDatabase.LoadAssetAtPath<NetworkRules>("Assets/PurrNet/Defaults/NetworkRules/ServerStrict.asset");
            var visibility = AssetDatabase.LoadAssetAtPath<NetworkVisibilityRuleSet>("Assets/PurrNet/Defaults/VisibilitySets/AlwaysVisible.asset");
            var netPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/_Project/Network/NetworkPrefabs.asset");

            var netManagerSo = new SerializedObject(netManager);
            if (rules != null) netManagerSo.FindProperty("_networkRules").objectReferenceValue = rules;
            if (visibility != null) netManagerSo.FindProperty("_visibilityRules").objectReferenceValue = visibility;
            if (netPrefabs != null) netManagerSo.FindProperty("_networkPrefabs").objectReferenceValue = netPrefabs;
            netManagerSo.FindProperty("_dontDestroyOnLoad").boolValue = true;
            netManagerSo.FindProperty("_transport").objectReferenceValue = mainTransport;

            var clientFlagsProp = netManagerSo.FindProperty("_startClientFlags");
            if (clientFlagsProp != null) clientFlagsProp.intValue = 0;

            var serverFlagsProp = netManagerSo.FindProperty("_startServerFlags");
            if (serverFlagsProp != null) serverFlagsProp.intValue = 0;

            netManagerSo.ApplyModifiedPropertiesWithoutUndo();

            var gameScope = Object.FindFirstObjectByType<GameLifetimeScope>();
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
                if (gameScopeGo.transform.parent == null) gameScopeGo.transform.SetParent(netGroup.transform);
            }

            var levelManager = Object.FindFirstObjectByType<LevelManager>();
            if (levelManager == null)
            {
                levelManager = gameScopeGo.AddComponent<LevelManager>();
            }

            var bootstrapper = Object.FindFirstObjectByType<Bootstrapper>();
            if (bootstrapper == null)
            {
                var bootstrapperGo = new GameObject("[BOOTSTRAPPER]");
                bootstrapperGo.transform.SetParent(netGroup.transform);
                bootstrapperGo.AddComponent<Bootstrapper>();
            }

            var sceneLobbyCtrl = Object.FindFirstObjectByType<LobbyNetworkController>();
            if (sceneLobbyCtrl != null)
            {
                Object.DestroyImmediate(sceneLobbyCtrl.gameObject);
            }

            var lobbyControllerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/LobbyNetworkController.prefab");

            var relay = Object.FindFirstObjectByType<NetworkEventRelay>();
            if (relay == null)
            {
                var relayGo = new GameObject("[NETWORK_EVENT_RELAY]");
                relayGo.transform.SetParent(netGroup.transform);
                relayGo.AddComponent<NetworkEventRelay>();
            }

            var audioRelay = Object.FindFirstObjectByType<NetworkAudioRelay>();
            if (audioRelay == null)
            {
                var audioRelayGo = new GameObject("[NETWORK_AUDIO_RELAY]");
                audioRelayGo.transform.SetParent(netGroup.transform);
                audioRelayGo.AddComponent<NetworkAudioRelay>();
            }

            var scopeSo = new SerializedObject(gameScope);
            scopeSo.FindProperty("_networkManager").objectReferenceValue = netManager;
            scopeSo.FindProperty("_levelManager").objectReferenceValue = levelManager;
            var lobbyPrefabProp = scopeSo.FindProperty("_lobbyNetworkControllerPrefab");
            if (lobbyPrefabProp != null) lobbyPrefabProp.objectReferenceValue = lobbyControllerPrefab;
            scopeSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = Object.FindFirstObjectByType<Game.Network.UI.ConnectionHUD>();
            if (hud != null)
            {
                var hudSo = new SerializedObject(hud);
                var showGuiProp = hudSo.FindProperty("_showGUI");
                if (showGuiProp != null)
                {
                    showGuiProp.boolValue = false;
                    hudSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // 4. Montar Jerarquía Completa de UI
            EnsureEventSystem();

            var uiGroup = GameObject.Find("--- UI ---");
            if (uiGroup == null)
            {
                uiGroup = new GameObject("--- UI ---");
                Undo.RegisterCreatedObjectUndo(uiGroup, "Create --- UI ---");
            }
            else
            {
                for (int i = uiGroup.transform.childCount - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(uiGroup.transform.GetChild(i).gameObject);
                }
            }

            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            var mainMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
            var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
            var pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);

            GameObject settingsInstance = null;
            if (settingsPrefab != null)
            {
                settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, uiGroup.transform);
                settingsInstance.SetActive(false);
            }

            if (mainMenuPrefab != null)
            {
                var mainMenuInstance = (GameObject)PrefabUtility.InstantiatePrefab(mainMenuPrefab, uiGroup.transform);
                mainMenuInstance.SetActive(true);

                if (settingsInstance != null)
                {
                    var mainView = mainMenuInstance.GetComponent<MainMenuView>();
                    var setView = settingsInstance.GetComponent<SettingsView>();
                    var so = new SerializedObject(mainView);
                    so.FindProperty("_settingsDialog").objectReferenceValue = setView;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (lobbyPrefab != null)
            {
                var lobbyInstance = (GameObject)PrefabUtility.InstantiatePrefab(lobbyPrefab, uiGroup.transform);
                lobbyInstance.SetActive(true);
                var canvas = lobbyInstance.GetComponent<Canvas>();
                if (canvas != null) canvas.enabled = false;
                var raycaster = lobbyInstance.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
            }

            if (pausePrefab != null)
            {
                var pauseInstance = (GameObject)PrefabUtility.InstantiatePrefab(pausePrefab, uiGroup.transform);
                pauseInstance.SetActive(true);
                var pauseView = pauseInstance.GetComponent<PauseMenuView>();
                if (pauseView != null) pauseView.SetPauseActive(false);
            }

            // 5. Guardar la escena Boot
            EditorSceneManager.MarkSceneDirty(bootScene);
            EditorSceneManager.SaveScene(bootScene, bootPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green><b>[TPOB] ¡Sistema UI Completo y Escena Boot.unity actualizados y guardados con éxito!</b></color>");
        }

        [MenuItem("TPOB/UI/4. Generar Prefab de Menú de Pausa", priority = 4)]
        public static void GeneratePauseMenuPrefab()
        {
            EnsureDirectory(PrefabsDirectory);
            UIAssetGenerator.EnsureUIAssets();

            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            if (settingsPrefab == null)
            {
                MainMenuBuilder.GenerateUIPrefabs();
                settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            }

            GameObject pauseMenuGo = BuildPauseMenuHierarchy(settingsPrefab);
            PrefabUtility.SaveAsPrefabAsset(pauseMenuGo, PauseMenuPrefabPath);
            Object.DestroyImmediate(pauseMenuGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[TPOB] Prefab Sci-Fi Canvas_PauseMenu generado con éxito en " + PauseMenuPrefabPath + "</color>");
        }

        [MenuItem("TPOB/UI/5. Instanciar Menú de Pausa en Escena Activa", priority = 5)]
        public static void InstantiatePauseMenuInScene()
        {
            EnsureEventSystem();

            var uiRoot = GameObject.Find("--- UI ---");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("--- UI ---");
                Undo.RegisterCreatedObjectUndo(uiRoot, "Create --- UI ---");
            }

            var pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            if (pausePrefab == null)
            {
                GeneratePauseMenuPrefab();
                pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            }

            if (Object.FindFirstObjectByType<PauseMenuView>() == null && pausePrefab != null)
            {
                var pauseInstance = (GameObject)PrefabUtility.InstantiatePrefab(pausePrefab, uiRoot.transform);
                Undo.RegisterCreatedObjectUndo(pauseInstance, "Instantiate Canvas_PauseMenu");
                Debug.Log("<color=cyan>[TPOB] Canvas_PauseMenu instanciado con éxito en escena activa bajo '--- UI ---'.</color>");
            }
            else
            {
                Debug.Log("<color=yellow>[TPOB] Canvas_PauseMenu ya se encuentra presente en la escena activa.</color>");
            }
        }

        [MenuItem("TPOB/UI/6. Instanciar UI Completa en Escena Activa", priority = 6)]
        public static void InstantiateFullUIInActiveScene()
        {
            EnsureEventSystem();

            var uiRoot = GameObject.Find("--- UI ---");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("--- UI ---");
                Undo.RegisterCreatedObjectUndo(uiRoot, "Create --- UI ---");
            }

            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            var mainMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
            var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
            var pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);

            if (settingsPrefab == null || mainMenuPrefab == null || lobbyPrefab == null || pausePrefab == null)
            {
                GenerateAllUIPrefabs();
                settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
                mainMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
                lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
                pausePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseMenuPrefabPath);
            }

            // Settings
            GameObject settingsInstance = null;
            var existingSettings = Object.FindFirstObjectByType<SettingsView>(FindObjectsInactive.Include);
            if (existingSettings == null && settingsPrefab != null)
            {
                settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, uiRoot.transform);
                settingsInstance.SetActive(false);
                Undo.RegisterCreatedObjectUndo(settingsInstance, "Instantiate Dialog_Settings");
            }
            else if (existingSettings != null)
            {
                settingsInstance = existingSettings.gameObject;
            }

            // Main Menu
            var existingMainMenu = Object.FindFirstObjectByType<MainMenuView>(FindObjectsInactive.Include);
            if (existingMainMenu == null && mainMenuPrefab != null)
            {
                var mainMenuInstance = (GameObject)PrefabUtility.InstantiatePrefab(mainMenuPrefab, uiRoot.transform);
                mainMenuInstance.SetActive(true);
                Undo.RegisterCreatedObjectUndo(mainMenuInstance, "Instantiate Canvas_MainMenu");

                if (settingsInstance != null)
                {
                    var mainView = mainMenuInstance.GetComponent<MainMenuView>();
                    var setView = settingsInstance.GetComponent<SettingsView>();
                    var so = new SerializedObject(mainView);
                    so.FindProperty("_settingsDialog").objectReferenceValue = setView;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Lobby
            var existingLobby = Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
            if (existingLobby == null && lobbyPrefab != null)
            {
                var lobbyInstance = (GameObject)PrefabUtility.InstantiatePrefab(lobbyPrefab, uiRoot.transform);
                lobbyInstance.SetActive(true);
                var canvas = lobbyInstance.GetComponent<Canvas>();
                if (canvas != null) canvas.enabled = false;
                var raycaster = lobbyInstance.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
                Undo.RegisterCreatedObjectUndo(lobbyInstance, "Instantiate Canvas_Lobby");
            }

            // Pause Menu
            var existingPause = Object.FindFirstObjectByType<PauseMenuView>(FindObjectsInactive.Include);
            if (existingPause == null && pausePrefab != null)
            {
                var pauseInstance = (GameObject)PrefabUtility.InstantiatePrefab(pausePrefab, uiRoot.transform);
                pauseInstance.SetActive(true);
                var pauseView = pauseInstance.GetComponent<PauseMenuView>();
                if (pauseView != null) pauseView.SetPauseActive(false);
                Undo.RegisterCreatedObjectUndo(pauseInstance, "Instantiate Canvas_PauseMenu");
            }

            Debug.Log("<color=green><b>[TPOB] Jerarquía completa de UI instanciada en escena activa.</b></color>");
        }

        public static void GenerateAllUIPrefabs()
        {
            UIAssetGenerator.EnsureUIAssets();
            MainMenuBuilder.GenerateUIPrefabs();
            LobbyBuilder.GenerateLobbyPrefab();
            GeneratePauseMenuPrefab();

            Debug.Log("<color=green><b>[TPOB] Todos los Prefabs de UI Sci-Fi Generados con éxito.</b></color>");
        }

        private static GameObject BuildPauseMenuHierarchy(GameObject settingsPrefab)
        {
            var canvasGo = CreateCanvasRoot("Canvas_PauseMenu", 500);
            var pauseView = canvasGo.AddComponent<PauseMenuView>();
            canvasGo.AddComponent<PauseMenuPresenter>();

            var canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var pauseRoot = CreateUIElement("PauseRoot", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = pauseRoot.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.04f, 0.07f, 0.88f);
            bgImg.raycastTarget = true; // Bloquea clics al mundo de juego detrás del menú

            var modalGo = CreateUIElement("Modal_Pause", pauseRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(540, 520), Vector2.zero);
            var modalImg = modalGo.AddComponent<Image>();
            modalImg.raycastTarget = false; // Marco estético, nunca intercepta clics a los botones

            Sprite panelSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.PanelFrame);
            if (panelSpr != null)
            {
                modalImg.sprite = panelSpr;
                modalImg.type = Image.Type.Sliced;
                modalImg.color = Color.white;
            }
            else
            {
                modalImg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);
            }

            var headerGo = CreateUIElement("Header", modalGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, 105), new Vector2(0, -56));
            var titleTmp = headerGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "<color=#00F0FF><b>PAUSA DEL SISTEMA</b></color>\n<size=14><color=#88A8C0>ESTADO DEL CHASIS: EN ESPERA OPERATIVA</color></size>";
            titleTmp.fontSize = 26;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;

            var buttonsGo = CreateUIElement("ButtonsContainer", modalGo.transform, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), new Vector2(420, 320), Vector2.zero);
            var vlayout = buttonsGo.AddComponent<VerticalLayoutGroup>();
            vlayout.spacing = 16;
            vlayout.childControlWidth = true;
            vlayout.childControlHeight = false;
            vlayout.childForceExpandWidth = true;
            vlayout.childForceExpandHeight = false;

            var resumeBtn = MainMenuBuilder.CreateSciFiButton(buttonsGo.transform, "Btn_Resume", "REANUDAR", 56, UIAssetGenerator.BtnGreen, new Color(0f, 0.95f, 0.58f, 1f));
            var optionsBtn = MainMenuBuilder.CreateSciFiButton(buttonsGo.transform, "Btn_Options", "CONFIGURACIÓN", 56, UIAssetGenerator.BtnCyan, new Color(0f, 0.94f, 1f, 1f));
            var exitMenuBtn = MainMenuBuilder.CreateSciFiButton(buttonsGo.transform, "Btn_ExitToMenu", "SALIR AL MENÚ", 56, UIAssetGenerator.BtnAmber, new Color(1f, 0.58f, 0f, 1f));
            var exitDesktopBtn = MainMenuBuilder.CreateSciFiButton(buttonsGo.transform, "Btn_ExitToDesktop", "CERRAR JUEGO", 56, UIAssetGenerator.BtnRed, new Color(1f, 0.24f, 0.35f, 1f));

            GameObject settingsInstance = null;
            if (settingsPrefab != null)
            {
                settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, canvasGo.transform);
                settingsInstance.name = "Dialog_Settings";
                settingsInstance.SetActive(false);

                var sCanvas = settingsInstance.GetComponent<Canvas>();
                if (sCanvas != null)
                {
                    sCanvas.overrideSorting = true;
                    sCanvas.sortingOrder = 600;
                }
            }

            var so = new SerializedObject(pauseView);
            so.FindProperty("_pauseRoot").objectReferenceValue = pauseRoot;
            so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            so.FindProperty("_resumeButton").objectReferenceValue = resumeBtn;
            so.FindProperty("_optionsButton").objectReferenceValue = optionsBtn;
            so.FindProperty("_exitToMenuButton").objectReferenceValue = exitMenuBtn;
            so.FindProperty("_exitToDesktopButton").objectReferenceValue = exitDesktopBtn;
            if (settingsInstance != null)
            {
                so.FindProperty("_settingsDialog").objectReferenceValue = settingsInstance;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            pauseRoot.SetActive(false);
            return canvasGo;
        }

        private static GameObject CreateCanvasRoot(string name, int sortingOrder = 500)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = go.AddComponent<GraphicRaycaster>();
            raycaster.blockingObjects = GraphicRaycaster.BlockingObjects.None;
            raycaster.ignoreReversedGraphics = true;
            return go;
        }

        private static GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return go;
        }

        public static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                var inputAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                if (inputAsset != null)
                {
                    inputModule.actionsAsset = inputAsset;
                }
                else
                {
                    try { inputModule.AssignDefaultActions(); } catch { }
                }
            }

            if (eventSystem.GetComponent<UIInputModuleFixer>() == null)
            {
                eventSystem.gameObject.AddComponent<UIInputModuleFixer>();
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
