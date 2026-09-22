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
    public static class PauseMenuBuilder
    {
        private const string PrefabsDirectory = "Assets/_Project/Prefabs/UI";
        private const string PauseMenuPrefabPath = PrefabsDirectory + "/Canvas_PauseMenu.prefab";
        private const string SettingsPrefabPath = PrefabsDirectory + "/Dialog_Settings.prefab";

        [MenuItem("TPOB/UI/5. Generar Prefab de Menú de Pausa")]
        public static void GeneratePauseMenuPrefab()
        {
            EnsureDirectory(PrefabsDirectory);

            var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            if (settingsPrefab == null)
            {
                MainMenuBuilder.GenerateUIPrefabs();
                settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingsPrefabPath);
            }

            GameObject pauseMenuGo = BuildPauseMenuHierarchy(settingsPrefab);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pauseMenuGo, PauseMenuPrefabPath);
            Object.DestroyImmediate(pauseMenuGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[TPOB] Prefab Canvas_PauseMenu generado con éxito en " + PauseMenuPrefabPath + "</color>");
        }

        [MenuItem("TPOB/UI/6. Instanciar Menú de Pausa en Escena Activa")]
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

        [MenuItem("TPOB/UI/7. Generar Todo el Sistema de UI (Todos los Prefabs)")]
        public static void GenerateAllUIPrefabs()
        {
            MainMenuBuilder.GenerateUIPrefabs();
            LobbyBuilder.GenerateLobbyPrefab();
            GeneratePauseMenuPrefab();

            Debug.Log("<color=green><b>[TPOB] Sistema Completo de UI Generado (Menú Principal, Opciones, Lobby y Pausa).</b></color>");
        }

        private static GameObject BuildPauseMenuHierarchy(GameObject settingsPrefab)
        {
            var canvasGo = CreateCanvasRoot("Canvas_PauseMenu");
            var pauseView = canvasGo.AddComponent<PauseMenuView>();
            canvasGo.AddComponent<PauseMenuPresenter>();

            var canvasGroup = canvasGo.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // 1. Pause Root (Overlay Backdrop)
            var pauseRoot = CreateUIElement("PauseRoot", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = pauseRoot.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.06f, 0.08f, 0.88f);

            // 2. Central Modal Panel
            var modalGo = CreateUIElement("Modal_Pause", pauseRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 500), Vector2.zero);
            var modalImg = modalGo.AddComponent<Image>();
            modalImg.color = new Color(0.08f, 0.11f, 0.16f, 0.96f);

            var modalOutline = modalGo.AddComponent<Outline>();
            modalOutline.effectColor = new Color(0f, 1f, 0.85f, 0.5f);
            modalOutline.effectDistance = new Vector2(2, -2);

            // 3. Header Title & Subtitle
            var headerGo = CreateUIElement("Header", modalGo.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, 100), new Vector2(0, -50));
            var titleTmp = headerGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "<color=#00FFFF><b>PAUSA DEL SISTEMA</b></color>\n<size=15><color=#88A0B0>ROBOT STATUS: STANDBY</color></size>";
            titleTmp.fontSize = 26;
            titleTmp.alignment = TextAlignmentOptions.Center;

            // 4. Buttons Container
            var buttonsGo = CreateUIElement("ButtonsContainer", modalGo.transform, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f), new Vector2(400, 320), Vector2.zero);
            var vlayout = buttonsGo.AddComponent<VerticalLayoutGroup>();
            vlayout.spacing = 16;
            vlayout.childControlWidth = true;
            vlayout.childControlHeight = false;
            vlayout.childForceExpandWidth = true;
            vlayout.childForceExpandHeight = false;

            // Buttons: Resume, Options, ExitToMenu, ExitToDesktop
            var resumeBtn = CreateStyledButton(buttonsGo.transform, "Btn_Resume", "▶  REANUDAR MISIÓN", new Color(0.0f, 0.7f, 0.7f, 0.9f), new Color(0.05f, 0.1f, 0.12f));
            var optionsBtn = CreateStyledButton(buttonsGo.transform, "Btn_Options", "⚙  CONFIGURACIÓN", new Color(0.18f, 0.24f, 0.32f), Color.white);
            var exitMenuBtn = CreateStyledButton(buttonsGo.transform, "Btn_ExitToMenu", "🚪  ABANDONAR AL MENÚ", new Color(0.35f, 0.22f, 0.05f), new Color(1f, 0.8f, 0.2f));
            var exitDesktopBtn = CreateStyledButton(buttonsGo.transform, "Btn_ExitToDesktop", "✕  SALIR AL ESCRITORIO", new Color(0.32f, 0.1f, 0.1f), new Color(1f, 0.4f, 0.4f));

            // 5. Settings Modal Dialog (Instantiated inside Canvas)
            GameObject settingsInstance = null;
            if (settingsPrefab != null)
            {
                settingsInstance = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, canvasGo.transform);
                settingsInstance.name = "Dialog_Settings";
                settingsInstance.SetActive(false);
            }

            // Wire Serialized Properties on PauseMenuView
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

            // Set root inactive initially
            pauseRoot.SetActive(false);

            return canvasGo;
        }

        private static GameObject CreateCanvasRoot(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // Overlays above gameplay and HUD

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return go;
        }

        private static Button CreateStyledButton(Transform parent, string name, string text, Color bgColor, Color textColor)
        {
            var btnGo = CreateUIElement(name, parent, Vector2.zero, Vector2.one, new Vector2(0, 54), Vector2.zero);
            var le = btnGo.AddComponent<LayoutElement>();
            le.preferredHeight = 54;
            le.flexibleWidth = 1;

            var img = btnGo.AddComponent<Image>();
            img.color = bgColor;

            var outline = btnGo.AddComponent<Outline>();
            outline.effectColor = new Color(bgColor.r * 1.4f, bgColor.g * 1.4f, bgColor.b * 1.4f, 0.7f);
            outline.effectDistance = new Vector2(1, -1);

            var btn = btnGo.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            var textGo = CreateUIElement("Text", btnGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = textColor;

            return btn;
        }

        private static void EnsureEventSystem()
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

            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
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
