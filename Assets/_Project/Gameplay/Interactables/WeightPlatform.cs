using System;
using DG.Tweening;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Báscula")]
    [DeclareBoxGroup("Feedback Visual")]
    [DeclareBoxGroup("Estado")]
    public sealed class WeightPlatform : NetworkBehaviour, IInteractableMechanism
    {
        [Group("Configuración de Báscula")]
        [SerializeField] private Transform _platformPlate;

        [Group("Configuración de Báscula")]
        [SerializeField] private Vector3 _detectionBoxHalfExtents = new Vector3(1.6f, 1.5f, 1.6f);

        [Group("Configuración de Báscula")]
        [SerializeField] private Vector3 _detectionBoxOffset = new Vector3(0f, 1.5f, 0f);

        [Group("Configuración de Báscula")]
        [SerializeField] private LayerMask _detectionLayerMask = ~0;

        [Group("Configuración de Báscula")]
        [SerializeField] private float _requiredMass = 80f;

        [Group("Configuración de Báscula")]
        [SerializeField] private Vector3 _sinkOffset = new Vector3(0f, -0.18f, 0f);

        [Group("Configuración de Báscula")]
        [SerializeField] private float _sinkDuration = 0.25f;

        [Group("Feedback Visual")]
        [SerializeField] private Renderer _indicatorRenderer;

        [Group("Feedback Visual")]
        [SerializeField] private Color _inactiveColor = new Color(0.9f, 0.2f, 0.2f);

        [Group("Feedback Visual")]
        [SerializeField] private Color _activeColor = new Color(0.2f, 0.95f, 0.35f);

        [Group("Estado")]
        [ShowInInspector]
        private bool _isActivated;

        [Group("Estado")]
        [ShowInInspector]
        private float _currentDetectedMass;

        private Vector3 _initialLocalPosition;
        private Tween _plateTween;
        private MaterialPropertyBlock _propBlock;
        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");

        private readonly Collider[] _hitBuffer = new Collider[32];
        private readonly Rigidbody[] _countedBodies = new Rigidbody[16];

        public bool IsActivated => _isActivated;
        public float CurrentDetectedMass => _currentDetectedMass;
        public float RequiredMass => _requiredMass;
        public event Action<bool> OnStateChanged;

        private void Awake()
        {
            if (_platformPlate == null)
            {
                _platformPlate = transform;
            }

            _initialLocalPosition = _platformPlate.localPosition;
            _propBlock = new MaterialPropertyBlock();

            if (_indicatorRenderer == null)
            {
                _indicatorRenderer = _platformPlate.GetComponent<Renderer>();
            }

            UpdateVisualColor(false);
        }

        protected override void OnDestroy()
        {
            _plateTween?.Kill();
            base.OnDestroy();
        }

        private void FixedUpdate()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            Vector3 center = transform.position + transform.TransformDirection(_detectionBoxOffset);
            int hitCount = Physics.OverlapBoxNonAlloc(center, _detectionBoxHalfExtents, _hitBuffer, transform.rotation, _detectionLayerMask, QueryTriggerInteraction.Collide);

            float totalMass = 0f;
            int countedCount = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _hitBuffer[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                Rigidbody rb = col.attachedRigidbody;
                if (rb != null)
                {
                    bool alreadyCounted = false;
                    for (int j = 0; j < countedCount; j++)
                    {
                        if (_countedBodies[j] == rb)
                        {
                            alreadyCounted = true;
                            break;
                        }
                    }

                    if (!alreadyCounted && countedCount < _countedBodies.Length)
                    {
                        _countedBodies[countedCount++] = rb;
                        totalMass += rb.mass;
                    }
                }
            }

            _currentDetectedMass = totalMass;
            bool shouldBeActive = totalMass >= _requiredMass;

            if (shouldBeActive != _isActivated)
            {
                _isActivated = shouldBeActive;

                if (!isSpawned)
                {
                    ApplyPlatformVisuals(shouldBeActive);
                }
                else
                {
                    ApplyPlatformVisualsObserversRpc(shouldBeActive);
                }

                OnStateChanged?.Invoke(shouldBeActive);
            }
        }

        public void Toggle(GameObject user) { }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void ApplyPlatformVisualsObserversRpc(bool active)
        {
            ApplyPlatformVisuals(active);
        }

        private void ApplyPlatformVisuals(bool active)
        {
            _isActivated = active;
            _plateTween?.Kill();

            Vector3 targetPos = active ? _initialLocalPosition + _sinkOffset : _initialLocalPosition;
            _plateTween = _platformPlate.DOLocalMove(targetPos, _sinkDuration).SetEase(Ease.OutBounce);

            UpdateVisualColor(active);
        }

        private void UpdateVisualColor(bool active)
        {
            if (_indicatorRenderer != null)
            {
                Color targetColor = active ? _activeColor : _inactiveColor;
                _propBlock.SetColor(BaseColorPropId, targetColor);
                _propBlock.SetColor(ColorPropId, targetColor);
                _indicatorRenderer.SetPropertyBlock(_propBlock);
            }
        }

        public void ConfigureMassThreshold(float requiredMass)
        {
            _requiredMass = requiredMass;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isActivated ? Color.green : Color.yellow;
            Vector3 center = transform.position + transform.TransformDirection(_detectionBoxOffset);
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, _detectionBoxHalfExtents * 2f);
        }
    }
}
