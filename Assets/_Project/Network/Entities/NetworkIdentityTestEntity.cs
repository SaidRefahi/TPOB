using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Network.Entities
{
    [DeclareBoxGroup("Network State")]
    public sealed class NetworkIdentityTestEntity : NetworkBehaviour
    {
        [Group("Network State")]
        [SerializeField, ReadOnly] private string _entityName = "TestEntity";

        [Group("Network State")]
        [ShowInInspector]
        public bool IsSpawnedState => isSpawned;

        [Group("Network State")]
        [ShowInInspector]
        public bool IsServerState => isServer;

        [Group("Network State")]
        [ShowInInspector]
        public bool IsOwnerState => isOwner;

        [Group("Network State")]
        [ShowInInspector]
        public int? OwnerId => owner.HasValue ? (int)owner.Value.id.value : null;

        protected override void OnSpawned()
        {
            base.OnSpawned();
            gameObject.name = $"{_entityName}_[ID:{id}]";
        }

        [Button("Ping Server Log")]
        private void PingServer()
        {
            if (isServer)
            {
                Debug.Log($"[NetworkIdentityTestEntity] Server Ping on ID: {id}");
            }
        }
    }
}
