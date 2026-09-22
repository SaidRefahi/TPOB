using Game.Core.Enums;
using Game.Core.Interfaces;
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

            var scopes = Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] != null && scopes[i].Container != null)
                {
                    try
                    {
                        if (_networkService == null) _networkService = scopes[i].Container.Resolve<INetworkService>();
                        if (_gameManager == null) _gameManager = scopes[i].Container.Resolve<IGameManager>();
                        if (_networkService != null && _gameManager != null) break;
                    }
                    catch { }
                }
            }
        }

        private void InitializeView()
        {
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
                _view.IpInputField.text = "127.0.0.1";
            }

            if (_view.PortInputField != null && string.IsNullOrEmpty(_view.PortInputField.text))
            {
                _view.PortInputField.text = "5000";
            }

            _view.SetStatusFeedback(string.Empty, Color.white);
        }

        private void SubscribeEvents()
        {
            if (_view.HostButton != null)
                _view.HostButton.onClick.AddListener(HandleHostClicked);

            if (_view.OpenJoinModalButton != null)
                _view.OpenJoinModalButton.onClick.AddListener(HandleOpenJoinModalClicked);

            if (_view.ConnectConfirmButton != null)
                _view.ConnectConfirmButton.onClick.AddListener(HandleConnectConfirmClicked);

            if (_view.ConnectCancelButton != null)
                _view.ConnectCancelButton.onClick.AddListener(HandleConnectCancelClicked);

            if (_view.OptionsButton != null)
                _view.OptionsButton.onClick.AddListener(HandleOptionsClicked);

            if (_view.ExitButton != null)
                _view.ExitButton.onClick.AddListener(HandleExitClicked);

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
            if (_view.HostButton != null)
                _view.HostButton.onClick.RemoveListener(HandleHostClicked);

            if (_view.OpenJoinModalButton != null)
                _view.OpenJoinModalButton.onClick.RemoveListener(HandleOpenJoinModalClicked);

            if (_view.ConnectConfirmButton != null)
                _view.ConnectConfirmButton.onClick.RemoveListener(HandleConnectConfirmClicked);

            if (_view.ConnectCancelButton != null)
                _view.ConnectCancelButton.onClick.RemoveListener(HandleConnectCancelClicked);

            if (_view.OptionsButton != null)
                _view.OptionsButton.onClick.RemoveListener(HandleOptionsClicked);

            if (_view.ExitButton != null)
                _view.ExitButton.onClick.RemoveListener(HandleExitClicked);

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }

            if (_networkService != null)
            {
                _networkService.OnDisconnected -= HandleNetworkDisconnected;
            }
        }

        private void HandleHostClicked()
        {
            ushort port = GetPortFromInput();
            _view.SetStatusFeedback($"Iniciando servidor Host en puerto {port}...", new Color(0.3f, 1f, 0.5f));

            if (_networkService != null)
            {
                _networkService.StartHost(port);
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
            string ip = _view.IpInputField != null && !string.IsNullOrWhiteSpace(_view.IpInputField.text)
                ? _view.IpInputField.text.Trim()
                : "127.0.0.1";

            ushort port = GetPortFromInput();

            _view.SetStatusFeedback($"Conectando a {ip}:{port}...", new Color(1f, 0.8f, 0.2f));

            if (_view.ConnectModalRoot != null)
            {
                _view.ConnectModalRoot.SetActive(false);
            }

            if (_networkService != null)
            {
                _networkService.StartClient(ip, port);
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

        private void HandleGameStateChanged(GameState oldState, GameState newState)
        {
            if (newState == GameState.Lobby || newState == GameState.InGame)
            {
                _view.SetActive(false);
            }
            else if (newState == GameState.Booting)
            {
                _view.SetActive(true);
                _view.SetStatusFeedback(string.Empty, Color.white);
            }
        }

        private void HandleNetworkDisconnected()
        {
            _view.SetActive(true);
            _view.SetStatusFeedback("Desconectado de la partida.", new Color(1f, 0.4f, 0.4f));
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
