using System.Collections.Generic;
using Game.Core.Enums;
using Game.Core.Interfaces;
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
            bool isOffline = nm == null || (!nm.isServer && !nm.isClient);

            if (isOffline)
            {
                if (_spawnLegsInEditor)
                {
                    SpawnPlayerForRole(-1, PlayerRole.Legs);
                }

                if (_spawnTorsoInEditor)
                {
                    SpawnPlayerForRole(-1, PlayerRole.Torso);
                }
            }
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (!isServer)
            {
                return;
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
        }

        protected override void OnDestroy()
        {
            if (_playerRegistry != null)
            {
                _playerRegistry.OnRoleAssigned -= HandleRoleAssigned;
            }

            base.OnDestroy();
        }

        private void HandleRoleAssigned(PlayerSlot slot)
        {
            if (!isServer || _spawnedPlayers.Contains(slot.PlayerId))
            {
                return;
            }

            SpawnPlayerForRole(slot.PlayerId, slot.Role);
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

            var instance = Instantiate(prefabToSpawn, spawnPos, spawnRot);
            var nm = networkManager != null ? networkManager : NetworkManager.main;

            if (instance.TryGetComponent<NetworkIdentity>(out var identity))
            {
                if (nm != null && nm.isServer && !identity.IsSpawned(nm.isServer))
                {
                    nm.Spawn(instance);
                }

                if (playerId >= 0)
                {
                    identity.GiveOwnership(new PlayerID((ulong)playerId, false));
                    _spawnedPlayers.Add(playerId);
                }
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
            SpawnPlayerForRole(-1, PlayerRole.Torso);
        }
    }
}
