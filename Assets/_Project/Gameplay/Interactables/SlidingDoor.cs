using DG.Tweening;
using PurrNet;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Compuerta")]
    [DeclareBoxGroup("Estado")]
    public sealed class SlidingDoor : NetworkBehaviour
    {
        [Group("Configuración de Compuerta")]
        [SerializeField] private Transform _doorLeaf;

        [Group("Configuración de Compuerta")]
        [SerializeField] private Vector3 _openOffset = new Vector3(0f, 3.5f, 0f);

        [Group("Configuración de Compuerta")]
        [SerializeField] private float _animationDuration = 0.8f;

        [Group("Configuración de Compuerta")]
        [SerializeField] private Ease _openEase = Ease.OutBack;

        [Group("Configuración de Compuerta")]
        [SerializeField] private Ease _closeEase = Ease.InCubic;

        [Group("Configuración de Compuerta")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Group("Estado")]
        [ShowInInspector]
        private bool _isOpened;

        private Vector3 _closedLocalPosition;
        private Tween _doorTween;

        public bool IsOpened => _isOpened;

        private void Awake()
        {
            if (_doorLeaf == null)
            {
                _doorLeaf = transform;
            }

            _closedLocalPosition = _doorLeaf.localPosition;

            if (_impulseSource == null)
            {
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            }
        }

        protected override void OnDestroy()
        {
            _doorTween?.Kill();
            base.OnDestroy();
        }

        public void OpenDoor()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (_isOpened) return;

            if (!isSpawned)
            {
                AnimateDoor(true);
            }
            else
            {
                SetDoorStateObserversRpc(true);
            }
        }

        public void CloseDoor()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (!_isOpened) return;

            if (!isSpawned)
            {
                AnimateDoor(false);
            }
            else
            {
                SetDoorStateObserversRpc(false);
            }
        }

        public void ToggleDoor()
        {
            if (_isOpened)
            {
                CloseDoor();
            }
            else
            {
                OpenDoor();
            }
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void SetDoorStateObserversRpc(bool open)
        {
            AnimateDoor(open);
        }

        private void AnimateDoor(bool open)
        {
            _isOpened = open;
            _doorTween?.Kill();

            Vector3 targetPos = open ? _closedLocalPosition + _openOffset : _closedLocalPosition;
            Ease ease = open ? _openEase : _closeEase;

            _doorTween = _doorLeaf.DOLocalMove(targetPos, _animationDuration)
                .SetEase(ease)
                .OnComplete(HandleAnimationComplete);
        }

        private void HandleAnimationComplete()
        {
            if (_impulseSource != null)
            {
                try
                {
                    _impulseSource.GenerateImpulse();
                }
                catch { }
            }
        }

        [Button("Abrir Compuerta")]
        private void DebugOpen() => OpenDoor();

        [Button("Cerrar Compuerta")]
        private void DebugClose() => CloseDoor();
    }
}
