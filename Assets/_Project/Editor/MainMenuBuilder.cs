using System.Collections.Generic;
using System.IO;
using Game.Gameplay.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class MainMenuBuilder
    {
        private const string PrefabsDirectory = "Assets/_Project/Prefabs/UI";
        private const string MainMenuPrefabPath = PrefabsDirectory + "/Canvas_MainMenu.prefab";
        private const string SettingsPrefabPath = PrefabsDirectory + "/Dialog_Settings.prefab";
        private const string InputAssetPath = "Assets/InputSystem_Actions.inputactions";

        private static readonly Color CyanAccent = new Color(0f, 0.94f, 1f, 1f);
        private static readonly Color AmberAccent = new Color(1f, 0.58f, 0f, 1f);
        private static readonly Color GreenAccent = new Color(0f, 0.95f, 0.58f, 1f);
        private static readonly Color RedAccent = new Color(1f, 0.24f, 0.35f, 1f);
        private static readonly Color SteelAccent = new Color(0.45f, 0.68f, 0.88f, 1f);

        [MenuItem("TPOB/UI/1. Generar Prefabs de Menú Principal y Opciones")]
        public static void GenerateUIPrefabs()
        {
            EnsureDirectory(PrefabsDirectory);
            UIAssetGenerator.EnsureUIAssets();

            GameObject settingsGo = BuildSettingsDialogHierarchy();
            GameObject settingsPrefab = PrefabUtility.SaveAsPrefabAsset(settingsGo, SettingsPrefabPath);
            Object.DestroyImmediate(settingsGo);

            GameObject mainMenuGo = BuildMainMenuHierarchy(settingsPrefab);
            PrefabUtility.SaveAsPrefabAsset(mainMenuGo, MainMenuPrefabPath);
            Object.DestroyImmediate(mainMenuGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[TPOB] Prefabs Sci-Fi de Menú Principal y Opciones generados con éxito en " + PrefabsDirectory + "</color>");
        }

        [MenuItem("TPOB/UI/2. Instanciar UI en Escena Activa (Boot)")]
        public static void InstantiateUIInScene()
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

            if (settingsPrefab == null || mainMenuPrefab == null)
            {
                GenerateUIPrefabs();
                settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
                mainMenuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPrefabPath);
            }

            GameObject settingsInstance;
            var existingSettings = Object.FindFirstObjectByType<SettingsView>(FindObjectsInactive.Include);
            if (existingSettings == null)
            {
                settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, uiRoot.transform);
                Undo.RegisterCreatedObjectUndo(settingsInstance, "Instantiate Dialog_Settings");
            }
            else
            {
                settingsInstance = existingSettings.gameObject;
            }

            var existingMainMenu = Object.FindFirstObjectByType<MainMenuView>(FindObjectsInactive.Include);
            if (existingMainMenu == null)
            {
                var mainMenuInstance = (GameObject)PrefabUtility.InstantiatePrefab(mainMenuPrefab, uiRoot.transform);
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

            Debug.Log("<color=cyan>[TPOB] UI instanciada en escena activa bajo '--- UI ---' con EventSystem configurado.</color>");
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            GameObject esGo;
            if (eventSystem == null)
            {
                esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }
            else
            {
                esGo = eventSystem.gameObject;
            }

            var legacyModule = esGo.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }

            var inputSystemModule = esGo.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = esGo.AddComponent<InputSystemUIInputModule>();
            }

            inputSystemModule.AssignDefaultActions();

            if (esGo.GetComponent<UIInputModuleFixer>() == null)
            {
                esGo.AddComponent<UIInputModuleFixer>();
            }
        }

        private static GameObject BuildMainMenuHierarchy(GameObject settingsPrefabRef)
        {
            var canvasGo = CreateCanvasRoot("Canvas_MainMenu", 100);
            var mainView = canvasGo.AddComponent<MainMenuView>();
            canvasGo.AddComponent<MainMenuPresenter>();

            Material bgMat = UIAssetGenerator.LoadBackdropMaterial(false);
            Sprite panelSprite = UIAssetGenerator.LoadSprite(UIAssetGenerator.PanelFrame);

            // 1. Pure Procedural CyberGridBackdrop Shader (lateral Cyan to Amber gradient, no photo)
            var bgGo = CreateUIElement("Background", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = Color.white;
            if (bgMat != null)
            {
                bgImg.material = bgMat;
            }

            // Top & Bottom HUD Telemetry Bars
            var topHudBar = CreateUIElement("HUD_TopBar", bgGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, 44), new Vector2(0, -22));
            var topHudImg = topHudBar.AddComponent<Image>();
            topHudImg.color = new Color(0.02f, 0.04f, 0.07f, 0.85f);

            var topHudTxtGo = CreateUIElement("HUD_TopText", topHudBar.transform, new Vector2(0f, 0f), new Vector2(0.7f, 1f), new Vector2(-20, 0), new Vector2(24, 0));
            var topHudTmp = topHudTxtGo.AddComponent<TextMeshProUGUI>();
            topHudTmp.text = "<color=#00F0FF>■ CHASIS: PROTOTIPO-02</color>     |     <color=#88AABB>TPOB SYSTEM</color>     |     <color=#FF9900>STANDBY ■</color>";
            topHudTmp.fontSize = 13;
            topHudTmp.alignment = TextAlignmentOptions.MidlineLeft;
            topHudTmp.raycastTarget = false;

            // Language Switcher Button (Top-Right)
            var langBtn = CreateSciFiButton(topHudBar.transform, "Btn_LanguageToggle", "IDIOMA: ESPAÑOL", 32, UIAssetGenerator.BtnNeutral, CyanAccent, 175);
            var langBtnRt = langBtn.GetComponent<RectTransform>();
            langBtnRt.anchorMin = new Vector2(1f, 0.5f);
            langBtnRt.anchorMax = new Vector2(1f, 0.5f);
            langBtnRt.pivot = new Vector2(1f, 0.5f);
            langBtnRt.anchoredPosition = new Vector2(-16, 0);
            var langTmp = langBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (langTmp != null) langTmp.fontSize = 13f;

            // 2. Central Menu Frame
            var containerGo = CreateUIElement("MenuContainer", bgGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 650), new Vector2(0, -10));
            var containerImg = containerGo.AddComponent<Image>();
            if (panelSprite != null)
            {
                containerImg.sprite = panelSprite;
                containerImg.type = Image.Type.Sliced;
                containerImg.color = new Color(1f, 1f, 1f, 0.96f);
            }
            else
            {
                containerImg.color = new Color(0.05f, 0.08f, 0.13f, 0.94f);
            }

            // Dual Top Accent Strip (Cyan Left + Amber Right)
            var leftStrip = CreateUIElement("LeftCyanStrip", containerGo.transform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(-18, 4), new Vector2(6, -6));
            leftStrip.AddComponent<Image>().color = CyanAccent;
            var rightStrip = CreateUIElement("RightAmberStrip", containerGo.transform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-18, 4), new Vector2(-6, -6));
            rightStrip.AddComponent<Image>().color = AmberAccent;

            // Header Title
            var titleGo = CreateUIElement("Title_Header", containerGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(510, 115), new Vector2(0, -72));
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "<size=13><color=#6699BB>PROYECTO DE SINCRONIZACIÓN COOPERATIVA</color></size>\n" +
                             "<b><size=38><color=#00F0FF>TWO PILOTS</color> <color=#FFFFFF>//</color> <color=#FF9900>ONE BODY</color></size></b>\n" +
                             "<size=15><color=#99B4C8>DIVISIÓN DE CONTROL: PIERNAS [CIAN] & TORSO [ÁMBAR]</color></size>";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            // Room Code Terminal Box
            var ipBoxGo = CreateUIElement("Row_LocalIP", containerGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460, 52), new Vector2(0, 148));
            var ipBoxImg = ipBoxGo.AddComponent<Image>();
            if (panelSprite != null)
            {
                ipBoxImg.sprite = panelSprite;
                ipBoxImg.type = Image.Type.Sliced;
            }
            ipBoxImg.color = new Color(0.03f, 0.06f, 0.10f, 0.95f);

            var ipHlg = ipBoxGo.AddComponent<HorizontalLayoutGroup>();
            ipHlg.padding = new RectOffset(16, 10, 6, 6);
            ipHlg.spacing = 10;
            ipHlg.childControlWidth = false;
            ipHlg.childControlHeight = true;
            ipHlg.childForceExpandWidth = false;
            ipHlg.childForceExpandHeight = true;

            var ipTextGo = CreateUIElement("Text_LocalIP", ipBoxGo.transform, Vector2.zero, Vector2.one, new Vector2(310, 40), Vector2.zero);
            var ipTmp = ipTextGo.AddComponent<TextMeshProUGUI>();
            ipTmp.text = "CÓDIGO DE SALA: <color=#00F0FF><b>TPOB-XXXX</b></color>";
            ipTmp.fontSize = 15;
            ipTmp.alignment = TextAlignmentOptions.MidlineLeft;
            ipTmp.raycastTarget = false;

            var copyBtn = CreateSciFiButton(ipBoxGo.transform, "Btn_CopyIP", "COPIAR", 38, UIAssetGenerator.BtnCyan, CyanAccent, 114);

            // Main Action Buttons Column
            var buttonsColGo = CreateUIElement("ButtonsColumn", containerGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460, 360), new Vector2(0, -38));
            var vlg = buttonsColGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var localBtn = CreateSciFiButton(buttonsColGo.transform, "Btn_LocalPlay", "LOCAL PLAY", 58, UIAssetGenerator.BtnAmber, AmberAccent);
            var hostBtn = CreateSciFiButton(buttonsColGo.transform, "Btn_Host", "HOST GAME", 58, UIAssetGenerator.BtnGreen, GreenAccent);
            var joinBtn = CreateSciFiButton(buttonsColGo.transform, "Btn_Join", "JOIN GAME", 58, UIAssetGenerator.BtnCyan, CyanAccent);
            var optBtn = CreateSciFiButton(buttonsColGo.transform, "Btn_Options", "OPTIONS", 58, UIAssetGenerator.BtnNeutral, SteelAccent);
            var exitBtn = CreateSciFiButton(buttonsColGo.transform, "Btn_Exit", "EXIT GAME", 58, UIAssetGenerator.BtnRed, RedAccent);

            // Status Feedback Footer
            var statusGo = CreateUIElement("Text_Status", containerGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(500, 38), new Vector2(0, 26));
            var statusText = statusGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "<color=#7799B0>Listo para iniciar enlace cooperativo.</color>";
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 15;
            statusText.raycastTarget = false;

            // 3. Modal Connect
            var modalRootGo = CreateUIElement("Modal_Connect", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500, 380), Vector2.zero);
            var modalBg = modalRootGo.AddComponent<Image>();
            if (panelSprite != null)
            {
                modalBg.sprite = panelSprite;
                modalBg.type = Image.Type.Sliced;
                modalBg.color = new Color(1f, 1f, 1f, 0.99f);
            }
            else
            {
                modalBg.color = new Color(0.06f, 0.09f, 0.14f, 0.98f);
            }

            var modalTitleGo = CreateUIElement("Modal_Title", modalRootGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(440, 56), new Vector2(0, -38));
            var modalTitleText = modalTitleGo.AddComponent<TextMeshProUGUI>();
            modalTitleText.text = "<b><size=24><color=#00F0FF>CONECTAR A SALA COOPERATIVA</color></size></b>";
            modalTitleText.alignment = TextAlignmentOptions.Center;
            modalTitleText.raycastTarget = false;

            var ipInput = CreateInputField(modalRootGo.transform, "Input_IP", "Ingresa Código de Sala (ej. TPOB-7821)", string.Empty, new Vector2(0, 58), new Vector2(420, 48));
            var portInput = CreateInputField(modalRootGo.transform, "Input_Port", "Puerto (opcional)", "5000", new Vector2(0, 0), new Vector2(420, 46));

            var helpTextGo = CreateUIElement("Modal_HelpText", modalRootGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(440, 48), new Vector2(0, 102));
            var helpTmp = helpTextGo.AddComponent<TextMeshProUGUI>();
            helpTmp.text = "<size=13><color=#88B4D0>Enlace directo mediante Relay PurrNet.\nPega el código que aparece en el menú del Host.</color></size>";
            helpTmp.alignment = TextAlignmentOptions.Center;
            helpTmp.raycastTarget = false;

            var modalBtnRow = CreateUIElement("Modal_Buttons", modalRootGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(420, 52), new Vector2(0, 34));
            var hlg = modalBtnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var confirmBtn = CreateSciFiButton(modalBtnRow.transform, "Btn_ConfirmConnect", "CONNECT", 48, UIAssetGenerator.BtnGreen, GreenAccent);
            var cancelBtn = CreateSciFiButton(modalBtnRow.transform, "Btn_CancelConnect", "CANCEL", 48, UIAssetGenerator.BtnRed, RedAccent);

            modalRootGo.SetActive(false);

            // Wire MainMenuView SerializedObject
            var so = new SerializedObject(mainView);
            so.FindProperty("_localPlayButton").objectReferenceValue = localBtn;
            so.FindProperty("_hostButton").objectReferenceValue = hostBtn;
            so.FindProperty("_openJoinModalButton").objectReferenceValue = joinBtn;
            so.FindProperty("_optionsButton").objectReferenceValue = optBtn;
            so.FindProperty("_exitButton").objectReferenceValue = exitBtn;
            so.FindProperty("_localIpText").objectReferenceValue = ipTmp;
            so.FindProperty("_copyIpButton").objectReferenceValue = copyBtn;
            so.FindProperty("_languageToggleButton").objectReferenceValue = langBtn;
            so.FindProperty("_languageButtonText").objectReferenceValue = langTmp;
            so.FindProperty("_connectHelpText").objectReferenceValue = helpTmp;
            so.FindProperty("_connectModalRoot").objectReferenceValue = modalRootGo;
            so.FindProperty("_ipInputField").objectReferenceValue = ipInput;
            so.FindProperty("_portInputField").objectReferenceValue = portInput;
            so.FindProperty("_connectConfirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("_connectCancelButton").objectReferenceValue = cancelBtn;
            so.FindProperty("_statusFeedbackText").objectReferenceValue = statusText;
            so.FindProperty("_connectHelpText").objectReferenceValue = helpTmp;
            so.FindProperty("_connectModalRoot").objectReferenceValue = modalRootGo;
            so.FindProperty("_ipInputField").objectReferenceValue = ipInput;
            so.FindProperty("_portInputField").objectReferenceValue = portInput;
            so.FindProperty("_connectConfirmButton").objectReferenceValue = confirmBtn;
            so.FindProperty("_connectCancelButton").objectReferenceValue = cancelBtn;
            so.FindProperty("_statusFeedbackText").objectReferenceValue = statusText;

            if (settingsPrefabRef != null)
            {
                var settingsViewComp = settingsPrefabRef.GetComponent<SettingsView>();
                so.FindProperty("_settingsDialog").objectReferenceValue = settingsViewComp;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return canvasGo;
        }

        private static GameObject BuildSettingsDialogHierarchy()
        {
            var canvasGo = CreateCanvasRoot("Dialog_Settings", 120);
            var settingsView = canvasGo.AddComponent<SettingsView>();
            canvasGo.AddComponent<SettingsPresenter>();

            Sprite panelSprite = UIAssetGenerator.LoadSprite(UIAssetGenerator.PanelFrame);

            // Dark Backdrop Overlay
            var overlayGo = CreateUIElement("Overlay", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.color = new Color(0.01f, 0.02f, 0.04f, 0.85f);

            // Modal Frame (900 x 700)
            var frameGo = CreateUIElement("Dialog_Frame", overlayGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(900, 700), Vector2.zero);
            var frameImg = frameGo.AddComponent<Image>();
            if (panelSprite != null)
            {
                frameImg.sprite = panelSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.color = new Color(1f, 1f, 1f, 0.99f);
            }
            else
            {
                frameImg.color = new Color(0.06f, 0.09f, 0.14f, 0.98f);
            }

            // Header Title
            var titleGo = CreateUIElement("Title", frameGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(820, 56), new Vector2(0, -34));
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "<b><size=25><color=#00F0FF>CONFIGURACIÓN DEL SISTEMA</color></size></b>   <size=14><color=#7799B5>// PANEL DE CALIBRACIÓN TÉCNICA</color></size>";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            // 3-Tab Navigation Bar
            var tabsRowGo = CreateUIElement("TabsRow", frameGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(820, 48), new Vector2(0, -88));
            var tabsHlg = tabsRowGo.AddComponent<HorizontalLayoutGroup>();
            tabsHlg.spacing = 14;
            tabsHlg.childControlWidth = true;
            tabsHlg.childControlHeight = true;
            tabsHlg.childForceExpandWidth = true;
            tabsHlg.childForceExpandHeight = true;

            var graphicsTabBtn = CreateTabButton(tabsRowGo.transform, "Btn_Tab_Graphics", "1. GRAPHICS", out var graphicsTabInd);
            var audioTabBtn = CreateTabButton(tabsRowGo.transform, "Btn_Tab_Audio", "2. AUDIO", out var audioTabInd);
            var controlsTabBtn = CreateTabButton(tabsRowGo.transform, "Btn_Tab_Controls", "3. CONTROLS", out var controlsTabInd);

            // Content Area Root
            var bodyRoot = CreateUIElement("BodyRoot", frameGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(820, 460), new Vector2(0, -18));

            // --- TAB 1: GRAPHICS PANEL ---
            var graphicsPanel = CreateTabPanel(bodyRoot.transform, "Panel_Graphics", 16);
            var resDropdown = CreateLabeledDropdown(graphicsPanel.transform, "Dropdown_Resolution", "Resolución de Pantalla");
            var fsDropdown = CreateLabeledDropdown(graphicsPanel.transform, "Dropdown_Fullscreen", "Modo de Visualización");
            var qualityDropdown = CreateLabeledDropdown(graphicsPanel.transform, "Dropdown_Quality", "Calidad Gráfica (URP)");
            var fpsDropdown = CreateLabeledDropdown(graphicsPanel.transform, "Dropdown_FpsLimit", "Límite de Fotogramas (FPS)");
            var vsyncToggle = CreateLabeledToggle(graphicsPanel.transform, "Toggle_VSync", "Sincronización Vertical (VSync)");

            // --- TAB 2: AUDIO PANEL ---
            var audioPanel = CreateTabPanel(bodyRoot.transform, "Panel_Audio", 18);
            var masterSlider = CreateLabeledSlider(audioPanel.transform, "Slider_MasterVolume", "Volumen Maestro General", out var masterValText);
            var musicSlider = CreateLabeledSlider(audioPanel.transform, "Slider_MusicVolume", "Volumen de Música Ambiental", out var musicValText);
            var sfxSlider = CreateLabeledSlider(audioPanel.transform, "Slider_SfxVolume", "Volumen de Efectos (SFX / Física)", out var sfxValText);
            var muteBgToggle = CreateLabeledToggle(audioPanel.transform, "Toggle_MuteBg", "Silenciar Audio con Ventana en Segundo Plano");
            audioPanel.SetActive(false);

            // --- TAB 3: CONTROLS PANEL ---
            var controlsPanel = CreateUIElement("Panel_Controls", bodyRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var ctrlVlg = controlsPanel.AddComponent<VerticalLayoutGroup>();
            ctrlVlg.spacing = 10;
            ctrlVlg.padding = new RectOffset(8, 8, 4, 4);
            ctrlVlg.childControlWidth = true;
            ctrlVlg.childControlHeight = false;
            ctrlVlg.childForceExpandWidth = true;
            ctrlVlg.childForceExpandHeight = false;

            var sensSlider = CreateLabeledSlider(controlsPanel.transform, "Slider_AimSens", "Sensibilidad de Puntería (Torso)", out var sensValText, 0.2f, 3.0f, 40f);
            var invertYToggle = CreateLabeledToggle(controlsPanel.transform, "Toggle_InvertY", "Invertir Eje Vertical de Cámara (Eje Y)", 38f);

            var rebindHeaderGo = CreateUIElement("RebindHeader", controlsPanel.transform, Vector2.zero, Vector2.one, new Vector2(0, 26), Vector2.zero);
            var rebindHeaderTmp = rebindHeaderGo.AddComponent<TextMeshProUGUI>();
            rebindHeaderTmp.text = "<b><size=14><color=#FF9900>ASIGNACIÓN DE TECLAS Y BOTONES (HAZ CLIC EN 'CAMBIAR' Y PRESIONA UNA TECLA):</color></size></b>";
            rebindHeaderTmp.alignment = TextAlignmentOptions.MidlineLeft;
            rebindHeaderTmp.raycastTarget = false;

            var gridGo = CreateUIElement("RebindGrid", controlsPanel.transform, Vector2.zero, Vector2.one, new Vector2(0, 320), Vector2.zero);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(394, 52);
            grid.spacing = new Vector2(14, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            var rebindList = new List<InputRebindRowUI>(10)
            {
                CreateRebindRow(gridGo.transform, "Rebind_MoveUp", "Mover Adelante", "Player/Move", 2, "W"),
                CreateRebindRow(gridGo.transform, "Rebind_MoveDown", "Mover Atrás", "Player/Move", 4, "S"),
                CreateRebindRow(gridGo.transform, "Rebind_MoveLeft", "Mover Izquierda", "Player/Move", 6, "A"),
                CreateRebindRow(gridGo.transform, "Rebind_MoveRight", "Mover Derecha", "Player/Move", 8, "D"),
                CreateRebindRow(gridGo.transform, "Rebind_Jump", "Saltar / Doble Salto (Piernas)", "Player/Jump", 0, "SPACE"),
                CreateRebindRow(gridGo.transform, "Rebind_Sprint", "Sprint Rápido (Piernas)", "Player/Sprint", 0, "LEFT SHIFT"),
                CreateRebindRow(gridGo.transform, "Rebind_Attack", "Patada / Lanzar Objeto", "Player/Attack", 1, "LEFT BUTTON"),
                CreateRebindRow(gridGo.transform, "Rebind_Interact", "Agarrar / Interactuar (Torso)", "Player/Interact", 0, "E"),
                CreateRebindRow(gridGo.transform, "Rebind_Magnet", "Rayo Magnético (Torso)", "Player/Crouch", 1, "C"),
                CreateRebindRow(gridGo.transform, "Rebind_Fuse", "Acople / Desacople (Fusión)", "Player/Fuse", 0, "R")
            };

            controlsPanel.SetActive(false);

            // Footer Buttons (Reset Defaults + Save & Close)
            var footerGo = CreateUIElement("FooterRow", frameGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(820, 52), new Vector2(0, 34));
            var footerHlg = footerGo.AddComponent<HorizontalLayoutGroup>();
            footerHlg.spacing = 24;
            footerHlg.childControlWidth = true;
            footerHlg.childControlHeight = true;
            footerHlg.childForceExpandWidth = true;
            footerHlg.childForceExpandHeight = true;

            var resetBtn = CreateSciFiButton(footerGo.transform, "Btn_ResetDefaults", "RESTORE DEFAULTS", 48, UIAssetGenerator.BtnAmber, AmberAccent);
            var closeBtn = CreateSciFiButton(footerGo.transform, "Btn_Close", "SAVE & CLOSE", 48, UIAssetGenerator.BtnCyan, CyanAccent);

            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);

            // Wire SettingsView SerializedObject
            var so = new SerializedObject(settingsView);
            so.FindProperty("_graphicsTabButton").objectReferenceValue = graphicsTabBtn;
            so.FindProperty("_audioTabButton").objectReferenceValue = audioTabBtn;
            so.FindProperty("_controlsTabButton").objectReferenceValue = controlsTabBtn;
            so.FindProperty("_graphicsPanel").objectReferenceValue = graphicsPanel;
            so.FindProperty("_audioPanel").objectReferenceValue = audioPanel;
            so.FindProperty("_controlsPanel").objectReferenceValue = controlsPanel;
            so.FindProperty("_graphicsTabIndicator").objectReferenceValue = graphicsTabInd;
            so.FindProperty("_audioTabIndicator").objectReferenceValue = audioTabInd;
            so.FindProperty("_controlsTabIndicator").objectReferenceValue = controlsTabInd;

            so.FindProperty("_masterVolumeSlider").objectReferenceValue = masterSlider;
            so.FindProperty("_masterVolumeValueText").objectReferenceValue = masterValText;
            so.FindProperty("_sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("_sfxVolumeValueText").objectReferenceValue = sfxValText;
            so.FindProperty("_musicVolumeSlider").objectReferenceValue = musicSlider;
            so.FindProperty("_musicVolumeValueText").objectReferenceValue = musicValText;
            so.FindProperty("_muteInBackgroundToggle").objectReferenceValue = muteBgToggle;

            so.FindProperty("_resolutionDropdown").objectReferenceValue = resDropdown;
            so.FindProperty("_fullscreenModeDropdown").objectReferenceValue = fsDropdown;
            so.FindProperty("_qualityDropdown").objectReferenceValue = qualityDropdown;
            so.FindProperty("_fpsLimitDropdown").objectReferenceValue = fpsDropdown;
            so.FindProperty("_vsyncToggle").objectReferenceValue = vsyncToggle;

            so.FindProperty("_sensitivitySlider").objectReferenceValue = sensSlider;
            so.FindProperty("_sensitivityValueText").objectReferenceValue = sensValText;
            so.FindProperty("_invertYToggle").objectReferenceValue = invertYToggle;
            so.FindProperty("_inputActionsAsset").objectReferenceValue = inputAsset;

            SerializedProperty rowsProp = so.FindProperty("_rebindRows");
            rowsProp.arraySize = rebindList.Count;
            for (int i = 0; i < rebindList.Count; i++)
            {
                rowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rebindList[i];
            }

            so.FindProperty("_resetDefaultsButton").objectReferenceValue = resetBtn;
            so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            canvasGo.SetActive(false);
            return canvasGo;
        }

        private static GameObject CreateTabPanel(Transform parent, string name, float spacing)
        {
            var panelGo = CreateUIElement(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var vlg = panelGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(14, 14, 12, 12);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return panelGo;
        }

        private static Button CreateTabButton(Transform parent, string name, string label, out Graphic indicatorGraphic)
        {
            var btn = CreateSciFiButton(parent, name, label, 46, UIAssetGenerator.BtnNeutral, CyanAccent);
            var indGo = CreateUIElement("ActiveIndicator", btn.transform, new Vector2(0.05f, 0f), new Vector2(0.95f, 0f), new Vector2(0, 4), new Vector2(0, 3));
            var indImg = indGo.AddComponent<Image>();
            indImg.color = CyanAccent;
            indImg.raycastTarget = false;
            indicatorGraphic = indImg;
            return btn;
        }

        public static Button CreateSciFiButton(
            Transform parent,
            string name,
            string label,
            float height,
            string spriteFileName,
            Color accentColor,
            float fixedWidth = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(fixedWidth > 0f ? fixedWidth : 400f, height);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (fixedWidth > 0f)
            {
                le.minWidth = fixedWidth;
                le.preferredWidth = fixedWidth;
                le.flexibleWidth = 0f;
            }
            else
            {
                le.flexibleWidth = 1f;
            }

            var img = go.AddComponent<Image>();
            Sprite spr = UIAssetGenerator.LoadSprite(spriteFileName);
            if (spr != null)
            {
                img.sprite = spr;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(accentColor.r * 0.25f, accentColor.g * 0.25f, accentColor.b * 0.25f, 0.95f);
            }
            img.raycastTarget = true;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);

            var accentGo = CreateUIElement("AccentBar", go.transform, new Vector2(0f, 0.15f), new Vector2(0f, 0.85f), new Vector2(5f, 0f), new Vector2(8f, 0f));
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.65f);
            accentImg.raycastTarget = false;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.82f, 0.88f, 0.95f, 1f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.targetGraphic = img;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = new Vector2(-28f, 0f);
            textRt.anchoredPosition = new Vector2(4f, 0f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{label}</b>";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = height >= 50f ? 18f : 15f;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var polish = go.AddComponent<UIButtonPolish>();
            var polishSo = new SerializedObject(polish);
            polishSo.FindProperty("_accentBar").objectReferenceValue = accentImg;
            polishSo.FindProperty("_borderOutline").objectReferenceValue = outline;
            polishSo.FindProperty("_labelRect").objectReferenceValue = textRt;
            polishSo.FindProperty("_normalAccentColor").colorValue = new Color(accentColor.r, accentColor.g, accentColor.b, 0.55f);
            polishSo.FindProperty("_hoverAccentColor").colorValue = new Color(accentColor.r, accentColor.g, accentColor.b, 1f);
            polishSo.ApplyModifiedPropertiesWithoutUndo();

            return btn;
        }

        private static InputRebindRowUI CreateRebindRow(
            Transform parent,
            string name,
            string displayName,
            string actionPath,
            int bindingIndex,
            string defaultKeyText)
        {
            var rowGo = CreateUIElement(name, parent, Vector2.zero, Vector2.one, new Vector2(394, 52), Vector2.zero);
            var rowImg = rowGo.AddComponent<Image>();
            Sprite panelSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.PanelFrame);
            if (panelSpr != null)
            {
                rowImg.sprite = panelSpr;
                rowImg.type = Image.Type.Sliced;
            }
            rowImg.color = new Color(0.05f, 0.09f, 0.14f, 0.92f);

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 10, 6, 6);
            hlg.spacing = 8;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var actionLblGo = CreateUIElement("ActionLabel", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(178, 0), Vector2.zero);
            var actionTmp = actionLblGo.AddComponent<TextMeshProUGUI>();
            actionTmp.text = displayName;
            actionTmp.fontSize = 13.5f;
            actionTmp.color = new Color(0.88f, 0.94f, 1f);
            actionTmp.alignment = TextAlignmentOptions.MidlineLeft;
            actionTmp.raycastTarget = false;

            var keyBadgeGo = CreateUIElement("KeyBadge", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(96, 0), Vector2.zero);
            var keyBadgeImg = keyBadgeGo.AddComponent<Image>();
            keyBadgeImg.color = new Color(0.02f, 0.05f, 0.08f, 0.95f);
            var valTxtGo = CreateUIElement("BindingText", keyBadgeGo.transform, Vector2.zero, Vector2.one, new Vector2(-8, 0), Vector2.zero);
            var valTmp = valTxtGo.AddComponent<TextMeshProUGUI>();
            valTmp.text = defaultKeyText;
            valTmp.fontSize = 13f;
            valTmp.fontStyle = FontStyles.Bold;
            valTmp.color = CyanAccent;
            valTmp.alignment = TextAlignmentOptions.Center;
            valTmp.raycastTarget = false;

            var rebindBtn = CreateSciFiButton(rowGo.transform, "Btn_Rebind", "REBIND", 38, UIAssetGenerator.BtnCyan, CyanAccent, 86);

            var rowComp = rowGo.AddComponent<InputRebindRowUI>();
            var so = new SerializedObject(rowComp);
            so.FindProperty("_actionPath").stringValue = actionPath;
            so.FindProperty("_bindingIndex").intValue = bindingIndex;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_actionLabelText").objectReferenceValue = actionTmp;
            so.FindProperty("_bindingValueText").objectReferenceValue = valTmp;
            so.FindProperty("_rebindButton").objectReferenceValue = rebindBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return rowComp;
        }

        private static GameObject CreateCanvasRoot(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return go;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, string defaultVal, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var bgImg = go.AddComponent<Image>();
            Sprite spr = UIAssetGenerator.LoadSprite(UIAssetGenerator.SliderTrack);
            if (spr != null)
            {
                bgImg.sprite = spr;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.04f, 0.06f, 0.09f, 0.96f);
            }

            var input = go.AddComponent<TMP_InputField>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = new Vector2(-24, -10);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 17;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;

            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(go.transform, false);
            var phRt = phGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.sizeDelta = new Vector2(-24, -10);

            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 16;
            phTmp.color = new Color(0.5f, 0.65f, 0.78f, 0.55f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            phTmp.raycastTarget = false;

            input.textComponent = tmp;
            input.placeholder = phTmp;
            input.text = defaultVal;

            return input;
        }

        private static Slider CreateLabeledSlider(
            Transform parent,
            string name,
            string label,
            out TextMeshProUGUI valText,
            float min = 0f,
            float max = 1f,
            float rowHeight = 56f)
        {
            var rowGo = CreateUIElement(name + "_Row", parent, Vector2.zero, Vector2.one, new Vector2(0, rowHeight), Vector2.zero);
            var rowImg = rowGo.AddComponent<Image>();
            rowImg.color = new Color(0.04f, 0.07f, 0.11f, 0.75f);

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 8, 8);
            layout.spacing = 16;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var labelGo = CreateUIElement("Label", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(310, 0), Vector2.zero);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = $"<b>{label}</b>";
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = new Color(0.88f, 0.94f, 1f);
            labelTmp.raycastTarget = false;

            var sliderGo = CreateUIElement("Slider", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(340, 0), Vector2.zero);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;

            // Track Background
            var bgGo = CreateUIElement("Background", sliderGo.transform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), Vector2.zero, Vector2.zero);
            var bgImg = bgGo.AddComponent<Image>();
            Sprite trackSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.SliderTrack);
            if (trackSpr != null)
            {
                bgImg.sprite = trackSpr;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);
            }

            // Fill Area
            var fillAreaGo = CreateUIElement("Fill Area", sliderGo.transform, new Vector2(0f, 0.25f), new Vector2(1f, 0.75f), new Vector2(-12f, 0f), Vector2.zero);
            var fillGo = CreateUIElement("Fill", fillAreaGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillImg = fillGo.AddComponent<Image>();
            Sprite fillSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.SliderFill);
            if (fillSpr != null)
            {
                fillImg.sprite = fillSpr;
                fillImg.type = Image.Type.Sliced;
                fillImg.color = Color.white;
            }
            else
            {
                fillImg.color = CyanAccent;
            }

            // Handle Slide Area
            var handleAreaGo = CreateUIElement("Handle Slide Area", sliderGo.transform, Vector2.zero, Vector2.one, new Vector2(-16f, 0f), Vector2.zero);
            var handleGo = CreateUIElement("Handle", handleAreaGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 28f), Vector2.zero);
            var handleImg = handleGo.AddComponent<Image>();
            Sprite knobSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.SliderKnob);
            if (knobSpr != null)
            {
                handleImg.sprite = knobSpr;
                handleImg.color = Color.white;
            }
            else
            {
                handleImg.color = CyanAccent;
            }

            slider.fillRect = fillGo.GetComponent<RectTransform>();
            slider.handleRect = handleGo.GetComponent<RectTransform>();
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            var valGo = CreateUIElement("ValueText", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(85, 0), Vector2.zero);
            valText = valGo.AddComponent<TextMeshProUGUI>();
            valText.fontSize = 17;
            valText.fontStyle = FontStyles.Bold;
            valText.alignment = TextAlignmentOptions.MidlineRight;
            valText.color = CyanAccent;
            valText.text = "100%";
            valText.raycastTarget = false;

            return slider;
        }

        private static TMP_Dropdown CreateLabeledDropdown(Transform parent, string name, string label)
        {
            var rowGo = CreateUIElement(name + "_Row", parent, Vector2.zero, Vector2.one, new Vector2(0, 56), Vector2.zero);
            var rowImg = rowGo.AddComponent<Image>();
            rowImg.color = new Color(0.04f, 0.07f, 0.11f, 0.75f);

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 8, 8);
            layout.spacing = 16;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var labelGo = CreateUIElement("Label", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(310, 0), Vector2.zero);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = $"<b>{label}</b>";
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = new Color(0.88f, 0.94f, 1f);
            labelTmp.raycastTarget = false;

            var dropGo = CreateUIElement("Dropdown", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(430, 0), Vector2.zero);
            var dropImg = dropGo.AddComponent<Image>();
            Sprite btnSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.BtnNeutral);
            if (btnSpr != null)
            {
                dropImg.sprite = btnSpr;
                dropImg.type = Image.Type.Sliced;
                dropImg.color = Color.white;
            }
            else
            {
                dropImg.color = new Color(0.10f, 0.15f, 0.22f, 0.95f);
            }

            var dropdown = dropGo.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = dropImg;

            var captionGo = CreateUIElement("Label", dropGo.transform, Vector2.zero, Vector2.one, new Vector2(-44, 0), new Vector2(6, 0));
            var captionTmp = captionGo.AddComponent<TextMeshProUGUI>();
            captionTmp.fontSize = 15;
            captionTmp.color = Color.white;
            captionTmp.alignment = TextAlignmentOptions.MidlineLeft;
            captionTmp.raycastTarget = false;

            var arrowGo = CreateUIElement("Arrow", dropGo.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(24, 24), new Vector2(-18, 0));
            var arrowTmp = arrowGo.AddComponent<TextMeshProUGUI>();
            arrowTmp.text = "▼";
            arrowTmp.fontSize = 14;
            arrowTmp.color = CyanAccent;
            arrowTmp.alignment = TextAlignmentOptions.Center;
            arrowTmp.raycastTarget = false;

            // Build functional Dropdown Template
            var templateGo = CreateUIElement("Template", dropGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0, 190), new Vector2(0, 2));
            templateGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            var templateImg = templateGo.AddComponent<Image>();
            templateImg.color = new Color(0.04f, 0.07f, 0.12f, 0.99f);
            var scrollRect = templateGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = CreateUIElement("Viewport", templateGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewportGo.AddComponent<Image>().color = Color.white;
            var mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = CreateUIElement("Content", viewportGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, 38), Vector2.zero);
            contentGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);

            var itemGo = CreateUIElement("Item", contentGo.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0, 36), Vector2.zero);
            var itemToggle = itemGo.AddComponent<Toggle>();

            var itemBgGo = CreateUIElement("Item Background", itemGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var itemBgImg = itemBgGo.AddComponent<Image>();
            itemBgImg.color = new Color(0.08f, 0.14f, 0.22f, 0.95f);

            var itemCheckGo = CreateUIElement("Item Checkmark", itemGo.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6, 28), new Vector2(6, 0));
            var itemCheckImg = itemCheckGo.AddComponent<Image>();
            itemCheckImg.color = CyanAccent;

            var itemLblGo = CreateUIElement("Item Label", itemGo.transform, Vector2.zero, Vector2.one, new Vector2(-30, 0), new Vector2(8, 0));
            var itemLblTmp = itemLblGo.AddComponent<TextMeshProUGUI>();
            itemLblTmp.fontSize = 14;
            itemLblTmp.color = Color.white;
            itemLblTmp.alignment = TextAlignmentOptions.MidlineLeft;

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = itemCheckImg;
            itemToggle.isOn = true;

            scrollRect.content = contentGo.GetComponent<RectTransform>();
            scrollRect.viewport = viewportGo.GetComponent<RectTransform>();

            dropdown.template = templateGo.GetComponent<RectTransform>();
            dropdown.captionText = captionTmp;
            dropdown.itemText = itemLblTmp;

            templateGo.SetActive(false);
            return dropdown;
        }

        private static Toggle CreateLabeledToggle(Transform parent, string name, string label, float rowHeight = 52f)
        {
            var rowGo = CreateUIElement(name + "_Row", parent, Vector2.zero, Vector2.one, new Vector2(0, rowHeight), Vector2.zero);
            var rowImg = rowGo.AddComponent<Image>();
            rowImg.color = new Color(0.04f, 0.07f, 0.11f, 0.75f);

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 6, 6);
            layout.spacing = 16;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var labelGo = CreateUIElement("Label", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(560, 0), Vector2.zero);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = $"<b>{label}</b>";
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = new Color(0.88f, 0.94f, 1f);
            labelTmp.raycastTarget = false;

            var toggleGo = CreateUIElement("ToggleBox", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(36, 36), Vector2.zero);
            var bgImg = toggleGo.AddComponent<Image>();
            Sprite boxSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.ToggleBox);
            if (boxSpr != null)
            {
                bgImg.sprite = boxSpr;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.08f, 0.14f, 0.22f);
            }

            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;

            var checkGo = CreateUIElement("Checkmark", toggleGo.transform, new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), Vector2.zero, Vector2.zero);
            var checkImg = checkGo.AddComponent<Image>();
            Sprite checkSpr = UIAssetGenerator.LoadSprite(UIAssetGenerator.ToggleCheck);
            if (checkSpr != null)
            {
                checkImg.sprite = checkSpr;
                checkImg.color = Color.white;
            }
            else
            {
                checkImg.color = CyanAccent;
            }
            toggle.graphic = checkImg;

            return toggle;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
