using System;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(HingeJoint))]
    [DeclareBoxGroup("Configuración del Balancín")]
    [DeclareBoxGroup("Estado Dinámico")]
    public sealed class SeeSawPlatform : NetworkBehaviour
    {
        [Group("Configuración del Balancín")]
        [SerializeField] private float _maxTiltAngle = 22f;

        [Group("Configuración del Balancín")]
        [SerializeField] private float _springForce = 40f;

        [Group("Configuración del Balancín")]
        [SerializeField] private float _springDamper = 15f;

        [Group("Configuración del Balancín")]
        [SerializeField] private float _balancedThreshold = 5f;

        [Group("Estado Dinámico")]
        [ShowInInspector, ReadOnly]
        public float CurrentAngle { get; private set; }

        [Group("Estado Dinámico")]
        [ShowInInspector, ReadOnly]
        public bool IsBalanced => Mathf.Abs(CurrentAngle) <= _balancedThreshold;

        public event Action<bool> OnBalanceChanged;

        private Rigidbody _rigidbody;
        private HingeJoint _hingeJoint;
        private bool _wasBalanced;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _hingeJoint = GetComponent<HingeJoint>();
            ConfigureHingeJoint();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            if (_rigidbody != null)
            {
                // Follow PurrNet physical authority rule: server simulates PhysX, clients interpolate
                _rigidbody.isKinematic = !isServer;
            }
        }

        private void ConfigureHingeJoint()
        {
            _hingeJoint.useLimits = true;
            var limits = _hingeJoint.limits;
            limits.min = -_maxTiltAngle;
            limits.max = _maxTiltAngle;
            limits.bounciness = 0.1f;
            _hingeJoint.limits = limits;

            _hingeJoint.useSpring = true;
            var spring = _hingeJoint.spring;
            spring.spring = _springForce;
            spring.damper = _springDamper;
            spring.targetPosition = 0f;
            _hingeJoint.spring = spring;
        }

        private void FixedUpdate()
        {
            if (_hingeJoint == null) return;

            CurrentAngle = _hingeJoint.angle;
            bool balanced = IsBalanced;

            if (balanced != _wasBalanced)
            {
                _wasBalanced = balanced;
                OnBalanceChanged?.Invoke(balanced);
            }
        }

        [Button("Restablecer Horizontal")]
        public void ResetToHorizontal()
        {
            if (_rigidbody != null)
            {
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.linearVelocity = Vector3.zero;
            }
            transform.localRotation = Quaternion.identity;
        }
    }
}
