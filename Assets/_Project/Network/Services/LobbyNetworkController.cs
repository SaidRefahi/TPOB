using System;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Network.Services
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Estado del Lobby")]
    public sealed class LobbyNetworkController : NetworkBehaviour, ILobbyService
    {
        [Group("Estado del Lobby")]
        [ShowInInspector, ReadOnly]
        private int _legsPlayerId = -1;

        [Group("Estado del Lobby")]
        [ShowInInspector, ReadOnly]
        private bool _legsReady;

        [Group("Estado del Lobby")]
        [ShowInInspector, ReadOnly]
        private int _torsoPlayerId = -1;

        [Group("Estado del Lobby")]
        [ShowInInspector, ReadOnly]
        private bool _torsoReady;

        public bool AreBothPlayersReady => _legsPlayerId != -1 && _torsoPlayerId != -1 && _legsReady && _torsoReady;
        public int LegsPlayerId => _legsPlayerId;
        public int TorsoPlayerId => _torsoPlayerId;
        public bool LegsReady => _legsReady;
        public bool TorsoReady => _torsoReady;

        public event Action<int, PlayerRole, bool> OnPlayerLobbyStateChanged;
        public event Action<bool> OnBothPlayersReadyStatusChanged;

        private IPlayerRegistry _playerRegistry;
        private ILevelManager _levelManager;
        private INetworkService _networkService;

        [Inject]
        public void Construct(
            IPlayerRegistry playerRegistry = null,
            ILevelManager levelManager = null,
            INetworkService networkService = null)
        {
            _playerRegistry = playerRegistry;
            _levelManager = levelManager;
            _networkService = networkService;
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            ResolveDependenciesIfNeeded();

            var nm = networkManager != null ? networkManager : NetworkManager.main;
            if (nm != null)
            {
                if (isServer)
                {
                    nm.onPlayerJoined -= HandleServerPlayerJoined;
                    nm.onPlayerJoined += HandleServerPlayerJoined;
                    nm.onPlayerLeft -= HandleServerPlayerLeft;
                    nm.onPlayerLeft += HandleServerPlayerLeft;

                    BroadcastLobbyState();
                }
                else
                {
                    RequestSync();
                }
            }
        }

        protected override void OnDestroy()
        {
            var nm = networkManager != null ? networkManager : NetworkManager.main;
            if (nm != null)
            {
                nm.onPlayerJoined -= HandleServerPlayerJoined;
                nm.onPlayerLeft -= HandleServerPlayerLeft;
            }

            base.OnDestroy();
        }

        private void ResolveDependenciesIfNeeded()
        {
            if (_playerRegistry != null && _levelManager != null && _networkService != null) return;

            var scopes = UnityEngine.Object.FindObjectsByType<VContainer.Unity.LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] != null && scopes[i].Container != null)
                {
                    try
                    {
                        if (_playerRegistry == null) _playerRegistry = scopes[i].Container.Resolve<IPlayerRegistry>();
                        if (_levelManager == null) _levelManager = scopes[i].Container.Resolve<ILevelManager>();
                        if (_networkService == null) _networkService = scopes[i].Container.Resolve<INetworkService>();
                        if (_playerRegistry != null && _levelManager != null && _networkService != null) break;
                    }
                    catch { }
                }
            }
        }

        private void HandleServerPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            if (!asServer || !isServer) return;
            BroadcastLobbyState();
        }

        private void HandleServerPlayerLeft(PlayerID player, bool asServer)
        {
            if (!asServer || !isServer) return;

            int leftPlayerId = (int)player.id.value;
            bool changed = false;

            if (_legsPlayerId == leftPlayerId)
            {
                _legsPlayerId = -1;
                _legsReady = false;
                changed = true;
            }

            if (_torsoPlayerId == leftPlayerId)
            {
                _torsoPlayerId = -1;
                _torsoReady = false;
                changed = true;
            }

            if (changed)
            {
                BroadcastLobbyState();
            }
        }

        #region ILobbyService Public API

        public void RequestSync()
        {
            if (isServer)
            {
                BroadcastLobbyState();
            }
            else if (isSpawned)
            {
                RequestSyncServerRpc();
            }
        }

        public void SelectRole(PlayerRole role)
        {
            ResolveDependenciesIfNeeded();
            int localId = GetLocalPlayerId();
            RequestRoleSelectionServerRpc(role, localId);
        }

        public void ToggleReady()
        {
            ResolveDependenciesIfNeeded();
            int localId = GetLocalPlayerId();
            RequestToggleReadyServerRpc(localId);
        }

        public void StartGame()
        {
            RequestStartGameServerRpc();
        }

        private int GetLocalPlayerId()
        {
            if (_networkService != null && _networkService.LocalPlayerId >= 0)
            {
                return _networkService.LocalPlayerId;
            }

            var nm = networkManager != null ? networkManager : NetworkManager.main;
            if (nm != null)
            {
                if (nm.isLocalPlayerReady)
                {
                    return (int)nm.localPlayer.id.value;
                }
                if (nm.isServer)
                {
                    return 0;
                }
            }

            return -1;
        }

        #endregion

        #region ServerRpc

        [ServerRpc(requireOwnership: false)]
        public void RequestSyncServerRpc()
        {
            if (!isServer) return;
            BroadcastLobbyState();
        }

        [ServerRpc(requireOwnership: false)]
        public void RequestRoleSelectionServerRpc(PlayerRole requestedRole, int clientReportedId = -1, RPCInfo info = default)
        {
            if (!isServer) return;

            int playerId = ResolveSenderPlayerId(info, clientReportedId);
            if (playerId < 0) return;

            ApplyRoleSelection(playerId, requestedRole);
            BroadcastLobbyState();
        }

        private void ApplyRoleSelection(int playerId, PlayerRole requestedRole)
        {
            if (requestedRole == PlayerRole.Legs)
            {
                if (_legsPlayerId != -1 && _legsPlayerId != playerId && _legsReady)
                {
                    return;
                }

                if (_legsPlayerId != -1 && _legsPlayerId != playerId)
                {
                    _torsoPlayerId = _legsPlayerId;
                    _torsoReady = false;
                }

                if (_torsoPlayerId == playerId)
                {
                    _torsoPlayerId = -1;
                    _torsoReady = false;
                }

                _legsPlayerId = playerId;
                _legsReady = false;
            }
            else if (requestedRole == PlayerRole.Torso)
            {
                if (_torsoPlayerId != -1 && _torsoPlayerId != playerId && _torsoReady)
                {
                    return;
                }

                if (_torsoPlayerId != -1 && _torsoPlayerId != playerId)
                {
                    _legsPlayerId = _torsoPlayerId;
                    _legsReady = false;
                }

                if (_legsPlayerId == playerId)
                {
                    _legsPlayerId = -1;
                    _legsReady = false;
                }

                _torsoPlayerId = playerId;
                _torsoReady = false;
            }
        }

        [ServerRpc(requireOwnership: false)]
        public void RequestToggleReadyServerRpc(int clientReportedId = -1, RPCInfo info = default)
        {
            if (!isServer) return;

            int playerId = ResolveSenderPlayerId(info, clientReportedId);
            if (playerId < 0) return;

            if (_legsPlayerId == playerId)
            {
                _legsReady = !_legsReady;
            }
            else if (_torsoPlayerId == playerId)
            {
                _torsoReady = !_torsoReady;
            }

            BroadcastLobbyState();
        }

        private int ResolveSenderPlayerId(RPCInfo info, int clientReportedId)
        {
            if (info.sender.id.value > 0)
            {
                return (int)info.sender.id.value;
            }

            if (clientReportedId > 0)
            {
                return clientReportedId;
            }

            if (isServer)
            {
                return 0;
            }

            int localId = GetLocalPlayerId();
            if (localId >= 0)
            {
                return localId;
            }

            return -1;
        }

        [ServerRpc(requireOwnership: false)]
        public void RequestStartGameServerRpc()
        {
            if (!isServer || !AreBothPlayersReady) return;

            ResolveDependenciesIfNeeded();

            if (_levelManager != null)
            {
                _levelManager.LoadRoomAsync(0).Forget();
            }
            else
            {
                Debug.LogWarning("[LobbyNetworkController] LevelManager not found when starting game.");
            }
        }

        private void BroadcastLobbyState()
        {
            SyncLobbyStateObserversRpc(_legsPlayerId, _legsReady, _torsoPlayerId, _torsoReady);
        }

        #endregion

        #region ObserversRpc

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void SyncLobbyStateObserversRpc(int legsId, bool legsReady, int torsoId, bool torsoReady)
        {
            _legsPlayerId = legsId;
            _legsReady = legsReady;
            _torsoPlayerId = torsoId;
            _torsoReady = torsoReady;

            ResolveDependenciesIfNeeded();

            if (_playerRegistry != null)
            {
                if (legsId != -1)
                {
                    _playerRegistry.TryAssignRole(legsId, PlayerRole.Legs);
                    _playerRegistry.TrySetPlayerReady(legsId, legsReady);
                }

                if (torsoId != -1)
                {
                    _playerRegistry.TryAssignRole(torsoId, PlayerRole.Torso);
                    _playerRegistry.TrySetPlayerReady(torsoId, torsoReady);
                }
            }

            OnPlayerLobbyStateChanged?.Invoke(legsId, PlayerRole.Legs, legsReady);
            OnPlayerLobbyStateChanged?.Invoke(torsoId, PlayerRole.Torso, torsoReady);
            OnBothPlayersReadyStatusChanged?.Invoke(AreBothPlayersReady);
        }

        #endregion
    }
}
