using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Core.Localization;
using Game.Network.Scopes;
using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MainMenuView))]
    public sealed class MainMenuPresenter : MonoBehaviour
    {
        private MainMenuView _view;
        private INetworkService _networkService;
        private IGameManager _gameManager;

        [Inject]
        public void Construct(INetworkService networkService = null, IGameManager gameManager = null)
        {
            _networkService = networkService;
            _gameManager = gameManager;
        }

        private void Awake()
        {
            _view = GetComponent<MainMenuView>();
        }

        private void Start()
        {
            ResolveDependencies();
            InitializeView();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void ResolveDependencies()
        {
            if (_networkService != null && _gameManager != null) return;

            var container = GameLifetimeScope.Instance != null ? GameLifetimeScope.Instance.Container : null;
            if (container != null)
            {
                try
                {
                    if (_networkService == null) _networkService = container.Resolve<INetworkService>();
                    if (_gameManager == null) _gameManager = container.Resolve<IGameManager>();
                }
                catch { }
            }
        }

        private string _cachedRoomCode;

        private string GetRoomCode()
        {
            if (string.IsNullOrEmpty(_cachedRoomCode))
            {
                RegenerateRoomCode();
            }
            return _cachedRoomCode;
        }

        public string RegenerateRoomCode()
        {
            _cachedRoomCode = "TPOB-" + UnityEngine.Random.Range(1000, 9999);
            if (_view != null && _view.LocalIpText != null)
            {
                _view.LocalIpText.text = $"{LocalizationService.Get("RoomCode")}: <color=#00FFFF><b>{_cachedRoomCode}</b></color>";
            }
            return _cachedRoomCode;
        }

        private void InitializeView()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_view.LocalPlayButton == null && _view.HostButton != null)
            {
                var hostTransform = _view.HostButton.transform;
                var parent = hostTransform.parent;
                var localGo = Instantiate(_view.HostButton.gameObject, parent);
                localGo.name = "Btn_LocalPlay";
                localGo.transform.SetSiblingIndex(0);
                var localBtn = localGo.GetComponent<UnityEngine.UI.Button>();
                var viewSoField = typeof(MainMenuView).GetField("_localPlayButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (viewSoField != null)
                {
                    viewSoField.SetValue(_view, localBtn);
                }
                localBtn.onClick.RemoveAllListeners();
                localBtn.onClick.AddListener(HandleLocalPlayClicked);
            }

            RegenerateRoomCode();
            RefreshLanguageTexts();

            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(false);
            }

            if (_view.SettingsDialog != null)
            {
                _view.SettingsDialog.SetActive(false);
            }

            if (_view.IpInputField != null && string.IsNullOrEmpty(_view.IpInputField.text))
            {
                _view.IpInputField.text = string.Empty;
            }

            if (_view.PortInputField != null && string.IsNullOrEmpty(_view.PortInputField.text))
            {
                _view.PortInputField.text = "5000";
            }

            _view.SetStatusFeedback(string.Empty, Color.white);
        }

        private void SubscribeEvents()
        {
            if (_view.LocalPlayButton != null)
                _view.LocalPlayButton.onClick.AddListener(HandleLocalPlayClicked);

            if (_view.HostButton != null)
                _view.HostButton.onClick.AddListener(HandleHostClicked);

            if (_view.OpenJoinModalButton != null)
                _view.OpenJoinModalButton.onClick.AddListener(HandleOpenJoinModalClicked);

            if (_view.CopyIpButton != null)
                _view.CopyIpButton.onClick.AddListener(HandleCopyCodeClicked);

            if (_view.ConnectConfirmButton != null)
                _view.ConnectConfirmButton.onClick.AddListener(HandleConnectConfirmClicked);

            if (_view.ConnectCancelButton != null)
                _view.ConnectCancelButton.onClick.AddListener(HandleConnectCancelClicked);

            if (_view.OptionsButton != null)
                _view.OptionsButton.onClick.AddListener(HandleOptionsClicked);

            if (_view.ExitButton != null)
                _view.ExitButton.onClick.AddListener(HandleExitClicked);

            if (_view.LanguageToggleButton != null)
                _view.LanguageToggleButton.onClick.AddListener(HandleLanguageToggleClicked);

            LocalizationService.OnLanguageChanged += HandleLanguageChanged;

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }

            if (_networkService != null)
            {
                _networkService.OnDisconnected -= HandleNetworkDisconnected;
                _networkService.OnDisconnected += HandleNetworkDisconnected;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_view.LocalPlayButton != null)
                _view.LocalPlayButton.onClick.RemoveListener(HandleLocalPlayClicked);

            if (_view.HostButton != null)
                _view.HostButton.onClick.RemoveListener(HandleHostClicked);

            if (_view.OpenJoinModalButton != null)
                _view.OpenJoinModalButton.onClick.RemoveListener(HandleOpenJoinModalClicked);

            if (_view.CopyIpButton != null)
                _view.CopyIpButton.onClick.RemoveListener(HandleCopyCodeClicked);

            if (_view.ConnectConfirmButton != null)
                _view.ConnectConfirmButton.onClick.RemoveListener(HandleConnectConfirmClicked);

            if (_view.ConnectCancelButton != null)
                _view.ConnectCancelButton.onClick.RemoveListener(HandleConnectCancelClicked);

            if (_view.OptionsButton != null)
                _view.OptionsButton.onClick.RemoveListener(HandleOptionsClicked);

            if (_view.ExitButton != null)
                _view.ExitButton.onClick.RemoveListener(HandleExitClicked);

            if (_view.LanguageToggleButton != null)
                _view.LanguageToggleButton.onClick.RemoveListener(HandleLanguageToggleClicked);

            LocalizationService.OnLanguageChanged -= HandleLanguageChanged;

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }

            if (_networkService != null)
            {
                _networkService.OnDisconnected -= HandleNetworkDisconnected;
            }
        }

        private void HandleCopyCodeClicked()
        {
            string code = GetRoomCode();
            GUIUtility.systemCopyBuffer = code;
            _view.SetStatusFeedback($"¡Código de sala {code} copiado al portapapeles!", new Color(0.4f, 1f, 0.4f));
        }

        private void HandleLocalPlayClicked()
        {
            ResolveDependencies();

            if (_gameManager != null)
            {
                _gameManager.IsLocalMode = true;
            }

            _view.SetActive(false);

            var levelManager = Object.FindFirstObjectByType<Game.Network.Services.LevelManager>();
            if (levelManager != null)
            {
                levelManager.LoadRoomAsync(0).Forget();
            }
            else
            {
                Debug.LogWarning("[MainMenuPresenter] No se encontró LevelManager para iniciar Modo Local.");
            }
        }

        private void HandleHostClicked()
        {
            ResolveDependencies();
            string code = GetRoomCode();
            _view.SetStatusFeedback($"Creando sala '{code}' en PurrNet... Pásale este código a tu amigo.", new Color(0.3f, 1f, 0.5f));

            if (_networkService != null)
            {
                _networkService.StartHostWithRoom(code);
            }
            else
            {
                _view.SetStatusFeedback("Error: Servicio de red no inicializado.", Color.red);
            }
        }

        private void HandleOpenJoinModalClicked()
        {
            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(true);
            }
        }

        private void HandleConnectConfirmClicked()
        {
            ResolveDependencies();
            string roomCode = _view.IpInputField != null && !string.IsNullOrWhiteSpace(_view.IpInputField.text)
                ? _view.IpInputField.text.Trim()
                : GetRoomCode();

            _view.SetStatusFeedback($"Conectando a sala '{roomCode}' en servidores de PurrNet...", new Color(1f, 0.8f, 0.2f));

            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(false);
            }

            if (_networkService != null)
            {
                _networkService.StartClientWithRoom(roomCode);
            }
            else
            {
                _view.SetStatusFeedback("Error: Servicio de red no inicializado.", Color.red);
            }
        }

        private void HandleConnectCancelClicked()
        {
            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(false);
            }
        }

        private void HandleOptionsClicked()
        {
            if (_view.SettingsDialog != null)
            {
                _view.SettingsDialog.SetActive(true);
            }
        }

        private void HandleExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleLanguageToggleClicked()
        {
            LocalizationService.ToggleLanguage();
        }

        private void HandleLanguageChanged()
        {
            RefreshLanguageTexts();
        }

        private void RefreshLanguageTexts()
        {
            if (_view == null) return;

            if (_view.LanguageButtonText != null)
            {
                _view.LanguageButtonText.text = LocalizationService.Get("LangToggle");
            }

            UpdateButtonLabel(_view.LocalPlayButton, LocalizationService.Get("LocalPlay"));
            UpdateButtonLabel(_view.HostButton, LocalizationService.Get("HostGame"));
            UpdateButtonLabel(_view.OpenJoinModalButton, LocalizationService.Get("JoinGame"));
            UpdateButtonLabel(_view.OptionsButton, LocalizationService.Get("Options"));
            UpdateButtonLabel(_view.ExitButton, LocalizationService.Get("ExitGame"));
            UpdateButtonLabel(_view.CopyIpButton, LocalizationService.Get("CopyCode"));
            UpdateButtonLabel(_view.ConnectConfirmButton, LocalizationService.Get("Connect"));
            UpdateButtonLabel(_view.ConnectCancelButton, LocalizationService.Get("Cancel"));

            if (_view.LocalIpText != null && !string.IsNullOrEmpty(_cachedRoomCode))
            {
                _view.LocalIpText.text = $"{LocalizationService.Get("RoomCode")}: <color=#00FFFF><b>{_cachedRoomCode}</b></color>";
            }
        }

        private static void UpdateButtonLabel(UnityEngine.UI.Button button, string newText)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = $"<b>{newText}</b>";
            }
        }

        private void HandleGameStateChanged(GameState oldState, GameState newState)
        {
            if (newState == GameState.Lobby || newState == GameState.InGame)
            {
                _view.SetActive(false);

                if (newState == GameState.Lobby)
                {
                    // Fail-safe: ensure LobbyView is enabled and refreshed
                    var lobbyView = Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
                    if (lobbyView != null)
                    {
                        lobbyView.gameObject.SetActive(true);
                        lobbyView.SetActive(true);
                        if (lobbyView.TryGetComponent<LobbyPresenter>(out var presenter))
                        {
                            presenter.RefreshUI();
                        }
                    }
                }
            }
            else if (newState == GameState.Booting)
            {
                _view.SetActive(true);
                _view.SetStatusFeedback(string.Empty, Color.white);
                RegenerateRoomCode();

                var lobbyView = Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
                if (lobbyView != null)
                {
                    lobbyView.SetActive(false);
                }

                if (_view.ConnectModalRoot != null)
                {
                    _view.ConnectModalRoot.SetActive(false);
                }

                if (_view.SettingsDialog != null)
                {
                    _view.SettingsDialog.SetActive(false);
                }

                UIInputModuleFixer.EnsureUIFocusAndInput();
            }
        }

        private void HandleNetworkDisconnected()
        {
            _view.SetActive(true);
            _view.SetStatusFeedback("Desconectado de la partida o conexión terminada.", new Color(1f, 0.4f, 0.4f));
            RegenerateRoomCode();

            var lobbyView = Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
            if (lobbyView != null)
            {
                lobbyView.SetActive(false);
            }

            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(false);
            }

            if (_view.SettingsDialog != null)
            {
                _view.SettingsDialog.SetActive(false);
            }

            UIInputModuleFixer.EnsureUIFocusAndInput();
        }

        private ushort GetPortFromInput()
        {
            if (_view.PortInputField != null && ushort.TryParse(_view.PortInputField.text, out ushort p))
            {
                return p;
            }
            return 5000;
        }
    }
}
