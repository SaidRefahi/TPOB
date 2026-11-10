using System;
using Game.Core.Commands;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Core.Structs;
using Game.Gameplay.Player.Commands;
using Game.Gameplay.Player.Death;
using Game.Gameplay.Player.Juice;
using Game.Gameplay.Player.Robot;
using Game.Network.Scopes;
using PurrNet;
using PurrNet.Transports;
using TriInspector;
using UnityEngine;
using VContainer;

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
    public sealed class TorsoController : NetworkBehaviour, IGrabber, IThrower, IMagnetOperator, IMoveable, IClimber, IInteractOperator, IFusionOperator, IKickable, IPushable
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
        [SerializeField] private float _magnetRadius = 1.25f;

        [Group("Imán")]
        [SerializeField] private LayerMask _magneticLayer = ~0;

        [Group("Mecanismos")]
        [SerializeField] private float _interactRadius = 2.2f;

        [Group("Mecanismos")]
        [SerializeField] private LayerMask _mechanismLayer = ~0;

        private readonly Collider[] _grabColliders = new Collider[8];
        private readonly Collider[] _magnetColliders = new Collider[32];
        private readonly RaycastHit[] _magnetRaycastHits = new RaycastHit[24];
        private readonly Collider[] _mechanismColliders = new Collider[16];

        private TorsoInputData _pendingInput;
        private IGrabbable _currentHeldObject;
        private IHeavyMagneticDraggable _lockedDraggable;
        private IMagnetic _currentMagneticTarget;
        private Vector3 _magnetBeamEndPoint;
        private bool _isMagnetActive;
        private float _regrabCooldownTimer;
        private bool _isClimbing;
        private Vector2 _climbDirection;
        private bool _isFused;
        private Quaternion _fusedWorldRotation = Quaternion.identity;
        private NetworkTransform _networkTransform;
        private TumbleController _tumbleController;
        private TorsoProceduralArms _proceduralArms;
        private Collider _selfCollider;
        private IGameEventBus _eventBus;
        private PlayerDeathHandler _deathHandler;
        private float _lastInputSendTime;
        private Vector2 _lastSentMoveDirection;
        private Vector2 _lastSentAimDirection;
        private bool _lastSentMagnetHeld;

        [Group("Fusión")]
        [SerializeField] private SyncVar<Quaternion> _syncedFusedRotation = new(Quaternion.identity);

        public bool IsHoldingObject => _currentHeldObject != null;
        public IGrabbable CurrentHeldObject => _currentHeldObject;
        public float CrawlSpeed => _crawlSpeed;
        public float ThrowForce => _throwForce;
        public bool IsMagnetActive => _isMagnetActive;
        public IMagnetic CurrentMagneticTarget => _currentMagneticTarget;
        public Vector3 MagnetBeamEndPoint => _magnetBeamEndPoint;
        public Transform AimPivot => _aimPivot;
        public Transform HoldSocket => _holdSocket;
        public Transform MagnetOrigin => _magnetOrigin;
        public Vector3 InitialScale => _initialScale;

        private Vector3 _initialScale = Vector3.one;

        public event Action<IGrabbable> OnObjectGrabbed;
        public event Action<IGrabbable> OnObjectReleased;
        public event Action OnThrow;
        public event Action<bool> OnMagnetStateChanged;

        [Inject]
        public void Construct(IGameEventBus eventBus = null)
        {
            _eventBus = eventBus;
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = !isServer;
            }

            _syncedFusedRotation.onChanged += HandleSyncedFusedRotationChanged;
        }

        protected override void OnDespawned()
        {
            if (_lockedDraggable != null)
            {
                _lockedDraggable.UnlockMagnet();
                _lockedDraggable = null;
            }

            _syncedFusedRotation.onChanged -= HandleSyncedFusedRotationChanged;
            base.OnDespawned();
        }

        private void Awake()
        {
            _initialScale = transform.localScale.sqrMagnitude > 0.01f ? transform.localScale : Vector3.one;

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

            if (_deathHandler == null)
            {
                _deathHandler = GetComponent<PlayerDeathHandler>();
            }

            if (_networkTransform == null)
            {
                _networkTransform = GetComponent<NetworkTransform>();
            }

            if (_tumbleController == null)
            {
                _tumbleController = GetComponent<TumbleController>();
            }

            if (_proceduralArms == null)
            {
                _proceduralArms = GetComponent<TorsoProceduralArms>();
            }

            _selfCollider = GetComponent<Collider>();
            if (_selfCollider != null)
            {
                PhysicsMaterial zeroFrictionMat = new PhysicsMaterial("FrictionlessTorso")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
                _selfCollider.material = zeroFrictionMat;
            }

            if (GetComponent<Game.Gameplay.Player.Robot.PlayerXRaySilhouette>() == null)
            {
                var xray = gameObject.AddComponent<Game.Gameplay.Player.Robot.PlayerXRaySilhouette>();
                xray.SetColor(new Color(1f, 0.43f, 0f, 0.85f));
            }

            if (GetComponent<Game.Gameplay.Player.Juice.MagneticBeamVisualizer>() == null)
            {
                gameObject.AddComponent<Game.Gameplay.Player.Juice.MagneticBeamVisualizer>();
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
            if (_deathHandler != null)
            {
                _deathHandler.OnRespawned -= HandleRespawned;
            }

            _syncedFusedRotation.onChanged -= HandleSyncedFusedRotationChanged;

            if (_coordinator != null)
            {
                _coordinator.UnregisterTorso(this);
            }

            base.OnDestroy();
        }

        private void OnEnable()
        {
            if (_deathHandler == null)
            {
                _deathHandler = GetComponent<PlayerDeathHandler>();
            }

            if (_deathHandler != null)
            {
                _deathHandler.OnRespawned += HandleRespawned;
            }
        }

        private void OnDisable()
        {
            if (_deathHandler != null)
            {
                _deathHandler.OnRespawned -= HandleRespawned;
            }
        }

        private void HandleRespawned()
        {
            transform.SetParent(null, true);

            if (_rigidbody != null)
            {
                _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }

            if (_lockedDraggable != null)
            {
                _lockedDraggable.UnlockMagnet();
                _lockedDraggable = null;
            }

            if (_currentHeldObject != null)
            {
                ReleaseHeldObject();
            }

            _isClimbing = false;
        }

        private void Update()
        {
            if (_deathHandler != null && !_deathHandler.IsAlive)
            {
                return;
            }

            if (_regrabCooldownTimer > 0f)
            {
                _regrabCooldownTimer -= Time.deltaTime;
            }

            bool hasAuthority = !isSpawned ? true : isOwner;
            if (!hasAuthority || _inputReader == null)
            {
                return;
            }

            if (_inputReader.ConsumeFuseTrigger())
            {
                if (_isFused)
                {
                    RequestSeparation();
                }
                else
                {
                    RequestFusion();
                }
            }

            TorsoInputData inputData = new TorsoInputData(
                _inputReader.MoveInput,
                _inputReader.LookInput,
                _inputReader.ConsumeGrabTrigger(),
                _inputReader.ConsumeThrowTrigger(),
                _inputReader.IsMagnetHeld,
                _inputReader.ConsumeInteractTrigger()
            );

            if (_isFused)
            {
                Vector2 aim = inputData.AimDirection;
                if (aim.sqrMagnitude >= 0.04f)
                {
                    Vector3 aimDir = new Vector3(aim.x, 0f, aim.y).normalized;
                    Quaternion targetRot = Quaternion.LookRotation(aimDir, Vector3.up);
                    _fusedWorldRotation = Quaternion.Slerp(_fusedWorldRotation, targetRot, _aimRotationSpeed * Time.deltaTime);
                }
                else
                {
                    Quaternion targetRot = Coordinator != null && Coordinator.Legs != null
                        ? Coordinator.Legs.transform.rotation
                        : (transform.parent != null ? transform.parent.rotation : _fusedWorldRotation);
                    _fusedWorldRotation = Quaternion.Slerp(_fusedWorldRotation, targetRot, _aimRotationSpeed * Time.deltaTime);
                }
            }

            if (!isSpawned || isServer)
            {
                ProcessTorsoInput(inputData);
            }
            else
            {
                _pendingInput.MoveDirection = inputData.MoveDirection;
                _pendingInput.AimDirection = inputData.AimDirection;
                _pendingInput.MagnetHeld = inputData.MagnetHeld;

                if (inputData.MagnetHeld != _isMagnetActive)
                {
                    _isMagnetActive = inputData.MagnetHeld;
                    OnMagnetStateChanged?.Invoke(_isMagnetActive);
                }

                bool actionsTriggered = inputData.GrabTriggered || inputData.ThrowTriggered || inputData.InteractTriggered;
                if (actionsTriggered)
                {
                    SendTorsoActionServerRpc(inputData.GrabTriggered, inputData.ThrowTriggered, inputData.InteractTriggered);
                }

                float now = Time.time;
                bool moveChanged = (inputData.MoveDirection - _lastSentMoveDirection).sqrMagnitude > 0.001f;
                bool aimChanged = (inputData.AimDirection - _lastSentAimDirection).sqrMagnitude > 0.001f;
                bool magnetChanged = inputData.MagnetHeld != _lastSentMagnetHeld;
                bool tickElapsed = (now - _lastInputSendTime) >= 0.02f;

                if (actionsTriggered || moveChanged || aimChanged || magnetChanged || tickElapsed)
                {
                    _lastInputSendTime = now;
                    _lastSentMoveDirection = inputData.MoveDirection;
                    _lastSentAimDirection = inputData.AimDirection;
                    _lastSentMagnetHeld = inputData.MagnetHeld;

                    SendTorsoInputServerRpc(inputData);
                }
            }

            if (_isMagnetActive)
            {
                UpdateMagnetTargeting();
            }
            else if (_currentMagneticTarget != null)
            {
                _currentMagneticTarget = null;
            }
        }

        private void LateUpdate()
        {
            if (_isFused)
            {
                transform.rotation = _fusedWorldRotation;
            }
        }

        [ServerRpc(Channel.Unreliable, requireOwnership: false)]
        private void SendTorsoInputServerRpc(TorsoInputData inputData, RPCInfo info = default)
        {
            if (owner.HasValue && info.sender != owner.Value && !info.sender.isServer)
            {
                return;
            }
            ProcessTorsoInput(inputData);
        }

        [ServerRpc(Channel.ReliableOrdered, requireOwnership: false)]
        private void SendTorsoActionServerRpc(bool grab, bool throwTrigger, bool interact, RPCInfo info = default)
        {
            if (owner.HasValue && info.sender != owner.Value && !info.sender.isServer)
            {
                return;
            }
            if (grab) _pendingInput.GrabTriggered = true;
            if (throwTrigger) _pendingInput.ThrowTriggered = true;
            if (interact) _pendingInput.InteractTriggered = true;
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
            if (_deathHandler != null && !_deathHandler.IsAlive)
            {
                return;
            }

            bool isSimulated = !isSpawned || isServer;
            if (!isSimulated || _rigidbody == null)
            {
                return;
            }

            if (!_isFused && !_rigidbody.isKinematic)
            {
                if (_tumbleController != null && _tumbleController.IsTumbling)
                {
                    // Tumble physics in progress
                }
                else if (_isClimbing)
                {
                    ApplyClimbing();
                }
                else
                {
                    ApplyCrawlLocomotion();
                }
            }

            ApplyAiming();
            if (!IsHoldingObject)
            {
                ApplyMechanismInteraction();
            }
            ApplyGrabAndRelease();
            ApplyThrow();
            ApplyMagnet();

            if (!_rigidbody.isKinematic)
            {
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void ApplyClimbing()
        {
            if (IsHoldingObject)
            {
                Vector3 currentVel = _rigidbody.linearVelocity;
                _rigidbody.linearVelocity = new Vector3(0f, 0f, currentVel.z);
                return;
            }

            Vector3 currentVelocity = _rigidbody.linearVelocity;
            Vector3 targetVelocity = new Vector3(_climbDirection.x * _crawlSpeed, _climbDirection.y * _crawlSpeed, currentVelocity.z);
            _rigidbody.linearVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, _acceleration * Time.fixedDeltaTime);
        }

        private void ApplyCrawlLocomotion()
        {
            Vector3 currentVelocity = _rigidbody.linearVelocity;
            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

            if (IsHoldingObject)
            {
                Vector3 stoppedHorizontal = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, (_acceleration * 2.5f) * Time.fixedDeltaTime);
                _rigidbody.linearVelocity = new Vector3(stoppedHorizontal.x, currentVelocity.y, stoppedHorizontal.z);
                return;
            }

            Vector2 input = _pendingInput.MoveDirection;
            if (input.sqrMagnitude < 0.01f)
            {
                Vector3 deceleratedHorizontal = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, (_acceleration * 1.5f) * Time.fixedDeltaTime);
                _rigidbody.linearVelocity = new Vector3(deceleratedHorizontal.x, currentVelocity.y, deceleratedHorizontal.z);
                return;
            }

            float surgeMultiplier = 1f;
            if (_proceduralArms != null && _proceduralArms.ComputeCrawlSupportCentroid(out _, out _, out float pullSurge))
            {
                surgeMultiplier = pullSurge;
            }

            Vector3 desiredMoveDir = new Vector3(input.x, 0f, input.y);
            if (desiredMoveDir.sqrMagnitude > 1f)
            {
                desiredMoveDir.Normalize();
            }

            Vector3 targetVelocity = desiredMoveDir * (_crawlSpeed * surgeMultiplier);
            Vector3 newHorizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, _acceleration * Time.fixedDeltaTime);

            _rigidbody.linearVelocity = new Vector3(newHorizontalVelocity.x, currentVelocity.y, newHorizontalVelocity.z);

            if (_pendingInput.AimDirection.sqrMagnitude < 0.04f && desiredMoveDir.sqrMagnitude > 0.001f)
            {
                Quaternion crawlRotation = Quaternion.LookRotation(desiredMoveDir.normalized, Vector3.up);
                Quaternion nextRot = Quaternion.Slerp(_rigidbody.rotation, crawlRotation, _aimRotationSpeed * Time.fixedDeltaTime);
                _rigidbody.MoveRotation(nextRot);
            }
        }

        private void ApplyAiming()
        {
            Vector2 aimInput = _pendingInput.AimDirection;

            if (_isFused)
            {
                if (aimInput.sqrMagnitude >= 0.04f)
                {
                    Vector3 aimDir = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(aimDir, Vector3.up);
                    _fusedWorldRotation = Quaternion.Slerp(_fusedWorldRotation, targetRotation, _aimRotationSpeed * Time.fixedDeltaTime);
                }
                else
                {
                    Quaternion targetRot = Coordinator != null && Coordinator.Legs != null
                        ? Coordinator.Legs.transform.rotation
                        : (transform.parent != null ? transform.parent.rotation : _fusedWorldRotation);
                    _fusedWorldRotation = Quaternion.Slerp(_fusedWorldRotation, targetRot, _aimRotationSpeed * Time.fixedDeltaTime);
                }

                transform.rotation = _fusedWorldRotation;
                if (isServer)
                {
                    _syncedFusedRotation.value = _fusedWorldRotation;
                }

                if (_aimPivot != null && _aimPivot != transform)
                {
                    _aimPivot.localRotation = Quaternion.identity;
                }
                return;
            }

            if (aimInput.sqrMagnitude < 0.04f)
            {
                return;
            }

            Vector3 crawlAimDir = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
            Quaternion crawlTargetRotation = Quaternion.LookRotation(crawlAimDir, Vector3.up);

            Quaternion nextAimRot = Quaternion.Slerp(_rigidbody.rotation, crawlTargetRotation, _aimRotationSpeed * Time.fixedDeltaTime);
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.MoveRotation(nextAimRot);
            }
            else
            {
                transform.rotation = nextAimRot;
            }

            if (_aimPivot != null && _aimPivot != transform)
            {
                _aimPivot.localRotation = Quaternion.identity;
            }
        }

        private void ApplyGrabAndRelease()
        {
            bool wantsReleaseOrGrab = _pendingInput.GrabTriggered || (IsHoldingObject && _pendingInput.InteractTriggered);
            if (!wantsReleaseOrGrab)
            {
                return;
            }

            _pendingInput.GrabTriggered = false;
            _pendingInput.InteractTriggered = false;

            if (IsHoldingObject)
            {
                ReleaseHeldObject();
                return;
            }

            if (_regrabCooldownTimer > 0f)
            {
                return;
            }

            Vector3 searchCenter = _holdSocket.position;
            int count = Physics.OverlapSphereNonAlloc(searchCenter, _grabRadius, _grabColliders, _grabLayer, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _grabColliders[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                var grabbable = col.GetComponentInParent<IGrabbable>();
                if (grabbable != null && !grabbable.IsGrabbed)
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
            else if (isServer)
            {
                NetworkIdentity targetId = null;
                if (target is Component comp)
                {
                    targetId = comp.GetComponent<NetworkIdentity>();
                }
                NotifyObjectGrabbedObserversRpc(targetId);
            }
            else
            {
                OnObjectGrabbed?.Invoke(_currentHeldObject);
            }
        }

        public void ReleaseHeldObject()
        {
            if (!IsHoldingObject)
            {
                return;
            }

            _regrabCooldownTimer = 0.35f;
            var released = _currentHeldObject;
            _currentHeldObject = null;
            Vector3 forward = _aimPivot != null ? _aimPivot.forward : transform.forward;
            Vector3 releaseVelocity = forward * 1.5f + Vector3.up * 0.5f;
            released.OnReleased(releaseVelocity);

            if (!isSpawned)
            {
                OnObjectReleased?.Invoke(released);
            }
            else if (isServer)
            {
                NotifyObjectReleasedObserversRpc();
            }
            else
            {
                OnObjectReleased?.Invoke(released);
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

            _regrabCooldownTimer = 0.35f;

            Vector3 forward = _aimPivot != null ? _aimPivot.forward : transform.forward;
            Vector3 throwVelocity = (forward + Vector3.up * _upwardThrowArc).normalized * _throwForce;

            var thrownObject = _currentHeldObject;
            _currentHeldObject = null;
            thrownObject.OnReleased(throwVelocity);

            if (!isSpawned)
            {
                OnThrow?.Invoke();
            }
            else if (isServer)
            {
                NotifyThrowObserversRpc();
            }
            else
            {
                OnThrow?.Invoke();
            }
        }

        private void UpdateMagnetTargeting()
        {
            if (!_isMagnetActive)
            {
                _currentMagneticTarget = null;
                _magnetBeamEndPoint = _magnetOrigin != null ? _magnetOrigin.position : transform.position;
                if (_lockedDraggable != null)
                {
                    _lockedDraggable.UnlockMagnet();
                    _lockedDraggable = null;
                }
                return;
            }

            Vector3 origin = _magnetOrigin != null ? _magnetOrigin.position : transform.position;
            Vector3 aimDir = _aimPivot != null ? _aimPivot.forward : transform.forward;
            if (aimDir.sqrMagnitude < 0.001f)
            {
                aimDir = transform.forward;
            }
            aimDir.Normalize();

            _magnetBeamEndPoint = origin + aimDir * _magnetRange;

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                _magnetRadius,
                aimDir,
                _magnetRaycastHits,
                _magnetRange,
                _magneticLayer,
                QueryTriggerInteraction.Collide
            );

            IMagnetic bestTarget = null;
            float closestSqrDist = float.MaxValue;
            RaycastHit closestWallHit = default;
            bool wallBlocked = false;
            float wallHitDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _magnetRaycastHits[i];
                Collider col = hit.collider;
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                var heavyDraggable = col.GetComponentInParent<IHeavyMagneticDraggable>();
                var magnetic = heavyDraggable != null ? (IMagnetic)heavyDraggable : col.GetComponentInParent<IMagnetic>();

                if (magnetic == null)
                {
                    if (!col.isTrigger && hit.distance > 0.01f && hit.distance < wallHitDistance)
                    {
                        wallHitDistance = hit.distance;
                        closestWallHit = hit;
                        wallBlocked = true;
                    }
                    continue;
                }

                if (heavyDraggable != null)
                {
                    if (heavyDraggable.IsMagnetLocked && heavyDraggable != _lockedDraggable)
                    {
                        continue;
                    }
                }
                else if (!magnetic.CanBeAttracted)
                {
                    continue;
                }

                float sqrDist = (hit.point - origin).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    bestTarget = magnetic;
                }
            }

            if (bestTarget != null)
            {
                Vector3 targetCenter = bestTarget.Transform != null ? bestTarget.Transform.position : origin;
                Vector3 toTarget = targetCenter - origin;
                float targetDist = toTarget.magnitude;

                if (wallBlocked && wallHitDistance < targetDist - 0.2f)
                {
                    bestTarget = null;
                    _magnetBeamEndPoint = closestWallHit.point;
                }
                else
                {
                    _magnetBeamEndPoint = targetCenter;
                }
            }
            else if (wallBlocked)
            {
                _magnetBeamEndPoint = closestWallHit.point;
            }

            if (bestTarget == null && !wallBlocked)
            {
                int closeCount = Physics.OverlapSphereNonAlloc(origin, 1.8f, _magnetColliders, _magneticLayer, QueryTriggerInteraction.Collide);
                for (int i = 0; i < closeCount; i++)
                {
                    Collider col = _magnetColliders[i];
                    if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;

                    var heavy = col.GetComponentInParent<IHeavyMagneticDraggable>();
                    var mag = heavy != null ? (IMagnetic)heavy : col.GetComponentInParent<IMagnetic>();
                    if (mag != null && (heavy != null || mag.CanBeAttracted))
                    {
                        bestTarget = mag;
                        _magnetBeamEndPoint = mag.Transform != null ? mag.Transform.position : origin;
                        break;
                    }
                }
            }

            _currentMagneticTarget = bestTarget;
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
                else if (isServer)
                {
                    NotifyMagnetStateObserversRpc(_isMagnetActive);
                }
                else
                {
                    OnMagnetStateChanged?.Invoke(_isMagnetActive);
                }
            }

            UpdateMagnetTargeting();

            if (!_isMagnetActive)
            {
                return;
            }

            Vector3 origin = _magnetOrigin != null ? _magnetOrigin.position : transform.position;

            if (_currentMagneticTarget is IHeavyMagneticDraggable heavyDraggable)
            {
                if (_lockedDraggable == null && !heavyDraggable.IsMagnetLocked)
                {
                    heavyDraggable.LockMagnet(_magnetOrigin != null ? _magnetOrigin : transform);
                    if (heavyDraggable.IsMagnetLocked)
                    {
                        _lockedDraggable = heavyDraggable;
                    }
                }
            }
            else if (_currentMagneticTarget != null)
            {
                if (_lockedDraggable != null)
                {
                    _lockedDraggable.UnlockMagnet();
                    _lockedDraggable = null;
                }

                if (_currentMagneticTarget.CanBeAttracted)
                {
                    _currentMagneticTarget.ApplyMagneticForce(origin, _magnetForce, Time.fixedDeltaTime);
                }
            }
            else
            {
                if (_lockedDraggable != null)
                {
                    _lockedDraggable.UnlockMagnet();
                    _lockedDraggable = null;
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

            Vector3 center = transform.position + transform.forward * 0.3f;
            int count = Physics.OverlapSphereNonAlloc(center, _interactRadius, _mechanismColliders, _mechanismLayer, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider col = _mechanismColliders[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;

                if (col.TryGetComponent<IInteractableMechanism>(out var mechanism) ||
                    (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<IInteractableMechanism>(out mechanism)) ||
                    (col.GetComponentInParent<IInteractableMechanism>() is { } pMech && (mechanism = pMech) != null))
                {
                    mechanism.Toggle(gameObject);
                    _pendingInput.GrabTriggered = false;
                    break;
                }
            }
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyObjectGrabbedObserversRpc(NetworkIdentity targetIdentity)
        {
            if (isServer)
            {
                OnObjectGrabbed?.Invoke(_currentHeldObject);
                return;
            }

            if (targetIdentity != null && targetIdentity.TryGetComponent<IGrabbable>(out var grabbable))
            {
                _currentHeldObject = grabbable;
                _currentHeldObject.OnGrabbed(_holdSocket);
            }

            OnObjectGrabbed?.Invoke(_currentHeldObject);
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyObjectReleasedObserversRpc()
        {
            var released = _currentHeldObject;
            _currentHeldObject = null;
            if (!isServer && released != null)
            {
                Vector3 forward = _aimPivot != null ? _aimPivot.forward : transform.forward;
                Vector3 releaseVelocity = forward * 1.5f + Vector3.up * 0.5f;
                released.OnReleased(releaseVelocity);
            }
            OnObjectReleased?.Invoke(released);
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyThrowObserversRpc()
        {
            var thrownObject = _currentHeldObject;
            _currentHeldObject = null;
            if (!isServer && thrownObject != null)
            {
                Vector3 forward = _aimPivot != null ? _aimPivot.forward : transform.forward;
                Vector3 throwVelocity = (forward + Vector3.up * _upwardThrowArc).normalized * _throwForce;
                thrownObject.OnReleased(throwVelocity);
            }
            OnThrow?.Invoke();
        }

        [ObserversRpc(runLocally: true)]
        private void NotifyMagnetStateObserversRpc(bool active)
        {
            if (_isMagnetActive == active) return;
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
        public RobotCoordinator Coordinator => _coordinator != null ? _coordinator : (_coordinator = FindFirstObjectByType<RobotCoordinator>());
        public bool CanFuse => (Coordinator != null) && _coordinator.CanFuse();
        public void ClearPendingFuseTrigger()
        {
            if (_inputReader != null)
            {
                _inputReader.ConsumeFuseTrigger();
            }
        }

        public void SetCoordinator(RobotCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public void SetFused(bool isFused, Quaternion initialRotation = default)
        {
            _isFused = isFused;

            if (isFused)
            {
                _fusedWorldRotation = initialRotation != default ? initialRotation : transform.rotation;
                if (isServer)
                {
                    _syncedFusedRotation.value = _fusedWorldRotation;
                }
            }

            if (_networkTransform == null)
            {
                _networkTransform = GetComponent<NetworkTransform>();
            }

            if (_networkTransform != null)
            {
                _networkTransform.enabled = !isFused;
            }

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }

                bool canSimulate = !isSpawned || isServer;
                _rigidbody.isKinematic = isFused || !canSimulate;
                if (isFused)
                {
                    _rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                }
                else
                {
                    _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
            }

            if (!isFused && _proceduralArms != null)
            {
                _proceduralArms.SnapAllHandsToGround();
            }
        }

        private void HandleSyncedFusedRotationChanged(Quaternion newRotation)
        {
            if (isOwner)
            {
                return;
            }

            _fusedWorldRotation = newRotation;
        }

        public void RequestFusion()
        {
            if (_coordinator == null)
            {
                _coordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            if (_coordinator != null)
            {
                _coordinator.RequestFusion();
            }
            else
            {
                Debug.LogWarning("[TorsoController] Cannot RequestFusion: RobotCoordinator not found in scene.");
            }
        }

        public void RequestSeparation()
        {
            if (_coordinator == null)
            {
                _coordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            if (_coordinator != null)
            {
                _coordinator.RequestSeparation();
            }
            else
            {
                Debug.LogWarning("[TorsoController] Cannot RequestSeparation: RobotCoordinator not found in scene.");
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

        public void OnKicked(Vector3 hitPoint, Vector3 direction, float kickForce)
        {
            if (_isFused || _rigidbody == null || _rigidbody.isKinematic) return;

            if (isSpawned && !isServer)
            {
                OnKickedServerRpc(hitPoint, direction, kickForce);
                return;
            }

            Vector3 launchDir = (direction + Vector3.up * 0.45f).normalized;
            _rigidbody.linearVelocity = launchDir * (kickForce * 1.35f);

            if (_tumbleController != null && kickForce >= 8f)
            {
                _tumbleController.TriggerTumble(launchDir * kickForce);
            }

            _eventBus?.Publish(new Game.Core.Events.AudioCueEvent(Game.Core.Enums.AudioCue.Kick, hitPoint, 1f, 1f));
        }

        [ServerRpc(requireOwnership: false)]
        private void OnKickedServerRpc(Vector3 hitPoint, Vector3 direction, float kickForce)
        {
            OnKicked(hitPoint, direction, kickForce);
        }

        public void OnPushed(Vector3 direction, float force)
        {
            if (_isFused || _rigidbody == null || _rigidbody.isKinematic) return;
            _rigidbody.AddForce(direction * force, ForceMode.Impulse);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 holdPos = _holdSocket != null ? _holdSocket.position : transform.position;
            Gizmos.DrawWireSphere(holdPos, _grabRadius);

            Gizmos.color = Color.cyan;
            Vector3 magPos = _magnetOrigin != null ? _magnetOrigin.position : transform.position;
            Vector3 aimDir = _aimPivot != null ? _aimPivot.forward : transform.forward;
            Gizmos.DrawRay(magPos, aimDir * _magnetRange);
            Gizmos.DrawWireSphere(magPos + aimDir * _magnetRange, _magnetRadius);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
        }
    }
}
