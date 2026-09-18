using System;
using DG.Tweening;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Socket")]
    [DeclareBoxGroup("Estado")]
    public sealed class PuzzleSocket : NetworkBehaviour, IInteractableMechanism
    {
        [Group("Configuración de Socket")]
        [SerializeField] private Transform _snapPoint;

        [Group("Configuración de Socket")]
        [SerializeField] private float _detectionRadius = 1.2f;

        [Group("Configuración de Socket")]
        [SerializeField] private LayerMask _keyLayerMask = ~0;

        [Group("Configuración de Socket")]
        [SerializeField] private float _snapDuration = 0.35f;

        [Group("Configuración de Socket")]
        [SerializeField] private bool _lockPermanently = true;

        [Group("Estado")]
        [ShowInInspector]
        private bool _isActivated;

        private readonly Collider[] _hitBuffer = new Collider[8];
        private MagneticKey _slottedKey;
        private Tween _snapTween;

        public bool IsActivated => _isActivated;
        public event Action<bool> OnStateChanged;

        private void Awake()
        {
            if (_snapPoint == null)
            {
                _snapPoint = transform;
            }
        }

        protected override void OnDestroy()
        {
            _snapTween?.Kill();
            base.OnDestroy();
        }

        private void FixedUpdate()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (_isActivated && _lockPermanently)
            {
                if (_slottedKey != null)
                {
                    _slottedKey.transform.position = _snapPoint.position;
                    _slottedKey.transform.rotation = _snapPoint.rotation;
                }
                return;
            }

            int count = Physics.OverlapSphereNonAlloc(transform.position, _detectionRadius, _hitBuffer, _keyLayerMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _hitBuffer[i];
                if (col == null) continue;

                if (col.TryGetComponent<MagneticKey>(out var key) || (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<MagneticKey>(out key)))
                {
                    if (!key.IsGrabbed)
                    {
                        SlotKeyServer(key);
                        break;
                    }
                }
            }
        }

        private void SlotKeyServer(MagneticKey key)
        {
            _slottedKey = key;
            _isActivated = true;

            var rb = key.GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
            }

            if (!isSpawned)
            {
                AnimateSnapKey(key.gameObject);
            }
            else
            {
                SnapKeyObserversRpc(key.gameObject);
            }

            OnStateChanged?.Invoke(true);
        }

        [ObserversRpc(runLocally: true)]
        private void SnapKeyObserversRpc(GameObject keyObj)
        {
            if (keyObj != null)
            {
                AnimateSnapKey(keyObj);
            }
        }

        private void AnimateSnapKey(GameObject keyObj)
        {
            _isActivated = true;
            _snapTween?.Kill();

            var rb = keyObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            _snapTween = DOTween.Sequence()
                .Append(keyObj.transform.DOMove(_snapPoint.position, _snapDuration).SetEase(Ease.OutBack))
                .Join(keyObj.transform.DORotateQuaternion(_snapPoint.rotation, _snapDuration).SetEase(Ease.OutBack));
        }

        public void Toggle(GameObject user)
        {
            // State driven by slotted physical item.
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isActivated ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        }
    }
}
