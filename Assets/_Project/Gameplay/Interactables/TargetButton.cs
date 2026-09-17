using System;
using DG.Tweening;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración del Pulsador")]
    [DeclareBoxGroup("Estado")]
    public sealed class TargetButton : NetworkBehaviour, IInteractableMechanism, IKickable, IPushable
    {
        [Group("Configuración del Pulsador")]
        [SerializeField] private Transform _buttonFace;

        [Group("Configuración del Pulsador")]
        [SerializeField] private Vector3 _pressOffset = new Vector3(0f, 0f, -0.15f);

        [Group("Configuración del Pulsador")]
        [SerializeField] private float _pressDuration = 0.2f;

        [Group("Configuración del Pulsador")]
        [SerializeField] private bool _isToggle = true;

        [Group("Configuración del Pulsador")]
        [SerializeField] private float _cooldown = 0.4f;

        [Group("Configuración del Pulsador")]
        [SerializeField] private float _resetDelay = 2f;

        [Group("Configuración del Pulsador")]
        [SerializeField] private Renderer _indicatorRenderer;

        [Group("Configuración del Pulsador")]
        [SerializeField] private Color _activeColor = Color.green;

        [Group("Configuración del Pulsador")]
        [SerializeField] private Color _inactiveColor = Color.red;

        [Group("Estado")]
        [ShowInInspector]
        private bool _isActivated;

        private Vector3 _initialLocalPosition;
        private Tween _pressTween;
        private Tween _resetTween;
        private MaterialPropertyBlock _propBlock;
        private float _lastTriggerTime = -10f;
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        public bool IsActivated => _isActivated;
        public event Action<bool> OnStateChanged;

        private void Awake()
        {
            if (_buttonFace == null)
            {
                _buttonFace = transform;
            }

            _initialLocalPosition = _buttonFace.localPosition;
            _propBlock = new MaterialPropertyBlock();

            if (_indicatorRenderer == null && _buttonFace != null)
            {
                _indicatorRenderer = _buttonFace.GetComponent<Renderer>();
            }

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            UpdateIndicatorColor(false);
        }

        protected override void OnDestroy()
        {
            _pressTween?.Kill();
            _resetTween?.Kill();
            base.OnDestroy();
        }

        public void Toggle(GameObject user)
        {
            TriggerActivation();
        }

        public void OnKicked(Vector3 hitPoint, Vector3 direction, float kickForce)
        {
            TriggerActivation();
        }

        public void OnPushed(Vector3 direction, float force)
        {
            if (force > 0.5f)
            {
                TriggerActivation();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (collision.collider.TryGetComponent<IGrabbable>(out _) ||
                collision.collider.attachedRigidbody != null ||
                collision.relativeVelocity.sqrMagnitude > 0.1f)
            {
                TriggerActivation();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (other.isTrigger && !other.CompareTag("Player")) return;

            if (other.TryGetComponent<IGrabbable>(out _) || other.attachedRigidbody != null)
            {
                TriggerActivation();
            }
        }

        private void TriggerActivation()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (Time.time - _lastTriggerTime < _cooldown)
            {
                return;
            }

            _lastTriggerTime = Time.time;

            if (_isToggle)
            {
                SetState(!_isActivated);
            }
            else
            {
                if (!_isActivated)
                {
                    SetState(true);
                }
            }
        }

        private void SetState(bool active)
        {
            _isActivated = active;

            if (!isSpawned)
            {
                ApplyVisualState(active);
            }
            else
            {
                ApplyVisualStateObserversRpc(active);
            }

            OnStateChanged?.Invoke(active);

            if (!_isToggle && active && _resetDelay > 0f)
            {
                _resetTween?.Kill();
                _resetTween = DOVirtual.DelayedCall(_resetDelay, ResetButtonServer);
            }
        }

        private void ResetButtonServer()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            SetState(false);
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void ApplyVisualStateObserversRpc(bool active)
        {
            ApplyVisualState(active);
        }

        private void ApplyVisualState(bool active)
        {
            _isActivated = active;
            _pressTween?.Kill();

            Vector3 targetPos = active ? _initialLocalPosition + _pressOffset : _initialLocalPosition;
            _pressTween = _buttonFace.DOLocalMove(targetPos, _pressDuration).SetEase(Ease.OutBounce);

            UpdateIndicatorColor(active);
        }

        private void UpdateIndicatorColor(bool active)
        {
            if (_indicatorRenderer != null)
            {
                Color targetColor = active ? _activeColor : _inactiveColor;
                _propBlock.SetColor(BaseColorPropId, targetColor);
                _propBlock.SetColor(ColorPropId, targetColor);
                _indicatorRenderer.SetPropertyBlock(_propBlock);
            }
        }

        [Button("Pulsar Diana")]
        private void DebugTrigger() => TriggerActivation();
    }
}
