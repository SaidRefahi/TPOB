using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Network.Injection
{
    public interface INetworkPrefabInjector
    {
        GameObject InstantiateAndInject(GameObject prefab, Vector3 position, Quaternion rotation);
        void InjectExisting(GameObject instance);
    }

    public sealed class NetworkPrefabInjector : INetworkPrefabInjector
    {
        private readonly IObjectResolver _container;

        public NetworkPrefabInjector(IObjectResolver container)
        {
            _container = container;
        }

        public GameObject InstantiateAndInject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(prefab, position, rotation);
            _container.InjectGameObject(instance);
            return instance;
        }

        public void InjectExisting(GameObject instance)
        {
            if (instance != null)
            {
                _container.InjectGameObject(instance);
            }
        }
    }
}
