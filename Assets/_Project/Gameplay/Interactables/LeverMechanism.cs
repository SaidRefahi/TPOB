using System;
using DG.Tweening;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Mecanismo")]
    [DeclareBoxGroup("Feedback Visual")]
    public sealed class LeverMechanism : NetworkBehaviour, IInteractableMechanism
    {
        [Group("Mecanismo")]
        [SerializeField] private Transform _handleTransform;

        [Group("Mecanismo")]
        [SerializeField] private Renderer _indicatorRenderer;

        [Group("Mecanismo")]
        [SerializeField] private Vector3 _offEulerAngles = new Vector3(-35f, 0f, 0f);

        [Group("Mecanismo")]
        [SerializeField] private Vector3 _onEulerAngles = new Vector3(35f, 0f, 0f);

        [Group("Mecanismo")]
        [SerializeField] private float _toggleDuration = 0.35f;

        [Group("Mecanismo")]
        [SerializeField] private bool _isActivated;

        [Group("Feedback Visual")]
        [SerializeField] private Color _offColor = new Color(0.9f, 0.2f, 0.15f);

        [Group("Feedback Visual")]
        [SerializeField] private Color _onColor = new Color(0.15f, 0.95f, 0.35f);

        private Tween _handleTween;
        private MaterialPropertyBlock _propBlock;

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        public bool IsActivated => _isActivated;
        public event Action<bool> OnStateChanged;

        private void Awake()
        {
            if (_indicatorRenderer == null && _handleTransform != null)
            {
                _indicatorRenderer = _handleTransform.GetComponentInChildren<Renderer>();
            }

            if (_handleTransform != null)
            {
                _handleTransform.localRotation = Quaternion.Euler(_isActivated ? _onEulerAngles : _offEulerAngles);
            }

            UpdateIndicatorVisuals(_isActivated);
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            UpdateVisuals(_isActivated);
        }

        protected override void OnDestroy()
        {
            _handleTween?.Kill();
            base.OnDestroy();
        }

        public void Toggle(GameObject user)
        {
            if (isSpawned && !isServer)
            {
                ToggleServerRpc();
                return;
            }

            _isActivated = !_isActivated;

            if (!isSpawned)
            {
                UpdateVisuals(_isActivated);
            }
            else
            {
                UpdateVisualsObserversRpc(_isActivated);
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void ToggleServerRpc()
        {
            Toggle(null);
        }

        private void UpdateVisuals(bool activated)
        {
            _isActivated = activated;

            if (_handleTransform != null)
            {
                _handleTween?.Kill();
                Vector3 targetEuler = activated ? _onEulerAngles : _offEulerAngles;
                _handleTween = _handleTransform.DOLocalRotate(targetEuler, _toggleDuration).SetEase(Ease.OutBack);
            }

            UpdateIndicatorVisuals(activated);
            OnStateChanged?.Invoke(activated);
        }

        private void UpdateIndicatorVisuals(bool activated)
        {
            if (_indicatorRenderer == null) return;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            _indicatorRenderer.GetPropertyBlock(_propBlock);
            Color targetColor = activated ? _onColor : _offColor;
            _propBlock.SetColor(BaseColorProp, targetColor);
            _propBlock.SetColor(ColorProp, targetColor);
            _propBlock.SetColor(EmissionColorProp, targetColor * 1.5f);
            _indicatorRenderer.SetPropertyBlock(_propBlock);
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void UpdateVisualsObserversRpc(bool activated)
        {
            UpdateVisuals(activated);
        }
    }
}
