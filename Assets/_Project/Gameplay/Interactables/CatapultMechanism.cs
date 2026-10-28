using DG.Tweening;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Gameplay.Player.Robot;
using PurrNet;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Catapulta")]
    [DeclareBoxGroup("Física de Lanzamiento")]
    [DeclareBoxGroup("Referencias Visuales")]
    [DeclareBoxGroup("Estado")]
    public sealed class CatapultMechanism : NetworkBehaviour, IGrabbable, ICatapultMechanism
    {
        [Group("Configuración de Catapulta")]
        [SerializeField] private Transform _anchorPoint;

        [Group("Configuración de Catapulta")]
        [SerializeField] private Transform _strapHandle;

        [Group("Configuración de Catapulta")]
        [SerializeField] private float _maxPullDistance = 7f;

        [Group("Configuración de Catapulta")]
        [SerializeField] private float _springResistance = 35f;

        [Group("Configuración de Catapulta")]
        [SerializeField] private float _minTensionThreshold = 0.1f;

        [Group("Física de Lanzamiento")]
        [SerializeField] private float _minLaunchSpeed = 16f;

        [Group("Física de Lanzamiento")]
        [SerializeField] private float _maxLaunchSpeed = 32f;

        [Group("Física de Lanzamiento")]
        [SerializeField] private float _launchElevationAngle = 26f;

        [Group("Referencias Visuales")]
        [SerializeField] private LineRenderer _strapLeft;

        [Group("Referencias Visuales")]
        [SerializeField] private LineRenderer _strapRight;

        [Group("Referencias Visuales")]
        [SerializeField] private Transform _postLeft;

        [Group("Referencias Visuales")]
        [SerializeField] private Transform _postRight;

        [Group("Referencias Visuales")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Group("Estado")]
        [SerializeField] private SyncVar<bool> _isGrabbed = new(false);

        [Group("Estado")]
        [SerializeField] private SyncVar<float> _currentTensionRatio = new(0f);

        private IGameEventBus _eventBus;
        private RobotCoordinator _attachedCoordinator;
        private Game.Gameplay.Player.Torso.TorsoController _heldTorso;
        private Transform _heldSocket;
        private Vector3 _handleDefaultLocalPos;

        public bool IsGrabbed => _isGrabbed.value;
        public bool IsTensioned => _isGrabbed.value && _currentTensionRatio.value >= _minTensionThreshold;
        public Transform Transform => _strapHandle != null ? _strapHandle : transform;
        public Rigidbody Rigidbody => null;
        public float CurrentTensionRatio => _currentTensionRatio.value;

        [Inject]
        public void Construct(IGameEventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        private void Awake()
        {
            if (_anchorPoint == null)
            {
                _anchorPoint = transform;
            }

            if (_strapHandle == null)
            {
                _strapHandle = transform;
            }

            _handleDefaultLocalPos = _strapHandle.localPosition;

            if (_impulseSource == null)
            {
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            }

            UpdateStrapLines();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            _isGrabbed.onChanged += HandleGrabbedStateChanged;
        }

        protected override void OnDespawned()
        {
            _isGrabbed.onChanged -= HandleGrabbedStateChanged;

            if (_attachedCoordinator != null)
            {
                _attachedCoordinator.UnregisterCatapult(this);
                _attachedCoordinator = null;
            }

            base.OnDespawned();
        }

        private void FixedUpdate()
        {
            if (_isGrabbed.value && _heldSocket != null)
            {
                _strapHandle.position = _heldSocket.position;
                UpdateStrapLines();
            }

            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (!_isGrabbed.value)
            {
                return;
            }

            if (_attachedCoordinator == null || !_attachedCoordinator.IsFused)
            {
                if (_heldTorso != null && _heldTorso.IsFused)
                {
                    var coordinator = _heldTorso.Coordinator;
                    if (coordinator != null && coordinator.IsFused)
                    {
                        _attachedCoordinator = coordinator;
                        _attachedCoordinator.RegisterCatapult(this);
                    }
                }

                if (_attachedCoordinator == null || !_attachedCoordinator.IsFused)
                {
                    return;
                }
            }

            Vector3 anchorPos = _anchorPoint.position;
            Vector3 robotPos = _attachedCoordinator.LaunchOriginPosition;
            Vector3 offset = new Vector3(anchorPos.x - robotPos.x, 0f, anchorPos.z - robotPos.z);

            float sqrDist = offset.sqrMagnitude;
            if (sqrDist < 0.04f)
            {
                _currentTensionRatio.value = 0f;
                return;
            }

            float dist = Mathf.Sqrt(sqrDist);
            _currentTensionRatio.value = Mathf.Clamp01(dist / _maxPullDistance);

            Vector3 springForce = offset.normalized * (_currentTensionRatio.value * _springResistance);
            _attachedCoordinator.ApplyCatapultResistance(springForce);
        }

        public void OnGrabbed(Transform holdSocket)
        {
            _heldSocket = holdSocket;
            _heldTorso = holdSocket != null ? holdSocket.GetComponentInParent<Game.Gameplay.Player.Torso.TorsoController>() : null;
            _isGrabbed.value = true;

            var coordinator = _heldTorso != null ? _heldTorso.Coordinator : UnityEngine.Object.FindFirstObjectByType<RobotCoordinator>();
            if (coordinator != null && coordinator.IsFused)
            {
                _attachedCoordinator = coordinator;
                _attachedCoordinator.RegisterCatapult(this);
            }
        }

        public void OnReleased(Vector3 throwVelocity)
        {
            if (_attachedCoordinator != null)
            {
                _attachedCoordinator.UnregisterCatapult(this);
                _attachedCoordinator = null;
            }

            _isGrabbed.value = false;
            _currentTensionRatio.value = 0f;
            _heldSocket = null;
            _heldTorso = null;
            _heldTorso = null;

            ResetStrapVisuals();
        }

        public void TriggerLaunch()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (!_isGrabbed.value || _attachedCoordinator == null || !_attachedCoordinator.IsFused)
            {
                return;
            }

            Vector3 forward = _anchorPoint.position - _attachedCoordinator.LaunchOriginPosition;
            forward.y = 0f;
            Vector3 forwardNorm = forward.sqrMagnitude > 0.001f ? forward.normalized : -_anchorPoint.forward;

            float elevationRad = Mathf.Clamp(_launchElevationAngle, 10f, 60f) * Mathf.Deg2Rad;
            Vector3 launchDir = (forwardNorm + Vector3.up * Mathf.Tan(elevationRad)).normalized;

            float launchSpeed = Mathf.Lerp(_minLaunchSpeed, _maxLaunchSpeed, _currentTensionRatio.value);
            Vector3 launchImpulse = launchDir * launchSpeed;

            var coordinatorToUnregister = _attachedCoordinator;
            coordinatorToUnregister.ExecuteCatapultLaunch(launchImpulse);
            _eventBus?.Publish(new CatapultLaunchedEvent(launchImpulse, _currentTensionRatio.value));

            if (coordinatorToUnregister.Torso != null && coordinatorToUnregister.Torso.IsHoldingObject)
            {
                coordinatorToUnregister.Torso.ReleaseHeldObject();
            }
            else
            {
                OnReleased(Vector3.zero);
            }

            if (!isSpawned)
            {
                ResetStrapVisuals();
                if (_impulseSource != null)
                {
                    try
                    {
                        _impulseSource.GenerateImpulse(launchImpulse.normalized * 1.6f);
                    }
                    catch { }
                }
            }
            else
            {
                NotifyLaunchObserversRpc(launchImpulse);
            }
        }

        private void HandleGrabbedStateChanged(bool grabbed)
        {
            if (!grabbed)
            {
                ResetStrapVisuals();
            }
        }

        [ObserversRpc]
        private void NotifyLaunchObserversRpc(Vector3 impulse)
        {
            _strapHandle.DOKill();
            _strapHandle.DOLocalMove(_handleDefaultLocalPos, 0.25f)
                .SetEase(Ease.OutElastic)
                .OnUpdate(UpdateStrapLines);

            if (_impulseSource != null)
            {
                try
                {
                    _impulseSource.GenerateImpulse(impulse.normalized * 1.6f);
                }
                catch { }
            }
        }

        private void ResetStrapVisuals()
        {
            _strapHandle.DOKill();
            _strapHandle.DOLocalMove(_handleDefaultLocalPos, 0.35f)
                .SetEase(Ease.OutBounce)
                .OnUpdate(UpdateStrapLines);
        }

        private void UpdateStrapLines()
        {
            Vector3 handlePos = _strapHandle.position;

            if (_strapLeft != null && _postLeft != null)
            {
                _strapLeft.SetPosition(0, _postLeft.position);
                _strapLeft.SetPosition(1, handlePos);
            }

            if (_strapRight != null && _postRight != null)
            {
                _strapRight.SetPosition(0, _postRight.position);
                _strapRight.SetPosition(1, handlePos);
            }
        }
    }
}
