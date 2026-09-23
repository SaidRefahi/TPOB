using System.IO;
using Game.Gameplay.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class MainMenuBuilder
    {
        private const string PrefabsDirectory = "Assets/_Project/Prefabs/UI";
        private const string MainMenuPrefabPath = PrefabsDirectory + "/Canvas_MainMenu.prefab";
        private const string SettingsPrefabPath = PrefabsDirectory + "/Dialog_Settings.prefab";

        [MenuItem("TPOB/UI/1. Generar Prefabs de Menú Principal y Opciones")]
        public static void GenerateUIPrefabs()
        {
            EnsureDirectory(PrefabsDirectory);

            GameObject settingsGo = BuildSettingsDialogHierarchy();
            GameObject settingsPrefab = PrefabUtility.SaveAsPrefabAsset(settingsGo, SettingsPrefabPath);
            Object.DestroyImmediate(settingsGo);

            GameObject mainMenuGo = BuildMainMenuHierarchy(settingsPrefab);
            GameObject mainMenuPrefab = PrefabUtility.SaveAsPrefabAsset(mainMenuGo, MainMenuPrefabPath);
            Object.DestroyImmediate(mainMenuGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[TPOB] Prefabs de UI generados con éxito en " + PrefabsDirectory + "</color>");
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

            GameObject settingsInstance = null;
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

            // Remove legacy standalone input module to prevent errors with new Input System
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
            var canvasGo = CreateCanvasRoot("Canvas_MainMenu");
            var mainView = canvasGo.AddComponent<MainMenuView>();
            canvasGo.AddComponent<MainMenuPresenter>();

            // Dark background panel
            var bgGo = CreateUIElement("Background", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.06f, 0.08f, 0.11f, 0.95f);

            // Container Panel
            var containerGo = CreateUIElement("MenuContainer", bgGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(500, 600), new Vector2(0, 0));

            // Header Title
            var titleGo = CreateUIElement("Title_Header", containerGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(600, 100), new Vector2(0, -60));
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "<b><size=38><color=#00FFFF>TWO PILOTS</color>, <color=#FF9900>ONE ROBOT</color></size></b>\n<size=18><color=#8899AA>MISIÓN COOPERATIVA ASIMÉTRICA</color></size>";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            // IP Info Row (Muestra la IP local del Host y permite copiarla para el amigo)
            var ipRowGo = CreateUIElement("Row_LocalIP", containerGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 36), new Vector2(0, 160));
            var ipHlg = ipRowGo.AddComponent<HorizontalLayoutGroup>();
            ipHlg.spacing = 8;
            ipHlg.childControlWidth = false;
            ipHlg.childControlHeight = true;
            ipHlg.childForceExpandWidth = false;
            ipHlg.childForceExpandHeight = true;

            var ipTextGo = CreateUIElement("Text_LocalIP", ipRowGo.transform, Vector2.zero, Vector2.one, new Vector2(280, 36), Vector2.zero);
            var ipTmp = ipTextGo.AddComponent<TextMeshProUGUI>();
            ipTmp.text = "Código de Sala: <color=#00FFFF>TPOB-XXXX</color>";
            ipTmp.fontSize = 15;
            ipTmp.alignment = TextAlignmentOptions.MidlineLeft;
            ipTmp.raycastTarget = false;

            var copyBtn = CreateButton(ipRowGo.transform, "Btn_CopyIP", "COPIAR", 36, new Color(0.18f, 0.28f, 0.38f));
            var copyBtnRt = copyBtn.GetComponent<RectTransform>();
            copyBtnRt.sizeDelta = new Vector2(100, 36);
            var copyLe = copyBtn.GetComponent<LayoutElement>();
            if (copyLe != null)
            {
                copyLe.minWidth = 100;
                copyLe.preferredWidth = 100;
                copyLe.flexibleWidth = 0;
            }

            // Buttons Column
            var buttonsColGo = CreateUIElement("ButtonsColumn", containerGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 320), new Vector2(0, -30));
            var vlg = buttonsColGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var hostBtn = CreateButton(buttonsColGo.transform, "Btn_Host", "CREAR PARTIDA (HOST)", 55, new Color(0.12f, 0.28f, 0.22f));
            var joinBtn = CreateButton(buttonsColGo.transform, "Btn_Join", "UNIRSE A PARTIDA", 55, new Color(0.15f, 0.22f, 0.32f));
            var optBtn = CreateButton(buttonsColGo.transform, "Btn_Options", "OPCIONES", 55, new Color(0.2f, 0.22f, 0.26f));
            var exitBtn = CreateButton(buttonsColGo.transform, "Btn_Exit", "SALIR DEL JUEGO", 55, new Color(0.28f, 0.12f, 0.12f));

            // Status Text
            var statusGo = CreateUIElement("Text_Status", containerGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(500, 40), new Vector2(0, 30));
            var statusText = statusGo.AddComponent<TextMeshProUGUI>();
            statusText.text = string.Empty;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 18;
            statusText.raycastTarget = false;

            // Modal Connect
            var modalRootGo = CreateUIElement("Modal_Connect", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460, 350), Vector2.zero);
            var modalBg = modalRootGo.AddComponent<Image>();
            modalBg.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

            var modalTitleGo = CreateUIElement("Modal_Title", modalRootGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(400, 50), new Vector2(0, -35));
            var modalTitleText = modalTitleGo.AddComponent<TextMeshProUGUI>();
            modalTitleText.text = "<b><size=22><color=#00FFFF>UNIRSE A SALA</color></size></b>";
            modalTitleText.alignment = TextAlignmentOptions.Center;
            modalTitleText.raycastTarget = false;

            var ipInput = CreateInputField(modalRootGo.transform, "Input_IP", "Código de Sala (ej. TPOB-7821)", string.Empty, new Vector2(0, 55), new Vector2(380, 44));
            var portInput = CreateInputField(modalRootGo.transform, "Input_Port", "Puerto (opcional)", "5000", new Vector2(0, 0), new Vector2(380, 44));

            var helpTextGo = CreateUIElement("Modal_HelpText", modalRootGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(420, 44), new Vector2(0, 100));
            var helpTmp = helpTextGo.AddComponent<TextMeshProUGUI>();
            helpTmp.text = "<size=12><color=#88AACC>Conexión automática en servidores gratuitos de PurrNet.\nIngresa el código que te pasó el Host (sin IP ni programas externos).</color></size>";
            helpTmp.alignment = TextAlignmentOptions.Center;
            helpTmp.raycastTarget = false;

            var modalBtnRow = CreateUIElement("Modal_Buttons", modalRootGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(380, 50), new Vector2(0, 35));
            var hlg = modalBtnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 15;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var confirmBtn = CreateButton(modalBtnRow.transform, "Btn_ConfirmConnect", "CONECTAR", 45, new Color(0.12f, 0.35f, 0.22f));
            var cancelBtn = CreateButton(modalBtnRow.transform, "Btn_CancelConnect", "CANCELAR", 45, new Color(0.3f, 0.15f, 0.15f));

            modalRootGo.SetActive(false);

            // Wire SerializedObject
            var so = new SerializedObject(mainView);
            so.FindProperty("_hostButton").objectReferenceValue = hostBtn;
            so.FindProperty("_openJoinModalButton").objectReferenceValue = joinBtn;
            so.FindProperty("_optionsButton").objectReferenceValue = optBtn;
            so.FindProperty("_exitButton").objectReferenceValue = exitBtn;
            so.FindProperty("_localIpText").objectReferenceValue = ipTmp;
            so.FindProperty("_copyIpButton").objectReferenceValue = copyBtn;
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
            var canvasGo = CreateCanvasRoot("Dialog_Settings");
            var settingsView = canvasGo.AddComponent<SettingsView>();
            canvasGo.AddComponent<SettingsPresenter>();

            // Dark semi-transparent overlay
            var overlayGo = CreateUIElement("Overlay", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayImg = overlayGo.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.75f);

            // Dialog Window Frame
            var frameGo = CreateUIElement("Dialog_Frame", overlayGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620, 680), Vector2.zero);
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.color = new Color(0.09f, 0.12f, 0.16f, 0.98f);

            // Dialog Title
            var titleGo = CreateUIElement("Title", frameGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(500, 50), new Vector2(0, -35));
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "<b><size=24><color=#00FFFF>AJUSTES Y CONFIGURACIÓN</color></size></b>";
            titleText.alignment = TextAlignmentOptions.Center;

            // Sliders & Controls Container
            var contentGo = CreateUIElement("SettingsContent", frameGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 480), new Vector2(0, -10));
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Master Volume
            var masterSlider = CreateLabeledSlider(contentGo.transform, "Slider_MasterVolume", "Volumen Maestro", out var masterValText);

            // SFX Volume
            var sfxSlider = CreateLabeledSlider(contentGo.transform, "Slider_SfxVolume", "Volumen Efectos (SFX)", out var sfxValText);

            // Aim Sensitivity
            var sensSlider = CreateLabeledSlider(contentGo.transform, "Slider_AimSens", "Sensibilidad de Puntería", out var sensValText, 0.2f, 3.0f);

            // Fullscreen Dropdown
            var fsDropdown = CreateLabeledDropdown(contentGo.transform, "Dropdown_Fullscreen", "Modo de Pantalla");

            // Resolution Dropdown
            var resDropdown = CreateLabeledDropdown(contentGo.transform, "Dropdown_Resolution", "Resolución");

            // VSync Toggle
            var vsyncToggle = CreateLabeledToggle(contentGo.transform, "Toggle_VSync", "Sincronización Vertical (VSync)");

            // Close Button
            var closeBtnGo = CreateUIElement("CloseButtonContainer", frameGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(300, 50), new Vector2(0, 35));
            var closeBtn = CreateButton(closeBtnGo.transform, "Btn_Close", "GUARDAR Y CERRAR", 45, new Color(0.15f, 0.25f, 0.35f));

            // Wire SerializedObject
            var so = new SerializedObject(settingsView);
            so.FindProperty("_masterVolumeSlider").objectReferenceValue = masterSlider;
            so.FindProperty("_masterVolumeValueText").objectReferenceValue = masterValText;
            so.FindProperty("_sfxVolumeSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("_sfxVolumeValueText").objectReferenceValue = sfxValText;
            so.FindProperty("_sensitivitySlider").objectReferenceValue = sensSlider;
            so.FindProperty("_sensitivityValueText").objectReferenceValue = sensValText;
            so.FindProperty("_fullscreenModeDropdown").objectReferenceValue = fsDropdown;
            so.FindProperty("_resolutionDropdown").objectReferenceValue = resDropdown;
            so.FindProperty("_vsyncToggle").objectReferenceValue = vsyncToggle;
            so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            canvasGo.SetActive(false);
            return canvasGo;
        }

        private static GameObject CreateCanvasRoot(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

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

        private static Button CreateButton(Transform parent, string name, string label, float height, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, height);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;

            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = bgColor * 1.3f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;
            btn.targetGraphic = img;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{label}</b>";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 18;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return btn;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, string defaultVal, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);

            var input = go.AddComponent<TMP_InputField>();

            // Text component
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = new Vector2(-20, -10);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 17;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;

            // Placeholder component
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.sizeDelta = new Vector2(-20, -10);

            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.text = placeholder;
            phTmp.fontSize = 17;
            phTmp.color = new Color(0.5f, 0.6f, 0.7f, 0.5f);
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            phTmp.raycastTarget = false;

            input.textComponent = tmp;
            input.placeholder = phTmp;
            input.text = defaultVal;

            return input;
        }

        private static Slider CreateLabeledSlider(Transform parent, string name, string label, out TextMeshProUGUI valText, float min = 0f, float max = 1f)
        {
            var rowGo = CreateUIElement(name + "_Row", parent, Vector2.zero, Vector2.one, new Vector2(0, 50), Vector2.zero);
            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var labelGo = CreateUIElement("Label", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(200, 0), Vector2.zero);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = new Color(0.85f, 0.9f, 0.95f);

            var sliderGo = CreateUIElement("Slider", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(220, 0), Vector2.zero);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;

            var sliderBg = sliderGo.AddComponent<Image>();
            sliderBg.color = new Color(0.12f, 0.15f, 0.2f);

            var valGo = CreateUIElement("ValueText", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(60, 0), Vector2.zero);
            valText = valGo.AddComponent<TextMeshProUGUI>();
            valText.fontSize = 16;
            valText.alignment = TextAlignmentOptions.MidlineRight;
            valText.color = new Color(0f, 1f, 0.85f);
            valText.text = "100%";

            return slider;
        }

        private static TMP_Dropdown CreateLabeledDropdown(Transform parent, string name, string label)
        {
            var rowGo = CreateUIElement(name + "_Row", parent, Vector2.zero, Vector2.one, new Vector2(0, 48), Vector2.zero);
            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var labelGo = CreateUIElement("Label", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(200, 0), Vector2.zero);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = new Color(0.85f, 0.9f, 0.95f);

            var dropGo = CreateUIElement("Dropdown", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(280, 0), Vector2.zero);
            var dropImg = dropGo.AddComponent<Image>();
            dropImg.color = new Color(0.12f, 0.16f, 0.22f);

            var dropdown = dropGo.AddComponent<TMP_Dropdown>();

            var captionGo = CreateUIElement("CaptionText", dropGo.transform, Vector2.zero, Vector2.one, new Vector2(-20, 0), Vector2.zero);
            var captionTmp = captionGo.AddComponent<TextMeshProUGUI>();
            captionTmp.fontSize = 15;
            captionTmp.alignment = TextAlignmentOptions.MidlineLeft;
            dropdown.captionText = captionTmp;

            return dropdown;
        }

        private static Toggle CreateLabeledToggle(Transform parent, string name, string label)
        {
            var rowGo = CreateUIElement(name + "_Row", parent, Vector2.zero, Vector2.one, new Vector2(0, 45), Vector2.zero);
            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 15;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var toggleGo = CreateUIElement("ToggleBox", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(30, 0), Vector2.zero);
            var bgImg = toggleGo.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.16f, 0.22f);

            var toggle = toggleGo.AddComponent<Toggle>();

            var checkGo = CreateUIElement("Checkmark", toggleGo.transform, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.8f), Vector2.zero, Vector2.zero);
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0f, 1f, 0.85f);
            toggle.graphic = checkImg;

            var labelGo = CreateUIElement("Label", rowGo.transform, Vector2.zero, Vector2.one, new Vector2(350, 0), Vector2.zero);
            var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 16;
            labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
            labelTmp.color = new Color(0.85f, 0.9f, 0.95f);

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
