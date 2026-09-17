using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Network.Events
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración")]
    [DeclareBoxGroup("Simulación")]
    public sealed class NetworkEventRelay : NetworkBehaviour, INetworkEventRelay
    {
        private IGameEventBus _eventBus;
        private IPlayerRegistry _playerRegistry;
        private bool _isRelayingFromNetwork;

        [Inject]
        public void Construct(IGameEventBus eventBus = null, IPlayerRegistry playerRegistry = null)
        {
            _eventBus = eventBus;
            _playerRegistry = playerRegistry;
        }

        [ServerRpc(requireOwnership: false)]
        public void RequestSwapRolesServerRpc()
        {
            if (!isServer) return;

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

            _playerRegistry?.SwapRoles();
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe<RobotFusedEvent>(OnLocalRobotFused);
                _eventBus.Subscribe<RobotSeparatedEvent>(OnLocalRobotSeparated);
                _eventBus.Subscribe<RoomCompletedEvent>(OnLocalRoomCompleted);
                _eventBus.Subscribe<PlayerDiedEvent>(OnLocalPlayerDied);
                _eventBus.Subscribe<PlayerRespawnedEvent>(OnLocalPlayerRespawned);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<RobotFusedEvent>(OnLocalRobotFused);
                _eventBus.Unsubscribe<RobotSeparatedEvent>(OnLocalRobotSeparated);
                _eventBus.Unsubscribe<RoomCompletedEvent>(OnLocalRoomCompleted);
                _eventBus.Unsubscribe<PlayerDiedEvent>(OnLocalPlayerDied);
                _eventBus.Unsubscribe<PlayerRespawnedEvent>(OnLocalPlayerRespawned);
            }
        }

        #region Event Handlers (Local to Network)

        private void OnLocalRobotFused(RobotFusedEvent evt)
        {
            if (_isRelayingFromNetwork || !isSpawned || !isServer) return;
            BroadcastRobotFusedObserversRpc(evt);
        }

        private void OnLocalRobotSeparated(RobotSeparatedEvent evt)
        {
            if (_isRelayingFromNetwork || !isSpawned || !isServer) return;
            BroadcastRobotSeparatedObserversRpc(evt);
        }

        private void OnLocalRoomCompleted(RoomCompletedEvent evt)
        {
            if (_isRelayingFromNetwork || !isSpawned || !isServer) return;
            BroadcastRoomCompletedObserversRpc(evt);
        }

        private void OnLocalPlayerDied(PlayerDiedEvent evt)
        {
            if (_isRelayingFromNetwork || !isSpawned || !isServer) return;
            BroadcastPlayerDiedObserversRpc(evt);
        }

        private void OnLocalPlayerRespawned(PlayerRespawnedEvent evt)
        {
            if (_isRelayingFromNetwork || !isSpawned || !isServer) return;
            BroadcastPlayerRespawnedObserversRpc(evt);
        }

        #endregion

        #region RPCs (Network to Local EventBus)

        [ObserversRpc(runLocally: false)]
        private void BroadcastRobotFusedObserversRpc(RobotFusedEvent evt)
        {
            PublishToLocalBus(evt);
        }

        [ObserversRpc(runLocally: false)]
        private void BroadcastRobotSeparatedObserversRpc(RobotSeparatedEvent evt)
        {
            PublishToLocalBus(evt);
        }

        [ObserversRpc(runLocally: false, bufferLast: true)]
        private void BroadcastRoomCompletedObserversRpc(RoomCompletedEvent evt)
        {
            PublishToLocalBus(evt);
        }

        [ObserversRpc(runLocally: false)]
        private void BroadcastPlayerDiedObserversRpc(PlayerDiedEvent evt)
        {
            PublishToLocalBus(evt);
        }

        [ObserversRpc(runLocally: false)]
        private void BroadcastPlayerRespawnedObserversRpc(PlayerRespawnedEvent evt)
        {
            PublishToLocalBus(evt);
        }

        private void PublishToLocalBus<T>(T eventData)
        {
            if (_eventBus == null) return;

            _isRelayingFromNetwork = true;
            try
            {
                _eventBus.Publish(eventData);
            }
            finally
            {
                _isRelayingFromNetwork = false;
            }
        }

        #endregion

        #region INetworkEventRelay Explicit API

        public void BroadcastPlayerDied(PlayerDiedEvent evt)
        {
            _eventBus?.Publish(evt);
        }

        public void BroadcastPlayerRespawned(PlayerRespawnedEvent evt)
        {
            _eventBus?.Publish(evt);
        }

        public void BroadcastRoomCompleted(RoomCompletedEvent evt)
        {
            _eventBus?.Publish(evt);
        }

        public void BroadcastRobotFused(RobotFusedEvent evt)
        {
            _eventBus?.Publish(evt);
        }

        public void BroadcastRobotSeparated(RobotSeparatedEvent evt)
        {
            _eventBus?.Publish(evt);
        }

        #endregion

        #region Tri-Inspector Debug Simulation

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Muerte Piernas")]
        public void SimulatePlayerDiedLegs()
        {
            BroadcastPlayerDied(new PlayerDiedEvent(1, PlayerRole.Legs, transform.position, DeathCause.Hazard));
        }

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Muerte Torso")]
        public void SimulatePlayerDiedTorso()
        {
            BroadcastPlayerDied(new PlayerDiedEvent(2, PlayerRole.Torso, transform.position, DeathCause.Hazard));
        }

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Respawn Piernas")]
        public void SimulatePlayerRespawnedLegs()
        {
            BroadcastPlayerRespawned(new PlayerRespawnedEvent(1, PlayerRole.Legs, transform.position + Vector3.up));
        }

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Respawn Torso")]
        public void SimulatePlayerRespawnedTorso()
        {
            BroadcastPlayerRespawned(new PlayerRespawnedEvent(2, PlayerRole.Torso, transform.position + Vector3.up));
        }

        [Group("Simulación")]
        [Button(ButtonSizes.Medium, "Simular Completar Sala")]
        public void SimulateRoomCompleted()
        {
            BroadcastRoomCompleted(new RoomCompletedEvent(1, 45.2f));
        }

        [Group("Simulación")]
        [Button(ButtonSizes.Medium, "Simular Fusión")]
        public void SimulateRobotFused()
        {
            BroadcastRobotFused(new RobotFusedEvent(transform.position));
        }

        [Group("Simulación")]
        [Button(ButtonSizes.Medium, "Simular Separación")]
        public void SimulateRobotSeparated()
        {
            BroadcastRobotSeparated(new RobotSeparatedEvent(transform.position));
        }

        #endregion
    }
}
