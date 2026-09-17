using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Muro")]
    [DeclareBoxGroup("Estado")]
    public sealed class BreakableWall : NetworkBehaviour, IKickable
    {
        [Group("Configuración de Muro")]
        [SerializeField] private float _minBreakForce = 5f;

        [Group("Configuración de Muro")]
        [SerializeField] private GameObject _intactVisual;

        [Group("Configuración de Muro")]
        [SerializeField] private GameObject _brokenPiecesRoot;

        [Group("Configuración de Muro")]
        [SerializeField] private Collider _solidCollider;

        [Group("Configuración de Muro")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Group("Configuración de Muro")]
        [SerializeField] private float _explosionForce = 350f;

        [Group("Configuración de Muro")]
        [SerializeField] private float _explosionRadius = 3.5f;

        [Group("Estado")]
        [ShowInInspector]
        private bool _isBroken;

        private Rigidbody[] _pieceBodies;

        public bool IsBroken => _isBroken;

        private void Awake()
        {
            if (_solidCollider == null)
            {
                _solidCollider = GetComponent<Collider>();
            }

            if (_impulseSource == null)
            {
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            }

            if (_brokenPiecesRoot != null)
            {
                _pieceBodies = _brokenPiecesRoot.GetComponentsInChildren<Rigidbody>(true);
                _brokenPiecesRoot.SetActive(false);
            }
        }

        public void OnKicked(Vector3 hitPoint, Vector3 direction, float kickForce)
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (_isBroken) return;

            if (kickForce >= _minBreakForce)
            {
                BreakWall(hitPoint, direction);
            }
        }

        public void BreakWall(Vector3 hitPoint, Vector3 direction)
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (_isBroken) return;

            if (!isSpawned)
            {
                ApplyBreak(hitPoint, direction);
            }
            else
            {
                BreakObserversRpc(hitPoint, direction);
            }
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void BreakObserversRpc(Vector3 hitPoint, Vector3 direction)
        {
            ApplyBreak(hitPoint, direction);
        }

        private void ApplyBreak(Vector3 hitPoint, Vector3 direction)
        {
            _isBroken = true;

            if (_solidCollider != null)
            {
                _solidCollider.enabled = false;
            }

            if (_intactVisual != null)
            {
                _intactVisual.SetActive(false);
            }

            if (_brokenPiecesRoot != null)
            {
                _brokenPiecesRoot.SetActive(true);

                if (_pieceBodies != null)
                {
                    for (int i = 0; i < _pieceBodies.Length; i++)
                    {
                        Rigidbody rb = _pieceBodies[i];
                        if (rb != null)
                        {
                            rb.isKinematic = false;
                            rb.AddExplosionForce(_explosionForce, hitPoint, _explosionRadius, 0.5f, ForceMode.Impulse);
                            rb.AddForce(direction * (_explosionForce * 0.3f), ForceMode.Impulse);
                        }
                    }
                }
            }

            if (_impulseSource != null)
            {
                try
                {
                    _impulseSource.GenerateImpulse();
                }
                catch { }
            }
        }

        [Button("Romper Muro (Simular)")]
        private void DebugBreak()
        {
            BreakWall(transform.position, transform.forward);
        }
    }
}
