using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Gameplay.Spawning;
using Game.Network.Scopes;
using Game.Network.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    public class RoomLifetimeScope : LifetimeScope
    {
        [SerializeField] private RoomController _roomController;
        [SerializeField] private SpawnPointManager _spawnPointManager;
        [SerializeField] private TPOBPlayerSpawner _playerSpawner;

        protected override LifetimeScope FindParent()
        {
            return FindFirstObjectByType<GameLifetimeScope>();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (FindParent() == null)
            {
                builder.Register<GameEventBus>(Lifetime.Singleton).As<IGameEventBus>();
                builder.Register<NetworkService>(Lifetime.Singleton).As<INetworkService>();
                builder.Register<PlayerRegistry>(Lifetime.Singleton).As<IPlayerRegistry>();
            }

            if (_roomController != null)
            {
                builder.RegisterComponent(_roomController).As<IRoomController>();
            }

            if (_spawnPointManager != null)
            {
                builder.RegisterComponent(_spawnPointManager);
            }

            if (_playerSpawner != null)
            {
                builder.RegisterComponent(_playerSpawner);
            }
            else
            {
                builder.RegisterComponentInHierarchy<TPOBPlayerSpawner>();
            }
        }
    }
}
