using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Player.Legs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DeclareBoxGroup("Configuración")]
    public sealed class KickableTestObject : NetworkBehaviour, IKickable, IPushable
    {
        [Group("Configuración")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Configuración")]
        [SerializeField] private float _mass = 2f;

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = !isServer;
            }
        }

        private void Awake()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            _rigidbody.mass = _mass;
        }

        public void OnKicked(Vector3 hitPoint, Vector3 direction, float kickForce)
        {
            if (!isServer || _rigidbody == null)
            {
                return;
            }

            _rigidbody.AddForceAtPosition(direction * kickForce, hitPoint, ForceMode.Impulse);
        }

        public void OnPushed(Vector3 direction, float force)
        {
            if (!isServer || _rigidbody == null)
            {
                return;
            }

            _rigidbody.AddForce(direction * force, ForceMode.Impulse);
        }
    }
}
