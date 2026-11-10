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

        private Transform _holdSocket;

        public void OnGrabbed(Transform holdSocket)
        {
            IsGrabbed = true;
            _holdSocket = holdSocket;

            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }
                _rigidbody.isKinematic = true;
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

                if (canSimulate && !_rigidbody.isKinematic)
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
            if (!canSimulate || IsGrabbed || _rigidbody == null || _rigidbody.isKinematic)
            {
                return;
            }

            Vector3 toOrigin = magnetOrigin - transform.position;
            float sqrDist = toOrigin.sqrMagnitude;
            if (sqrDist > 0.01f)
            {
                float dist = Mathf.Sqrt(sqrDist);
                float speedFactor = Mathf.Clamp01(dist / 1.2f);
                float currentSpeed = Mathf.Max(2f, _magneticAttractSpeed * speedFactor);
                Vector3 targetVelocity = (toOrigin / dist) * currentSpeed;
                _rigidbody.linearVelocity = Vector3.MoveTowards(_rigidbody.linearVelocity, targetVelocity, force * deltaTime);
            }
        }
    }
}
