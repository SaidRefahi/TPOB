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

        [MenuItem("TPOB/UI/3. Generar Prefab de Lobby")]
        public static void GenerateLobbyPrefab()
        {
            EnsureDirectory(PrefabsDirectory);

            GameObject lobbyGo = BuildLobbyHierarchy();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(lobbyGo, LobbyPrefabPath);
            Object.DestroyImmediate(lobbyGo);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[TPOB] Prefab Canvas_Lobby generado con éxito en " + LobbyPrefabPath + "</color>");
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

            // Dark background panel
            var bgGo = CreateUIElement("Background", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.05f, 0.07f, 0.1f, 0.97f);

            // Header Title
            var headerGo = CreateUIElement("Header", bgGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000, 90), new Vector2(0, -55));
            var headerTmp = headerGo.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "<b><size=34><color=#00FFFF>SALA DE PREPARACIÓN</color></size></b>\n<size=17><color=#8899AA>SELECCIÓN DE ROL Y SINCRONIZACIÓN DE PILOTOS</color></size>";
            headerTmp.alignment = TextAlignmentOptions.Center;

            // Two Cards Row
            var cardsRowGo = CreateUIElement("CardsRow", bgGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1400, 680), new Vector2(0, 15));
            var hlg = cardsRowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // LEGS CARD (Cyan Theme)
            var legsCard = BuildRoleCard(
                cardsRowGo.transform,
                "Card_Legs",
                "PIERNAS (LEGS)",
                new Color(0f, 1f, 1f),
                new Color(0.06f, 0.12f, 0.15f),
                "<b>Misión:</b> Locomoción ágil, saltos de altura, sprint continuo y demolición física.",
                "- <b>Salto / Doble Salto</b>: Supera desniveles y plataformas altas.\n" +
                "- <b>Sprint Rápido</b>: Cruza compuertas temporizadas a alta velocidad.\n" +
                "- <b>Patada Física</b>: Empuja bloques, cajas pesadas y activa botones.\n" +
                "- <b>Fusión / Base</b>: Chasis motriz que transporta al Torso acoplado.",
                "- <b>[WASD] / [Stick Izq]</b>: Moverse y posicionarse\n" +
                "- <b>[Espacio] / [Botón A]</b>: Saltar / Doble salto en el aire\n" +
                "- <b>[Shift] / [Gatillo Izq]</b>: Sprint / Carrera rápida\n" +
                "- <b>[F] / [Botón X]</b>: Patada física de impacto\n" +
                "- <b>[R] / [Botón Y]</b>: Acople / Desacople con Torso",
                out var legsBtn,
                out var legsStatus,
                out var legsCanvasGroup,
                out var legsAbilitiesText,
                out var legsControlsText
            );

            // TORSO CARD (Amber Theme)
            var torsoCard = BuildRoleCard(
                cardsRowGo.transform,
                "Card_Torso",
                "TORSO (TORSO)",
                new Color(1f, 0.6f, 0f),
                new Color(0.15f, 0.1f, 0.05f),
                "<b>Misión:</b> Puntería 360°, manipulación electromagnética y resolución balística.",
                "- <b>Torreta Libre</b>: Giro omnidireccional 360° independiente del chasis.\n" +
                "- <b>Agarre y Lanzamiento</b>: Manipulación balística precisa de objetos y llaves.\n" +
                "- <b>Rayo Magnético</b>: Haz tractor continuo para atraer objetos distantes.\n" +
                "- <b>Modo Oruga</b>: Rueda por ductos estrechos cuando está desacoplado.",
                "- <b>[Mouse] / [Stick Der]</b>: Apuntar retícula en 360°\n" +
                "- <b>[Click Izq / E] / [Gatillo Der]</b>: Agarrar / Soltar / Interactuar\n" +
                "- <b>[Click Der / Q] / [Botón RB]</b>: Lanzar con impulso cargado\n" +
                "- <b>[Shift / C] / [Gatillo Izq]</b>: Activar Rayo Magnético tractor\n" +
                "- <b>[R] / [Botón Y]</b>: Acople / Desacople con Piernas",
                out var torsoBtn,
                out var torsoStatus,
                out var torsoCanvasGroup,
                out var torsoAbilitiesText,
                out var torsoControlsText
            );

            // Bottom Bar Container
            var bottomBarGo = CreateUIElement("BottomBar", bgGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1400, 110), new Vector2(0, 45));

            // Status info text
            var statusInfoGo = CreateUIElement("StatusInfo", bottomBarGo.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1000, 35), new Vector2(0, -10));
            var statusTmp = statusInfoGo.AddComponent<TextMeshProUGUI>();
            statusTmp.text = "Selecciona un rol para comenzar la sincronización...";
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.fontSize = 17;
            statusTmp.color = new Color(0.3f, 0.85f, 1f);

            // Buttons row
            var btnRowGo = CreateUIElement("ButtonsRow", bottomBarGo.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(1000, 50), new Vector2(0, 15));
            var btnHlg = btnRowGo.AddComponent<HorizontalLayoutGroup>();
            btnHlg.spacing = 20;
            btnHlg.childControlWidth = true;
            btnHlg.childControlHeight = true;
            btnHlg.childForceExpandWidth = true;
            btnHlg.childForceExpandHeight = true;

            var leaveBtn = CreateButton(btnRowGo.transform, "Btn_LeaveLobby", "SALIR AL MENÚ", 48, new Color(0.25f, 0.12f, 0.12f));
            var readyBtn = CreateButton(btnRowGo.transform, "Btn_ToggleReady", "CONFIRMAR (LISTO)", 48, new Color(0.12f, 0.35f, 0.22f));
            var readyTmp = readyBtn.GetComponentInChildren<TextMeshProUGUI>();
            var startBtn = CreateButton(btnRowGo.transform, "Btn_StartGame", "INICIAR OPERACIÓN", 48, new Color(0.12f, 0.45f, 0.35f));

            // Wire SerializedObject
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
            so.ApplyModifiedPropertiesWithoutUndo();

            return canvasGo;
        }

        private static GameObject BuildRoleCard(
            Transform parent,
            string cardName,
            string title,
            Color accentColor,
            Color bgColor,
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
            bgImg.color = bgColor;

            var vlg = cardGo.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(25, 25, 25, 25);
            vlg.spacing = 12;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Title
            var titleGo = CreateUIElement("CardTitle", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 40), Vector2.zero);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = $"<b>{title}</b>";
            titleTmp.fontSize = 22;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = accentColor;

            // Status text
            var statusGo = CreateUIElement("CardStatus", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 30), Vector2.zero);
            statusText = statusGo.AddComponent<TextMeshProUGUI>();
            statusText.text = "<color=#888888>[ DISPONIBLE — CLIC PARA ELEGIR ]</color>";
            statusText.fontSize = 15;
            statusText.alignment = TextAlignmentOptions.Center;

            // Select Button
            selectBtn = CreateButton(cardGo.transform, "Btn_Select", $"ELEGIR {title}", 45, accentColor * 0.4f);

            // Lore / Mission
            var loreGo = CreateUIElement("Lore", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 45), Vector2.zero);
            var loreTmp = loreGo.AddComponent<TextMeshProUGUI>();
            loreTmp.text = loreText;
            loreTmp.fontSize = 14;
            loreTmp.color = new Color(0.9f, 0.95f, 1f);

            // Abilities Box
            var abHeaderGo = CreateUIElement("AbilitiesHeader", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 24), Vector2.zero);
            var abHeaderTmp = abHeaderGo.AddComponent<TextMeshProUGUI>();
            abHeaderTmp.text = "<b><size=15>HABILIDADES PRINCIPALES:</size></b>";
            abHeaderTmp.color = accentColor;

            var abContentGo = CreateUIElement("AbilitiesContent", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 120), Vector2.zero);
            abilitiesTmp = abContentGo.AddComponent<TextMeshProUGUI>();
            abilitiesTmp.text = abilitiesText;
            abilitiesTmp.fontSize = 13;
            abilitiesTmp.color = new Color(0.85f, 0.9f, 0.95f);

            // Controls Box
            var ctrlHeaderGo = CreateUIElement("ControlsHeader", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 24), Vector2.zero);
            var ctrlHeaderTmp = ctrlHeaderGo.AddComponent<TextMeshProUGUI>();
            ctrlHeaderTmp.text = "<b><size=15>CONTROLES ASIGNADOS:</size></b>";
            ctrlHeaderTmp.color = accentColor;

            var ctrlContentGo = CreateUIElement("ControlsContent", cardGo.transform, Vector2.zero, Vector2.zero, new Vector2(0, 130), Vector2.zero);
            controlsTmp = ctrlContentGo.AddComponent<TextMeshProUGUI>();
            controlsTmp.text = controlsText;
            controlsTmp.fontSize = 13;
            controlsTmp.color = new Color(0.85f, 0.9f, 0.95f);

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

        private static Button CreateButton(Transform parent, string name, string label, float height, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(250, height);

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
            colors.highlightedColor = bgColor * 1.35f;
            colors.pressedColor = bgColor * 0.75f;
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
            tmp.fontSize = 17;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            return btn;
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
