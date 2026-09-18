using Game.Core.Enums;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Hazards
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [DeclareBoxGroup("Configuración")]
    public sealed class KillVolume : NetworkBehaviour
    {
        [Group("Configuración")]
        [SerializeField] private DeathCause _deathCause = DeathCause.Fall;

        [Group("Configuración")]
        [SerializeField] private Transform _objectSafePoint;

        [Group("Configuración")]
        [SerializeField] private Vector3 _defaultObjectResetPosition = new Vector3(0f, 1f, 0f);

        private BoxCollider _boxCollider;

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            _boxCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isSpawned && !isServer)
            {
                return;
            }

            // 1. Detect Players / Damageables
            if (other.TryGetComponent<IDamageable>(out var damageable) ||
                (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<IDamageable>(out damageable)) ||
                (other.GetComponentInParent<IDamageable>() is { } pDamageable && (damageable = pDamageable) != null))
            {
                if (damageable.IsAlive)
                {
                    damageable.Kill(_deathCause);
                }
                return;
            }

            // 2. Detect Puzzle Items (Keys, Boxes) and rescue them so puzzles don't break
            if (other.attachedRigidbody != null)
            {
                var rb = other.attachedRigidbody;
                if (rb.TryGetComponent<IGrabbable>(out _) || rb.TryGetComponent<IPushable>(out _) || rb.TryGetComponent<IMagnetic>(out _))
                {
                    Vector3 resetPos = _objectSafePoint != null ? _objectSafePoint.position : _defaultObjectResetPosition;
                    if (!rb.isKinematic)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.position = resetPos;
                    rb.transform.position = resetPos;
                }
            }
        }
    }
}
