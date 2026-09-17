using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración")]
    public sealed class PushableBox : NetworkBehaviour, IPushable
    {
        [Group("Configuración")]
        [SerializeField] private float _pushForceMultiplier = 1.5f;

        private Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void OnPushed(Vector3 direction, float force)
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                Vector3 horizontalDir = new Vector3(direction.x, 0f, direction.z).normalized;
                _rigidbody.AddForce(horizontalDir * (force * _pushForceMultiplier), ForceMode.Impulse);
            }
        }
    }
}
