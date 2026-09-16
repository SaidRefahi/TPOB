using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Network.Scopes
{
    [DefaultExecutionOrder(-990)]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManagerPrefab;
        [SerializeField] private GameObject _testEntityPrefab;

        private INetworkService _networkService;
        private IPlayerRegistry _playerRegistry;

        [Inject]
        public void Construct(INetworkService networkService, IPlayerRegistry playerRegistry)
        {
            _networkService = networkService;
            _playerRegistry = playerRegistry;
        }

        private void Start()
        {
            EnsureNetworkManager();
        }

        private void EnsureNetworkManager()
        {
            if (NetworkManager.main == null && _networkManagerPrefab != null)
            {
                Instantiate(_networkManagerPrefab);
            }
        }

        [Button("Spawn Test Entity (Server Only)")]
        public void SpawnTestEntity()
        {
            if (_networkService != null && _networkService.IsServer && _testEntityPrefab != null)
            {
                var instance = Instantiate(_testEntityPrefab, Vector3.zero, Quaternion.identity);
                if (instance.TryGetComponent<NetworkIdentity>(out var identity))
                {
                    // PurrNet automatically spawns or registers scene identities
                }
            }
        }
    }
}
