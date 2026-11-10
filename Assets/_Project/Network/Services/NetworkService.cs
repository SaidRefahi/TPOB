using System;
using Cysharp.Threading.Tasks;
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
        private readonly LanDiscoveryService _lanDiscovery;
        private NetworkManager _cachedManager;

        public ILanDiscoveryService LanDiscovery => _lanDiscovery;

        private NetworkManager Manager
        {
            get
            {
                if (_cachedManager == null && NetworkManager.main != null)
                {
                    BindManager(NetworkManager.main);
                }
                return _cachedManager;
            }
        }

        public bool IsServer => Manager != null && _cachedManager.isServer;
        public bool IsClient => Manager != null && _cachedManager.isClient;
        public bool IsConnected => Manager != null && 
            (_cachedManager.serverState == ConnectionState.Connected || _cachedManager.clientState == ConnectionState.Connected);

        public int LocalPlayerId => Manager != null && _cachedManager.isLocalPlayerReady 
            ? (int)_cachedManager.localPlayer.id.value 
            : -1;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<int, bool> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;

        private readonly GameObject _lobbyPrefab;
        private readonly Game.Network.Injection.INetworkPrefabInjector _prefabInjector;

        public NetworkService(
            IGameEventBus eventBus, 
            GameObject lobbyPrefab = null,
            Game.Network.Injection.INetworkPrefabInjector prefabInjector = null)
        {
            _eventBus = eventBus;
            _lobbyPrefab = lobbyPrefab;
            _prefabInjector = prefabInjector;
            _lanDiscovery = new LanDiscoveryService();
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

            if (_cachedManager.isOffline)
            {
                _cachedManager.cookieScope = PurrNet.Modules.CookieScope.LiveWithProcess;
            }

            _cachedManager.onServerConnectionState += HandleServerConnectionState;
            _cachedManager.onClientConnectionState += HandleClientConnectionState;
            _cachedManager.onPlayerJoined += HandlePlayerJoined;
            _cachedManager.onPlayerLeft += HandlePlayerLeft;

            if (_cachedManager.serverState == ConnectionState.Connected || _cachedManager.clientState == ConnectionState.Connected)
            {
                Debug.Log("[NetworkService] Manager bound while already connected; notifying OnConnected.");
                OnConnected?.Invoke();
            }
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

        private string _fallbackRoomName = "TPOB-ROOM";

        public string RoomName
        {
            get
            {
                var purr = GetPurrTransport();
                return purr != null ? purr.roomName : _fallbackRoomName;
            }
            set
            {
                _fallbackRoomName = value;
                var purr = GetPurrTransport();
                if (purr != null)
                {
                    purr.roomName = value;
                }
            }
        }

        public string ServerAddress
        {
            get
            {
                var udp = GetUDPTransport();
                return udp != null ? udp.address : "127.0.0.1";
            }
            set
            {
                var udp = GetUDPTransport();
                if (udp != null)
                {
                    udp.address = value;
                }
            }
        }

        public ushort ServerPort
        {
            get
            {
                var udp = GetUDPTransport();
                return udp != null ? udp.serverPort : (ushort)5000;
            }
            set
            {
                var udp = GetUDPTransport();
                if (udp != null)
                {
                    udp.serverPort = value;
                }
            }
        }

        public int CurrentPing
        {
            get
            {
                if (_cachedManager == null || !_cachedManager.isClient) return 0;
                var transport = _cachedManager.currentTransport;
                if (transport == null) return 0;

                try
                {
                    int rtt = transport.GetRoundTripTime(default, false);
                    return rtt >= 0 ? rtt : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public string ConnectionQualityText
        {
            get
            {
                if (_cachedManager == null || !IsConnected) return "Desconectado";
                if (_cachedManager.transport is UDPTransport)
                {
                    int ping = CurrentPing;
                    return ping > 0 ? $"Red Local (LAN) | {ping}ms" : "Red Local (LAN)";
                }

                var purr = GetPurrTransport();
                if (purr != null)
                {
                    var link = purr.clientSessionLink;
                    int ping = CurrentPing;
                    string pingStr = ping > 0 ? $" | {ping}ms" : "";
                    switch (link)
                    {
                        case PurrTransport.SessionLink.P2P:
                            return $"P2P Directo{pingStr}";
                        case PurrTransport.SessionLink.Relay:
                            string reg = !string.IsNullOrEmpty(purr.region) ? $" ({purr.region})" : "";
                            return $"Relay{reg}{pingStr}";
                        case PurrTransport.SessionLink.Resolving:
                            return $"Negociando P2P{pingStr}";
                        default:
                            return $"Conectado{pingStr}";
                    }
                }

                return "Conectado";
            }
        }

        private PurrTransport GetPurrTransport()
        {
            EnsureManager();
            if (_cachedManager == null) return null;
            if (_cachedManager.transport is PurrTransport purr) return purr;
            var comp = _cachedManager.GetComponent<PurrTransport>();
            if (comp == null)
            {
                comp = _cachedManager.gameObject.AddComponent<PurrTransport>();
            }
            return comp;
        }

        private UDPTransport GetUDPTransport()
        {
            EnsureManager();
            if (_cachedManager == null) return null;
            if (_cachedManager.transport is UDPTransport udp) return udp;
            var comp = _cachedManager.GetComponent<UDPTransport>();
            if (comp == null)
            {
                comp = _cachedManager.gameObject.AddComponent<UDPTransport>();
                comp.serverPort = 5000;
                comp.address = "127.0.0.1";
            }
            return comp;
        }

        public async UniTask<bool> SwitchTransportAsync(GenericTransport newTransport, System.Threading.CancellationToken ct = default)
        {
            EnsureManager();
            if (_cachedManager == null || newTransport == null) return false;
            if (_cachedManager.transport == newTransport) return true;

            if (_cachedManager.serverState != ConnectionState.Disconnected ||
                _cachedManager.clientState != ConnectionState.Disconnected)
            {
                Disconnect();
                await UniTask.WaitUntil(() => 
                    _cachedManager.serverState == ConnectionState.Disconnected &&
                    _cachedManager.clientState == ConnectionState.Disconnected, 
                    cancellationToken: ct);
            }

            if (_cachedManager.transport != null)
            {
                try 
                { 
                    _cachedManager.transport.StopServer();
                    _cachedManager.transport.StopClient();
                } 
                catch { }
                _cachedManager.transport.enabled = false;
            }

            newTransport.enabled = true;
            _cachedManager.transport = newTransport;
            return true;
        }

        private void SetActiveTransport(GenericTransport targetTransport)
        {
            EnsureManager();
            if (_cachedManager == null || targetTransport == null) return;
            if (_cachedManager.transport == targetTransport) return;

            if (_cachedManager.serverState == ConnectionState.Disconnected &&
                _cachedManager.clientState == ConnectionState.Disconnected)
            {
                if (_cachedManager.transport != null)
                {
                    try 
                    { 
                        _cachedManager.transport.StopServer();
                        _cachedManager.transport.StopClient();
                    } 
                    catch { }
                    _cachedManager.transport.enabled = false;
                }
                targetTransport.enabled = true;
                _cachedManager.transport = targetTransport;
            }
        }

        public void StartHost()
        {
            EnsureManager();
            if (_cachedManager == null)
            {
                Debug.LogError("[NetworkService] Cannot StartHost: NetworkManager not found!");
                return;
            }

            if (_cachedManager.serverState == ConnectionState.Disconnecting ||
                _cachedManager.clientState == ConnectionState.Disconnecting)
            {
                WaitAndStartHostAsync().Forget();
                return;
            }

            ExecuteStartHostAsync().Forget();
        }

        private async UniTaskVoid WaitAndStartHostAsync()
        {
            while (_cachedManager != null &&
                   (_cachedManager.serverState == ConnectionState.Disconnecting ||
                    _cachedManager.clientState == ConnectionState.Disconnecting))
            {
                await UniTask.Yield();
            }

            if (_cachedManager != null)
            {
                ExecuteStartHostAsync().Forget();
            }
        }

        private async UniTaskVoid ExecuteStartHostAsync()
        {
            var purr = GetPurrTransport();
            if (purr != null)
            {
                SetActiveTransport(purr);
                if (string.IsNullOrWhiteSpace(purr.roomName))
                {
                    purr.roomName = !string.IsNullOrWhiteSpace(_fallbackRoomName) ? _fallbackRoomName : "TPOB-ROOM";
                }
                purr.attemptDirectConnection = true;
                purr.masterServer = "https://purrtransport.purrservers.com/";

                if (string.IsNullOrEmpty(purr.region) || purr.region == "australia")
                {
                    try
                    {
                        var pingTask = PurrTransportUtils.ActualGetRelayServerAsync(purr.masterServer);
                        var completedTask = await System.Threading.Tasks.Task.WhenAny(
                            pingTask, 
                            System.Threading.Tasks.Task.Delay(1500)
                        );

                        if (completedTask == pingTask && pingTask.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                        {
                            var bestServer = pingTask.Result;
                            if (!string.IsNullOrEmpty(bestServer.region))
                            {
                                purr.SetServer(bestServer);
                                Debug.Log($"[NetworkService] Selected lowest-latency relay: {bestServer.region} ({bestServer.host})");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[NetworkService] Relay auto-ping skipped: {ex.Message}");
                    }
                }
            }

            _cachedManager.StartServer();
            _cachedManager.StartClient();

            if (_cachedManager.serverState == ConnectionState.Connected || _cachedManager.clientState == ConnectionState.Connected)
            {
                EnsureLobbyControllerSpawned();
                OnConnected?.Invoke();
            }
        }

        public void StartHost(ushort port)
        {
            ServerPort = port;
            StartLanHost(port);
        }

        public void StartHostWithRoom(string roomName)
        {
            RoomName = roomName;
            StartHost();
        }

        public void StartLanHost(ushort port = 5000)
        {
            EnsureManager();
            if (_cachedManager == null)
            {
                Debug.LogError("[NetworkService] Cannot StartLanHost: NetworkManager not found!");
                return;
            }

            var udp = GetUDPTransport();
            if (udp != null)
            {
                SetActiveTransport(udp);
                udp.serverPort = port;
            }

            _cachedManager.StartServer();
            _cachedManager.StartClient();

            if (_cachedManager.serverState == ConnectionState.Connected || _cachedManager.clientState == ConnectionState.Connected)
            {
                EnsureLobbyControllerSpawned();
                OnConnected?.Invoke();
            }

            _lanDiscovery.StartBroadcasting(Environment.MachineName, port, "TPOB-LAN");
        }

        public void StartClient()
        {
            EnsureManager();
            if (_cachedManager == null)
            {
                Debug.LogError("[NetworkService] Cannot StartClient: NetworkManager not found!");
                return;
            }

            if (_cachedManager.clientState == ConnectionState.Disconnecting)
            {
                WaitAndStartClientAsync().Forget();
                return;
            }

            ExecuteStartClient();
        }

        private async UniTaskVoid WaitAndStartClientAsync()
        {
            while (_cachedManager != null && _cachedManager.clientState == ConnectionState.Disconnecting)
            {
                await UniTask.Yield();
            }

            if (_cachedManager != null)
            {
                ExecuteStartClient();
            }
        }

        private void ExecuteStartClient()
        {
            var purr = GetPurrTransport();
            if (purr != null)
            {
                SetActiveTransport(purr);
                if (string.IsNullOrWhiteSpace(purr.roomName))
                {
                    purr.roomName = !string.IsNullOrWhiteSpace(_fallbackRoomName) ? _fallbackRoomName : "TPOB-ROOM";
                }
                purr.attemptDirectConnection = true;
                purr.masterServer = "https://purrtransport.purrservers.com/";
            }

            _cachedManager.StartClient();

            if (_cachedManager.clientState == ConnectionState.Connected)
            {
                OnConnected?.Invoke();
            }
        }

        public void StartClient(string address, ushort port)
        {
            var purr = GetPurrTransport();
            if (purr != null && !IsIpAddress(address))
            {
                RoomName = address;
                StartClient();
            }
            else
            {
                StartLanClient(address, port);
            }
        }

        public void StartClientWithRoom(string roomName)
        {
            if (IsIpAddress(roomName))
            {
                StartLanClient(roomName, 5000);
            }
            else
            {
                RoomName = roomName;
                StartClient();
            }
        }

        public void StartLanClient(string ip, ushort port = 5000)
        {
            EnsureManager();
            if (_cachedManager == null)
            {
                Debug.LogError("[NetworkService] Cannot StartLanClient: NetworkManager not found!");
                return;
            }

            var udp = GetUDPTransport();
            if (udp != null)
            {
                SetActiveTransport(udp);
                udp.address = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
                udp.serverPort = port;
            }

            _cachedManager.StartClient();

            if (_cachedManager.clientState == ConnectionState.Connected)
            {
                OnConnected?.Invoke();
            }
        }

        private static bool IsIpAddress(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            if (trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            return System.Net.IPAddress.TryParse(trimmed, out _);
        }

        private bool _isDisconnecting;

        public void Disconnect()
        {
            _lanDiscovery?.StopBroadcasting();
            _lanDiscovery?.StopListening();

            EnsureManager();
            if (_cachedManager == null) return;
            if (_isDisconnecting) return;
            _isDisconnecting = true;

            try
            {
                if (_cachedManager.serverState != ConnectionState.Disconnected)
                {
                    _cachedManager.StopServer();
                }

                if (_cachedManager.clientState != ConnectionState.Disconnected)
                {
                    _cachedManager.StopClient();
                }
            }
            finally
            {
                _isDisconnecting = false;
            }
        }

        private void EnsureManager()
        {
            if (_cachedManager == null)
            {
                var nm = NetworkManager.main != null ? NetworkManager.main : UnityEngine.Object.FindFirstObjectByType<NetworkManager>();
                if (nm != null)
                {
                    BindManager(nm);
                }
            }
        }

        private void HandleServerConnectionState(ConnectionState state)
        {
            Debug.Log($"[NetworkService] Server Connection State: {state}");
            if (state == ConnectionState.Connected)
            {
                EnsureLobbyControllerSpawned();
                OnConnected?.Invoke();
            }
            else if (state == ConnectionState.Disconnected)
            {
                if (_cachedManager == null || !_cachedManager.isClient || _cachedManager.clientState == ConnectionState.Disconnected)
                {
                    OnDisconnected?.Invoke();
                }
            }
        }

        private void EnsureLobbyControllerSpawned()
        {
            if (_cachedManager == null || !_cachedManager.isServer) return;

            var existing = UnityEngine.Object.FindFirstObjectByType<LobbyNetworkController>();
            if (existing != null && existing.isSpawned)
            {
                return;
            }

            GameObject prefabToSpawn = _lobbyPrefab;
            if (prefabToSpawn != null)
            {
                var instance = _prefabInjector != null
                    ? _prefabInjector.InstantiateAndInject(prefabToSpawn, Vector3.zero, Quaternion.identity)
                    : UnityEngine.Object.Instantiate(prefabToSpawn);
                _cachedManager.Spawn(instance);
            }
            else
            {
                Debug.LogWarning("[NetworkService] Cannot spawn LobbyNetworkController: No lobbyPrefab provided to NetworkService.");
            }
        }

        private void HandleClientConnectionState(ConnectionState state)
        {
            Debug.Log($"[NetworkService] Client Connection State: {state}");
            if (state == ConnectionState.Connected)
            {
                OnConnected?.Invoke();
            }
            else if (state == ConnectionState.Disconnected)
            {
                if (_cachedManager == null || !_cachedManager.isServer || _cachedManager.serverState == ConnectionState.Disconnected)
                {
                    OnDisconnected?.Invoke();
                }
            }
        }

        private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            if (_cachedManager != null && _cachedManager.isServer && !asServer)
            {
                return;
            }

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
            _lanDiscovery?.Dispose();
            UnbindManager();
        }
    }
}
