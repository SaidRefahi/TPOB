using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
using PurrNet;
using PurrNet.Transports;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Player.Robot
{
    [DisallowMultipleComponent]
    [DrawWithTriInspector]
    [DeclareBoxGroup("Referencias")]
    [DeclareBoxGroup("Configuración")]
    [DeclareBoxGroup("Estado Sincronizado")]
    [DeclareBoxGroup("Acciones")]
    [DeclareBoxGroup("Simulación de Muerte")]
    public sealed class RobotCoordinator : NetworkBehaviour, IRobotCoordinator
    {
        [Group("Referencias")]
        [SerializeField] private LegsController _legs;

        [Group("Referencias")]
        [SerializeField] private TorsoController _torso;

        [Group("Referencias")]
        [SerializeField] private FusionSocket _socket;

        [Group("Configuración")]
        [SerializeField] private float _maxFusionDistance = 3.5f;

        [Group("Estado Sincronizado")]
        [SerializeField] private SyncVar<bool> _isFused = new(false);

        private readonly SeparationStrategy _separationStrategy = new();
        private readonly FusionStrategy _fusionStrategy = new();

        private IRobotStrategy _currentStrategy;
        private RobotContext _context;
        private IGameEventBus _eventBus;

        public bool IsFused => _isFused.value;
        public LegsController Legs => _legs;
        public TorsoController Torso => _torso;
        public FusionSocket Socket => _socket;
        public IRobotStrategy CurrentStrategy => _currentStrategy;

        [Inject]
        public void Construct(IGameEventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        private void Awake()
        {
            _currentStrategy = _separationStrategy;
        }

        private void Start()
        {
            if (_legs == null)
            {
                _legs = FindFirstObjectByType<LegsController>();
            }

            if (_legs != null)
            {
                _legs.SetCoordinator(this);
                if (_socket == null)
                {
                    _socket = _legs.FusionSocket;
                }
            }

            if (_torso == null)
            {
                _torso = FindFirstObjectByType<TorsoController>();
            }

            if (_torso != null)
            {
                _torso.SetCoordinator(this);
            }

            RebuildContext();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            _isFused.onChanged += HandleFusedStateChanged;

            if (_isFused.value)
            {
                ApplyStrategy(_fusionStrategy);
            }
        }

        protected override void OnDespawned()
        {
            _isFused.onChanged -= HandleFusedStateChanged;
            base.OnDespawned();
        }

        protected override void OnDestroy()
        {
            _isFused.onChanged -= HandleFusedStateChanged;
            base.OnDestroy();
        }

        private void Update()
        {
            if (_currentStrategy != null && _context != null)
            {
                _currentStrategy.OnTick(_context, Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (_currentStrategy != null && _context != null)
            {
                _currentStrategy.OnFixedTick(_context, Time.fixedDeltaTime);
            }
        }

        public void RegisterLegs(LegsController legs)
        {
            _legs = legs;
            if (_legs != null)
            {
                _legs.SetCoordinator(this);
                if (_socket == null)
                {
                    _socket = _legs.FusionSocket;
                }
            }

            RebuildContext();
        }

        public void UnregisterLegs(LegsController legs)
        {
            if (_legs == legs)
            {
                _legs = null;
                RebuildContext();
            }
        }

        public void RegisterTorso(TorsoController torso)
        {
            _torso = torso;
            if (_torso != null)
            {
                _torso.SetCoordinator(this);
            }

            RebuildContext();
        }

        public void UnregisterTorso(TorsoController torso)
        {
            if (_torso == torso)
            {
                _torso = null;
                RebuildContext();
            }
        }

        public void SetSocket(FusionSocket socket)
        {
            _socket = socket;
            RebuildContext();
        }

        public bool CanFuse()
        {
            EnsureReferences();

            if (_isFused.value || _legs == null || _torso == null)
            {
                return false;
            }

            FusionSocket targetSocket = _socket != null ? _socket : _legs.FusionSocket;
            if (targetSocket == null)
            {
                return false;
            }

            Vector3 socketPos = targetSocket.AttachPoint.position;
            Vector3 torsoPos = _torso.transform.position;
            float sqrDist = (torsoPos - socketPos).sqrMagnitude;
            return sqrDist <= (_maxFusionDistance * _maxFusionDistance);
        }

        public void RequestFusion()
        {
            bool hasAuthority = !isSpawned || isServer;
            if (hasAuthority)
            {
                TryFuseOnServer();
            }
            else
            {
                RequestFusionServerRpc();
            }
        }

        [ServerRpc(Channel.ReliableOrdered)]
        private void RequestFusionServerRpc(RPCInfo info = default)
        {
            TryFuseOnServer();
        }

        private void TryFuseOnServer()
        {
            if (!CanFuse())
            {
                return;
            }

            _isFused.value = true;

            if (!isSpawned)
            {
                PerformFusionLocally();
            }
            else
            {
                OnFusedObserversRpc();
            }
        }

        [ObserversRpc(runLocally: true)]
        private void OnFusedObserversRpc()
        {
            PerformFusionLocally();
        }

        private void PerformFusionLocally()
        {
            RebuildContext();
            ApplyStrategy(_fusionStrategy);

            Vector3 pos = _socket != null ? _socket.AttachPoint.position : transform.position;
            _eventBus?.Publish(new RobotFusedEvent(pos));
        }

        public void RequestSeparation()
        {
            bool hasAuthority = !isSpawned || isServer;
            if (hasAuthority)
            {
                TrySeparateOnServer();
            }
            else
            {
                RequestSeparationServerRpc();
            }
        }

        [ServerRpc(Channel.ReliableOrdered)]
        private void RequestSeparationServerRpc(RPCInfo info = default)
        {
            TrySeparateOnServer();
        }

        private void TrySeparateOnServer()
        {
            if (!_isFused.value)
            {
                return;
            }

            _isFused.value = false;

            Vector3 offset = _socket != null ? _socket.SeparationOffset : new Vector3(0f, 0.25f, 1.5f);
            Vector3 sepPos = _legs != null
                ? _legs.transform.position + _legs.transform.forward * offset.z + Vector3.up * offset.y
                : transform.position;

            if (!isSpawned)
            {
                PerformSeparationLocally(sepPos);
            }
            else
            {
                OnSeparatedObserversRpc(sepPos);
            }
        }

        [ObserversRpc(runLocally: true)]
        private void OnSeparatedObserversRpc(Vector3 separationPosition)
        {
            PerformSeparationLocally(separationPosition);
        }

        private void PerformSeparationLocally(Vector3 separationPosition)
        {
            RebuildContext();
            ApplyStrategy(_separationStrategy);

            _eventBus?.Publish(new RobotSeparatedEvent(separationPosition));
        }

        private void HandleFusedStateChanged(bool isFused)
        {
            if (isFused && (_currentStrategy == null || !_currentStrategy.IsFused))
            {
                PerformFusionLocally();
            }
            else if (!isFused && (_currentStrategy != null && _currentStrategy.IsFused))
            {
                Vector3 offset = _socket != null ? _socket.SeparationOffset : new Vector3(0f, 0.25f, 1.5f);
                Vector3 sepPos = _legs != null
                    ? _legs.transform.position + _legs.transform.forward * offset.z + Vector3.up * offset.y
                    : transform.position;
                PerformSeparationLocally(sepPos);
            }
        }

        private void ApplyStrategy(IRobotStrategy newStrategy)
        {
            if (_currentStrategy == newStrategy)
            {
                return;
            }

            _currentStrategy?.OnExit(_context);
            _currentStrategy = newStrategy;
            _currentStrategy?.OnEnter(_context);
        }

        private void EnsureReferences()
        {
            if (_legs == null)
            {
                _legs = FindFirstObjectByType<LegsController>();
            }

            if (_legs != null && _socket == null)
            {
                _socket = _legs.FusionSocket;
            }

            if (_torso == null)
            {
                _torso = FindFirstObjectByType<TorsoController>();
            }
        }

        private void RebuildContext()
        {
            EnsureReferences();

            if (_legs == null || _torso == null)
            {
                _context = null;
                return;
            }

            if (_socket == null && _legs.FusionSocket != null)
            {
                _socket = _legs.FusionSocket;
            }

            Rigidbody legsRb = _legs.GetComponent<Rigidbody>();
            Rigidbody torsoRb = _torso.GetComponent<Rigidbody>();

            float legsMass = legsRb != null ? legsRb.mass : 70f;
            float torsoMass = torsoRb != null ? torsoRb.mass : 40f;
            bool canSimulate = !isSpawned || isServer;

            _context = new RobotContext(
                _legs,
                _torso,
                _socket,
                legsRb,
                torsoRb,
                legsMass,
                torsoMass,
                canSimulate
            );
        }

        [Group("Acciones")]
        [Button(ButtonSizes.Large, "Simular Fusión")]
        public void SimulateFusion()
        {
            EnsureReferences();
            if (!CanFuse())
            {
                Debug.LogWarning("[RobotCoordinator] Cannot fuse: distance too far, already fused, or missing references.");
            }
            RequestFusion();
        }

        [Group("Acciones")]
        [Button(ButtonSizes.Large, "Simular Separación")]
        public void SimulateSeparation()
        {
            EnsureReferences();
            RequestSeparation();
        }

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Muerte Piernas")]
        public void SimulateLegsDeath()
        {
            EnsureReferences();
            var pos = _legs != null ? _legs.transform.position : transform.position;
            if (_eventBus != null)
            {
                _eventBus.Publish(new PlayerDiedEvent(1, PlayerRole.Legs, pos, DeathCause.Hazard));
            }
            else
            {
                var relay = Object.FindFirstObjectByType<Game.Network.Events.NetworkEventRelay>();
                relay?.BroadcastPlayerDied(new PlayerDiedEvent(1, PlayerRole.Legs, pos, DeathCause.Hazard));
            }
        }

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Muerte Torso")]
        public void SimulateTorsoDeath()
        {
            EnsureReferences();
            var pos = _torso != null ? _torso.transform.position : transform.position;
            if (_eventBus != null)
            {
                _eventBus.Publish(new PlayerDiedEvent(2, PlayerRole.Torso, pos, DeathCause.Hazard));
            }
            else
            {
                var relay = Object.FindFirstObjectByType<Game.Network.Events.NetworkEventRelay>();
                relay?.BroadcastPlayerDied(new PlayerDiedEvent(2, PlayerRole.Torso, pos, DeathCause.Hazard));
            }
        }

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Respawn Piernas")]
        public void SimulateLegsRespawn()
        {
            EnsureReferences();
            var pos = _legs != null ? _legs.transform.position : transform.position;
            if (_eventBus != null)
            {
                _eventBus.Publish(new PlayerRespawnedEvent(1, PlayerRole.Legs, pos));
            }
            else
            {
                var relay = Object.FindFirstObjectByType<Game.Network.Events.NetworkEventRelay>();
                relay?.BroadcastPlayerRespawned(new PlayerRespawnedEvent(1, PlayerRole.Legs, pos));
            }
        }

        [Group("Simulación de Muerte")]
        [Button(ButtonSizes.Medium, "Simular Respawn Torso")]
        public void SimulateTorsoRespawn()
        {
            EnsureReferences();
            var pos = _torso != null ? _torso.transform.position : transform.position;
            if (_eventBus != null)
            {
                _eventBus.Publish(new PlayerRespawnedEvent(2, PlayerRole.Torso, pos));
            }
            else
            {
                var relay = Object.FindFirstObjectByType<Game.Network.Events.NetworkEventRelay>();
                relay?.BroadcastPlayerRespawned(new PlayerRespawnedEvent(2, PlayerRole.Torso, pos));
            }
        }
    }
}
