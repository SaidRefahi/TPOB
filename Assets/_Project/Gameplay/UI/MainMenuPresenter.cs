using System;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Core.Localization;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuPresenter : MonoBehaviour
    {
        [SerializeField] private MainMenuView _view;

        private INetworkService _networkService;
        private IGameManager _gameManager;
        private IGameEventBus _eventBus;
        private string _generatedRoomCode;
        private LanGameInfo _lastFoundLanGame;

        [Inject]
        public void Construct(INetworkService networkService, IGameManager gameManager, IGameEventBus eventBus)
        {
            _networkService = networkService;
            _gameManager = gameManager;
            _eventBus = eventBus;
        }

        private void Start()
        {
            ResolveDependencies();
            InitializeView();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            StopLanDiscovery();
            UnsubscribeEvents();
        }

        private void ResolveDependencies()
        {
            if (_networkService == null || _gameManager == null)
            {
                var container = Game.Network.Scopes.GameLifetimeScope.Instance != null 
                    ? Game.Network.Scopes.GameLifetimeScope.Instance.Container 
                    : null;

                if (container == null)
                {
                    var lifetimeScope = UnityEngine.Object.FindFirstObjectByType<VContainer.Unity.LifetimeScope>();
                    if (lifetimeScope != null)
                    {
                        container = lifetimeScope.Container;
                    }
                }

                if (container != null)
                {
                    try
                    {
                        _networkService ??= container.Resolve<INetworkService>();
                        _gameManager ??= container.Resolve<IGameManager>();
                        _eventBus ??= container.Resolve<IGameEventBus>();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MainMenuPresenter] Container resolve notice: {ex.Message}");
                    }
                }

                if (_networkService == null)
                {
                    var nm = PurrNet.NetworkManager.main != null ? PurrNet.NetworkManager.main : UnityEngine.Object.FindFirstObjectByType<PurrNet.NetworkManager>();
                    if (nm != null)
                    {
                        _networkService = new Game.Network.Services.NetworkService(_eventBus);
                    }
                }

                if (_gameManager == null && _networkService != null)
                {
                    _gameManager = new Game.Network.Services.GameManager(_networkService, _eventBus);
                }
            }

            if (_view == null)
            {
                _view = GetComponent<MainMenuView>();
            }

            HookNetworkAndManagerEvents();
        }

        private void HookNetworkAndManagerEvents()
        {
            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }

            if (_networkService != null)
            {
                _networkService.OnConnected -= HandleNetworkConnected;
                _networkService.OnConnected += HandleNetworkConnected;
                _networkService.OnDisconnected -= HandleNetworkDisconnected;
                _networkService.OnDisconnected += HandleNetworkDisconnected;
            }
        }

        private void InitializeView()
        {
            if (_view == null) return;

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

            _view.SetLanGameBanner(false);
            _view.SetStatusFeedback(string.Empty, Color.white);
        }

        private void SubscribeEvents()
        {
            if (_view.LocalPlayButton != null)
                _view.LocalPlayButton.onClick.AddListener(HandleLocalPlayClicked);

            if (_view.HostButton != null)
                _view.HostButton.onClick.AddListener(HandleHostClicked);

            if (_view.LanHostButton != null)
                _view.LanHostButton.onClick.AddListener(HandleLanHostClicked);

            if (_view.OpenJoinModalButton != null)
                _view.OpenJoinModalButton.onClick.AddListener(HandleOpenJoinModalClicked);

            if (_view.LanGameItemButton != null)
                _view.LanGameItemButton.onClick.AddListener(HandleJoinLanGameClicked);

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

            HookNetworkAndManagerEvents();
        }

        private void UnsubscribeEvents()
        {
            if (_view.LocalPlayButton != null)
                _view.LocalPlayButton.onClick.RemoveListener(HandleLocalPlayClicked);

            if (_view.HostButton != null)
                _view.HostButton.onClick.RemoveListener(HandleHostClicked);

            if (_view.LanHostButton != null)
                _view.LanHostButton.onClick.RemoveListener(HandleLanHostClicked);

            if (_view.OpenJoinModalButton != null)
                _view.OpenJoinModalButton.onClick.RemoveListener(HandleOpenJoinModalClicked);

            if (_view.LanGameItemButton != null)
                _view.LanGameItemButton.onClick.RemoveListener(HandleJoinLanGameClicked);

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
                _networkService.OnConnected -= HandleNetworkConnected;
                _networkService.OnDisconnected -= HandleNetworkDisconnected;
            }
        }

        private void RegenerateRoomCode()
        {
            int randSuffix = UnityEngine.Random.Range(1000, 9999);
            _generatedRoomCode = $"TPOB-{randSuffix}";

            if (_view.LocalIpText != null)
            {
                _view.LocalIpText.text = _generatedRoomCode;
            }

            if (_networkService != null)
            {
                _networkService.RoomName = _generatedRoomCode;
            }
        }

        private string GetRoomCode()
        {
            if (string.IsNullOrEmpty(_generatedRoomCode))
            {
                RegenerateRoomCode();
            }
            return _generatedRoomCode;
        }

        private void HandleCopyCodeClicked()
        {
            string code = GetRoomCode();
            GUIUtility.systemCopyBuffer = code;
            _view.SetStatusFeedback($"Código '{code}' copiado al portapapeles.", Color.cyan);
        }

        private void HandleLocalPlayClicked()
        {
            ResolveDependencies();

            var levelManager = UnityEngine.Object.FindFirstObjectByType<Game.Network.Services.LevelManager>();
            if (levelManager != null && levelManager.TotalRooms > 1 && _view.LevelSelectModal != null)
            {
                _view.LevelSelectModal.OnRoomSelected -= HandleLocalRoomSelected;
                _view.LevelSelectModal.OnRoomSelected += HandleLocalRoomSelected;
                _view.LevelSelectModal.Populate(levelManager.TotalRooms, 0);
                _view.LevelSelectModal.Show();
                return;
            }

            StartLocalGame(0);
        }

        private void HandleLocalRoomSelected(int roomIndex)
        {
            if (_view.LevelSelectModal != null)
            {
                _view.LevelSelectModal.OnRoomSelected -= HandleLocalRoomSelected;
                _view.LevelSelectModal.Hide();
            }

            StartLocalGame(roomIndex);
        }

        private void StartLocalGame(int roomIndex)
        {
            ResolveDependencies();

            if (_gameManager != null)
            {
                _gameManager.IsLocalMode = true;
            }

            _view.SetActive(false);

            var levelManager = UnityEngine.Object.FindFirstObjectByType<Game.Network.Services.LevelManager>();
            if (levelManager != null)
            {
                levelManager.LoadRoomAsync(roomIndex).Forget();
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
            _view.SetStatusFeedback($"Creando sala online '{code}' con auto-selección de mejor ping...", new Color(0.3f, 1f, 0.5f));

            if (_networkService != null)
            {
                _networkService.StartHostWithRoom(code);
            }
            else
            {
                _view.SetStatusFeedback("Error: Servicio de red no inicializado.", Color.red);
            }
        }

        private void HandleLanHostClicked()
        {
            ResolveDependencies();
            string localIp = GetLocalIPv4();
            _view.SetStatusFeedback($"Creando servidor en Red Local (LAN)... Tu IP: {localIp}", new Color(0.3f, 1f, 0.5f));

            if (_networkService != null)
            {
                _networkService.StartLanHost(5000);
            }
            else
            {
                _view.SetStatusFeedback("Error: Servicio de red no inicializado.", Color.red);
            }
        }

        private string GetLocalIPv4()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void HandleOpenJoinModalClicked()
        {
            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(true);
            }

            _view.SetLanGameBanner(false);

            if (_networkService?.LanDiscovery != null)
            {
                _networkService.LanDiscovery.OnGameDiscovered -= HandleLanGameDiscovered;
                _networkService.LanDiscovery.OnGameDiscovered += HandleLanGameDiscovered;
                _networkService.LanDiscovery.OnGameLost -= HandleLanGameLost;
                _networkService.LanDiscovery.OnGameLost += HandleLanGameLost;
                _networkService.LanDiscovery.StartListening();
            }
        }

        private void HandleLanGameDiscovered(LanGameInfo info)
        {
            _lastFoundLanGame = info;
            _view.SetLanGameBanner(true, $"🟢 Partida en WiFi: {info.HostName} ({info.IpAddress}) [Entrar]");
        }

        private void HandleLanGameLost(LanGameInfo info)
        {
            if (_lastFoundLanGame.IpAddress == info.IpAddress && _lastFoundLanGame.Port == info.Port)
            {
                _view.SetLanGameBanner(false);
            }
        }

        private void HandleJoinLanGameClicked()
        {
            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(false);
            }

            StopLanDiscovery();

            _view.SetStatusFeedback($"Conectando por WiFi a {_lastFoundLanGame.HostName} ({_lastFoundLanGame.IpAddress})...", new Color(0.3f, 1f, 0.5f));
            _networkService?.StartLanClient(_lastFoundLanGame.IpAddress, _lastFoundLanGame.Port);
        }

        private void StopLanDiscovery()
        {
            if (_networkService?.LanDiscovery != null)
            {
                _networkService.LanDiscovery.OnGameDiscovered -= HandleLanGameDiscovered;
                _networkService.LanDiscovery.OnGameLost -= HandleLanGameLost;
                _networkService.LanDiscovery.StopListening();
            }
        }

        private void HandleConnectConfirmClicked()
        {
            ResolveDependencies();
            StopLanDiscovery();

            string roomCode = _view.IpInputField != null && !string.IsNullOrWhiteSpace(_view.IpInputField.text)
                ? _view.IpInputField.text.Trim()
                : GetRoomCode();

            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(false);
            }

            if (_networkService != null)
            {
                if (System.Net.IPAddress.TryParse(roomCode, out _) || roomCode.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    _view.SetStatusFeedback($"Conectando por LAN a IP '{roomCode}'...", new Color(0.3f, 1f, 0.5f));
                    _networkService.StartLanClient(roomCode, GetPortFromInput());
                }
                else
                {
                    _view.SetStatusFeedback($"Conectando a sala '{roomCode}' en servidores de PurrNet...", new Color(1f, 0.8f, 0.2f));
                    _networkService.StartClientWithRoom(roomCode);
                }
            }
            else
            {
                _view.SetStatusFeedback("Error: Servicio de red no inicializado.", Color.red);
            }
        }

        private void HandleConnectCancelClicked()
        {
            StopLanDiscovery();
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
                _view.LanguageButtonText.text = LocalizationService.CurrentLanguage == AppLanguage.Spanish ? "EN" : "ES";
            }

            SetButtonText(_view.LocalPlayButton, LocalizationService.Get("LocalPlay"));
            SetButtonText(_view.HostButton, LocalizationService.Get("HostGame"));
            SetButtonText(_view.LanHostButton, LocalizationService.Get("HostLan"));
            SetButtonText(_view.OpenJoinModalButton, LocalizationService.Get("JoinGame"));
            SetButtonText(_view.OptionsButton, LocalizationService.Get("Options"));
            SetButtonText(_view.ExitButton, LocalizationService.Get("ExitGame"));
            SetButtonText(_view.ConnectConfirmButton, LocalizationService.Get("Connect"));
            SetButtonText(_view.ConnectCancelButton, LocalizationService.Get("Cancel"));
            SetButtonText(_view.CopyIpButton, LocalizationService.Get("CopyCode"));

            if (_view.ConnectHelpText != null)
            {
                string help = LocalizationService.Get("mainmenu_connect_help");
                _view.ConnectHelpText.text = (!string.IsNullOrEmpty(help) && help != "mainmenu_connect_help")
                    ? help
                    : "Ingresa el código de sala online o una IP local (ej. 192.168.1.15) para unirte.";
            }
        }

        private static void SetButtonText(UnityEngine.UI.Button button, string text)
        {
            if (button == null || string.IsNullOrEmpty(text)) return;
            var tmp = button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = text;
            }
        }

        private void HandleNetworkConnected()
        {
            if (_gameManager != null && _gameManager.CurrentState == GameState.Booting)
            {
                _gameManager.ChangeState(GameState.Lobby);
            }
            else
            {
                TransitionToLobby();
            }
        }

        private void TransitionToLobby()
        {
            _view.SetActive(false);

            var lobbyView = UnityEngine.Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
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

        private void HandleGameStateChanged(GameState oldState, GameState newState)
        {
            if (newState == GameState.Lobby)
            {
                TransitionToLobby();
            }
            else if (newState == GameState.Booting)
            {
                _view.SetActive(true);
                _view.SetStatusFeedback(string.Empty, Color.white);
                RegenerateRoomCode();

                var lobbyView = UnityEngine.Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
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

            var lobbyView = UnityEngine.Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include);
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
