using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Network.Services;
using PurrNet;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Network.Scopes
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private LevelManager _levelManager;

        protected override void Awake()
        {
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // Core Event Bus & Game State
            builder.Register<GameEventBus>(Lifetime.Singleton).As<IGameEventBus>();
            builder.Register<GameManager>(Lifetime.Singleton).As<IGameManager>();

            // Network Infrastructure
            builder.Register<NetworkService>(Lifetime.Singleton).As<INetworkService>();
            builder.Register<PlayerRegistry>(Lifetime.Singleton).As<IPlayerRegistry>();

            // Level Manager
            if (_levelManager != null)
            {
                builder.RegisterComponent(_levelManager).As<ILevelManager>();
            }
            else
            {
                builder.RegisterComponentInHierarchy<LevelManager>().As<ILevelManager>();
            }

            // Register NetworkManager instance if referenced or present
            if (_networkManager != null)
            {
                builder.RegisterComponent(_networkManager);
            }
        }
    }
}
