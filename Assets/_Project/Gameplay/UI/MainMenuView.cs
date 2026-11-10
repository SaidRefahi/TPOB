using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("Botones Principales")]
        [SerializeField] private Button _localPlayButton;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _lanHostButton;
        [SerializeField] private Button _openJoinModalButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _exitButton;

        [Header("Modal de Conexión")]
        [SerializeField] private GameObject _connectModalRoot;
        [SerializeField] private TMP_InputField _ipInputField;
        [SerializeField] private TMP_InputField _portInputField;
        [SerializeField] private Button _connectConfirmButton;
        [SerializeField] private Button _connectCancelButton;

        [Header("Descubrimiento Automático LAN")]
        [SerializeField] private Button _lanGameItemButton;
        [SerializeField] private TextMeshProUGUI _lanGameItemText;

        [Header("Información de Red e IP Local")]
        [SerializeField] private TextMeshProUGUI _localIpText;
        [SerializeField] private Button _copyIpButton;
        [SerializeField] private TextMeshProUGUI _connectHelpText;

        [Header("Diálogo de Opciones")]
        [SerializeField] private SettingsView _settingsDialog;

        [Header("Selector de Idioma")]
        [SerializeField] private Button _languageToggleButton;
        [SerializeField] private TextMeshProUGUI _languageButtonText;

        [Header("Selector de Nivel")]
        [SerializeField] private LevelSelectModalUI _levelSelectModal;

        [Header("Estado y Feedback")]
        [SerializeField] private TextMeshProUGUI _statusFeedbackText;

        public Button LocalPlayButton
        {
            get
            {
                if (_localPlayButton == null)
                {
                    var t = transform.Find("Background/MenuContainer/ButtonsColumn/Btn_LocalPlay");
                    if (t != null) _localPlayButton = t.GetComponent<Button>();
                }
                return _localPlayButton;
            }
        }

        public Button HostButton
        {
            get
            {
                if (_hostButton == null)
                {
                    var t = transform.Find("Background/MenuContainer/ButtonsColumn/Btn_Host");
                    if (t != null) _hostButton = t.GetComponent<Button>();
                }
                return _hostButton;
            }
        }

        public Button LanHostButton
        {
            get
            {
                if (_lanHostButton == null)
                {
                    var t = transform.Find("Background/MenuContainer/ButtonsColumn/Btn_LanHost");
                    if (t != null) _lanHostButton = t.GetComponent<Button>();
                }
                return _lanHostButton;
            }
        }

        public Button OpenJoinModalButton
        {
            get
            {
                if (_openJoinModalButton == null)
                {
                    var t = transform.Find("Background/MenuContainer/ButtonsColumn/Btn_Join");
                    if (t != null) _openJoinModalButton = t.GetComponent<Button>();
                }
                return _openJoinModalButton;
            }
        }

        public Button OptionsButton
        {
            get
            {
                if (_optionsButton == null)
                {
                    var t = transform.Find("Background/MenuContainer/ButtonsColumn/Btn_Options");
                    if (t != null) _optionsButton = t.GetComponent<Button>();
                }
                return _optionsButton;
            }
        }

        public Button ExitButton
        {
            get
            {
                if (_exitButton == null)
                {
                    var t = transform.Find("Background/MenuContainer/ButtonsColumn/Btn_Exit");
                    if (t != null) _exitButton = t.GetComponent<Button>();
                }
                return _exitButton;
            }
        }

        public GameObject ConnectModalRoot
        {
            get
            {
                if (_connectModalRoot == null)
                {
                    var t = transform.Find("Modal_Connect");
                    if (t != null) _connectModalRoot = t.gameObject;
                }
                return _connectModalRoot;
            }
        }

        public TMP_InputField IpInputField
        {
            get
            {
                if (_ipInputField == null)
                {
                    var t = transform.Find("Modal_Connect/Input_IP");
                    if (t != null) _ipInputField = t.GetComponent<TMP_InputField>();
                }
                return _ipInputField;
            }
        }

        public TMP_InputField PortInputField
        {
            get
            {
                if (_portInputField == null)
                {
                    var t = transform.Find("Modal_Connect/Input_Port");
                    if (t != null) _portInputField = t.GetComponent<TMP_InputField>();
                }
                return _portInputField;
            }
        }

        public Button ConnectConfirmButton
        {
            get
            {
                if (_connectConfirmButton == null)
                {
                    var t = transform.Find("Modal_Connect/Modal_Buttons/Btn_ConfirmConnect");
                    if (t != null) _connectConfirmButton = t.GetComponent<Button>();
                }
                return _connectConfirmButton;
            }
        }

        public Button ConnectCancelButton
        {
            get
            {
                if (_connectCancelButton == null)
                {
                    var t = transform.Find("Modal_Connect/Modal_Buttons/Btn_CancelConnect");
                    if (t != null) _connectCancelButton = t.GetComponent<Button>();
                }
                return _connectCancelButton;
            }
        }

        public TextMeshProUGUI ConnectHelpText
        {
            get
            {
                if (_connectHelpText == null)
                {
                    var t = transform.Find("Modal_Connect/Modal_HelpText");
                    if (t != null) _connectHelpText = t.GetComponent<TextMeshProUGUI>();
                }
                return _connectHelpText;
            }
        }

        public Button LanGameItemButton => _lanGameItemButton;
        public TextMeshProUGUI LanGameItemText => _lanGameItemText;

        public TextMeshProUGUI LocalIpText
        {
            get
            {
                if (_localIpText == null)
                {
                    var t = transform.Find("Background/MenuContainer/Row_LocalIP/Text_LocalIP");
                    if (t != null) _localIpText = t.GetComponent<TextMeshProUGUI>();
                }
                return _localIpText;
            }
        }

        public Button CopyIpButton
        {
            get
            {
                if (_copyIpButton == null)
                {
                    var t = transform.Find("Background/MenuContainer/Row_LocalIP/Btn_CopyIP");
                    if (t != null) _copyIpButton = t.GetComponent<Button>();
                }
                return _copyIpButton;
            }
        }

        public Button LanguageToggleButton
        {
            get
            {
                if (_languageToggleButton == null)
                {
                    var t = transform.Find("Background/HUD_TopBar/Btn_LanguageToggle");
                    if (t != null) _languageToggleButton = t.GetComponent<Button>();
                }
                return _languageToggleButton;
            }
        }

        public TextMeshProUGUI LanguageButtonText
        {
            get
            {
                if (_languageButtonText == null && LanguageToggleButton != null)
                {
                    _languageButtonText = _languageToggleButton.GetComponentInChildren<TextMeshProUGUI>();
                }
                return _languageButtonText;
            }
        }

        public SettingsView SettingsDialog => _settingsDialog;
        public LevelSelectModalUI LevelSelectModal => _levelSelectModal;

        public TextMeshProUGUI StatusFeedbackText
        {
            get
            {
                if (_statusFeedbackText == null)
                {
                    var t = transform.Find("Background/MenuContainer/Text_Status");
                    if (t != null) _statusFeedbackText = t.GetComponent<TextMeshProUGUI>();
                }
                return _statusFeedbackText;
            }
        }

        private Canvas _canvas;
        private GraphicRaycaster _raycaster;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();
        }

        public void SetActive(bool active)
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_raycaster == null) _raycaster = GetComponent<GraphicRaycaster>();

            if (_canvas != null) _canvas.enabled = active;
            if (_raycaster != null) _raycaster.enabled = active;

            if (!active)
            {
                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null)
                {
                    var selected = eventSystem.currentSelectedGameObject;
                    if (selected != null && selected.transform.IsChildOf(transform))
                    {
                        eventSystem.SetSelectedGameObject(null);
                    }
                }
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void SetStatusFeedback(string message, Color color)
        {
            if (_statusFeedbackText != null)
            {
                _statusFeedbackText.text = message;
                _statusFeedbackText.color = color;
            }
        }

        public void SetLanGameBanner(bool visible, string text = "")
        {
            if (_lanGameItemButton != null)
            {
                _lanGameItemButton.gameObject.SetActive(visible);
            }

            if (_lanGameItemText != null && visible)
            {
                _lanGameItemText.text = text;
            }
        }
    }
}
