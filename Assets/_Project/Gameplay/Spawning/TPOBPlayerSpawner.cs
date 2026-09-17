using System.Collections.Generic;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Spawning
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Prefabs")]
    [DeclareBoxGroup("Configuración")]
    public sealed class TPOBPlayerSpawner : NetworkBehaviour
    {
        [Group("Prefabs")]
        [SerializeField] private GameObject _legsPrefab;

        [Group("Prefabs")]
        [SerializeField] private GameObject _torsoPrefab;

        [Group("Configuración")]
        [SerializeField] private SpawnPointManager _spawnPointManager;

        [Group("Configuración")]
        [SerializeField] private bool _autoSpawnOnStart = true;

        [Group("Configuración")]
        [SerializeField] private bool _spawnLegsInEditor = true;

        [Group("Configuración")]
        [SerializeField] private bool _spawnTorsoInEditor = true;

        private IPlayerRegistry _playerRegistry;
        private readonly HashSet<int> _spawnedPlayers = new(4);

        [Inject]
        public void Construct(IPlayerRegistry playerRegistry = null)
        {
            _playerRegistry = playerRegistry;
        }

        private void Start()
        {
            if (!_autoSpawnOnStart)
            {
                return;
            }

            var nm = networkManager != null ? networkManager : NetworkManager.main;
            bool isNetworkPresent = nm != null;

            // In networked scenes, spawning is handled strictly on the server in OnSpawned().
            // Never instantiate local un-networked players if NetworkManager is present.
            if (!isNetworkPresent)
            {
                if (_spawnLegsInEditor)
                {
                    SpawnPlayerForRole(-1, PlayerRole.Legs);
                }

                if (_spawnTorsoInEditor)
                {
                    SpawnPlayerForRole(-2, PlayerRole.Torso);
                }
            }
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            var nm = networkManager != null ? networkManager : NetworkManager.main;
            if (nm != null)
            {
                nm.onLocalPlayerReceivedID -= HandleLocalPlayerReceivedId;
                nm.onLocalPlayerReceivedID += HandleLocalPlayerReceivedId;

                if (isServer)
                {
                    nm.onPlayerJoined -= HandleServerPlayerJoined;
                    nm.onPlayerJoined += HandleServerPlayerJoined;
                }
            }

            if (!isServer)
            {
                return;
            }

            if (_playerRegistry == null)
            {
                var scopes = Object.FindObjectsByType<VContainer.Unity.LifetimeScope>(FindObjectsSortMode.None);
                for (int i = 0; i < scopes.Length; i++)
                {
                    if (scopes[i] != null && scopes[i].Container != null)
                    {
                        try
                        {
                            _playerRegistry = scopes[i].Container.Resolve<IPlayerRegistry>();
                            if (_playerRegistry != null) break;
                        }
                        catch { }
                    }
                }
            }

            if (_playerRegistry != null)
            {
                _playerRegistry.OnRoleAssigned += HandleRoleAssigned;

                var currentPlayers = _playerRegistry.ConnectedPlayers;
                for (int i = 0; i < currentPlayers.Count; i++)
                {
                    HandleRoleAssigned(currentPlayers[i]);
                }
            }

#if UNITY_EDITOR
            if (_spawnLegsInEditor && !_spawnedPlayers.Contains(-1))
            {
                bool hasLegs = false;
                if (_playerRegistry != null)
                {
                    var players = _playerRegistry.ConnectedPlayers;
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (players[i].Role == PlayerRole.Legs)
                        {
                            hasLegs = true;
                            break;
                        }
                    }
                }

                if (!hasLegs)
                {
                    SpawnPlayerForRole(-1, PlayerRole.Legs);
                }
            }

            if (_spawnTorsoInEditor && !_spawnedPlayers.Contains(-2))
            {
                bool hasTorso = false;
                if (_playerRegistry != null)
                {
                    var players = _playerRegistry.ConnectedPlayers;
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (players[i].Role == PlayerRole.Torso)
                        {
                            hasTorso = true;
                            break;
                        }
                    }
                }

                if (!hasTorso)
                {
                    SpawnPlayerForRole(-2, PlayerRole.Torso);
                }
            }
