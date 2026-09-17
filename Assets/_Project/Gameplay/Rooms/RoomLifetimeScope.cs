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
        }

        private sealed class NullCameraCoordinator : ICameraCoordinator
        {
            public bool IsFused => false;
            public void RegisterTargets(Transform legs, Transform torso) { }
            public void SetFused(bool isFused) { }
            public void TriggerImpulse(Vector3 velocity, float force = 1f) { }
        }
    }
}
