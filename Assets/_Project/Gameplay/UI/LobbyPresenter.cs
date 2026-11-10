using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Network.Scopes;
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
            if (_lobbyService != null && _networkService != null && _gameManager != null) return;

            var container = GameLifetimeScope.Instance != null ? GameLifetimeScope.Instance.Container : null;
            if (container != null)
            {
                try
                {
                    if (_lobbyService == null)
                    {
                        _lobbyService = container.Resolve<ILobbyService>();
                        HookLobbyEvents();
                    }
                    if (_networkService == null)
                    {
                        _networkService = container.Resolve<INetworkService>();
                        HookNetworkEvents();
                    }
                    if (_gameManager == null)
                    {
                        _gameManager = container.Resolve<IGameManager>();
                        HookGameManagerEvents();
                    }
                }
                catch { }
            }
        }

        private void HookLobbyEvents()
        {
            if (_lobbyService == null) return;
            _lobbyService.OnPlayerLobbyStateChanged -= HandlePlayerLobbyStateChanged;
            _lobbyService.OnPlayerLobbyStateChanged += HandlePlayerLobbyStateChanged;

            _lobbyService.OnBothPlayersReadyStatusChanged -= HandleBothPlayersReadyStatusChanged;
            _lobbyService.OnBothPlayersReadyStatusChanged += HandleBothPlayersReadyStatusChanged;

            _lobbyService.OnSelectedRoomChanged -= HandleSelectedRoomChanged;
            _lobbyService.OnSelectedRoomChanged += HandleSelectedRoomChanged;
        }

        private void HookNetworkEvents()
        {
            if (_networkService == null) return;
            _networkService.OnPlayerConnected -= HandlePlayerConnected;
            _networkService.OnPlayerConnected += HandlePlayerConnected;
            _networkService.OnDisconnected -= HandleNetworkDisconnected;
            _networkService.OnDisconnected += HandleNetworkDisconnected;
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

            if (_view.SelectRoomButton != null)
                _view.SelectRoomButton.onClick.AddListener(HandleSelectRoomClicked);

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

            if (_view.SelectRoomButton != null)
                _view.SelectRoomButton.onClick.RemoveListener(HandleSelectRoomClicked);

            if (_lobbyService != null)
            {
                _lobbyService.OnPlayerLobbyStateChanged -= HandlePlayerLobbyStateChanged;
                _lobbyService.OnBothPlayersReadyStatusChanged -= HandleBothPlayersReadyStatusChanged;
                _lobbyService.OnSelectedRoomChanged -= HandleSelectedRoomChanged;
            }

            if (_networkService != null)
            {
                _networkService.OnPlayerConnected -= HandlePlayerConnected;
                _networkService.OnDisconnected -= HandleNetworkDisconnected;
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
            if (_view != null)
            {
                _view.ResetUI();
                _view.SetActive(false);
            }
            UIInputModuleFixer.EnsureUIFocusAndInput();

            if (_networkService != null && (_networkService.IsConnected || _networkService.IsServer || _networkService.IsClient))
            {
                _networkService.Disconnect();
            }
            else if (_gameManager != null)
            {
                _gameManager.ChangeState(GameState.Booting);
            }
        }

        private void HandleNetworkDisconnected()
        {
            if (_view != null)
            {
                _view.ResetUI();
                _view.SetActive(false);
            }
            UIInputModuleFixer.EnsureUIFocusAndInput();
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

        private void HandleSelectRoomClicked()
        {
            var levelManager = Object.FindFirstObjectByType<Game.Network.Services.LevelManager>();
            int totalRooms = levelManager != null ? levelManager.TotalRooms : 1;

            if (_view.LevelSelectModal != null)
            {
                _view.LevelSelectModal.OnRoomSelected -= HandleRoomSelectedFromModal;
                _view.LevelSelectModal.OnRoomSelected += HandleRoomSelectedFromModal;
                _view.LevelSelectModal.Populate(totalRooms, _lobbyService != null ? _lobbyService.SelectedRoomIndex : 0);
                _view.LevelSelectModal.Show();
            }
        }

        private void HandleRoomSelectedFromModal(int roomIndex)
        {
            if (_view.LevelSelectModal != null)
            {
                _view.LevelSelectModal.OnRoomSelected -= HandleRoomSelectedFromModal;
                _view.LevelSelectModal.Hide();
            }

            _lobbyService?.SetSelectedRoom(roomIndex);
        }

        private void HandleSelectedRoomChanged(int roomIndex)
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
                if (_view != null)
                {
                    _view.SetActive(true);
                }
                _lobbyService?.RequestSync();
                RefreshUI();
            }
            else
            {
                if (_view != null)
                {
                    _view.ResetUI();
                    _view.SetActive(false);
                }
                UIInputModuleFixer.EnsureUIFocusAndInput();
            }
        }

        private float _pingUpdateTimer;

        private void Update()
        {
            if (_view == null || !_view.gameObject.activeInHierarchy) return;

            _pingUpdateTimer -= Time.deltaTime;
            if (_pingUpdateTimer <= 0f)
            {
                _pingUpdateTimer = 1.5f;
                UpdateNetworkStatusBadge();
            }
        }

        private void UpdateNetworkStatusBadge()
        {
            if (_networkService == null || _view == null) return;
            string quality = _networkService.IsConnected ? "Conectado" : "Desconectado";
            Color color = _networkService.IsConnected ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            _view.SetNetworkStatus($"<size=14><color=#88AACC>Enlace de Red: </color><b>{quality}</b></size>", color);
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
                    else if (nm.isClient)
                    {
                        localId = 1;
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

            // Update Network Connection Quality Badge
            UpdateNetworkStatusBadge();

            // Update Cards
            _view.UpdateCard(PlayerRole.Legs, legsId != -1, legsReady, isLocalLegs);
            _view.UpdateCard(PlayerRole.Torso, torsoId != -1, torsoReady, isLocalTorso);

            // Update Ready Button
            bool hasSelectedRole = isLocalLegs || isLocalTorso;
            bool isLocalReady = (isLocalLegs && legsReady) || (isLocalTorso && torsoReady);
            _view.SetReadyButtonState(isLocalReady, hasSelectedRole);

            // Update Start Game Button (Host only)
            _view.SetStartButtonInteractable(bothReady, isHost);

            // Update Selected Room (Host only can modify, both see it)
            int selectedRoom = _lobbyService != null ? _lobbyService.SelectedRoomIndex : 0;
            _view.UpdateSelectedRoom(selectedRoom, isHost);

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