#endif
        }

        protected override void OnDestroy()
        {
            var nm = networkManager != null ? networkManager : NetworkManager.main;
            if (nm != null)
            {
                nm.onLocalPlayerReceivedID -= HandleLocalPlayerReceivedId;
                nm.onPlayerJoined -= HandleServerPlayerJoined;
            }

            if (_playerRegistry != null)
            {
                _playerRegistry.OnRoleAssigned -= HandleRoleAssigned;
            }

            base.OnDestroy();
        }

        private void HandleLocalPlayerReceivedId(PlayerID localPlayer)
        {
            if (!isServer) return;

            var legs = Object.FindFirstObjectByType<LegsController>();
            if (legs != null && legs.TryGetComponent<NetworkIdentity>(out var legsIdentity))
            {
                if (!legsIdentity.hasOwner)
                {
                    legsIdentity.GiveOwnership(localPlayer);
                }
            }
        }

        private void HandleServerPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            if (!asServer) return;

            var nm = networkManager != null ? networkManager : NetworkManager.main;
            bool isHost = player.isServer || (nm != null && nm.isLocalPlayerReady && nm.localPlayer == player);

            if (isHost)
            {
                var existingLegs = Object.FindFirstObjectByType<LegsController>();
                if (existingLegs != null && existingLegs.TryGetComponent<NetworkIdentity>(out var legsId))
                {
                    legsId.GiveOwnership(player);
                }
            }
            else
            {
                var existingTorso = Object.FindFirstObjectByType<TorsoController>();
                if (existingTorso != null && existingTorso.TryGetComponent<NetworkIdentity>(out var torsoId))
                {
                    torsoId.GiveOwnership(player);
                }
            }
        }

        [ServerRpc(requireOwnership: false)]
        public void RequestSwapRolesServerRpc()
        {
            SwapRolesOnServer();
        }

        public void SwapRolesOnServer()
        {
            if (!isServer) return;

            var legs = Object.FindFirstObjectByType<LegsController>();
            var torso = Object.FindFirstObjectByType<TorsoController>();

            if (legs == null || torso == null) return;

            if (!legs.TryGetComponent<NetworkIdentity>(out var legsId) ||
                !torso.TryGetComponent<NetworkIdentity>(out var torsoId)) return;

            var legsOwner = legsId.owner;
            var torsoOwner = torsoId.owner;

            if (legsOwner.HasValue && torsoOwner.HasValue)
            {
                legsId.GiveOwnership(torsoOwner.Value);
                torsoId.GiveOwnership(legsOwner.Value);
            }
            else if (legsOwner.HasValue && !torsoOwner.HasValue)
            {
                legsId.RemoveOwnership();
                torsoId.GiveOwnership(legsOwner.Value);
            }
            else if (!legsOwner.HasValue && torsoOwner.HasValue)
            {
                torsoId.RemoveOwnership();
                legsId.GiveOwnership(torsoOwner.Value);
            }
        }

        private void HandleRoleAssigned(PlayerSlot slot)
        {
            if (!isServer)
            {
                return;
            }

            if (slot.Role == PlayerRole.Legs)
            {
                var existingLegs = Object.FindFirstObjectByType<LegsController>();
                if (existingLegs != null && existingLegs.TryGetComponent<NetworkIdentity>(out var legsIdentity))
                {
                    legsIdentity.GiveOwnership(new PlayerID((ulong)slot.PlayerId, false));
                    _spawnedPlayers.Remove(-1);
                    _spawnedPlayers.Add(slot.PlayerId);
                    return;
                }
            }
            else if (slot.Role == PlayerRole.Torso)
            {
                var existingTorso = Object.FindFirstObjectByType<TorsoController>();
                if (existingTorso != null && existingTorso.TryGetComponent<NetworkIdentity>(out var torsoIdentity))
                {
                    torsoIdentity.GiveOwnership(new PlayerID((ulong)slot.PlayerId, false));
                    _spawnedPlayers.Remove(-2);
                    _spawnedPlayers.Add(slot.PlayerId);
                    return;
                }
            }

            if (!_spawnedPlayers.Contains(slot.PlayerId))
            {
                SpawnPlayerForRole(slot.PlayerId, slot.Role);
            }
        }

        public GameObject SpawnPlayerForRole(int playerId, PlayerRole role)
        {
            GameObject prefabToSpawn = null;
            if (role == PlayerRole.Legs)
            {
                prefabToSpawn = _legsPrefab;
            }
            else if (role == PlayerRole.Torso)
            {
                prefabToSpawn = _torsoPrefab;
            }

            if (prefabToSpawn == null)
            {
                return null;
            }

            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;

            if (_spawnPointManager != null)
            {
                var point = _spawnPointManager.GetSpawnPoint(role);
                if (point != null)
                {
                    spawnPos = point.Position;
                    spawnRot = point.Rotation;
                }
            }

            var nm = networkManager != null ? networkManager : NetworkManager.main;
            bool canNetworkSpawn = nm != null && nm.isServer && isSpawned;

            GameObject instance;
            if (canNetworkSpawn)
            {
                instance = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            }
            else
            {
                instance = UnityProxy.InstantiateDirectly(prefabToSpawn, spawnPos, spawnRot);
            }

            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(prefabToSpawn, spawnPos, spawnRot);
            }

            if (instance == null)
            {
                return null;
            }

            if (instance.TryGetComponent<NetworkIdentity>(out var identity))
            {
                if (canNetworkSpawn && nm != null && !identity.IsSpawned(nm.isServer))
                {
                    nm.Spawn(instance);
                }

                if (canNetworkSpawn && nm != null)
                {
                    if (playerId >= 0)
                    {
                        identity.GiveOwnership(new PlayerID((ulong)playerId, false));
                    }
                    else if (role == PlayerRole.Legs && nm.isLocalPlayerReady)
                    {
                        identity.GiveOwnership(nm.localPlayer);
                    }
                }
            }

            if (playerId >= 0 || playerId == -1 || playerId == -2)
            {
                _spawnedPlayers.Add(playerId);
            }

            return instance;
        }

        [Button("Spawn Piernas (Test)")]
        private void TestSpawnLegs()
        {
            SpawnPlayerForRole(-1, PlayerRole.Legs);
        }

        [Button("Spawn Torso (Test)")]
        private void TestSpawnTorso()
        {
            SpawnPlayerForRole(-2, PlayerRole.Torso);
        }
    }
}
