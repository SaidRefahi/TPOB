using System;
using Game.Core.Commands;
using Game.Core.Interfaces;
using Game.Core.Structs;
using Game.Gameplay.Player.Commands;
using Game.Gameplay.Player.Death;
using Game.Gameplay.Player.Juice;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Player.Torso;
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
    [DeclareBoxGroup("Paredes Imantadas")]
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
        [SerializeField] private float _kickRadius = 2.5f;

        [Group("Patada")]
        [SerializeField] private float _minKickForce = 2.5f;

        [Group("Patada")]
        [SerializeField] private float _maxKickForce = 28f;

        [Group("Patada")]
        [SerializeField] private float _kickChargeDuration = 1.0f;

        [Group("Patada")]
        [SerializeField] private float _kickCooldown = 0.5f;

        [Group("Patada")]
        [SerializeField] private LayerMask _kickLayer = ~0;

        [Group("Patada")]
        [SerializeField] private KickTrajectoryVisualizer _trajectoryVisualizer;

        [Group("Patada")]
        [SerializeField] private KickChargeFlash _chargeFlash;

        [Group("Impulsos de Cámara")]
        [SerializeField] private CinemachineImpulseSource _kickImpulseSource;

        [Group("Impulsos de Cámara")]
        [SerializeField] private float _kickImpulseForce = 1.2f;

        [Group("Impulsos de Cámara")]
        [SerializeField] private float _hardLandingThreshold = 10f;

        [Group("Paredes Imantadas")]
        [SerializeField] private LayerMask _magneticWallLayer = ~0;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _wallProbeDistance = 1.2f;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _wallAlignSpeed = 12f;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _wallJumpForce = 8.5f;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _magneticDetachCooldown = 0.35f;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _defaultAdhesionForce = 35f;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _probeHeightOffset = 0.45f;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _footprintRadius = 0.30f;

        [Group("Paredes Imantadas")]
        [SerializeField] private float _transitionLatchDuration = 0.25f;

        [Group("Paredes Imantadas")]
        [SerializeField] private MagneticBootsVisuals _bootsVisuals;

        private readonly RaycastHit[] _groundHits = new RaycastHit[1];
        private readonly RaycastHit[] _downCenterHits = new RaycastHit[1];
        private readonly RaycastHit[] _downFrontHits = new RaycastHit[1];
        private readonly RaycastHit[] _downBackHits = new RaycastHit[1];
        private readonly RaycastHit[] _downLeftHits = new RaycastHit[1];
        private readonly RaycastHit[] _downRightHits = new RaycastHit[1];
        private readonly RaycastHit[] _whiskerLowHits = new RaycastHit[1];
        private readonly RaycastHit[] _whiskerMidHits = new RaycastHit[1];
        private readonly RaycastHit[] _ledgeWrapHits = new RaycastHit[1];
        private readonly RaycastHit[] _wallHits = new RaycastHit[4];
        private readonly Collider[] _kickColliders = new Collider[8];
        private readonly Collider[] _wallColliders = new Collider[4];

        private LegsInputData _pendingInput;
        private bool _isGrounded;
        private bool _isOnMagneticSurface;
        private Vector3 _currentSurfaceNormal = Vector3.up;
        private Vector3 _targetSurfaceNormal = Vector3.up;
        private IMagneticSurface _currentMagneticWall;
        private float _detachCooldownTimer;
        private float _transitionLatchTimer;
        private float _nextKickTime;
        private float _catapultAirTime;
        private bool _isFused;
        private PlayerDeathHandler _deathHandler;
        private Game.Gameplay.Player.Juice.TumbleController _tumbleController;
        private Game.Network.Audio.NetworkAudioRelay _audioRelay;
        private bool _isChargingKick;
        private float _currentChargeTimer;
        private bool _hasTriggeredMaxCharge;
        private float _pendingKickCharge = 1f;
        private LegsSplineHoseAnimator _splineAnimator;
        private Collider _selfCollider;

        public Vector2 MoveInput => _pendingInput.MoveDirection;
        public bool IsGrounded => _isGrounded;
        public bool IsSprinting => _pendingInput.SprintHeld;
        public bool CanKick => Time.time >= _nextKickTime;
        public float WalkSpeed => _walkSpeed;
        public float SprintSpeed => _sprintSpeed;
        public bool IsOnMagneticSurface => _isOnMagneticSurface;
        public Vector3 CurrentSurfaceNormal => _currentSurfaceNormal;
        public Vector3 TargetSurfaceNormal => _targetSurfaceNormal;
        public bool IsTransitioning => _transitionLatchTimer > 0f;
        public LayerMask MagneticWallLayer => _magneticWallLayer;

        public event Action OnKicked;
        public event Action<bool, Vector3> OnMagneticSurfaceChanged;

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
                _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
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

            if (_deathHandler == null)
            {
                _deathHandler = GetComponent<PlayerDeathHandler>();
            }

            if (_tumbleController == null)
            {
                _tumbleController = GetComponent<Game.Gameplay.Player.Juice.TumbleController>();
            }

            if (_selfCollider == null)
            {
                _selfCollider = GetComponent<Collider>();
            }

            if (_selfCollider != null)
            {
                if (_selfCollider.sharedMaterial == null || _selfCollider.sharedMaterial.dynamicFriction > 0f)
                {
                    PhysicsMaterial frictionlessMat = new PhysicsMaterial("FrictionlessLegs")
                    {
                        dynamicFriction = 0f,
                        staticFriction = 0f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Minimum
                    };
                    _selfCollider.material = frictionlessMat;
                }
            }

            if (_splineAnimator == null)
            {
                _splineAnimator = GetComponent<LegsSplineHoseAnimator>();
                if (_splineAnimator == null)
                {
                    _splineAnimator = GetComponentInChildren<LegsSplineHoseAnimator>();
                }
            }

            if (_trajectoryVisualizer == null)
            {
                _trajectoryVisualizer = GetComponent<KickTrajectoryVisualizer>();
                if (_trajectoryVisualizer == null)
                {
                    _trajectoryVisualizer = gameObject.AddComponent<KickTrajectoryVisualizer>();
                }
            }

            if (_chargeFlash == null)
            {
                _chargeFlash = GetComponent<KickChargeFlash>();
                if (_chargeFlash == null)
                {
                    _chargeFlash = gameObject.AddComponent<KickChargeFlash>();
                }
            }

            if (_bootsVisuals == null)
            {
                _bootsVisuals = GetComponent<MagneticBootsVisuals>();
                if (_bootsVisuals == null)
                {
                    _bootsVisuals = GetComponentInChildren<MagneticBootsVisuals>();
                }
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
            CancelKickCharge();

            if (_coordinator != null)
            {
                _coordinator.UnregisterLegs(this);
            }

            base.OnDestroy();
        }

        private void OnDisable()
        {
            CancelKickCharge();
            if (_isOnMagneticSurface)
            {
                SetMagneticState(false);
            }
        }

        private void Update()
        {
            if (_deathHandler != null && !_deathHandler.IsAlive)
            {
                CancelKickCharge();
                if (_isOnMagneticSurface)
                {
                    SetMagneticState(false);
                }
                return;
            }

            if (_tumbleController != null && _tumbleController.IsTumbling)
            {
                CancelKickCharge();
                if (_isOnMagneticSurface)
                {
                    SetMagneticState(false);
                }
                return;
            }

            bool hasAuthority = !isSpawned ? true : isOwner;
            if (!hasAuthority || _inputReader == null)
            {
                return;
            }

            UpdateKickCharge();

            if (_commandInvoker != null)
            {
                _commandInvoker.Execute(new MoveCommand(_inputReader.MoveInput, _inputReader.IsSprintHeld));

                if (_inputReader.ConsumeJumpTrigger())
                {
                    _commandInvoker.Execute(new JumpCommand());
                }

                if (_inputReader.ConsumeFuseTrigger())
                {
                    if (_isFused)
                        _commandInvoker.Execute(new SeparateCommand());
                    else
                        _commandInvoker.Execute(new FuseCommand());
                }
                return;
            }

            if (_inputReader.ConsumeFuseTrigger())
            {
                if (_isFused)
                    RequestSeparation();
                else
                    RequestFusion();
            }

            LegsInputData inputData = new LegsInputData(
                _inputReader.MoveInput,
                _inputReader.ConsumeJumpTrigger(),
                false,
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
            if (_deathHandler != null && !_deathHandler.IsAlive)
            {
                return;
            }

            if (_rigidbody == null)
            {
                return;
            }

            // Both server and client compute ground contact so animations and prediction are always synchronized
            UpdateGroundStatus();

            bool isSimulated = !isSpawned || isServer;
            if (!isSimulated)
            {
                return;
            }

            if (_tumbleController != null && _tumbleController.IsTumbling)
            {
                return;
            }

            ApplyLocomotion();
            ApplyJumpAndGravity();
            ApplyKick();

            // Zero residual angular velocity to prevent PhysX contact torque from spinning the robot when idle
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        private void UpdateGroundStatus()
        {
            Vector3 origin = _groundCheckPoint != null ? _groundCheckPoint.position : transform.position;
            Vector3 travelDirection = CalculateTravelDirection();

            if (_detachCooldownTimer > 0f)
            {
                _detachCooldownTimer -= Time.fixedDeltaTime;
            }

            if (_transitionLatchTimer > 0f)
            {
                _transitionLatchTimer -= Time.fixedDeltaTime;
            }

            // 1. Proactive detection of concave transition ahead (Wall, floor, ceiling junction)
            bool concaveAhead = false;
            Vector3 concaveNormal = Vector3.up;
            IMagneticSurface concaveSurface = null;
            float concaveDist = float.MaxValue;

            if (_detachCooldownTimer <= 0f && travelDirection.sqrMagnitude > 0.001f)
            {
                concaveAhead = DetectConcaveSurfaceAhead(origin, travelDirection, out concaveNormal, out concaveSurface, out concaveDist);
            }

            // Approaching concave surface junction (threshold around 0.65m)
            if (concaveAhead && concaveDist <= (_footprintRadius + 0.35f))
            {
                if (concaveSurface != null && concaveSurface.IsActive)
                {
                    _currentMagneticWall = concaveSurface;
                    _targetSurfaceNormal = concaveNormal;
                    _transitionLatchTimer = _transitionLatchDuration;
                    SetMagneticState(true);
                    _isGrounded = true;
                    _currentSurfaceNormal = Vector3.Slerp(_currentSurfaceNormal, _targetSurfaceNormal, _wallAlignSpeed * Time.fixedDeltaTime);
                    return;
                }
                else if (concaveSurface == null && concaveNormal.y > 0.7f && _isOnMagneticSurface)
                {
                    // Transitioned from magnetic wall down to regular floor
                    _currentMagneticWall = null;
                    _targetSurfaceNormal = concaveNormal;
                    _transitionLatchTimer = _transitionLatchDuration;
                    SetMagneticState(false);
                    _isGrounded = true;
                    _currentSurfaceNormal = Vector3.Slerp(_currentSurfaceNormal, _targetSurfaceNormal, _wallAlignSpeed * Time.fixedDeltaTime);
                    return;
                }
            }

            // 2. Evaluate ground footprint under feet
            bool footprintHit = EvaluateFootprint(origin, out Vector3 footNormal, out IMagneticSurface footSurface, out float footDist);

            // 3. Proactive detection of convex ledge ahead (Outer corners / over the edge)
            bool convexAhead = false;
            Vector3 convexNormal = Vector3.up;
            IMagneticSurface convexSurface = null;

            if (!footprintHit && _isOnMagneticSurface && _detachCooldownTimer <= 0f && travelDirection.sqrMagnitude > 0.001f)
            {
                convexAhead = DetectConvexLedgeAhead(origin, travelDirection, out convexNormal, out convexSurface);
            }

            if (convexAhead)
            {
                if (convexSurface != null && convexSurface.IsActive)
                {
                    _currentMagneticWall = convexSurface;
                    _targetSurfaceNormal = convexNormal;
                    _transitionLatchTimer = _transitionLatchDuration;
                    SetMagneticState(true);
                    _isGrounded = true;
                    _currentSurfaceNormal = Vector3.Slerp(_currentSurfaceNormal, _targetSurfaceNormal, _wallAlignSpeed * Time.fixedDeltaTime);
                    return;
                }
                else if (convexSurface == null && convexNormal.y > 0.7f)
                {
                    _currentMagneticWall = null;
                    _targetSurfaceNormal = convexNormal;
                    _transitionLatchTimer = _transitionLatchDuration;
                    SetMagneticState(false);
                    _isGrounded = true;
                    _currentSurfaceNormal = Vector3.Slerp(_currentSurfaceNormal, _targetSurfaceNormal, _wallAlignSpeed * Time.fixedDeltaTime);
                    return;
                }
            }

            // 4. Ground state resolution with transition latching (hysteresis)
            if (_transitionLatchTimer > 0f)
            {
                _isGrounded = true;
                if (footprintHit && footSurface != null && footSurface.IsActive)
                {
                    _currentMagneticWall = footSurface;
                    _targetSurfaceNormal = footNormal;
                }
                _currentSurfaceNormal = Vector3.Slerp(_currentSurfaceNormal, _targetSurfaceNormal, _wallAlignSpeed * Time.fixedDeltaTime);
                return;
            }

            if (_isOnMagneticSurface)
            {
                if (footprintHit)
                {
                    if (footSurface != null && footSurface.IsActive)
                    {
                        _currentMagneticWall = footSurface;
                        _targetSurfaceNormal = footNormal;
                        _isGrounded = true;
                    }
                    else if (footNormal.y > 0.7f)
                    {
                        // Walked onto normal floor
                        SetMagneticState(false);
                        _targetSurfaceNormal = Vector3.up;
                        _isGrounded = true;
                    }
                    else
                    {
                        // Inactive wall
                        SetMagneticState(false);
                        _isGrounded = false;
                    }
                }
                else
                {
                    // Past boundary into open air
                    SetMagneticState(false);
                    _isGrounded = false;
                }
            }
            else
            {
                bool wasGrounded = _isGrounded;
                _isGrounded = footprintHit;

                if (_isGrounded)
                {
                    _targetSurfaceNormal = footNormal.sqrMagnitude > 0.01f ? footNormal : Vector3.up;
                }
                else
                {
                    _targetSurfaceNormal = Vector3.up;
                }

                if (_catapultAirTime > 0f && _rigidbody != null && _rigidbody.linearVelocity.y < 0f && _isGrounded)
                {
                    _catapultAirTime = 0f;
                }

                if (!wasGrounded && _isGrounded && _rigidbody != null)
                {
                    if (_rigidbody.linearVelocity.y <= -_hardLandingThreshold && _kickImpulseSource != null)
                    {
                        _kickImpulseSource.GenerateImpulse(Vector3.down * 0.8f);
                        PlayAudioCue(Game.Core.Enums.AudioCue.HardLanding, origin);
                    }
                }

                if (_detachCooldownTimer <= 0f)
                {
                    ProbeImmediateMagneticContact(origin);
                }
            }

            _currentSurfaceNormal = Vector3.Slerp(_currentSurfaceNormal, _targetSurfaceNormal, _wallAlignSpeed * Time.fixedDeltaTime);
        }

        private bool EvaluateFootprint(Vector3 origin, out Vector3 dominantNormal, out IMagneticSurface dominantSurface, out float minDistance)
        {
            dominantNormal = Vector3.up;
            dominantSurface = null;
            minDistance = float.MaxValue;

            Vector3 downDir = -transform.up;
            float rayDist = _isOnMagneticSurface ? (_groundCheckDistance + 0.45f) : (_groundCheckDistance + 0.25f);
            LayerMask mask = _groundLayer | _magneticWallLayer;

            Vector3 fwdOffset = transform.forward * _footprintRadius;
            Vector3 rightOffset = transform.right * _footprintRadius;
            Vector3 baseOrigin = origin + transform.up * 0.15f;

            // Probe 1: Center
            bool centerHit = CheckSingleFootProbe(baseOrigin, downDir, rayDist, mask, _downCenterHits, ref dominantNormal, ref dominantSurface, ref minDistance);
            // Probe 2: Front
            bool frontHit = CheckSingleFootProbe(baseOrigin + fwdOffset, downDir, rayDist, mask, _downFrontHits, ref dominantNormal, ref dominantSurface, ref minDistance);
            // Probe 3: Back
            bool backHit = CheckSingleFootProbe(baseOrigin - fwdOffset, downDir, rayDist, mask, _downBackHits, ref dominantNormal, ref dominantSurface, ref minDistance);
            // Probe 4: Left
            bool leftHit = CheckSingleFootProbe(baseOrigin - rightOffset, downDir, rayDist, mask, _downLeftHits, ref dominantNormal, ref dominantSurface, ref minDistance);
            // Probe 5: Right
            bool rightHit = CheckSingleFootProbe(baseOrigin + rightOffset, downDir, rayDist, mask, _downRightHits, ref dominantNormal, ref dominantSurface, ref minDistance);

            return centerHit || frontHit || backHit || leftHit || rightHit;
        }

        private bool CheckSingleFootProbe(Vector3 probeOrigin, Vector3 direction, float distance, LayerMask mask, RaycastHit[] hitBuffer, ref Vector3 bestNormal, ref IMagneticSurface bestSurface, ref float bestDistance)
        {
            int count = Physics.RaycastNonAlloc(probeOrigin, direction, hitBuffer, distance, mask, QueryTriggerInteraction.Ignore);
            if (count == 0)
            {
                return false;
            }

            RaycastHit hit = hitBuffer[0];
            Collider col = hit.collider;
            if (col == null || col == _selfCollider || col.transform.root == transform.root)
            {
                return false;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestNormal = hit.normal;

                if (col.TryGetComponent<IMagneticSurface>(out var mag) || (col.GetComponentInParent<IMagneticSurface>() is { } pMag && (mag = pMag) != null))
                {
                    if (mag.IsActive)
                    {
                        bestSurface = mag;
                        Vector3 surfaceNorm = mag.GetSurfaceNormal(hit.point);
                        if (surfaceNorm.sqrMagnitude > 0.01f)
                        {
                            bestNormal = surfaceNorm;
                        }
                    }
                }
            }

            return true;
        }

        private bool DetectConcaveSurfaceAhead(Vector3 origin, Vector3 travelDirection, out Vector3 aheadNormal, out IMagneticSurface aheadSurface, out float aheadDist)
        {
            aheadNormal = Vector3.up;
            aheadSurface = null;
            aheadDist = float.MaxValue;

            if (travelDirection.sqrMagnitude < 0.001f)
            {
                return false;
            }

            LayerMask mask = _groundLayer | _magneticWallLayer;
            Vector3 lowOrigin = origin + transform.up * 0.15f;
            Vector3 midOrigin = origin + transform.up * _probeHeightOffset;

            // Low whisker (foot level)
            bool hitLow = CheckSingleWhisker(lowOrigin, travelDirection, _wallProbeDistance, mask, _whiskerLowHits, ref aheadNormal, ref aheadSurface, ref aheadDist);
            // Mid whisker (waist level)
            bool hitMid = CheckSingleWhisker(midOrigin, travelDirection, _wallProbeDistance, mask, _whiskerMidHits, ref aheadNormal, ref aheadSurface, ref aheadDist);

            return hitLow || hitMid;
        }

        private bool CheckSingleWhisker(Vector3 probeOrigin, Vector3 direction, float distance, LayerMask mask, RaycastHit[] hitBuffer, ref Vector3 bestNormal, ref IMagneticSurface bestSurface, ref float bestDist)
        {
            int count = Physics.RaycastNonAlloc(probeOrigin, direction, hitBuffer, distance, mask, QueryTriggerInteraction.Ignore);
            if (count == 0)
            {
                return false;
            }

            RaycastHit hit = hitBuffer[0];
            Collider col = hit.collider;
            if (col == null || col == _selfCollider || col.transform.root == transform.root)
            {
                return false;
            }

            // Only consider surfaces with significant angle relative to current surface (concave junction)
            if (Vector3.Dot(_currentSurfaceNormal, hit.normal) > 0.85f)
            {
                return false;
            }

            if (hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestNormal = hit.normal;

                if (col.TryGetComponent<IMagneticSurface>(out var mag) || (col.GetComponentInParent<IMagneticSurface>() is { } pMag && (mag = pMag) != null))
                {
                    if (mag.IsActive)
                    {
                        bestSurface = mag;
                        Vector3 surfaceNorm = mag.GetSurfaceNormal(hit.point);
                        if (surfaceNorm.sqrMagnitude > 0.01f)
                        {
                            bestNormal = surfaceNorm;
                        }
                        return true;
                    }
                }
                else if (hit.normal.y > 0.7f)
                {
                    return true;
                }
            }

            return bestSurface != null || bestNormal.y > 0.7f;
        }

        private bool DetectConvexLedgeAhead(Vector3 origin, Vector3 travelDirection, out Vector3 wrapNormal, out IMagneticSurface wrapSurface)
        {
            wrapNormal = Vector3.up;
            wrapSurface = null;

            if (travelDirection.sqrMagnitude < 0.001f)
            {
                return false;
            }

            // Cast from ahead of the character back/inward around the 90° corner
            Vector3 castOrigin = origin + travelDirection * (_footprintRadius + 0.2f) + transform.up * 0.1f;
            Vector3 wrapDir = (-transform.up - travelDirection * 0.65f).normalized;
            float wrapDistance = _groundCheckDistance + 0.65f;
            LayerMask mask = _groundLayer | _magneticWallLayer;

            int count = Physics.RaycastNonAlloc(castOrigin, wrapDir, _ledgeWrapHits, wrapDistance, mask, QueryTriggerInteraction.Ignore);
            if (count == 0)
            {
                return false;
            }

            RaycastHit hit = _ledgeWrapHits[0];
            Collider col = hit.collider;
            if (col == null || col == _selfCollider || col.transform.root == transform.root)
            {
                return false;
            }

            // Must turn significantly away from current surface normal
            if (Vector3.Dot(_currentSurfaceNormal, hit.normal) > 0.85f)
            {
                return false;
            }

            if (col.TryGetComponent<IMagneticSurface>(out var mag) || (col.GetComponentInParent<IMagneticSurface>() is { } pMag && (mag = pMag) != null))
            {
                if (mag.IsActive)
                {
                    wrapSurface = mag;
                    Vector3 surfaceNorm = mag.GetSurfaceNormal(hit.point);
                    wrapNormal = surfaceNorm.sqrMagnitude > 0.01f ? surfaceNorm : hit.normal;
                    return true;
                }
            }
            else if (hit.normal.y > 0.7f)
            {
                wrapNormal = hit.normal;
                return true;
            }

            return false;
        }

        private Vector3 CalculateTravelDirection()
        {
            Vector2 input = _pendingInput.MoveDirection;

            if (_isOnMagneticSurface)
            {
                Vector3 N = _currentSurfaceNormal;
                Vector3 tangentUp;
                Vector3 tangentRight;

                if (Mathf.Abs(N.y) > 0.7f)
                {
                    tangentUp = Vector3.ProjectOnPlane(Vector3.forward, N).normalized;
                    tangentRight = Vector3.ProjectOnPlane(Vector3.right, N).normalized;
                }
                else
                {
                    tangentUp = Vector3.ProjectOnPlane(Vector3.up, N).normalized;
                    tangentRight = Vector3.Cross(N, tangentUp).normalized;
                }

                if (input.sqrMagnitude > 0.01f)
                {
                    return (tangentRight * input.x + tangentUp * input.y).normalized;
                }

                return transform.forward;
            }

            if (input.sqrMagnitude > 0.01f)
            {
                return new Vector3(input.x, 0f, input.y).normalized;
            }

            return transform.forward;
        }

        private void ProbeImmediateMagneticContact(Vector3 origin)
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(origin + transform.up * 0.4f, 0.45f, _wallColliders, _magneticWallLayer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider col = _wallColliders[i];
                if (col == null || col == _selfCollider || col.transform.root == transform.root)
                {
                    continue;
                }

                if ((col.TryGetComponent<IMagneticSurface>(out var magSurface) || (col.GetComponentInParent<IMagneticSurface>() is { } pMag && (magSurface = pMag) != null)) && magSurface.IsActive)
                {
                    _currentMagneticWall = magSurface;
                    _targetSurfaceNormal = magSurface.GetSurfaceNormal(origin);
                    _currentSurfaceNormal = _targetSurfaceNormal;
                    _transitionLatchTimer = _transitionLatchDuration;
                    SetMagneticState(true);
                    _isGrounded = true;
                    break;
                }
            }
        }

        private void SetMagneticState(bool isAttached)
        {
            if (_isOnMagneticSurface == isAttached)
            {
                return;
            }

            _isOnMagneticSurface = isAttached;

            if (_bootsVisuals != null)
            {
                _bootsVisuals.SetMagneticState(isAttached);
            }

            if (_rigidbody != null)
            {
                _rigidbody.useGravity = !isAttached;
            }

            if (isAttached)
            {
                PlayAudioCue(Game.Core.Enums.AudioCue.MagnetPull, transform.position);
            }
            else
            {
                _currentMagneticWall = null;
                _targetSurfaceNormal = Vector3.up;
                _currentSurfaceNormal = Vector3.up;
                PlayAudioCue(Game.Core.Enums.AudioCue.Release, transform.position);
            }

            OnMagneticSurfaceChanged?.Invoke(isAttached, _currentSurfaceNormal);
        }

        private void ApplyLocomotion()
        {
            if (_catapultAirTime > 0f)
            {
                _catapultAirTime -= Time.fixedDeltaTime;
                return;
            }

            Vector2 input = _pendingInput.MoveDirection;
            float targetSpeed = _pendingInput.SprintHeld ? _sprintSpeed : _walkSpeed;
            if (_coordinator != null)
            {
                targetSpeed *= _coordinator.DragSpeedMultiplier;
            }

            if (_isOnMagneticSurface)
            {
                Vector3 N = _currentSurfaceNormal;
                Vector3 tangentUp;
                Vector3 tangentRight;

                if (Mathf.Abs(N.y) > 0.7f)
                {
                    tangentUp = Vector3.ProjectOnPlane(Vector3.forward, N).normalized;
                    tangentRight = Vector3.ProjectOnPlane(Vector3.right, N).normalized;
                }
                else
                {
                    tangentUp = Vector3.ProjectOnPlane(Vector3.up, N).normalized;
                    tangentRight = Vector3.Cross(N, tangentUp).normalized;
                }

                Vector3 desiredMoveDir = (tangentRight * input.x + tangentUp * input.y);

                Vector3 currentVel = _rigidbody.linearVelocity;
                Vector3 normalVel = Vector3.Project(currentVel, N);
                Vector3 tangentVel = currentVel - normalVel;
                Vector3 targetTangentVel = desiredMoveDir * targetSpeed;

                Vector3 newTangentVel = Vector3.MoveTowards(tangentVel, targetTangentVel, _acceleration * Time.fixedDeltaTime);

                // Damp and clamp normal velocity to avoid excessive penetration or bouncing against PhysX
                float normalSpeed = Vector3.Dot(currentVel, N);
                float clampedNormalSpeed = Mathf.Clamp(normalSpeed, -0.3f, 0.1f);
                Vector3 clampedNormalVel = N * clampedNormalSpeed;

                _rigidbody.linearVelocity = newTangentVel + clampedNormalVel;

                float adhesion = _currentMagneticWall != null ? _currentMagneticWall.AdhesionForce : _defaultAdhesionForce;
                _rigidbody.AddForce(-N * adhesion, ForceMode.Acceleration);

                Quaternion surfaceAlignRot = Quaternion.FromToRotation(transform.up, N) * transform.rotation;
                if (desiredMoveDir.sqrMagnitude > 0.001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(desiredMoveDir.normalized, N);
                    transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, _rotationSpeed * Time.fixedDeltaTime);
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, surfaceAlignRot, _wallAlignSpeed * Time.fixedDeltaTime);
                }
                return;
            }

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
            else if (transform.up != Vector3.up)
            {
                Quaternion uprightRotation = Quaternion.FromToRotation(transform.up, Vector3.up) * transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, uprightRotation, _wallAlignSpeed * Time.fixedDeltaTime);
            }
        }

        private void ApplyJumpAndGravity()
        {
            if (_pendingInput.JumpTriggered)
            {
                _pendingInput.JumpTriggered = false;

                if (_coordinator != null && _coordinator.TryTriggerCatapultLaunch())
                {
                    return;
                }

                if (_isOnMagneticSurface)
                {
                    Vector3 N = _currentSurfaceNormal;
                    Vector3 pushAway = N * 0.85f;
                    Vector3 forwardBonus = N.y < -0.7f ? transform.forward * 0.35f : Vector3.up * 0.35f;
                    Vector3 jumpDir = (pushAway + forwardBonus).normalized;

                    SetMagneticState(false);
                    _detachCooldownTimer = _magneticDetachCooldown;
                    _rigidbody.linearVelocity = jumpDir * _wallJumpForce;

                    PlayAudioCue(Game.Core.Enums.AudioCue.Jump, transform.position);
                    return;
                }

                if (_isGrounded)
                {
                    Vector3 velocity = _rigidbody.linearVelocity;
                    velocity.y = _jumpForce;
                    _rigidbody.linearVelocity = velocity;
                    PlayAudioCue(Game.Core.Enums.AudioCue.Jump, transform.position);
                }
            }

            if (!_isOnMagneticSurface && !_isGrounded && _rigidbody.linearVelocity.y < 0f)
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

            float chargeRatio = _pendingKickCharge;
            float effectiveForce = Mathf.Lerp(_minKickForce, _maxKickForce, chargeRatio);

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

                if (col.TryGetComponent<IKickable>(out var kickable) ||
                    (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<IKickable>(out kickable)) ||
                    (col.GetComponentInParent<IKickable>() is { } pKick && (kickable = pKick) != null))
                {
                    kickable.OnKicked(col.bounds.center, direction, effectiveForce);
                }
                else if (col.TryGetComponent<IPushable>(out var pushable) ||
                         (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<IPushable>(out pushable)) ||
                         (col.GetComponentInParent<IPushable>() is { } pPush && (pushable = pPush) != null))
                {
                    pushable.OnPushed(direction, effectiveForce);
                }
                else if (col.attachedRigidbody != null)
                {
                    col.attachedRigidbody.AddForce(direction * effectiveForce, ForceMode.Impulse);
                }
            }

            if (!isSpawned)
            {
                TriggerKickImpulse(chargeRatio);
                OnKicked?.Invoke();
            }
            else
            {
                PlayKickEffectObserversRpc(kickOrigin, chargeRatio);
            }
        }

        [ObserversRpc(runLocally: true)]
        private void PlayKickEffectObserversRpc(Vector3 origin, float chargeRatio)
        {
            TriggerKickImpulse(chargeRatio);
            PlayAudioCue(Game.Core.Enums.AudioCue.Kick, origin);
            OnKicked?.Invoke();
        }

        private void PlayAudioCue(Game.Core.Enums.AudioCue cue, Vector3 position)
        {
            if (_audioRelay == null)
            {
                _audioRelay = FindFirstObjectByType<Game.Network.Audio.NetworkAudioRelay>();
            }
            if (_audioRelay != null)
            {
                _audioRelay.PlayNetworkAudio(cue, position, 0.9f, 1f);
            }
        }

        private void TriggerKickImpulse(float chargeRatio = 1f)
        {
            if (_kickImpulseSource != null)
            {
                float impulseMultiplier = Mathf.Lerp(0.04f, 1.35f, chargeRatio);
                _kickImpulseSource.GenerateImpulse(transform.forward * (_kickImpulseForce * impulseMultiplier));
            }
        }

        private void UpdateKickCharge()
        {
            if (_isFused || !CanKick)
            {
                if (_isChargingKick)
                {
                    CancelKickCharge();
                }
                return;
            }

            if (_inputReader.IsKickHeld)
            {
                if (!_isChargingKick)
                {
                    _isChargingKick = true;
                    _currentChargeTimer = 0f;
                    _hasTriggeredMaxCharge = false;
                }

                _currentChargeTimer += Time.deltaTime;
                float chargeRatio = Mathf.Clamp01(_currentChargeTimer / _kickChargeDuration);

                if (chargeRatio >= 1f && !_hasTriggeredMaxCharge)
                {
                    _hasTriggeredMaxCharge = true;
                    TriggerMaxChargeFeedback();
                }

                TorsoController torso = GetTorsoInKickRange(out Vector3 torsoPosition, out Collider torsoCollider);
                if (torso != null && _trajectoryVisualizer != null)
                {
                    Vector3 direction = (torsoPosition - transform.position).normalized;
                    direction.y = 0.2f;
                    direction = direction.normalized;

                    Vector3 launchDir = (direction + Vector3.up * 0.45f).normalized;
                    float currentForce = Mathf.Lerp(_minKickForce, _maxKickForce, chargeRatio);
                    Vector3 launchVelocity = launchDir * (currentForce * 1.35f);

                    _trajectoryVisualizer.RenderTrajectory(torsoPosition, launchVelocity, _kickLayer, torsoCollider, _selfCollider);
                }
                else if (_trajectoryVisualizer != null)
                {
                    _trajectoryVisualizer.ClearTrajectory();
                }
            }
            else if (_isChargingKick || _inputReader.ConsumeKickReleased())
            {
                float finalCharge = 0f;
                if (_isChargingKick)
                {
                    finalCharge = Mathf.Clamp01(_currentChargeTimer / _kickChargeDuration);
                    _isChargingKick = false;
                }

                if (_trajectoryVisualizer != null)
                {
                    _trajectoryVisualizer.ClearTrajectory();
                }

                ExecuteChargedKick(finalCharge);
                _currentChargeTimer = 0f;
                _hasTriggeredMaxCharge = false;
            }
        }

        private TorsoController GetTorsoInKickRange(out Vector3 torsoPosition, out Collider torsoCollider)
        {
            torsoPosition = Vector3.zero;
            torsoCollider = null;
            Vector3 kickOrigin = _kickPoint != null ? _kickPoint.position : transform.position + transform.forward * 0.8f;
            float kickRadiusSqr = _kickRadius * _kickRadius;

            if (_coordinator != null && _coordinator.Torso != null)
            {
                TorsoController torso = _coordinator.Torso;
                if (!torso.IsFused)
                {
                    Vector3 center = torso.transform.position;
                    var col = torso.GetComponent<Collider>();
                    if (col != null)
                    {
                        center = col.bounds.center;
                        torsoCollider = col;
                    }

                    if ((center - kickOrigin).sqrMagnitude <= kickRadiusSqr)
                    {
                        torsoPosition = center;
                        return torso;
                    }
                }
            }

            int hitCount = Physics.OverlapSphereNonAlloc(kickOrigin, _kickRadius, _kickColliders, _kickLayer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = _kickColliders[i];
                if (col == null || col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (col.TryGetComponent<TorsoController>(out var torso) ||
                    (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<TorsoController>(out torso)) ||
                    (col.GetComponentInParent<TorsoController>() is { } pTorso && (torso = pTorso) != null))
                {
                    if (!torso.IsFused)
                    {
                        torsoPosition = col.bounds.center;
                        torsoCollider = col;
                        return torso;
                    }
                }
            }

            return null;
        }

        private void ExecuteChargedKick(float chargeRatio)
        {
            if (_commandInvoker != null)
            {
                _commandInvoker.Execute(new KickCommand(chargeRatio));
            }
            else
            {
                Kick(chargeRatio);
                if (isSpawned && !isServer)
                {
                    LegsInputData inputData = new LegsInputData(
                        _inputReader.MoveInput,
                        false,
                        true,
                        _inputReader.IsSprintHeld
                    );
                    SendChargedInputServerRpc(inputData, chargeRatio);
                }
            }
        }

        [ServerRpc(Channel.Unreliable)]
        private void SendChargedInputServerRpc(LegsInputData inputData, float chargeRatio, RPCInfo info = default)
        {
            _pendingKickCharge = chargeRatio;
            ProcessInput(inputData);
        }

        private void TriggerMaxChargeFeedback()
        {
            Vector3 footPos = _splineAnimator != null
                ? _splineAnimator.GetNextKickingFootPosition()
                : (_kickPoint != null ? _kickPoint.position : transform.position + transform.forward * 0.8f + Vector3.up * 0.15f);

            if (_chargeFlash != null)
            {
                _chargeFlash.TriggerFlash(footPos);
            }

            PlayAudioCue(Game.Core.Enums.AudioCue.KickMaxCharge, footPos);

            if (isSpawned)
            {
                PlayMaxChargeEffectObserversRpc(footPos);
            }
        }

        [ObserversRpc(runLocally: false)]
        private void PlayMaxChargeEffectObserversRpc(Vector3 footPosition)
        {
            if (_chargeFlash != null)
            {
                _chargeFlash.TriggerFlash(footPosition);
            }

            PlayAudioCue(Game.Core.Enums.AudioCue.KickMaxCharge, footPosition);
        }

        private void CancelKickCharge()
        {
            if (!_isChargingKick)
            {
                return;
            }

            _isChargingKick = false;
            _currentChargeTimer = 0f;
            _hasTriggeredMaxCharge = false;

            if (_trajectoryVisualizer != null)
            {
                _trajectoryVisualizer.ClearTrajectory();
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

        public void Kick() => Kick(1f);

        public void Kick(float chargeRatio)
        {
            _pendingKickCharge = Mathf.Clamp01(chargeRatio);
            _pendingInput.KickTriggered = true;
        }

        public void ApplyCatapultLaunch(Vector3 launchImpulse)
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_rigidbody != null)
            {
                _catapultAirTime = 1.5f;
                _isGrounded = false;
                _rigidbody.linearVelocity = launchImpulse;
                PlayAudioCue(Game.Core.Enums.AudioCue.Jump, transform.position);
            }
        }

        public FusionSocket FusionSocket => _fusionSocket;
        public bool IsFused => _isFused;
        public bool CanFuse => (_coordinator != null || (_coordinator = FindFirstObjectByType<RobotCoordinator>()) != null) && _coordinator.CanFuse();

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
                Debug.LogWarning("[LegsController] Cannot RequestFusion: RobotCoordinator not found in scene.");
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
                Debug.LogWarning("[LegsController] Cannot RequestSeparation: RobotCoordinator not found in scene.");
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 groundOrigin = _groundCheckPoint != null ? _groundCheckPoint.position : transform.position;

            // 1. Kick Gizmo
            Gizmos.color = Color.red;
            Vector3 kickOrigin = _kickPoint != null ? _kickPoint.position : transform.position + transform.forward * 0.8f;
            Gizmos.DrawWireSphere(kickOrigin, _kickRadius);

            // 2. Footprint Probes Gizmos
            float downDist = _isOnMagneticSurface ? (_groundCheckDistance + 0.45f) : (_groundCheckDistance + 0.25f);
            Vector3 downDir = -transform.up;
            Vector3 baseOrigin = groundOrigin + transform.up * 0.15f;
            Vector3 fwdOffset = transform.forward * _footprintRadius;
            Vector3 rightOffset = transform.right * _footprintRadius;

            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(baseOrigin, baseOrigin + downDir * downDist);
            Gizmos.DrawLine(baseOrigin + fwdOffset, baseOrigin + fwdOffset + downDir * downDist);
            Gizmos.DrawLine(baseOrigin - fwdOffset, baseOrigin - fwdOffset + downDir * downDist);
            Gizmos.DrawLine(baseOrigin - rightOffset, baseOrigin - rightOffset + downDir * downDist);
            Gizmos.DrawLine(baseOrigin + rightOffset, baseOrigin + rightOffset + downDir * downDist);

            // 3. Whiskers Concave Probes
            Gizmos.color = Color.yellow;
            Vector3 probeDir = transform.forward;
            Vector3 lowOrigin = groundOrigin + transform.up * 0.15f;
            Vector3 midOrigin = groundOrigin + transform.up * _probeHeightOffset;
            Gizmos.DrawLine(lowOrigin, lowOrigin + probeDir * _wallProbeDistance);
            Gizmos.DrawLine(midOrigin, midOrigin + probeDir * _wallProbeDistance);

            // 4. Ledge Wrap Convex Probe
            Gizmos.color = Color.magenta;
            Vector3 wrapOrigin = groundOrigin + probeDir * (_footprintRadius + 0.2f) + transform.up * 0.1f;
            Vector3 wrapDir = (-transform.up - probeDir * 0.65f).normalized;
            Gizmos.DrawLine(wrapOrigin, wrapOrigin + wrapDir * (_groundCheckDistance + 0.65f));

            // 5. Surface Normal Vectors
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(groundOrigin, _currentSurfaceNormal * 1.5f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(groundOrigin, _targetSurfaceNormal * 1.5f);
        }
    }
}
