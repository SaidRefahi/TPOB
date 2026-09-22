using Game.Core.Enums;
using Game.Core.Interfaces;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LobbyView))]
    public sealed class LobbyPresenter : MonoBehaviour
    {
        private LobbyView _view;
        private ILobbyService _lobbyService;
        private INetworkService _networkService;
        private IGameManager _gameManager;

        [Inject]
        public void Construct(
            ILobbyService lobbyService = null,
            INetworkService networkService = null,
            IGameManager gameManager = null)
        {
            _lobbyService = lobbyService;
            _networkService = networkService;
            _gameManager = gameManager;
        }

        private void Awake()
        {
            _view = GetComponent<LobbyView>();
        }

        private void Start()
        {
            ResolveDependencies();
            SubscribeEvents();
            RefreshUI();

            // Set initial visibility based on game state
            if (_gameManager != null)
            {
                _view.SetActive(_gameManager.CurrentState == GameState.Lobby);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void ResolveDependencies()
        {
            if (_lobbyService != null && _networkService != null && _gameManager != null) return;

            var scopes = Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] != null && scopes[i].Container != null)
                {
                    try
                    {
                        if (_lobbyService == null) _lobbyService = scopes[i].Container.Resolve<ILobbyService>();
                        if (_networkService == null) _networkService = scopes[i].Container.Resolve<INetworkService>();
                        if (_gameManager == null) _gameManager = scopes[i].Container.Resolve<IGameManager>();
                        if (_lobbyService != null && _networkService != null && _gameManager != null) break;
                    }
                    catch { }
                }
            }
        }

        private void SubscribeEvents()
        {
            if (_view.SelectLegsButton != null)
                _view.SelectLegsButton.onClick.AddListener(HandleSelectLegsClicked);

            if (_view.SelectTorsoButton != null)
                _view.SelectTorsoButton.onClick.AddListener(HandleSelectTorsoClicked);

            if (_view.ReadyToggleButton != null)
                _view.ReadyToggleButton.onClick.AddListener(HandleReadyToggleClicked);

            if (_view.StartGameButton != null)
                _view.StartGameButton.onClick.AddListener(HandleStartGameClicked);

            if (_view.LeaveLobbyButton != null)
                _view.LeaveLobbyButton.onClick.AddListener(HandleLeaveLobbyClicked);

            if (_lobbyService != null)
            {
                _lobbyService.OnPlayerLobbyStateChanged -= HandlePlayerLobbyStateChanged;
                _lobbyService.OnPlayerLobbyStateChanged += HandlePlayerLobbyStateChanged;

                _lobbyService.OnBothPlayersReadyStatusChanged -= HandleBothPlayersReadyStatusChanged;
                _lobbyService.OnBothPlayersReadyStatusChanged += HandleBothPlayersReadyStatusChanged;
            }

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_view.SelectLegsButton != null)
                _view.SelectLegsButton.onClick.RemoveListener(HandleSelectLegsClicked);

            if (_view.SelectTorsoButton != null)
                _view.SelectTorsoButton.onClick.RemoveListener(HandleSelectTorsoClicked);

            if (_view.ReadyToggleButton != null)
                _view.ReadyToggleButton.onClick.RemoveListener(HandleReadyToggleClicked);

            if (_view.StartGameButton != null)
                _view.StartGameButton.onClick.RemoveListener(HandleStartGameClicked);

            if (_view.LeaveLobbyButton != null)
                _view.LeaveLobbyButton.onClick.RemoveListener(HandleLeaveLobbyClicked);

            if (_lobbyService != null)
            {
                _lobbyService.OnPlayerLobbyStateChanged -= HandlePlayerLobbyStateChanged;
                _lobbyService.OnBothPlayersReadyStatusChanged -= HandleBothPlayersReadyStatusChanged;
            }

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void HandleSelectLegsClicked()
        {
            _lobbyService?.SelectRole(PlayerRole.Legs);
        }

        private void HandleSelectTorsoClicked()
        {
            _lobbyService?.SelectRole(PlayerRole.Torso);
        }

        private void HandleReadyToggleClicked()
        {
            _lobbyService?.ToggleReady();
        }

        private void HandleStartGameClicked()
        {
            _lobbyService?.StartGame();
        }

        private void HandleLeaveLobbyClicked()
        {
            _networkService?.Disconnect();
        }

        private void HandlePlayerLobbyStateChanged(int playerId, PlayerRole role, bool isReady)
        {
            RefreshUI();
        }

        private void HandleBothPlayersReadyStatusChanged(bool bothReady)
        {
            RefreshUI();
        }

        private void HandleGameStateChanged(GameState oldState, GameState newState)
        {
            if (newState == GameState.Lobby)
            {
                _view.SetActive(true);
                RefreshUI();
            }
            else
            {
                _view.SetActive(false);
            }
        }

        public void RefreshUI()
        {
            if (_view == null) return;

            ResolveDependencies();

            int localId = _networkService != null ? _networkService.LocalPlayerId : -1;

            int legsId = _lobbyService != null ? _lobbyService.LegsPlayerId : -1;
            int torsoId = _lobbyService != null ? _lobbyService.TorsoPlayerId : -1;
            bool legsReady = _lobbyService != null && _lobbyService.LegsReady;
            bool torsoReady = _lobbyService != null && _lobbyService.TorsoReady;
            bool bothReady = _lobbyService != null && _lobbyService.AreBothPlayersReady;

            bool isLocalLegs = localId >= 0 && legsId == localId;
            bool isLocalTorso = localId >= 0 && torsoId == localId;

            // Update Cards
            _view.UpdateCard(PlayerRole.Legs, legsId != -1, legsReady, isLocalLegs);
            _view.UpdateCard(PlayerRole.Torso, torsoId != -1, torsoReady, isLocalTorso);

            // Update Ready Button
            bool hasSelectedRole = isLocalLegs || isLocalTorso;
            bool isLocalReady = (isLocalLegs && legsReady) || (isLocalTorso && torsoReady);
            _view.SetReadyButtonState(isLocalReady, hasSelectedRole);

            // Update Start Game Button (Host only)
            bool isHost = _networkService != null && _networkService.IsServer;
            _view.SetStartButtonInteractable(bothReady, isHost);

            // Update Match Status Text
            if (bothReady)
            {
                if (isHost)
                {
                    _view.SetMatchStatus("¡Ambos pilotos confirmados! Pulsa 'INICIAR OPERACIÓN'.", new Color(0.3f, 1f, 0.4f));
                }
                else
                {
                    _view.SetMatchStatus("¡Pilotos confirmados! Esperando que el Host inicie...", new Color(0.3f, 1f, 0.4f));
                }
            }
            else if (legsId == -1 || torsoId == -1)
            {
                _view.SetMatchStatus("Esperando que ambos roles sean asignados (1 Piernas y 1 Torso)...", new Color(1f, 0.75f, 0.2f));
            }
            else
            {
                _view.SetMatchStatus("Roles elegidos. Esperando que ambos pilotos pulsen 'CONFIRMAR (LISTO)'...", new Color(0.2f, 0.85f, 1f));
            }
        }
    }
}
