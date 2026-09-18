using Game.Core.Audio;
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

        public static GameLifetimeScope Instance { get; private set; }

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                var nms = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
                for (int i = 0; i < nms.Length; i++)
                {
                    if (nms[i] != null && nms[i] != Instance._networkManager && nms[i] != NetworkManager.main)
                    {
                        Destroy(nms[i].gameObject);
                    }
                }

                if (transform.parent != null && transform.parent.name == "--- NETWORKING ---")
                {
                    Destroy(transform.parent.gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
                return;
            }

            Instance = this;

            var nm = _networkManager != null ? _networkManager : FindFirstObjectByType<NetworkManager>();
            if (nm != null)
            {
                _networkManager = nm;
                if (nm.transform.parent != null)
                {
                    nm.transform.SetParent(null);
                }
            }

            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnDestroy();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            // Core Event Bus & Game State
            builder.Register<GameEventBus>(Lifetime.Singleton).As<IGameEventBus>();
            builder.Register<GameManager>(Lifetime.Singleton).As<IGameManager>();

            // Audio Service
            var audioService = FindFirstObjectByType<AudioService>();
            if (audioService == null)
            {
                audioService = gameObject.AddComponent<AudioService>();
            }
            builder.RegisterComponent(audioService).As<IAudioService>();

            // Network Infrastructure
            builder.Register<NetworkService>(Lifetime.Singleton).As<INetworkService>();
            builder.Register<PlayerRegistry>(Lifetime.Singleton).As<IPlayerRegistry>();

            // Level Manager
            var levelManager = _levelManager != null ? _levelManager : FindFirstObjectByType<LevelManager>();
            if (levelManager != null)
            {
                builder.RegisterComponent(levelManager).As<ILevelManager>();
            }

            // Register NetworkManager instance if referenced or present
            var networkManager = _networkManager != null ? _networkManager : FindFirstObjectByType<NetworkManager>();
            if (networkManager != null)
            {
                builder.RegisterComponent(networkManager);
            }

            // Connection HUD
            var hud = FindFirstObjectByType<Game.Network.UI.ConnectionHUD>();
            if (hud != null)
            {
                builder.RegisterComponent(hud);
            }
        }
    }
}
