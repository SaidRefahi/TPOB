using System;
using DG.Tweening;
using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Player.Torso;
using PurrNet;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Player.Death
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración")]
    [DeclareBoxGroup("Referencias")]
    public sealed class PlayerDeathHandler : NetworkBehaviour, IDamageable
    {
        [Group("Configuración")]
        [SerializeField] private PlayerRole _role;

        [Group("Referencias")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Referencias")]
        [SerializeField] private GameObject _visualModel;

        [Group("Referencias")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        private IGameEventBus _eventBus;
        private IRespawnCoordinator _respawnCoordinator;
        private IRobotCoordinator _robotCoordinator;
        private bool _isAlive = true;
        private Tween _respawnTween;

        public bool IsAlive => _isAlive;
        public PlayerRole Role => _role;

        public event Action<DeathCause> OnDied;
        public event Action OnRespawned;

        [Inject]
        public void Construct(
            IGameEventBus eventBus = null,
            IRespawnCoordinator respawnCoordinator = null,
            IRobotCoordinator robotCoordinator = null)
        {
            _eventBus = eventBus;
            _respawnCoordinator = respawnCoordinator;
            _robotCoordinator = robotCoordinator;
        }

        private void Awake()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_impulseSource == null)
            {
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            }

            if (_visualModel == null)
            {
                var mesh = transform.Find(_role == PlayerRole.Legs ? "LegsMesh" : "TorsoMesh");
                if (mesh != null)
                {
                    _visualModel = mesh.gameObject;
                }
            }
        }

        protected override void OnDestroy()
        {
            _respawnTween?.Kill();
            base.OnDestroy();
        }

        public void Kill(DeathCause cause = DeathCause.Hazard)
        {
            if (!_isAlive)
            {
                return;
            }

            if (isSpawned && !isServer)
            {
                KillServerRpc(cause);
                return;
            }

            _isAlive = false;

            // 1. Separate if fused
            if (_robotCoordinator != null && _robotCoordinator.IsFused)
            {
                _robotCoordinator.RequestSeparation();
            }

            // 2. Drop held object if Torso
            if (TryGetComponent<TorsoController>(out var torso))
            {
                torso.ReleaseHeldObject();
            }

            // 3. Freeze physics
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = true;
            }

            // 4. Publish Event
            int pId = isSpawned && owner.HasValue ? (int)owner.Value.id.value : (_role == PlayerRole.Legs ? 1 : 2);
            var diedEvent = new PlayerDiedEvent(pId, _role, transform.position, cause);
            _eventBus?.Publish(diedEvent);

            // 5. Notify Observers (Visuals + FX)
            if (!isSpawned)
            {
                UpdateDeathVisuals(transform.position, cause);
            }
            else
            {
                NotifyDeathObserversRpc(transform.position, cause);
            }

            // 6. Schedule Respawn
            if (_respawnCoordinator == null)
            {
                _respawnCoordinator = FindFirstObjectByType<RespawnCoordinator>();
            }

            _respawnCoordinator?.RequestRespawn(this, _role, 1.0f);
        }

        [ServerRpc(requireOwnership: false)]
        private void KillServerRpc(DeathCause cause)
        {
            Kill(cause);
        }

        public void Respawn(Vector3 position, Quaternion rotation)
        {
            if (isSpawned && !isServer)
            {
                return;
            }

            _isAlive = true;

            transform.position = position;
            transform.rotation = rotation;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = !isServer;
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.position = position;
                _rigidbody.rotation = rotation;
            }

            int pId = isSpawned && owner.HasValue ? (int)owner.Value.id.value : (_role == PlayerRole.Legs ? 1 : 2);
            var respawnEvent = new PlayerRespawnedEvent(pId, _role, position);
            _eventBus?.Publish(respawnEvent);

            if (!isSpawned)
            {
                UpdateRespawnVisuals(position, rotation);
            }
            else
            {
                NotifyRespawnObserversRpc(position, rotation);
            }
        }

        private void UpdateDeathVisuals(Vector3 position, DeathCause cause)
        {
            _isAlive = false;

            if (_visualModel != null)
            {
                _visualModel.SetActive(false);
            }

            if (_impulseSource != null)
            {
                _impulseSource.GenerateImpulse(0.8f);
            }

            OnDied?.Invoke(cause);
        }

        private void UpdateRespawnVisuals(Vector3 position, Quaternion rotation)
        {
            _isAlive = true;

            transform.position = position;
            transform.rotation = rotation;

            if (_visualModel != null)
            {
                _visualModel.SetActive(true);
                _respawnTween?.Kill();
                _visualModel.transform.localScale = Vector3.zero;
                _respawnTween = _visualModel.transform.DOScale(Vector3.one * (_role == PlayerRole.Torso ? 0.85f : 1f), 0.35f).SetEase(Ease.OutBack);
            }

            OnRespawned?.Invoke();
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void NotifyDeathObserversRpc(Vector3 position, DeathCause cause)
        {
            UpdateDeathVisuals(position, cause);
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void NotifyRespawnObserversRpc(Vector3 position, Quaternion rotation)
        {
            UpdateRespawnVisuals(position, rotation);
        }
    }
}
