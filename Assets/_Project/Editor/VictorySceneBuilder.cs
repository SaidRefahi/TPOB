using System.IO;
using System.Linq;
using Game.Gameplay.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class VictorySceneBuilder
    {
        private const string VictoryScenePath = "Assets/_Project/Scenes/Victory.unity";

        private static readonly Color CyanAccent = new Color(0f, 0.94f, 1f, 1f);
        private static readonly Color GreenAccent = new Color(0f, 0.95f, 0.58f, 1f);
        private static readonly Color DarkBg = new Color(0.02f, 0.05f, 0.09f, 1f);

        [MenuItem("TPOB/Tools/Build Victory Scene", priority = 105)]
        public static void BuildVictoryScene()
        {
            UIAssetGenerator.EnsureUIAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Camera & Lighting
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = DarkBg;
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0, 1, -10);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.92f, 1f);
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. UI Canvas (EventSystem is provided persistently by Boot.unity)
            var canvasGo = new GameObject("Canvas_Victory");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            Sprite panelSprite = UIAssetGenerator.LoadSprite(UIAssetGenerator.PanelFrame);

            // Backdrop
            var bgGo = CreateUIElement("Backdrop", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.01f, 0.02f, 0.04f, 0.92f);

            // Frame (840 x 520)
            var frameGo = CreateUIElement("Victory_Frame", bgGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(840, 520), Vector2.zero);
            var frameImg = frameGo.AddComponent<Image>();
            if (panelSprite != null)
            {
                frameImg.sprite = panelSprite;
                frameImg.type = Image.Type.Sliced;
                frameImg.color = new Color(1f, 1f, 1f, 0.98f);
            }
            else
            {
                frameImg.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);
            }

            // Title
            var titleGo = CreateUIElement("Title", frameGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(760, 70), new Vector2(0, -50));
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "<b><size=42><color=#00FFFF>¡MISIÓN CUMPLIDA!</color></size></b>";
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;

            // Subtitle
            var subGo = CreateUIElement("Subtitle", frameGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(760, 50), new Vector2(0, -115));
            var subTmp = subGo.AddComponent<TextMeshProUGUI>();
            subTmp.text = "<size=20><color=#99D4FF>LOS PILOTOS HAN ESCAPADO CON ÉXITO DEL SECTOR</color></size>";
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.raycastTarget = false;

            // Badge / Status Box
            var badgeGo = CreateUIElement("StatusBadge", frameGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(680, 80), new Vector2(0, 20));
            var badgeImg = badgeGo.AddComponent<Image>();
            badgeImg.color = new Color(0.02f, 0.05f, 0.09f, 0.9f);
            var badgeTextGo = CreateUIElement("BadgeText", badgeGo.transform, Vector2.zero, Vector2.one, new Vector2(-20, 0), Vector2.zero);
            var badgeTmp = badgeTextGo.AddComponent<TextMeshProUGUI>();
            badgeTmp.text = "<size=15><color=#00FF99>• PROTOCOLO DE EXTRACCIÓN: <b>COMPLETADO</b>\n• SINCRONIZACIÓN DE CHASIS: <b>100% ÓPTIMA</b></color></size>";
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.raycastTarget = false;

            // Buttons Row
            var btnRowGo = CreateUIElement("ButtonsRow", frameGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(700, 56), new Vector2(0, 50));
            var hlg = btnRowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            var retryBtn = MainMenuBuilder.CreateSciFiButton(btnRowGo.transform, "Btn_Retry", "REINTENTAR SALA", 54, UIAssetGenerator.BtnGreen, GreenAccent);
            var menuBtn = MainMenuBuilder.CreateSciFiButton(btnRowGo.transform, "Btn_MainMenu", "MENÚ PRINCIPAL", 54, UIAssetGenerator.BtnCyan, CyanAccent);

            // Presenter
            var presenter = canvasGo.AddComponent<VictoryPresenter>();
            var so = new SerializedObject(presenter);
            so.FindProperty("_retryButton").objectReferenceValue = retryBtn;
            so.FindProperty("_mainMenuButton").objectReferenceValue = menuBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Save Scene
            string dir = Path.GetDirectoryName(VictoryScenePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            EditorSceneManager.SaveScene(scene, VictoryScenePath);
            AssetDatabase.Refresh();

            EnsureSceneInBuildSettings(VictoryScenePath);

            Debug.Log("<color=green>[TPOB] Escena de Victoria generada con éxito en " + VictoryScenePath + "</color>");
        }

        public static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("<color=cyan>[TPOB] Escena agregada a EditorBuildSettings: " + scenePath + "</color>");
            }
        }

        private static GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            return go;
        }
    }
}
