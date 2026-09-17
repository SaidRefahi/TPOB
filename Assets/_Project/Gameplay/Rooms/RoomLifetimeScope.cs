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
        [SerializeField] private Game.Gameplay.Player.Robot.RobotCoordinator _robotCoordinator;
        [SerializeField] private Game.Gameplay.Camera.CameraController _cameraController;
        [SerializeField] private Game.Network.Events.NetworkEventRelay _networkEventRelay;

        protected override LifetimeScope FindParent()
        {
            var parent = FindFirstObjectByType<GameLifetimeScope>();
            if (parent != null)
            {
                if (parent.Container == null)
                {
                    try
                    {
                        parent.Build();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[RoomLifetimeScope] Parent GameLifetimeScope.Build() deferred or failed: {ex.Message}");
                    }
                }

                if (parent.Container != null)
                {
                    return parent;
                }
            }
            return null;
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

            var playerSpawner = _playerSpawner != null ? _playerSpawner : FindFirstObjectByType<TPOBPlayerSpawner>();
            if (playerSpawner != null)
            {
                builder.RegisterComponent(playerSpawner);
            }

            var robotCoordinator = _robotCoordinator != null ? _robotCoordinator : FindFirstObjectByType<Game.Gameplay.Player.Robot.RobotCoordinator>();
            if (robotCoordinator != null)
            {
                builder.RegisterComponent(robotCoordinator).As<IRobotCoordinator>();
            }

            var cameraController = _cameraController != null ? _cameraController : FindFirstObjectByType<Game.Gameplay.Camera.CameraController>();
            if (cameraController != null)
            {
                builder.RegisterComponent(cameraController).As<ICameraCoordinator>();
            }
            else
            {
                builder.Register<NullCameraCoordinator>(Lifetime.Scoped).As<ICameraCoordinator>();
            }

            var eventRelay = _networkEventRelay != null ? _networkEventRelay : FindFirstObjectByType<Game.Network.Events.NetworkEventRelay>();
            if (eventRelay != null)
            {
                builder.RegisterComponent(eventRelay).As<INetworkEventRelay>();
            }
            else
            {
                builder.Register<NullNetworkEventRelay>(Lifetime.Scoped).As<INetworkEventRelay>();
            }

            var hud = FindFirstObjectByType<Game.Network.UI.ConnectionHUD>();
            if (hud != null)
            {
                builder.RegisterComponent(hud);
            }

            var roomCompletion = FindFirstObjectByType<RoomCompletion>();
            if (roomCompletion != null)
            {
                builder.RegisterComponent(roomCompletion);
            }

            var checkpointSystem = FindFirstObjectByType<Game.Gameplay.Checkpoints.CheckpointSystem>();
            if (checkpointSystem != null)
            {
                builder.RegisterComponent(checkpointSystem).As<ICheckpointSystem>();
            }

            var respawnCoordinator = FindFirstObjectByType<Game.Gameplay.Player.Death.RespawnCoordinator>();
            if (respawnCoordinator != null)
            {
                builder.RegisterComponent(respawnCoordinator).As<IRespawnCoordinator>();
            }
        }

        private sealed class NullCameraCoordinator : ICameraCoordinator
        {
            public bool IsFused => false;
            public void RegisterTargets(Transform legs, Transform torso) { }
            public void SetFused(bool isFused) { }
            public void TriggerImpulse(Vector3 velocity, float force = 1f) { }
        }

        private sealed class NullNetworkEventRelay : INetworkEventRelay
        {
            public void BroadcastPlayerDied(PlayerDiedEvent evt) { }
            public void BroadcastPlayerRespawned(PlayerRespawnedEvent evt) { }
            public void BroadcastRoomCompleted(RoomCompletedEvent evt) { }
            public void BroadcastRobotFused(RobotFusedEvent evt) { }
            public void BroadcastRobotSeparated(RobotSeparatedEvent evt) { }
        }
    }
}
