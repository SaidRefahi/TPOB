using System;
using Game.Core.Events;
using Game.Core.Interfaces;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

namespace Game.Network.Services
{
    public sealed class NetworkService : INetworkService, IDisposable
    {
        private readonly IGameEventBus _eventBus;
        private NetworkManager _cachedManager;

        public bool IsServer => _cachedManager != null && _cachedManager.isServer;
        public bool IsClient => _cachedManager != null && _cachedManager.isClient;
        public bool IsConnected => _cachedManager != null && 
            (_cachedManager.serverState == ConnectionState.Connected || _cachedManager.clientState == ConnectionState.Connected);

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<int, bool> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;

        public NetworkService(IGameEventBus eventBus)
        {
            _eventBus = eventBus;
            InitializeManager();
        }

        private void InitializeManager()
        {
            if (NetworkManager.main != null)
            {
                BindManager(NetworkManager.main);
            }
        }

        public void BindManager(NetworkManager manager)
        {
            if (_cachedManager != null)
            {
                UnbindManager();
            }

            _cachedManager = manager;
            if (_cachedManager == null) return;

            _cachedManager.onServerConnectionState += HandleServerConnectionState;
            _cachedManager.onClientConnectionState += HandleClientConnectionState;
            _cachedManager.onPlayerJoined += HandlePlayerJoined;
            _cachedManager.onPlayerLeft += HandlePlayerLeft;
        }

        private void UnbindManager()
        {
            if (_cachedManager == null) return;

            _cachedManager.onServerConnectionState -= HandleServerConnectionState;
            _cachedManager.onClientConnectionState -= HandleClientConnectionState;
            _cachedManager.onPlayerJoined -= HandlePlayerJoined;
            _cachedManager.onPlayerLeft -= HandlePlayerLeft;
            _cachedManager = null;
        }

        public void StartHost()
        {
            EnsureManager();
            if (_cachedManager == null) return;

            _cachedManager.StartServer();
            _cachedManager.StartClient();
        }

        public void StartClient()
        {
            EnsureManager();
            if (_cachedManager == null) return;

            _cachedManager.StartClient();
        }

        public void Disconnect()
        {
            if (_cachedManager == null) return;

            if (_cachedManager.isServer)
            {
                _cachedManager.StopServer();
            }

            if (_cachedManager.isClient)
            {
                _cachedManager.StopClient();
            }
        }

        private void EnsureManager()
        {
            if (_cachedManager == null && NetworkManager.main != null)
            {
                BindManager(NetworkManager.main);
            }
        }

        private void HandleServerConnectionState(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                OnConnected?.Invoke();
            }
            else if (state == ConnectionState.Disconnected)
            {
                OnDisconnected?.Invoke();
            }
        }

        private void HandleClientConnectionState(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                OnConnected?.Invoke();
            }
            else if (state == ConnectionState.Disconnected)
            {
                OnDisconnected?.Invoke();
            }
        }

        private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            int playerId = (int)player.id.value;
            bool isLocal = _cachedManager != null && _cachedManager.localPlayer == player;

            OnPlayerConnected?.Invoke(playerId, isLocal);
            _eventBus?.Publish(new PlayerConnectedEvent(playerId, isLocal));
        }

        private void HandlePlayerLeft(PlayerID player, bool asServer)
        {
            int playerId = (int)player.id.value;

            OnPlayerDisconnected?.Invoke(playerId);
            _eventBus?.Publish(new PlayerDisconnectedEvent(playerId));
        }

        public void Dispose()
        {
            UnbindManager();
        }
    }
}
