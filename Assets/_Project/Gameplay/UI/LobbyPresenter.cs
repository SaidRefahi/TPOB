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
            bool isLobby = _gameManager != null && _gameManager.CurrentState == GameState.Lobby;
            _view.SetActive(isLobby);
            if (isLobby)
            {
                _lobbyService?.RequestSync();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void ResolveDependencies()
        {
            if (_lobbyService == null || _networkService == null || _gameManager == null)
            {
                var scopes = Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
                for (int i = 0; i < scopes.Length; i++)
                {
                    if (scopes[i] != null && scopes[i].Container != null)
                    {
                        try
                        {
                            if (_lobbyService == null)
                            {
                                _lobbyService = scopes[i].Container.Resolve<ILobbyService>();
                                HookLobbyEvents();
                            }
                            if (_networkService == null)
                            {
                                _networkService = scopes[i].Container.Resolve<INetworkService>();
                                HookNetworkEvents();
                            }
                            if (_gameManager == null)
                            {
                                _gameManager = scopes[i].Container.Resolve<IGameManager>();
                                HookGameManagerEvents();
                            }
                            if (_lobbyService != null && _networkService != null && _gameManager != null) break;
                        }
                        catch { }
                    }
                }
            }

            if (_lobbyService == null)
            {
                var lobbyCtrl = Object.FindFirstObjectByType<Game.Network.Services.LobbyNetworkController>();
                if (lobbyCtrl != null)
                {
                    _lobbyService = lobbyCtrl;
                    HookLobbyEvents();
                }
            }
        }

        private void HookLobbyEvents()
        {
            if (_lobbyService == null) return;
            _lobbyService.OnPlayerLobbyStateChanged -= HandlePlayerLobbyStateChanged;
            _lobbyService.OnPlayerLobbyStateChanged += HandlePlayerLobbyStateChanged;

            _lobbyService.OnBothPlayersReadyStatusChanged -= HandleBothPlayersReadyStatusChanged;
            _lobbyService.OnBothPlayersReadyStatusChanged += HandleBothPlayersReadyStatusChanged;
        }

        private void HookNetworkEvents()
        {
            if (_networkService == null) return;
            _networkService.OnPlayerConnected -= HandlePlayerConnected;
            _networkService.OnPlayerConnected += HandlePlayerConnected;
        }

        private void HookGameManagerEvents()
        {
            if (_gameManager == null) return;
            _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            _gameManager.OnGameStateChanged += HandleGameStateChanged;
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

            HookLobbyEvents();
            HookNetworkEvents();
            HookGameManagerEvents();
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

            if (_networkService != null)
            {
                _networkService.OnPlayerConnected -= HandlePlayerConnected;
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

        private void HandlePlayerConnected(int playerId, bool isLocal)
        {
            _lobbyService?.RequestSync();
            RefreshUI();
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
                _lobbyService?.RequestSync();
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

            int legsId = _lobbyService != null ? _lobbyService.LegsPlayerId : -1;
            int torsoId = _lobbyService != null ? _lobbyService.TorsoPlayerId : -1;
            bool legsReady = _lobbyService != null && _lobbyService.LegsReady;
            bool torsoReady = _lobbyService != null && _lobbyService.TorsoReady;
            bool bothReady = _lobbyService != null && _lobbyService.AreBothPlayersReady;

            int localId = -1;
            if (_networkService != null && _networkService.LocalPlayerId >= 0)
            {
                localId = _networkService.LocalPlayerId;
            }
            else
            {
                var nm = PurrNet.NetworkManager.main;
                if (nm != null)
                {
                    if (nm.isLocalPlayerReady)
                    {
                        localId = (int)nm.localPlayer.id.value;
                    }
                    else if (nm.isServer)
                    {
                        localId = 0;
                    }
                }
            }

            bool isHost = (_networkService != null && _networkService.IsServer) || (PurrNet.NetworkManager.main != null && PurrNet.NetworkManager.main.isServer);

            bool isLocalLegs;
            bool isLocalTorso;

            if (localId >= 0)
            {
                isLocalLegs = (legsId == localId);
                isLocalTorso = (torsoId == localId);
            }
            else if (isHost)
            {
                isLocalLegs = (legsId == 0);
                isLocalTorso = (torsoId == 0);
            }
            else
            {
                // Client whose localId hasn't resolved yet:
                // Host is ID 0. If slot is 0, it's definitely the host (teammate, NOT local).
                isLocalLegs = (legsId > 0);
                isLocalTorso = (torsoId > 0);
            }

            // Update Room Code Header
            string roomCode = _networkService != null ? _networkService.RoomName : string.Empty;
            if (_view.RoomCodeText != null && !string.IsNullOrEmpty(roomCode))
            {
                _view.RoomCodeText.text = $"<b><size=34><color=#00FFFF>SALA DE PREPARACIÓN</color></size></b>\n<size=18><color=#FFFFFF>CÓDIGO DE SALA: </color><color=#00FFFF><b>{roomCode}</b></color> <color=#8899AA>(Compártelo con tu compañero)</color></size>";
            }

            // Update Cards
            _view.UpdateCard(PlayerRole.Legs, legsId != -1, legsReady, isLocalLegs);
            _view.UpdateCard(PlayerRole.Torso, torsoId != -1, torsoReady, isLocalTorso);

            // Update Ready Button
            bool hasSelectedRole = isLocalLegs || isLocalTorso;
            bool isLocalReady = (isLocalLegs && legsReady) || (isLocalTorso && torsoReady);
            _view.SetReadyButtonState(isLocalReady, hasSelectedRole);

            // Update Start Game Button (Host only)
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
