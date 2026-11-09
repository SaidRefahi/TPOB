using System.IO;
using Game.Gameplay.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    public static class LobbyBuilder
    {
        private const string PrefabsDirectory = "Assets/_Project/Prefabs/UI";
        private const string LobbyPrefabPath = PrefabsDirectory + "/Canvas_Lobby.prefab";

        private static readonly Color CyanAccent = new Color(0f, 0.94f, 1f, 1f);
        private static readonly Color AmberAccent = new Color(1f, 0.58f, 0f, 1f);
        private static readonly Color GreenAccent = new Color(0f, 0.95f, 0.58f, 1f);
        private static readonly Color RedAccent = new Color(1f, 0.24f, 0.35f, 1f);

        [MenuItem("TPOB/UI/3. Generar Prefab de Lobby")]
        public static void GenerateLobbyPrefab()
        {
            EnsureDirectory(PrefabsDirectory);
            UIAssetGenerator.EnsureUIAssets();

            GameObject lobbyGo = BuildLobbyHierarchy();
            PrefabUtility.SaveAsPrefabAsset(lobbyGo, LobbyPrefabPath);
            Object.DestroyImmediate(lobbyGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[TPOB] Prefab Sci-Fi Canvas_Lobby generado con éxito en " + LobbyPrefabPath + "</color>");
        }

        [MenuItem("TPOB/UI/4. Instanciar UI Completa en Escena (Boot)")]
        public static void InstantiateFullUIInScene()
        {
            MainMenuBuilder.InstantiateUIInScene();

            var uiRoot = GameObject.Find("--- UI ---");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("--- UI ---");
                Undo.RegisterCreatedObjectUndo(uiRoot, "Create --- UI ---");
            }

            var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
            if (lobbyPrefab == null)
            {
                GenerateLobbyPrefab();
                lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPrefabPath);
            }

            if (Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include) == null && lobbyPrefab != null)
            {
                var lobbyInstance = (GameObject)PrefabUtility.InstantiatePrefab(lobbyPrefab, uiRoot.transform);
                lobbyInstance.SetActive(true);
                var canvas = lobbyInstance.GetComponent<Canvas>();
                if (canvas != null) canvas.enabled = false;
                var raycaster = lobbyInstance.GetComponent<GraphicRaycaster>();
                if (raycaster != null) raycaster.enabled = false;
                Undo.RegisterCreatedObjectUndo(lobbyInstance, "Instantiate Canvas_Lobby");
            }

            Debug.Log("<color=cyan>[TPOB] Jerarquía completa de UI instanciada (MainMenu, Settings, Lobby) bajo '--- UI ---'.</color>");
        }

        private static GameObject BuildLobbyHierarchy()
        {
            var canvasGo = CreateCanvasRoot("Canvas_Lobby");
            var lobbyView = canvasGo.AddComponent<LobbyView>();
            canvasGo.AddComponent<LobbyPresenter>();

            Material bgMat = UIAssetGenerator.LoadBackdropMaterial(true);
            Sprite panelSprite = UIAssetGenerator.LoadSprite(UIAssetGenerator.PanelFrame);

            // 1. Pure Procedural CyberGridBackdrop Shader (lateral Cyan to Amber gradient, no photo)
            var bgGo = CreateUIElement("Background", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = Color.white;
            if (bgMat != null)
            {
                bgImg.material = bgMat;
            }

            // 2. Top Header Banner (Background/Header)
            var headerFrameGo = CreateUIElement("HeaderBanner", bgGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1460, 88), new Vector2(0, -52));
            var headerFrameImg = headerFrameGo.AddComponent<Image>();
            if (panelSprite != null)
            {
                headerFrameImg.sprite = panelSprite;
                headerFrameImg.type = Image.Type.Sliced;
            }
            headerFrameImg.color = new Color(0.04f, 0.08f, 0.13f, 0.92f);

            var headerGo = CreateUIElement("Header", bgGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1380, 84), new Vector2(0, -52));
            var headerTmp = headerGo.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "<b><size=32><color=#00F0FF>HANGAR DE SINCRONIZACIÓN NEURAL</color></size></b>\n<size=17><color=#FFFFFF>CÓDIGO DE SALA: </color><color=#00F0FF><b>---</b></color></size>";
            headerTmp.alignment = TextAlignmentOptions.Center;
            headerTmp.raycastTarget = false;

            // 3. Two Pilot Cards Row
            var cardsRowGo = CreateUIElement("CardsRow", bgGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1460, 680), new Vector2(0, 12));
            var hlg = cardsRowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 36;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // LEGS CARD (Cyan Theme)
            BuildRoleCard(
                cardsRowGo.transform,
                "Card_Legs",
                "PILOTO 01 // PIERNAS (LEGS)",
                "CHASIS MOTRIZ E IMPULSO MAGNÉTICO",
                CyanAccent,
                UIAssetGenerator.CardCyan,
                UIAssetGenerator.BtnCyan,
                "<b>Misión Operativa:</b> Locomoción ágil, saltos de precisión, adherencia magnética en muros/techos y demolición.",
                "• <b>Salto / Doble Salto</b>: Supera desniveles y brechas verticales.\n" +
                "• <b>Botas Magnéticas</b>: Camina por paredes y techos ferromagnéticos.\n" +
                "• <b>Patada Cinética</b>: Empuja bloques pesados y activa interruptores.\n" +
                "• <b>Base de Fusión</b>: Transporta al Torso acoplado a máxima velocidad.",
                "• <b>[WASD] / [Stick Izq]</b>: Desplazamiento omnidireccional\n" +
                "• <b>[Espacio] / [Botón A]</b>: Salto / Propulsión doble\n" +
                "• <b>[Shift] / [Stick Izq]</b>: Sprint de alta velocidad\n" +
                "• <b>[Click Izq] / [Botón X]</b>: Patada cinética de impacto\n" +
                "• <b>[R] / [Botón Y]</b>: Acople / Desacople con Torso",
                out var legsBtn,
                out var legsStatus,
                out var legsCanvasGroup,
                out var legsAbilitiesText,
                out var legsControlsText
            );

            // TORSO CARD (Amber Theme)
            BuildRoleCard(
                cardsRowGo.transform,
                "Card_Torso",
                "PILOTO 02 // TORSO (TORSO)",
                "TORRETA BALÍSTICA Y MANIPULACIÓN ELECTROMAGNÉTICA",
                AmberAccent,
                UIAssetGenerator.CardAmber,
                UIAssetGenerator.BtnAmber,
                "<b>Misión Operativa:</b> Puntería 360°, tracción electromagnética a distancia, lanzamiento balístico e infiltración.",
                "• <b>Torreta 360°</b>: Giro independiente del chasis inferior.\n" +
                "• <b>Agarre y Lanzamiento</b>: Manipulación balística de cajas y baterías.\n" +
                "• <b>Rayo Magnético</b>: Haz tractor continuo para objetos lejanos.\n" +
                "• <b>Modo Oruga</b>: Infiltración por ductos estrechos al desacoplarse.",
                "• <b>[Mouse] / [Stick Der]</b>: Apuntar retícula en 360°\n" +
                "• <b>[E] / [Botón Y]</b>: Agarrar / Soltar / Interactuar\n" +
                "• <b>[Click Izq] / [Botón X]</b>: Lanzar con impulso balístico\n" +
                "• <b>[C] / [Botón B]</b>: Activar Rayo Magnético tractor\n" +
                "• <b>[R] / [Botón Y]</b>: Acople / Desacople con Piernas",
                out var torsoBtn,
                out var torsoStatus,
                out var torsoCanvasGroup,
                out var torsoAbilitiesText,
                out var torsoControlsText
            );

            // 4. Bottom Command Bar
            var bottomBarGo = CreateUIElement("BottomBar", bgGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1460, 118), new Vector2(0, 56));
            var bottomBarImg = bottomBarGo.AddComponent<Image>();
            if (panelSprite != null)
            {
                bottomBarImg.sprite = panelSprite;
                bottomBarImg.type = Image.Type.Sliced;
            }
            bottomBarImg.color = new Color(0.04f, 0.07f, 0.12f, 0.94f);

            var statusInfoGo = CreateUIElement("StatusInfo", bottomBarGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1200, 34), new Vector2(0, -14));
            var statusTmp = statusInfoGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "Selecciona tu módulo de piloto para iniciar la sincronización...";
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.fontSize = 16;
            statusTmp.color = CyanAccent;
            statusTmp.raycastTarget = false;

            var btnRowGo = CreateUIElement("ButtonsRow", bottomBarGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1280, 52), new Vector2(0, 16));
            var btnHlg = btnRowGo.AddComponent<HorizontalLayoutGroup>();
            btnHlg.spacing = 24;
            btnHlg.childControlWidth = true;
            btnHlg.childControlHeight = true;
            btnHlg.childForceExpandWidth = true;
            btnHlg.childForceExpandHeight = true;

            var leaveBtn = MainMenuBuilder.CreateSciFiButton(btnRowGo.transform, "Btn_LeaveLobby", "LEAVE LOBBY", 50, UIAssetGenerator.BtnRed, RedAccent);
            var readyBtn = MainMenuBuilder.CreateSciFiButton(btnRowGo.transform, "Btn_ToggleReady", "READY", 50, UIAssetGenerator.BtnAmber, AmberAccent);
            var readyTmp = readyBtn.GetComponentInChildren<TextMeshProUGUI>();
            var startBtn = MainMenuBuilder.CreateSciFiButton(btnRowGo.transform, "Btn_StartGame", "START GAME", 50, UIAssetGenerator.BtnGreen, GreenAccent);

            // Wire LobbyView SerializedObject
            var so = new SerializedObject(lobbyView);
            so.FindProperty("_selectLegsButton").objectReferenceValue = legsBtn;
            so.FindProperty("_legsStatusText").objectReferenceValue = legsStatus;
            so.FindProperty("_legsCanvasGroup").objectReferenceValue = legsCanvasGroup;
            so.FindProperty("_legsAbilitiesText").objectReferenceValue = legsAbilitiesText;
            so.FindProperty("_legsControlsText").objectReferenceValue = legsControlsText;

            so.FindProperty("_selectTorsoButton").objectReferenceValue = torsoBtn;
            so.FindProperty("_torsoStatusText").objectReferenceValue = torsoStatus;
            so.FindProperty("_torsoCanvasGroup").objectReferenceValue = torsoCanvasGroup;
            so.FindProperty("_torsoAbilitiesText").objectReferenceValue = torsoAbilitiesText;
            so.FindProperty("_torsoControlsText").objectReferenceValue = torsoControlsText;

            so.FindProperty("_readyToggleButton").objectReferenceValue = readyBtn;
            so.FindProperty("_readyButtonText").objectReferenceValue = readyTmp;
            so.FindProperty("_startGameButton").objectReferenceValue = startBtn;
            so.FindProperty("_matchStatusText").objectReferenceValue = statusTmp;
            so.FindProperty("_leaveLobbyButton").objectReferenceValue = leaveBtn;
            so.FindProperty("_roomCodeText").objectReferenceValue = headerTmp;
            so.ApplyModifiedPropertiesWithoutUndo();

            return canvasGo;
        }

        private static GameObject BuildRoleCard(
            Transform parent,
            string cardName,
            string title,
            string subtitle,
            Color accentColor,
            string cardSpriteFile,
            string btnSpriteFile,
            string loreText,
            string abilitiesText,
            string controlsText,
            out Button selectBtn,
            out TextMeshProUGUI statusText,
            out CanvasGroup canvasGroup,
            out TextMeshProUGUI abilitiesTmp,
            out TextMeshProUGUI controlsTmp)
        {
            var cardGo = CreateUIElement(cardName, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            canvasGroup = cardGo.AddComponent<CanvasGroup>();

            var bgImg = cardGo.AddComponent<Image>();
            Sprite cardSpr = UIAssetGenerator.LoadSprite(cardSpriteFile);
            if (cardSpr != null)
            {
                bgImg.sprite = cardSpr;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.color = new Color(0.05f, 0.09f, 0.14f, 0.94f);
            }

            var vlg = cardGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 26, 24);
            vlg.spacing = 10;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title & Technical Subtitle
            var titleGo = CreateUIElement("CardTitle", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 48), Vector2.zero);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = $"<b><size=23>{title}</size></b>\n<size=12><color=#99B4CC>{subtitle}</color></size>";
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = accentColor;
            titleTmp.raycastTarget = false;

            // Status Badge Box
            var statusBoxGo = CreateUIElement("CardStatusBox", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 34), Vector2.zero);
            var statusBoxImg = statusBoxGo.AddComponent<Image>();
            statusBoxImg.color = new Color(0.02f, 0.04f, 0.07f, 0.85f);

            var statusGo = CreateUIElement("CardStatus", statusBoxGo.transform, Vector2.zero, Vector2.one, new Vector2(-16, 0), Vector2.zero);
            statusText = statusGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "<color=#8899AA>[ DISPONIBLE — CLIC PARA ELEGIR ]</color>";
            statusText.fontSize = 14.5f;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.raycastTarget = false;

            // Select Button with UIButtonPolish
            selectBtn = MainMenuBuilder.CreateSciFiButton(cardGo.transform, "Btn_Select", $"CHOOSE {title}", 50, btnSpriteFile, accentColor);

            // Lore / Mission Summary
            var loreGo = CreateUIElement("Lore", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 46), Vector2.zero);
            var loreTmp = loreGo.AddComponent<TextMeshProUGUI>();
            loreTmp.text = loreText;
            loreTmp.fontSize = 14;
            loreTmp.color = new Color(0.92f, 0.96f, 1f);
            loreTmp.raycastTarget = false;

            // Abilities Box
            var abHeaderGo = CreateUIElement("AbilitiesHeader", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 24), Vector2.zero);
            var abHeaderTmp = abHeaderGo.AddComponent<TextMeshProUGUI>();
            abHeaderTmp.text = "<b><size=15>■ MÓDULOS DE HABILIDAD:</size></b>";
            abHeaderTmp.color = accentColor;
            abHeaderTmp.raycastTarget = false;

            var abBoxGo = CreateUIElement("AbilitiesBox", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 124), Vector2.zero);
            var abBoxImg = abBoxGo.AddComponent<Image>();
            abBoxImg.color = new Color(0.02f, 0.04f, 0.07f, 0.68f);

            var abContentGo = CreateUIElement("AbilitiesContent", abBoxGo.transform, Vector2.zero, Vector2.one, new Vector2(-24, -14), Vector2.zero);
            abilitiesTmp = abContentGo.AddComponent<TextMeshProUGUI>();
            abilitiesTmp.text = abilitiesText;
            abilitiesTmp.fontSize = 13.5f;
            abilitiesTmp.color = new Color(0.88f, 0.93f, 0.98f);
            abilitiesTmp.raycastTarget = false;

            // Controls Box
            var ctrlHeaderGo = CreateUIElement("ControlsHeader", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 24), Vector2.zero);
            var ctrlHeaderTmp = ctrlHeaderGo.AddComponent<TextMeshProUGUI>();
            ctrlHeaderTmp.text = "<b><size=15>■ ESQUEMA DE CONTROLES:</size></b>";
            ctrlHeaderTmp.color = accentColor;
            ctrlHeaderTmp.raycastTarget = false;

            var ctrlBoxGo = CreateUIElement("ControlsBox", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 134), Vector2.zero);
            var ctrlBoxImg = ctrlBoxGo.AddComponent<Image>();
            ctrlBoxImg.color = new Color(0.02f, 0.04f, 0.07f, 0.68f);

            var ctrlContentGo = CreateUIElement("ControlsContent", ctrlBoxGo.transform, Vector2.zero, Vector2.one, new Vector2(-24, -14), Vector2.zero);
            controlsTmp = ctrlContentGo.AddComponent<TextMeshProUGUI>();
            controlsTmp.text = controlsText;
            controlsTmp.fontSize = 13.5f;
            controlsTmp.color = new Color(0.88f, 0.93f, 0.98f);
            controlsTmp.raycastTarget = false;

            return cardGo;
        }

        private static GameObject CreateCanvasRoot(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

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
