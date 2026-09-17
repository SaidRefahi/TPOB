using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DeclareBoxGroup("Configuración Llave")]
    public sealed class MagneticKey : NetworkBehaviour, IMagnetic, IGrabbable
    {
        [Group("Configuración Llave")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Configuración Llave")]
        [SerializeField] private float _magneticAttractSpeed = 12f;

        [Group("Configuración Llave")]
        [SerializeField] private string _keyId = "Key_A";

        public string KeyId => _keyId;
        public bool IsGrabbed { get; private set; }
        public bool CanBeAttracted => !IsGrabbed;
        public Transform Transform => transform;

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = !isServer || IsGrabbed;
            }
        }

        private void Awake()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            _rigidbody.mass = 0.5f;
        }

        private void Start()
        {
            if (!isSpawned && _rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
        }

        public void OnGrabbed(Transform holdSocket)
        {
            IsGrabbed = true;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }

            transform.SetParent(holdSocket);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        public void OnReleased(Vector3 throwVelocity)
        {
            IsGrabbed = false;
            transform.SetParent(null);

            if (_rigidbody != null)
            {
                bool canSimulate = !isSpawned || isServer;
                _rigidbody.isKinematic = !canSimulate;

                if (canSimulate)
                {
                    _rigidbody.linearVelocity = throwVelocity;
                }
            }
        }

        public void ApplyMagneticForce(Vector3 magnetOrigin, float force, float deltaTime)
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate || IsGrabbed || _rigidbody == null)
            {
                return;
            }

            Vector3 toOrigin = magnetOrigin - transform.position;
            if (toOrigin.sqrMagnitude > 0.01f)
            {
                Vector3 targetVelocity = toOrigin.normalized * _magneticAttractSpeed;
                _rigidbody.linearVelocity = Vector3.MoveTowards(_rigidbody.linearVelocity, targetVelocity, force * deltaTime);
            }
        }
    }
}
