using System;
using Game.Core.Commands;
using Game.Core.Interfaces;
using Game.Core.Structs;
using Game.Gameplay.Player.Commands;
using Game.Gameplay.Player.Robot;
using PurrNet;
using PurrNet.Transports;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Player.Legs
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DeclareBoxGroup("Componentes")]
    [DeclareBoxGroup("Locomoción")]
    [DeclareBoxGroup("Ground Check")]
    [DeclareBoxGroup("Patada")]
    [DeclareBoxGroup("Impulsos de Cámara")]
    [DeclareBoxGroup("Fusión")]
    public sealed class LegsController : NetworkBehaviour, IMoveable, IKicker, IFusionOperator
    {
        [Group("Componentes")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Componentes")]
        [SerializeField] private FusionSocket _fusionSocket;

        [Group("Componentes")]
        [SerializeField] private RobotCoordinator _coordinator;

        [Group("Componentes")]
        [SerializeField] private LegsInputReader _inputReader;

        [Group("Componentes")]
        [SerializeField] private CommandInvoker _commandInvoker;

        [Group("Componentes")]
        [SerializeField] private Transform _groundCheckPoint;

        [Group("Componentes")]
        [SerializeField] private Transform _kickPoint;

        [Group("Locomoción")]
        [SerializeField] private float _walkSpeed = 5f;

        [Group("Locomoción")]
        [SerializeField] private float _sprintSpeed = 8.5f;

        [Group("Locomoción")]
        [SerializeField] private float _acceleration = 25f;

        [Group("Locomoción")]
        [SerializeField] private float _baseMass = 70f;

        [Group("Locomoción")]
        [SerializeField] private float _rotationSpeed = 12f;

        [Group("Locomoción")]
        [SerializeField] private float _jumpForce = 7f;

        [Group("Locomoción")]
        [SerializeField] private float _fallMultiplier = 2.5f;

        [Group("Ground Check")]
        [SerializeField] private float _groundCheckDistance = 0.25f;

        [Group("Ground Check")]
        [SerializeField] private LayerMask _groundLayer = ~0;

        [Group("Patada")]
        [SerializeField] private float _kickRadius = 1.3f;

        [Group("Patada")]
        [SerializeField] private float _kickForce = 16f;

        [Group("Patada")]
        [SerializeField] private float _kickCooldown = 0.5f;

        [Group("Patada")]
        [SerializeField] private LayerMask _kickLayer = ~0;

        [Group("Impulsos de Cámara")]
        [SerializeField] private CinemachineImpulseSource _kickImpulseSource;

        [Group("Impulsos de Cámara")]
        [SerializeField] private float _kickImpulseForce = 1.2f;

        [Group("Impulsos de Cámara")]
        [SerializeField] private float _hardLandingThreshold = 10f;

        private readonly RaycastHit[] _groundHits = new RaycastHit[1];
        private readonly Collider[] _kickColliders = new Collider[8];

        private LegsInputData _pendingInput;
        private bool _isGrounded;
        private float _nextKickTime;
        private bool _isFused;

        public Vector2 MoveInput => _pendingInput.MoveDirection;
        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _pendingInput.SprintHeld;
        public bool CanKick => Time.time >= _nextKickTime;

        public event Action OnKicked;

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

            if (_rigidbody != null)
            {
                _baseMass = _rigidbody.mass;
            }

            if (_fusionSocket == null)
            {
                _fusionSocket = GetComponentInChildren<FusionSocket>();
            }

            if (_kickImpulseSource == null)
            {
                _kickImpulseSource = GetComponent<CinemachineImpulseSource>();
            }

            if (_inputReader == null)
            {
                _inputReader = GetComponent<LegsInputReader>();
            }

            if (_commandInvoker == null)
            {
                _commandInvoker = GetComponent<CommandInvoker>();
            }
        }

        private void Start()
        {
            if (!isSpawned && _rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }

            if (_coordinator == null)
            {
                _coordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            if (_coordinator != null)
            {
                _coordinator.RegisterLegs(this);
            }
        }

        protected override void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.UnregisterLegs(this);
            }

            base.OnDestroy();
        }

        private void Update()
        {
            bool hasAuthority = !isSpawned || isOwner;
            if (!hasAuthority || _inputReader == null)
            {
                return;
            }

            if (_commandInvoker != null)
            {
                _commandInvoker.Execute(new MoveCommand(_inputReader.MoveInput, _inputReader.IsSprintHeld));

                if (_inputReader.ConsumeJumpTrigger())
                {
                    _commandInvoker.Execute(new JumpCommand());
                }

                if (_inputReader.ConsumeKickTrigger())
                {
                    _commandInvoker.Execute(new KickCommand());
                }
                return;
            }

            LegsInputData inputData = new LegsInputData(
                _inputReader.MoveInput,
                _inputReader.ConsumeJumpTrigger(),
                _inputReader.ConsumeKickTrigger(),
                _inputReader.IsSprintHeld
            );

            if (!isSpawned || isServer)
            {
                ProcessInput(inputData);
            }
            else
            {
                SendInputServerRpc(inputData);
            }
        }

        [ServerRpc(Channel.Unreliable)]
        private void SendInputServerRpc(LegsInputData inputData, RPCInfo info = default)
        {
            ProcessInput(inputData);
        }

        private void ProcessInput(LegsInputData inputData)
        {
            _pendingInput.MoveDirection = inputData.MoveDirection;
            _pendingInput.SprintHeld = inputData.SprintHeld;

            if (inputData.JumpTriggered)
            {
                _pendingInput.JumpTriggered = true;
            }

            if (inputData.KickTriggered)
            {
                _pendingInput.KickTriggered = true;
            }
        }

        private void FixedUpdate()
        {
            bool isSimulated = !isSpawned || isServer;
            if (!isSimulated || _rigidbody == null)
            {
                return;
            }

            UpdateGroundStatus();
            ApplyLocomotion();
            ApplyJumpAndGravity();
            ApplyKick();
        }

        private void UpdateGroundStatus()
        {
            Vector3 origin = _groundCheckPoint != null ? _groundCheckPoint.position : transform.position;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, _groundHits, _groundCheckDistance, _groundLayer);
            bool wasGrounded = _isGrounded;
            _isGrounded = hitCount > 0;

            if (!wasGrounded && _isGrounded && _rigidbody != null)
            {
                if (_rigidbody.linearVelocity.y <= -_hardLandingThreshold && _kickImpulseSource != null)
                {
                    _kickImpulseSource.GenerateImpulse(Vector3.down * 0.8f);
                }
            }
        }

        private void ApplyLocomotion()
        {
            Vector2 input = _pendingInput.MoveDirection;
            float targetSpeed = _pendingInput.SprintHeld ? _sprintSpeed : _walkSpeed;

            Vector3 targetVelocity = new Vector3(input.x, 0f, input.y) * targetSpeed;
            Vector3 currentVelocity = _rigidbody.linearVelocity;

            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 newHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, _acceleration * Time.fixedDeltaTime);

            _rigidbody.linearVelocity = new Vector3(newHorizontalVelocity.x, currentVelocity.y, newHorizontalVelocity.z);

            if (targetVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
            }
        }

        private void ApplyJumpAndGravity()
        {
            if (_pendingInput.JumpTriggered)
            {
                _pendingInput.JumpTriggered = false;

                if (_isGrounded)
                {
                    Vector3 velocity = _rigidbody.linearVelocity;
                    velocity.y = _jumpForce;
                    _rigidbody.linearVelocity = velocity;
                }
            }

            if (!_isGrounded && _rigidbody.linearVelocity.y < 0f)
            {
                _rigidbody.linearVelocity += Vector3.up * (Physics.gravity.y * (_fallMultiplier - 1f) * Time.fixedDeltaTime);
            }
        }

        private void ApplyKick()
        {
            if (!_pendingInput.KickTriggered)
            {
                return;
            }

            _pendingInput.KickTriggered = false;

            if (Time.time < _nextKickTime)
            {
                return;
            }

            _nextKickTime = Time.time + _kickCooldown;

            Vector3 kickOrigin = _kickPoint != null ? _kickPoint.position : transform.position + transform.forward * 0.8f;
            int hitCount = Physics.OverlapSphereNonAlloc(kickOrigin, _kickRadius, _kickColliders, _kickLayer);

            Vector3 forwardDirection = transform.forward;

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _kickColliders[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                Vector3 direction = (col.bounds.center - transform.position).normalized;
                direction.y = 0.2f;
                direction = direction.normalized;

                if (col.TryGetComponent<IKickable>(out var kickable))
                {
                    kickable.OnKicked(col.bounds.center, direction, _kickForce);
                }
                else if (col.TryGetComponent<IPushable>(out var pushable))
                {
                    pushable.OnPushed(direction, _kickForce);
                }
                else if (col.attachedRigidbody != null)
                {
                    col.attachedRigidbody.AddForce(direction * _kickForce, ForceMode.Impulse);
                }
            }

            if (!isSpawned)
            {
                TriggerKickImpulse();
                OnKicked?.Invoke();
            }
            else
            {
                PlayKickEffectObserversRpc(kickOrigin);
            }
        }

        [ObserversRpc(runLocally: true)]
        private void PlayKickEffectObserversRpc(Vector3 origin)
        {
            TriggerKickImpulse();
            OnKicked?.Invoke();
        }

        private void TriggerKickImpulse()
        {
            if (_kickImpulseSource != null)
            {
                _kickImpulseSource.GenerateImpulse(transform.forward * _kickImpulseForce);
            }
        }

        public void SetMoveInput(Vector2 input)
        {
            _pendingInput.MoveDirection = input;
        }

        public void SetSprint(bool isSprinting)
        {
            _pendingInput.SprintHeld = isSprinting;
        }

        public void Jump()
        {
            _pendingInput.JumpTriggered = true;
        }

        public void Kick()
        {
            _pendingInput.KickTriggered = true;
        }

        public FusionSocket FusionSocket => _fusionSocket;
        public bool IsFused => _isFused;
        public bool CanFuse => _coordinator != null && _coordinator.CanFuse();

        public void SetCoordinator(RobotCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void SetFusionSocket(FusionSocket socket)
        {
            _fusionSocket = socket;
        }

        public void SetFused(bool isFused, float additionalMass)
        {
            _isFused = isFused;

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_rigidbody != null)
            {
                if (_baseMass <= 0.01f)
                {
                    _baseMass = _rigidbody.mass;
                }

                _rigidbody.mass = isFused ? (_baseMass + additionalMass) : _baseMass;
            }
        }

        public void RequestFusion()
        {
            if (_coordinator != null)
            {
                _coordinator.RequestFusion();
            }
        }

        public void RequestSeparation()
        {
            if (_coordinator != null)
            {
                _coordinator.RequestSeparation();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 groundOrigin = _groundCheckPoint != null ? _groundCheckPoint.position : transform.position;
            Gizmos.DrawLine(groundOrigin, groundOrigin + Vector3.down * _groundCheckDistance);

            Gizmos.color = Color.red;
            Vector3 kickOrigin = _kickPoint != null ? _kickPoint.position : transform.position + transform.forward * 0.8f;
            Gizmos.DrawWireSphere(kickOrigin, _kickRadius);
        }
    }
}
