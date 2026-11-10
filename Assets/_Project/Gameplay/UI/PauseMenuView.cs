using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Contenedor Raíz de Pausa")]
        [SerializeField] private GameObject _pauseRoot;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Botones Principales")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _exitToMenuButton;
        [SerializeField] private Button _exitToDesktopButton;

        [Header("Estado de Conexión")]
        [SerializeField] private TextMeshProUGUI _connectionQualityText;

        [Header("Diálogo de Configuración")]
        [SerializeField] private GameObject _settingsDialog;

        private SettingsView _cachedSettingsView;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;

        public Button ResumeButton
        {
            get
            {
                if (_resumeButton == null)
                {
                    var t = transform.Find("PauseRoot/Modal_Pause/ButtonsContainer/Btn_Resume");
                    if (t != null) _resumeButton = t.GetComponent<Button>();
                }
                return _resumeButton;
            }
        }

        public Button OptionsButton
        {
            get
            {
                if (_optionsButton == null)
                {
                    var t = transform.Find("PauseRoot/Modal_Pause/ButtonsContainer/Btn_Options");
                    if (t != null) _optionsButton = t.GetComponent<Button>();
                }
                return _optionsButton;
            }
        }

        public Button ExitToMenuButton
        {
            get
            {
                if (_exitToMenuButton == null)
                {
                    var t = transform.Find("PauseRoot/Modal_Pause/ButtonsContainer/Btn_ExitToMenu");
                    if (t != null) _exitToMenuButton = t.GetComponent<Button>();
                }
                return _exitToMenuButton;
            }
        }

        public Button ExitToDesktopButton
        {
            get
            {
                if (_exitToDesktopButton == null)
                {
                    var t = transform.Find("PauseRoot/Modal_Pause/ButtonsContainer/Btn_ExitToDesktop");
                    if (t != null) _exitToDesktopButton = t.GetComponent<Button>();
                }
                return _exitToDesktopButton;
            }
        }

        public TextMeshProUGUI ConnectionQualityText
        {
            get
            {
                if (_connectionQualityText == null)
                {
                    var t = transform.Find("PauseRoot/Modal_Pause/Text_ConnectionQuality");
                    if (t != null) _connectionQualityText = t.GetComponent<TextMeshProUGUI>();
                }
                return _connectionQualityText;
            }
        }

        public GameObject SettingsDialog
        {
            get
            {
                if (_settingsDialog == null)
                {
                    var t = transform.Find("Dialog_Settings");
                    if (t != null) _settingsDialog = t.gameObject;
                }
                return _settingsDialog;
            }
        }

        public GameObject PauseRoot
        {
            get
            {
                if (_pauseRoot == null)
                {
                    var t = transform.Find("PauseRoot");
                    if (t != null) _pauseRoot = t.gameObject;
                }
                return _pauseRoot;
            }
        }

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null)
                {
                    _canvasGroup = GetComponent<CanvasGroup>();
                }
                return _canvasGroup;
            }
        }

        public SettingsView SettingsView
        {
            get
            {
                if (_cachedSettingsView == null && SettingsDialog != null)
                {
                    _cachedSettingsView = SettingsDialog.GetComponent<SettingsView>();
                }
                return _cachedSettingsView;
            }
        }

        public bool IsPauseActive => PauseRoot != null && PauseRoot.activeSelf;
        public bool IsSettingsActive => SettingsDialog != null && SettingsDialog.activeSelf;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();
        }

        public void SetPauseActive(bool active)
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_raycaster == null) _raycaster = GetComponent<GraphicRaycaster>();

            if (_canvas != null)
            {
                _canvas.enabled = active;
                _canvas.sortingOrder = 500;
            }

            if (_raycaster != null)
            {
                _raycaster.enabled = active;
            }

            if (PauseRoot != null)
            {
                PauseRoot.SetActive(active);
            }

            if (CanvasGroup != null)
            {
                CanvasGroup.alpha = active ? 1f : 0f;
                CanvasGroup.interactable = active;
                CanvasGroup.blocksRaycasts = active;
            }
        }

        public void SetSettingsActive(bool active)
        {
            if (_settingsDialog != null)
            {
                _settingsDialog.SetActive(active);
            }
        }

        public void SetConnectionQuality(string text, Color color)
        {
            if (_connectionQualityText != null)
            {
                _connectionQualityText.text = text;
                _connectionQualityText.color = color;
            }
        }
    }
}
