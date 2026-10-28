using System;
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
    [DeclareBoxGroup("Configuración de Compuerta Pesada")]
    [DeclareBoxGroup("Feedback y Juicing")]
    [DeclareBoxGroup("Estado")]
    public sealed class HeavyMagneticDoor : NetworkBehaviour, IHeavyMagneticDraggable, IInteractableMechanism
    {
        [Group("Configuración de Compuerta Pesada")]
        [SerializeField] private Transform _doorLeaf;

        [Group("Configuración de Compuerta Pesada")]
        [SerializeField] private Transform _handlePoint;

        [Group("Configuración de Compuerta Pesada")]
        [SerializeField] private Vector3 _openOffset = new Vector3(3.8f, 0f, 0f);

        [Group("Configuración de Compuerta Pesada")]
        [SerializeField] private float _requiredPullDistance = 3.5f;

        [Group("Configuración de Compuerta Pesada")]
        [SerializeField] private float _dragSpeedMultiplier = 0.35f;

        [Group("Configuración de Compuerta Pesada")]
        [SerializeField] private bool _autoCloseOnRelease;

        [Group("Configuración de Compuerta Pesada")]
        [SerializeField] private float _autoCloseSpeed = 0.6f;

        [Group("Feedback y Juicing")]
        [SerializeField] private Renderer _handleIndicatorRenderer;

        [Group("Feedback y Juicing")]
        [SerializeField] private Color _activeMagnetColor = Color.cyan;

        [Group("Feedback y Juicing")]
        [SerializeField] private Color _idleMagnetColor = new Color(0.2f, 0.4f, 0.8f);

        [Group("Feedback y Juicing")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Group("Estado")]
        [SerializeField] private SyncVar<float> _openProgress = new(0f);

        [Group("Estado")]
        [SerializeField] private SyncVar<bool> _isFullyOpened = new(false);

        [Group("Estado")]
        [SerializeField] private SyncVar<bool> _isMagnetLocked = new(false);

        private IGameEventBus _eventBus;
        private Vector3 _closedLocalPosition;
        private Vector3 _startRobotPosition;
        private Transform _attachedOperator;
        private Game.Gameplay.Player.Torso.TorsoController _attachedTorso;
        private RobotCoordinator _attachedCoordinator;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        public bool CanBeAttracted => !_isFullyOpened.value;
        public Transform Transform => _handlePoint != null ? _handlePoint : transform;
        public bool IsMagnetLocked => _isMagnetLocked.value;
        public float OpenProgress => _openProgress.value;
        public bool IsActivated => _isFullyOpened.value;

        public event Action<bool> OnStateChanged;

        [Inject]
        public void Construct(IGameEventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        private void Awake()
        {
            if (_doorLeaf == null)
            {
                _doorLeaf = transform;
            }

            if (_handlePoint == null)
            {
                _handlePoint = transform;
            }

            _closedLocalPosition = _doorLeaf.localPosition;
            _propBlock = new MaterialPropertyBlock();

            if (_impulseSource == null)
            {
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            }

            UpdateHandleVisuals(false);
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            _openProgress.onChanged += HandleProgressChanged;
            _isMagnetLocked.onChanged += HandleMagnetLockedChanged;
            _isFullyOpened.onChanged += HandleFullyOpenedChanged;

            UpdateDoorPosition(_openProgress.value);
            UpdateHandleVisuals(_isMagnetLocked.value);
        }

        protected override void OnDespawned()
        {
            _openProgress.onChanged -= HandleProgressChanged;
            _isMagnetLocked.onChanged -= HandleMagnetLockedChanged;
            _isFullyOpened.onChanged -= HandleFullyOpenedChanged;

            if (_attachedCoordinator != null)
            {
                _attachedCoordinator.ResetDragSpeedMultiplier();
                _attachedCoordinator = null;
            }

            base.OnDespawned();
        }

        private void FixedUpdate()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (_isFullyOpened.value) return;

            if (_isMagnetLocked.value)
            {
                if (_attachedCoordinator == null || !_attachedCoordinator.IsFused)
                {
                    if (_attachedTorso != null && _attachedTorso.IsFused)
                    {
                        var coordinator = _attachedTorso.Coordinator;
                        if (coordinator != null && coordinator.IsFused)
                        {
                            _attachedCoordinator = coordinator;
                            _attachedCoordinator.SetDragSpeedMultiplier(_dragSpeedMultiplier);
                            _startRobotPosition = _attachedCoordinator.Legs != null
                                ? _attachedCoordinator.Legs.transform.position
                                : (_attachedOperator != null ? _attachedOperator.position : transform.position);
                        }
                    }
                }

                if (_attachedCoordinator != null && _attachedCoordinator.IsFused)
                {
                    Vector3 currentRobotPos = _attachedCoordinator.Legs != null
                        ? _attachedCoordinator.Legs.transform.position
                        : _attachedCoordinator.transform.position;

                    Vector3 travel = currentRobotPos - _startRobotPosition;
                    travel.y = 0f;
                    float dist = travel.magnitude;

                    if (dist > 0.05f)
                    {
                        float targetProgress = Mathf.Clamp01(dist / _requiredPullDistance);
                        if (targetProgress > _openProgress.value)
                        {
                            _openProgress.value = targetProgress;
                            UpdateDoorPosition(_openProgress.value);

                            if (_openProgress.value >= 1f)
                            {
                                _isFullyOpened.value = true;
                                UnlockMagnet();
                                if (!isSpawned)
                                {
                                    if (_impulseSource != null)
                                    {
                                        try { _impulseSource.GenerateImpulse(Vector3.down * 1.2f); } catch { }
                                    }
                                }
                                else
                                {
                                    NotifyFullyOpenedObserversRpc();
                                }
                                OnStateChanged?.Invoke(true);
                                _eventBus?.Publish(new HeavyDoorProgressChangedEvent(GetInstanceID(), 1f, true));
                            }
                        }
                    }
                }
            }
            else if (_autoCloseOnRelease && _openProgress.value > 0f && !_isFullyOpened.value)
            {
                _openProgress.value = Mathf.Max(0f, _openProgress.value - _autoCloseSpeed * Time.fixedDeltaTime);
                UpdateDoorPosition(_openProgress.value);
            }
        }

        public void ApplyMagneticForce(Vector3 magnetOrigin, float force, float deltaTime)
        {
        }

        public void LockMagnet(Transform operatorTransform)
        {
            if (_isFullyOpened.value) return;

            _attachedOperator = operatorTransform;
            _attachedTorso = operatorTransform != null ? operatorTransform.GetComponentInParent<Game.Gameplay.Player.Torso.TorsoController>() : null;
            var coordinator = _attachedTorso != null ? _attachedTorso.Coordinator : UnityEngine.Object.FindFirstObjectByType<RobotCoordinator>();

            if (coordinator != null && coordinator.IsFused)
            {
                _attachedCoordinator = coordinator;
                _attachedCoordinator.SetDragSpeedMultiplier(_dragSpeedMultiplier);
                _startRobotPosition = _attachedCoordinator.Legs != null
                    ? _attachedCoordinator.Legs.transform.position
                    : operatorTransform.position;
            }

            _isMagnetLocked.value = true;
            UpdateHandleVisuals(true);
        }

        public void UnlockMagnet()
        {
            if (_attachedCoordinator != null)
            {
                _attachedCoordinator.ResetDragSpeedMultiplier();
                _attachedCoordinator = null;
            }

            _attachedOperator = null;
            _attachedTorso = null;
            _isMagnetLocked.value = false;
            UpdateHandleVisuals(false);
        }

        public void ApplyDragMotion(Vector3 displacement)
        {
        }

        public void Toggle(GameObject user)
        {
        }

        private void HandleProgressChanged(float progress)
        {
            UpdateDoorPosition(progress);
        }

        private void HandleMagnetLockedChanged(bool locked)
        {
            UpdateHandleVisuals(locked);
        }

        private void HandleFullyOpenedChanged(bool opened)
        {
            if (opened)
            {
                UpdateDoorPosition(1f);
                OnStateChanged?.Invoke(true);
            }
        }

        private void UpdateDoorPosition(float progress)
        {
            if (_doorLeaf != null)
            {
                _doorLeaf.localPosition = Vector3.Lerp(_closedLocalPosition, _closedLocalPosition + _openOffset, progress);
            }
        }

        private void UpdateHandleVisuals(bool active)
        {
            if (_handleIndicatorRenderer != null)
            {
                Color targetColor = active ? _activeMagnetColor : _idleMagnetColor;
                _propBlock.SetColor(BaseColorPropId, targetColor);
                _propBlock.SetColor(ColorPropId, targetColor);
                _handleIndicatorRenderer.SetPropertyBlock(_propBlock);
            }
        }

        [ObserversRpc]
        private void NotifyFullyOpenedObserversRpc()
        {
            if (_impulseSource != null)
            {
                try
                {
                    _impulseSource.GenerateImpulse(Vector3.down * 1.2f);
                }
                catch { }
            }
        }

        [Button("Simular Apertura Completa")]
        private void DebugForceOpen()
        {
            _openProgress.value = 1f;
            _isFullyOpened.value = true;
            UpdateDoorPosition(1f);
            OnStateChanged?.Invoke(true);
        }
    }
}
