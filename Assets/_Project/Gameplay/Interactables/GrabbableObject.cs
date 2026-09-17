using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DeclareBoxGroup("Configuración")]
    public sealed class GrabbableObject : NetworkBehaviour, IGrabbable, IMagnetic, IPushable, IKickable
    {
        [Group("Configuración")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Configuración")]
        [SerializeField] private float _magneticAttractSpeed = 10f;

        [Group("Configuración")]
        [SerializeField] private float _mass = 1.5f;

        public bool IsGrabbed { get; private set; }
        public bool CanBeAttracted => !IsGrabbed;
        public Transform Transform => transform;
        public Rigidbody Rigidbody => _rigidbody;

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

            _rigidbody.mass = _mass;
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

        public void OnPushed(Vector3 direction, float force)
        {
            bool canSimulate = !isSpawned || isServer;
            if (canSimulate && !IsGrabbed && _rigidbody != null)
            {
                _rigidbody.AddForce(direction * force, ForceMode.Impulse);
            }
        }

        public void OnKicked(Vector3 hitPoint, Vector3 direction, float kickForce)
        {
            bool canSimulate = !isSpawned || isServer;
            if (canSimulate && !IsGrabbed && _rigidbody != null)
            {
                _rigidbody.AddForceAtPosition(direction * kickForce, hitPoint, ForceMode.Impulse);
            }
        }
    }
}
