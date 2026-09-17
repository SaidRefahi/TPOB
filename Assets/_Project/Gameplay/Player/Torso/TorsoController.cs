using System;
using Game.Core.Commands;
using Game.Core.Interfaces;
using Game.Core.Structs;
using Game.Gameplay.Player.Commands;
using Game.Gameplay.Player.Robot;
using PurrNet;
using PurrNet.Transports;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Player.Torso
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DeclareBoxGroup("Componentes")]
    [DeclareBoxGroup("Locomoción Torso")]
    [DeclareBoxGroup("Apuntado")]
    [DeclareBoxGroup("Manipulación y Agarre")]
    [DeclareBoxGroup("Lanzamiento")]
    [DeclareBoxGroup("Imán")]
    [DeclareBoxGroup("Mecanismos")]
    [DeclareBoxGroup("Fusión")]
    public sealed class TorsoController : NetworkBehaviour, IGrabber, IThrower, IMagnetOperator, IMoveable, IClimber, IInteractOperator, IFusionOperator
    {
        [Group("Componentes")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Componentes")]
        [SerializeField] private RobotCoordinator _coordinator;

        [Group("Componentes")]
        [SerializeField] private TorsoInputReader _inputReader;

        [Group("Componentes")]
        [SerializeField] private CommandInvoker _commandInvoker;

        [Group("Componentes")]
        [SerializeField] private Transform _aimPivot;

        [Group("Componentes")]
        [SerializeField] private Transform _holdSocket;

        [Group("Componentes")]
        [SerializeField] private Transform _magnetOrigin;

        [Group("Locomoción Torso")]
        [SerializeField] private float _crawlSpeed = 3.5f;

        [Group("Locomoción Torso")]
        [SerializeField] private float _acceleration = 18f;

        [Group("Apuntado")]
        [SerializeField] private float _aimRotationSpeed = 16f;

        [Group("Manipulación y Agarre")]
        [SerializeField] private float _grabRadius = 1.4f;

        [Group("Manipulación y Agarre")]
        [SerializeField] private LayerMask _grabLayer = ~0;

        [Group("Lanzamiento")]
        [SerializeField] private float _throwForce = 14f;

        [Group("Lanzamiento")]
        [SerializeField] private float _upwardThrowArc = 0.35f;

        [Group("Imán")]
        [SerializeField] private float _magnetRange = 7f;

        [Group("Imán")]
        [SerializeField] private float _magnetForce = 22f;

        [Group("Imán")]
        [SerializeField] private LayerMask _magneticLayer = ~0;

        [Group("Mecanismos")]
        [SerializeField] private float _interactRadius = 1.8f;

        [Group("Mecanismos")]
        [SerializeField] private LayerMask _mechanismLayer = ~0;

        private readonly Collider[] _grabColliders = new Collider[8];
        private readonly Collider[] _magnetColliders = new Collider[16];
        private readonly Collider[] _mechanismColliders = new Collider[8];

        private TorsoInputData _pendingInput;
        private IGrabbable _currentHeldObject;
        private bool _isMagnetActive;
        private bool _isClimbing;
        private Vector2 _climbDirection;
        private bool _isFused;

        public bool IsHoldingObject => _currentHeldObject != null;
        public IGrabbable CurrentHeldObject => _currentHeldObject;
        public float ThrowForce => _throwForce;
        public bool IsMagnetActive => _isMagnetActive;

        public event Action<IGrabbable> OnObjectGrabbed;
        public event Action<IGrabbable> OnObjectReleased;
        public event Action OnThrow;
        public event Action<bool> OnMagnetStateChanged;

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

            if (_inputReader == null)
            {
                _inputReader = GetComponent<TorsoInputReader>();
            }

            if (_commandInvoker == null)
            {
                _commandInvoker = GetComponent<CommandInvoker>();
            }

            if (_holdSocket == null)
            {
                _holdSocket = transform;
            }

            if (_magnetOrigin == null)
            {
                _magnetOrigin = transform;
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
                _coordinator.RegisterTorso(this);
            }
        }

        protected override void OnDestroy()
        {
            if (_coordinator != null)
            {
                _coordinator.UnregisterTorso(this);
            }

            base.OnDestroy();
        }

        private void Update()
        {
            bool hasAuthority = !isSpawned ? true : isOwner;
            if (!hasAuthority || _inputReader == null)
            {
                return;
            }

            TorsoInputData inputData = new TorsoInputData(
                _inputReader.MoveInput,
                _inputReader.LookInput,
                _inputReader.ConsumeGrabTrigger(),
                _inputReader.ConsumeThrowTrigger(),
                _inputReader.IsMagnetHeld,
                _inputReader.ConsumeInteractTrigger()
            );

            if (!isSpawned || isServer)
            {
                ProcessTorsoInput(inputData);
            }
            else
            {
                SendTorsoInputServerRpc(inputData);
            }
        }

        [ServerRpc(Channel.Unreliable)]
        private void SendTorsoInputServerRpc(TorsoInputData inputData, RPCInfo info = default)
        {
            ProcessTorsoInput(inputData);
        }

        private void ProcessTorsoInput(TorsoInputData inputData)
        {
            _pendingInput.MoveDirection = inputData.MoveDirection;
            _pendingInput.AimDirection = inputData.AimDirection;
            _pendingInput.MagnetHeld = inputData.MagnetHeld;

            if (inputData.GrabTriggered) _pendingInput.GrabTriggered = true;
            if (inputData.ThrowTriggered) _pendingInput.ThrowTriggered = true;
            if (inputData.InteractTriggered) _pendingInput.InteractTriggered = true;
        }

        private void FixedUpdate()
        {
            bool isSimulated = !isSpawned || isServer;
            if (!isSimulated || _rigidbody == null)
            {
                return;
            }

            if (!_isFused && !_rigidbody.isKinematic)
            {
                if (_isClimbing)
                {
                    ApplyClimbing();
                }
                else
                {
                    ApplyCrawlLocomotion();
                }
            }

            ApplyAiming();
            ApplyGrabAndRelease();
            ApplyThrow();
            ApplyMagnet();
            ApplyMechanismInteraction();
        }

        private void ApplyClimbing()
        {
            Vector3 currentVelocity = _rigidbody.linearVelocity;
            Vector3 targetVelocity = new Vector3(_climbDirection.x * _crawlSpeed, _climbDirection.y * _crawlSpeed, currentVelocity.z);
            _rigidbody.linearVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, _acceleration * Time.fixedDeltaTime);
        }

        private void ApplyCrawlLocomotion()
        {
            Vector2 input = _pendingInput.MoveDirection;
            Vector3 targetVelocity = new Vector3(input.x, 0f, input.y) * _crawlSpeed;
            Vector3 currentVelocity = _rigidbody.linearVelocity;

            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            Vector3 newHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, _acceleration * Time.fixedDeltaTime);

            _rigidbody.linearVelocity = new Vector3(newHorizontalVelocity.x, currentVelocity.y, newHorizontalVelocity.z);

            if (_pendingInput.AimDirection.sqrMagnitude < 0.04f && targetVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion crawlRotation = Quaternion.LookRotation(targetVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, crawlRotation, _aimRotationSpeed * Time.fixedDeltaTime);
            }
        }

        private void ApplyAiming()
        {
            Vector2 aimInput = _pendingInput.AimDirection;
            if (aimInput.sqrMagnitude < 0.04f)
            {
                return;
            }

            Vector3 aimDir = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(aimDir, Vector3.up);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _aimRotationSpeed * Time.fixedDeltaTime);
            if (_aimPivot != null && _aimPivot != transform)
            {
                _aimPivot.localRotation = Quaternion.identity;
            }
        }

        private void ApplyGrabAndRelease()
        {
            if (!_pendingInput.GrabTriggered)
            {
                return;
            }

            _pendingInput.GrabTriggered = false;

            if (IsHoldingObject)
            {
                ReleaseHeldObject();
                return;
            }

            Vector3 searchCenter = _holdSocket.position;
            int count = Physics.OverlapSphereNonAlloc(searchCenter, _grabRadius, _grabColliders, _grabLayer);

            for (int i = 0; i < count; i++)
            {
                Collider col = _grabColliders[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (col.TryGetComponent<IGrabbable>(out var grabbable) && !grabbable.IsGrabbed)
                {
                    Grab(grabbable);
                    break;
                }
            }
        }

        public void Grab(IGrabbable target)
        {
            if (target == null || IsHoldingObject)
            {
                return;
            }

            _currentHeldObject = target;
            _currentHeldObject.OnGrabbed(_holdSocket);

            if (!isSpawned)
            {
                OnObjectGrabbed?.Invoke(_currentHeldObject);
            }
            else
            {
                NotifyObjectGrabbedObserversRpc();
            }
        }

        public void ReleaseHeldObject()
        {
            if (!IsHoldingObject)
            {
                return;
            }

            var released = _currentHeldObject;
            _currentHeldObject = null;
            released.OnReleased(Vector3.down * 0.1f);

            if (!isSpawned)
            {
                OnObjectReleased?.Invoke(released);
            }
            else
            {
                NotifyObjectReleasedObserversRpc();
            }
        }

        private void ApplyThrow()
        {
            if (!_pendingInput.ThrowTriggered)
            {
                return;
            }

            _pendingInput.ThrowTriggered = false;

            if (!IsHoldingObject)
            {
                return;
            }

            Throw();
        }

        public void Throw()
        {
            if (!IsHoldingObject)
            {
                return;
            }

            Vector3 forward = _aimPivot != null ? _aimPivot.forward : transform.forward;
            Vector3 throwVelocity = (forward + Vector3.up * _upwardThrowArc).normalized * _throwForce;

            var thrownObject = _currentHeldObject;
            _currentHeldObject = null;
            thrownObject.OnReleased(throwVelocity);

            if (!isSpawned)
            {
                OnThrow?.Invoke();
            }
            else
            {
                NotifyThrowObserversRpc();
            }
        }

        private void ApplyMagnet()
        {
            bool wantsMagnet = _pendingInput.MagnetHeld;

            if (wantsMagnet != _isMagnetActive)
            {
                _isMagnetActive = wantsMagnet;
                if (!isSpawned)
                {
                    OnMagnetStateChanged?.Invoke(_isMagnetActive);
                }
                else
                {
                    NotifyMagnetStateObserversRpc(_isMagnetActive);
                }
            }

            if (!_isMagnetActive)
            {
                return;
            }

            Vector3 origin = _magnetOrigin.position;
            int count = Physics.OverlapSphereNonAlloc(origin, _magnetRange, _magnetColliders, _magneticLayer);

            for (int i = 0; i < count; i++)
            {
                Collider col = _magnetColliders[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (col.TryGetComponent<IMagnetic>(out var magnetic) && magnetic.CanBeAttracted)
                {
                    magnetic.ApplyMagneticForce(origin, _magnetForce, Time.fixedDeltaTime);
                }
            }
        }

        public void SetMagnetActive(bool active)
        {
            _pendingInput.MagnetHeld = active;
        }

        private void ApplyMechanismInteraction()
        {
            if (!_pendingInput.InteractTriggered)
            {
                return;
            }

            _pendingInput.InteractTriggered = false;

            Vector3 center = transform.position;
            int count = Physics.OverlapSphereNonAlloc(center, _interactRadius, _mechanismColliders, _mechanismLayer);

            for (int i = 0; i < count; i++)
            {
                Collider col = _mechanismColliders[i];
                if (col == null) continue;

                if (col.TryGetComponent<IInteractableMechanism>(out var mechanism))
                {
                    mechanism.Toggle(gameObject);
                    break;
                }
            }
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyObjectGrabbedObserversRpc()
        {
            OnObjectGrabbed?.Invoke(_currentHeldObject);
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyObjectReleasedObserversRpc()
        {
            OnObjectReleased?.Invoke(_currentHeldObject);
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyThrowObserversRpc()
        {
            OnThrow?.Invoke();
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyMagnetStateObserversRpc(bool active)
        {
            _isMagnetActive = active;
            OnMagnetStateChanged?.Invoke(active);
        }

        public Vector2 MoveInput => _pendingInput.MoveDirection;
        public bool IsGrounded => true;
        public bool IsSprinting => false;
        public void SetMoveInput(Vector2 input) => _pendingInput.MoveDirection = input;
        public void SetSprint(bool isSprinting) { }
        public void Jump() { }

        public void TriggerGrab() => _pendingInput.GrabTriggered = true;
        public void TriggerInteract() => _pendingInput.InteractTriggered = true;

        public bool IsClimbing => _isClimbing;
        public bool IsFused => _isFused;
        public bool CanFuse => _coordinator != null && _coordinator.CanFuse();

        public void SetCoordinator(RobotCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void SetFused(bool isFused)
        {
            _isFused = isFused;

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                }

                bool canSimulate = !isSpawned || isServer;
                _rigidbody.isKinematic = isFused || !canSimulate;
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

        public void Climb(Vector2 direction)
        {
            _isClimbing = direction.sqrMagnitude > 0.01f;
            _climbDirection = direction;
        }

        public void StopClimbing()
        {
            _isClimbing = false;
            _climbDirection = Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 holdPos = _holdSocket != null ? _holdSocket.position : transform.position;
            Gizmos.DrawWireSphere(holdPos, _grabRadius);

            Gizmos.color = Color.cyan;
            Vector3 magPos = _magnetOrigin != null ? _magnetOrigin.position : transform.position;
            Gizmos.DrawWireSphere(magPos, _magnetRange);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
