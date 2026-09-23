using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        [Header("Botones Principales")]
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _openJoinModalButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _exitButton;

        [Header("Modal de Conexión")]
        [SerializeField] private GameObject _connectModalRoot;
        [SerializeField] private TMP_InputField _ipInputField;
        [SerializeField] private TMP_InputField _portInputField;
        [SerializeField] private Button _connectConfirmButton;
        [SerializeField] private Button _connectCancelButton;

        [Header("Información de Red e IP Local")]
        [SerializeField] private TextMeshProUGUI _localIpText;
        [SerializeField] private Button _copyIpButton;
        [SerializeField] private TextMeshProUGUI _connectHelpText;

        [Header("Diálogo de Opciones")]
        [SerializeField] private SettingsView _settingsDialog;

        [Header("Estado y Feedback")]
        [SerializeField] private TextMeshProUGUI _statusFeedbackText;

        public Button HostButton => _hostButton;
        public Button OpenJoinModalButton => _openJoinModalButton;
        public Button OptionsButton => _optionsButton;
        public Button ExitButton => _exitButton;

        public GameObject ConnectModalRoot => _connectModalRoot;
        public TMP_InputField IpInputField => _ipInputField;
        public TMP_InputField PortInputField => _portInputField;
        public Button ConnectConfirmButton => _connectConfirmButton;
        public Button ConnectCancelButton => _connectCancelButton;
        public TextMeshProUGUI ConnectHelpText => _connectHelpText;

        public TextMeshProUGUI LocalIpText => _localIpText;
        public Button CopyIpButton => _copyIpButton;

        public SettingsView SettingsDialog => _settingsDialog;
        public TextMeshProUGUI StatusFeedbackText => _statusFeedbackText;

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
    }
}
