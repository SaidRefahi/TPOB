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

        public SettingsView SettingsDialog => _settingsDialog;
        public TextMeshProUGUI StatusFeedbackText => _statusFeedbackText;

        public void SetActive(bool active) => gameObject.SetActive(active);

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
