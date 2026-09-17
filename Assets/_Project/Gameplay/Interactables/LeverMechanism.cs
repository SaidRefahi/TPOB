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
    public sealed class LeverMechanism : NetworkBehaviour, IInteractableMechanism
    {
        [Group("Mecanismo")]
        [SerializeField] private Transform _handleTransform;

        [Group("Mecanismo")]
        [SerializeField] private Vector3 _offEulerAngles = new Vector3(-35f, 0f, 0f);

        [Group("Mecanismo")]
        [SerializeField] private Vector3 _onEulerAngles = new Vector3(35f, 0f, 0f);

        [Group("Mecanismo")]
        [SerializeField] private float _toggleDuration = 0.35f;

        [Group("Mecanismo")]
        [SerializeField] private bool _isActivated;

        private Tween _handleTween;

        public bool IsActivated => _isActivated;
        public event Action<bool> OnStateChanged;

        protected override void OnDestroy()
        {
            _handleTween?.Kill();
            base.OnDestroy();
        }

        public void Toggle(GameObject user)
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate)
            {
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

        private void UpdateVisuals(bool activated)
        {
            _isActivated = activated;

            if (_handleTransform != null)
            {
                _handleTween?.Kill();
                Vector3 targetEuler = activated ? _onEulerAngles : _offEulerAngles;
                _handleTween = _handleTransform.DOLocalRotate(targetEuler, _toggleDuration).SetEase(Ease.OutBack);
            }

            OnStateChanged?.Invoke(activated);
        }

        [ObserversRpc(runLocally: true)]
        private void UpdateVisualsObserversRpc(bool activated)
        {
            UpdateVisuals(activated);
        }
    }
}
