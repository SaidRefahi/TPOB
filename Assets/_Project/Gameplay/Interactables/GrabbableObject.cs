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

        private Transform _holdSocket;

        public void OnGrabbed(Transform holdSocket)
        {
            IsGrabbed = true;
            _holdSocket = holdSocket;

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.linearVelocity = Vector3.zero;
            }

            if (_holdSocket != null)
            {
                transform.position = _holdSocket.position;
                transform.rotation = _holdSocket.rotation;
            }

            if (isServer)
            {
                SyncGrabStateObserversRpc(true);
            }
        }

        public void OnReleased(Vector3 throwVelocity)
        {
            IsGrabbed = false;
            _holdSocket = null;

            if (_rigidbody != null)
            {
                bool canSimulate = !isSpawned || isServer;
                _rigidbody.isKinematic = !canSimulate;

                if (canSimulate)
                {
                    _rigidbody.linearVelocity = throwVelocity;
                }
            }

            if (isServer)
            {
                SyncGrabStateObserversRpc(false);
            }
        }

        [ObserversRpc(runLocally: false)]
        private void SyncGrabStateObserversRpc(bool isGrabbed)
        {
            IsGrabbed = isGrabbed;
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = !isServer || isGrabbed;
            }
        }

        private void FixedUpdate()
        {
            bool canSimulate = !isSpawned || isServer;
            if (canSimulate && IsGrabbed && _holdSocket != null)
            {
                transform.position = _holdSocket.position;
                transform.rotation = _holdSocket.rotation;

                if (_rigidbody != null)
                {
                    _rigidbody.position = _holdSocket.position;
                    _rigidbody.rotation = _holdSocket.rotation;
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
