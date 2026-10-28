using System;
using System.Runtime.CompilerServices;
using Game.Gameplay.Player.Legs;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Player.Torso
{
    public enum ArmSide
    {
        Left = 0,
        Right = 1
    }

    [Serializable]
    public sealed class ProceduralFinger
    {
        [SerializeField] private Transform _proximal;
        [SerializeField] private Transform _distal;

        [SerializeField, HideInInspector] private Quaternion _proximalRestLocalRot = Quaternion.identity;
        [SerializeField, HideInInspector] private Quaternion _distalRestLocalRot = Quaternion.identity;
        [SerializeField, HideInInspector] private bool _hasRestPose;

        public Transform Proximal => _proximal;
        public Transform Distal => _distal;
        public bool HasRestPose => _hasRestPose;

        public ProceduralFinger() { }

        public ProceduralFinger(Transform proximal, Transform distal)
        {
            _proximal = proximal;
            _distal = distal;
        }

        public void Configure(Transform proximal, Transform distal)
        {
            _proximal = proximal;
            _distal = distal;
            _hasRestPose = false;
        }

        public void CacheRestPose(bool forceRecache = false)
        {
            if (_hasRestPose && !forceRecache) return;

            if (_proximal != null)
            {
                _proximalRestLocalRot = _proximal.localRotation;
            }
            if (_distal != null)
            {
                _distalRestLocalRot = _distal.localRotation;
            }
            _hasRestPose = true;
        }

        public void ApplyCurl(float curlAngle, float distalMultiplier)
        {
            if (_proximal != null)
            {
                _proximal.localRotation = _proximalRestLocalRot * Quaternion.Euler(curlAngle, 0f, 0f);
            }
            if (_distal != null)
            {
                _distal.localRotation = _distalRestLocalRot * Quaternion.Euler(curlAngle * distalMultiplier, 0f, 0f);
            }
        }
    }

    [Serializable]
    public sealed class ProceduralArm : ISerializationCallbackReceiver
    {
        [SerializeField] private ArmSide _side;
        [SerializeField] private Transform _shoulder;
        [SerializeField] private Transform[] _bones = new Transform[8];
        [SerializeField] private ProceduralFinger[] _fingers = new ProceduralFinger[3];

        [SerializeField, HideInInspector] private Quaternion[] _restLocalRotations = new Quaternion[8];
        [SerializeField, HideInInspector] private Vector3[] _restLocalSegmentDirs = new Vector3[7];
        [SerializeField, HideInInspector] private float[] _restSegmentLengths = new float[7];
        [SerializeField, HideInInspector] private Vector3[] _restBoneLocalPositions = new Vector3[8];
        [SerializeField, HideInInspector] private Quaternion[] _restBoneLocalRotations = new Quaternion[8];
        [SerializeField, HideInInspector] private float _totalChainLength;
        [SerializeField, HideInInspector] private bool _hasValidBindPose;

        [NonSerialized] private Vector3[] _arcFineSamples;
        [NonSerialized] private float[] _arcCumulativeLengths;
        [NonSerialized] private Vector3[] _splinePoints;

        [NonSerialized] private Vector3 _currentHandPos;
        [NonSerialized] private Vector3 _targetHandPos;
        [NonSerialized] private Vector3 _handVelocity;
        [NonSerialized] private Quaternion _currentHandRot = Quaternion.identity;
        [NonSerialized] private float _currentFingerCurl;

        [NonSerialized] private Vector3 _plantedGroundPos;
        [NonSerialized] private Vector3 _stepStartPos;
        [NonSerialized] private Vector3 _stepTargetPos;
        [NonSerialized] private float _crawlStepTimer;
        [NonSerialized] private bool _isCrawlingSwing;

        public ArmSide Side => _side;
        public Transform Shoulder => _shoulder;
        public Transform[] Bones => _bones;
        public ProceduralFinger[] Fingers => _fingers;
        public float TotalChainLength => _totalChainLength;
        public bool HasValidBindPose => _hasValidBindPose;
        public Transform HandBone => (_bones != null && _bones.Length >= 8) ? _bones[7] : null;

        public Vector3 CurrentHandPos { get => _currentHandPos; set => _currentHandPos = value; }
        public Vector3 TargetHandPos { get => _targetHandPos; set => _targetHandPos = value; }
        public Vector3 HandVelocity { get => _handVelocity; set => _handVelocity = value; }
        public Quaternion CurrentHandRot { get => _currentHandRot; set => _currentHandRot = value; }
        public float CurrentFingerCurl { get => _currentFingerCurl; set => _currentFingerCurl = value; }
        public Vector3 PlantedGroundPos { get => _plantedGroundPos; set => _plantedGroundPos = value; }
        public Vector3 StepStartPos { get => _stepStartPos; set => _stepStartPos = value; }
        public Vector3 StepTargetPos { get => _stepTargetPos; set => _stepTargetPos = value; }
        public float CrawlStepTimer { get => _crawlStepTimer; set => _crawlStepTimer = value; }
        public bool IsCrawlingSwing { get => _isCrawlingSwing; set => _isCrawlingSwing = value; }

        public void Configure(ArmSide side, Transform shoulder, Transform[] bones, ProceduralFinger[] fingers)
        {
            _side = side;
            _shoulder = shoulder;
            _bones = bones;
            _fingers = fingers;
            _hasValidBindPose = false;
            EnsureBuffers();
        }

        public void EnsureBuffers()
        {
            if (_bones == null || _bones.Length != 8) _bones = new Transform[8];
            if (_fingers == null || _fingers.Length != 3) _fingers = new ProceduralFinger[3];
            if (_restLocalRotations == null || _restLocalRotations.Length != 8) _restLocalRotations = new Quaternion[8];
            if (_restLocalSegmentDirs == null || _restLocalSegmentDirs.Length != 7) _restLocalSegmentDirs = new Vector3[7];
            if (_restSegmentLengths == null || _restSegmentLengths.Length != 7) _restSegmentLengths = new float[7];
            if (_restBoneLocalPositions == null || _restBoneLocalPositions.Length != 8) _restBoneLocalPositions = new Vector3[8];
            if (_restBoneLocalRotations == null || _restBoneLocalRotations.Length != 8) _restBoneLocalRotations = new Quaternion[8];

            if (_arcFineSamples == null || _arcFineSamples.Length != 25) _arcFineSamples = new Vector3[25];
            if (_arcCumulativeLengths == null || _arcCumulativeLengths.Length != 25) _arcCumulativeLengths = new float[25];
            if (_splinePoints == null || _splinePoints.Length != 8) _splinePoints = new Vector3[8];
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            EnsureBuffers();
        }

        public void CacheRestPose(Transform rootTransform, bool forceRecache = false)
        {
            EnsureBuffers();
            if (_bones == null || _bones.Length < 8 || _bones[0] == null) return;
            if (_hasValidBindPose && !forceRecache && _totalChainLength > 0.05f) return;

            Quaternion invRot = rootTransform != null ? Quaternion.Inverse(rootTransform.rotation) : Quaternion.identity;

            for (int i = 0; i < 8; i++)
            {
                if (_bones[i] != null)
                {
                    _restBoneLocalPositions[i] = _bones[i].localPosition;
                    _restBoneLocalRotations[i] = _bones[i].localRotation;
                    _restLocalRotations[i] = invRot * _bones[i].rotation;
                }
            }

            _totalChainLength = 0f;
            for (int i = 0; i < 7; i++)
            {
                if (_bones[i] != null && _bones[i + 1] != null)
                {
                    Vector3 seg = _bones[i + 1].position - _bones[i].position;
                    float len = seg.magnitude;
                    _restSegmentLengths[i] = len;
                    _totalChainLength += len;
                    _restLocalSegmentDirs[i] = invRot * (len > 0.0001f ? seg / len : Vector3.down);
                }
            }

            if (_fingers != null)
            {
                for (int f = 0; f < _fingers.Length; f++)
                {
                    if (_fingers[f] != null)
                    {
                        _fingers[f].CacheRestPose(forceRecache);
                    }
                }
            }

            if (_totalChainLength > 0.05f)
            {
                _hasValidBindPose = true;
            }
        }

        public void InitializeHandPos(Vector3 initialPos, Quaternion initialRot)
        {
            _currentHandPos = initialPos;
            _targetHandPos = initialPos;
            _currentHandRot = initialRot;
            _plantedGroundPos = initialPos;
            _stepStartPos = initialPos;
            _stepTargetPos = initialPos;
            _handVelocity = Vector3.zero;
        }

        public void SolveSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Transform rootTransform)
        {
            EnsureBuffers();
            if (_bones == null || _bones.Length < 8 || _bones[0] == null) return;

            Quaternion rootRot = rootTransform != null ? rootTransform.rotation : Quaternion.identity;

            _arcFineSamples[0] = p0;
            _arcCumulativeLengths[0] = 0f;
            float totalCurveLength = 0f;

            for (int k = 1; k < 25; k++)
            {
                float u = k / 24f;
                Vector3 pt = BezierSplineHose.EvaluatePoint(p0, p1, p2, p3, u);
                _arcFineSamples[k] = pt;
                totalCurveLength += Vector3.Distance(_arcFineSamples[k - 1], pt);
                _arcCumulativeLengths[k] = totalCurveLength;
            }

            _splinePoints[0] = p0;
            float lengthScale = (_totalChainLength > 0.001f && totalCurveLength > 0.001f)
                ? (totalCurveLength / _totalChainLength)
                : 1f;

            float accumulatedDist = 0f;
            for (int i = 1; i < 8; i++)
            {
                accumulatedDist += _restSegmentLengths[i - 1] * lengthScale;
                _splinePoints[i] = SamplePointAtDistance(accumulatedDist, totalCurveLength);
            }

            for (int i = 0; i < 7; i++)
            {
                Transform bone = _bones[i];
                if (bone == null) continue;

                Vector3 cur = _splinePoints[i];
                Vector3 nxt = _splinePoints[i + 1];
                Vector3 newDir = nxt - cur;
                if (newDir.sqrMagnitude > 0.0001f) newDir.Normalize();
                else newDir = Vector3.down;

                Vector3 restDir = rootRot * _restLocalSegmentDirs[i];
                Quaternion restRot = rootRot * _restLocalRotations[i];

                Quaternion deltaRot = Quaternion.FromToRotation(restDir, newDir);
                bone.position = cur;
                bone.rotation = deltaRot * restRot;
            }

            Transform hand = _bones[7];
            if (hand != null)
            {
                hand.position = p3;
                hand.rotation = _currentHandRot;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3 SamplePointAtDistance(float distance, float totalLength)
        {
            if (distance <= 0f) return _arcFineSamples[0];
            if (distance >= totalLength) return _arcFineSamples[24];

            for (int k = 0; k < 24; k++)
            {
                float d0 = _arcCumulativeLengths[k];
                float d1 = _arcCumulativeLengths[k + 1];
                if (distance >= d0 && distance <= d1)
                {
                    float segLen = d1 - d0;
                    float factor = segLen > 0.00001f ? (distance - d0) / segLen : 0f;
                    return Vector3.LerpUnclamped(_arcFineSamples[k], _arcFineSamples[k + 1], factor);
                }
            }

            return _arcFineSamples[24];
        }

        public void ApplyFingerCurl(float curlAngle, float distalMultiplier)
        {
            _currentFingerCurl = curlAngle;
            if (_fingers == null) return;
            for (int f = 0; f < _fingers.Length; f++)
            {
                if (_fingers[f] != null)
                {
                    _fingers[f].ApplyCurl(curlAngle, distalMultiplier);
                }
            }
        }
    }

    [DisallowMultipleComponent]
    [DeclareBoxGroup("Referencias")]
    [DeclareBoxGroup("Geometría de Reposo")]
    [DeclareBoxGroup("Inercia y Bamboleo")]
    [DeclareBoxGroup("Agarre y Manipulación")]
    [DeclareBoxGroup("Imán Magnético")]
    [DeclareBoxGroup("Marcha Gateo")]
    [DeclareBoxGroup("Curvatura Spline")]
    [DeclareBoxGroup("Dedos Articulados")]
    public sealed class TorsoProceduralArms : MonoBehaviour
    {
        [Group("Referencias")]
        [SerializeField] private TorsoController _controller;

        [Group("Referencias")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Referencias")]
        [SerializeField] private Transform _aimPivot;

        [Group("Referencias")]
        [SerializeField] private ProceduralArm _leftArm = new ProceduralArm();

        [Group("Referencias")]
        [SerializeField] private ProceduralArm _rightArm = new ProceduralArm();

        [Group("Geometría de Reposo")]
        [Tooltip("Separación horizontal de las manos desde el centro del torso en reposo")]
        [SerializeField] private float _restArmSpacing = 0.50f;

        [Group("Geometría de Reposo")]
        [Tooltip("Altura vertical de las manos respecto al pivote del torso")]
        [SerializeField] private float _restArmHeight = -0.32f;

        [Group("Geometría de Reposo")]
        [Tooltip("Avance frontal de las manos en reposo")]
        [SerializeField] private float _restArmForwardOffset = 0.08f;

        [Group("Geometría de Reposo")]
        [SerializeField] private float _idleBreathingAmplitude = 0.025f;

        [Group("Geometría de Reposo")]
        [SerializeField] private float _idleBreathingFrequency = 2.0f;

        [Group("Inercia y Bamboleo")]
        [Tooltip("Retraso inercial de las manos al acelerar")]
        [SerializeField] private float _inertiaLagAmount = 0.09f;

        [Group("Inercia y Bamboleo")]
        [Tooltip("Fuerza centrífuga lateral que abre los brazos al girar")]
        [SerializeField] private float _centrifugalSwayAmount = 0.12f;

        [Group("Inercia y Bamboleo")]
        [Tooltip("Suavizado de posición de mano (muelle amortiguado)")]
        [SerializeField] private float _handPositionSmoothTime = 0.07f;

        [Group("Inercia y Bamboleo")]
        [SerializeField] private float _maxInertiaOffset = 0.35f;

        [Group("Agarre y Manipulación")]
        [Tooltip("Separación entre manos al sujetar un objeto")]
        [SerializeField] private float _holdingHandSpacing = 0.26f;

        [Group("Agarre y Manipulación")]
        [SerializeField] private float _holdingForwardOffset = -0.04f;

        [Group("Agarre y Manipulación")]
        [SerializeField] private float _holdingCurlAngle = 55f;

        [Group("Agarre y Manipulación")]
        [SerializeField] private float _throwThrustDuration = 0.22f;

        [Group("Agarre y Manipulación")]
        [SerializeField] private float _throwThrustDistance = 0.55f;

        [Group("Imán Magnético")]
        [Tooltip("Extensión de los brazos al activar el imán")]
        [SerializeField] private float _magnetReachDistance = 0.85f;

        [Group("Imán Magnético")]
        [SerializeField] private float _magnetHandSpacing = 0.22f;

        [Group("Imán Magnético")]
        [SerializeField] private float _magnetJitterAmplitude = 0.025f;

        [Group("Imán Magnético")]
        [SerializeField] private float _magnetJitterFrequency = 45f;

        [Group("Imán Magnético")]
        [SerializeField] private float _magnetCurlAngle = -22f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlCycleDuration = 0.36f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlStrideForward = 0.45f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlStepHeight = 0.20f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlHandSpread = 0.48f;

        [Group("Marcha Gateo")]
        [SerializeField] private LayerMask _crawlGroundLayer = ~0;

        [Group("Curvatura Spline")]
        [Tooltip("Apertura lateral de la manguera al salir del hombro")]
        [SerializeField] private float _hoseArchOutward = 0.22f;

        [Group("Curvatura Spline")]
        [Tooltip("Caída vertical en P1")]
        [SerializeField] private float _hoseArchDown = 0.12f;

        [Group("Curvatura Spline")]
        [Tooltip("Distancia previa a la muñeca para tangencia P2")]
        [SerializeField] private float _wristApproachDistance = 0.16f;

        [Group("Curvatura Spline")]
        [Tooltip("Elevación en P2")]
        [SerializeField] private float _wristApproachHeight = 0.10f;

        [Group("Dedos Articulados")]
        [SerializeField] private float _idleFingerCurl = 15f;

        [Group("Dedos Articulados")]
        [SerializeField] private float _fingerCurlSpeed = 240f;

        [Group("Dedos Articulados")]
        [SerializeField] private float _distalCurlMultiplier = 1.25f;

        private readonly RaycastHit[] _crawlRayHits = new RaycastHit[4];
        private float _throwTimer;
        private float _crawlTimer;
        private bool _isInitialized;

        public ProceduralArm LeftArm => _leftArm;
        public ProceduralArm RightArm => _rightArm;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<TorsoController>();
            }

            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_aimPivot == null && _controller != null)
            {
                _aimPivot = _controller.AimPivot;
            }

            if (_aimPivot == null)
            {
                _aimPivot = transform;
            }

            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnThrow += HandleThrow;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnThrow -= HandleThrow;
            }
        }

        private void Start()
        {
            EnsureInitialized();
            ResetHandPositions();
        }

        private void EnsureInitialized()
        {
            if (_isInitialized) return;

            if (_leftArm == null || _leftArm.Bones == null || _leftArm.Bones[0] == null ||
                _rightArm == null || _rightArm.Bones == null || _rightArm.Bones[0] == null)
            {
                AutoFindArmatureBones();
            }

            if (_leftArm != null)
            {
                _leftArm.EnsureBuffers();
                _leftArm.CacheRestPose(transform, forceRecache: false);
            }

            if (_rightArm != null)
            {
                _rightArm.EnsureBuffers();
                _rightArm.CacheRestPose(transform, forceRecache: false);
            }

            if (_crawlGroundLayer.value == 0)
            {
                _crawlGroundLayer = LayerMask.GetMask("Default", "Ground", "Environment");
                if (_crawlGroundLayer.value == 0) _crawlGroundLayer = ~0;
            }

            _isInitialized = true;
        }

        public void ResetHandPositions()
        {
            Transform aimTrans = _aimPivot != null ? _aimPivot : transform;
            Vector3 aimPos = aimTrans.position;
            Quaternion aimRot = aimTrans.rotation;

            if (_leftArm != null)
            {
                Vector3 leftRest = aimPos + aimRot * new Vector3(-_restArmSpacing, _restArmHeight, _restArmForwardOffset);
                _leftArm.InitializeHandPos(leftRest, aimRot);
            }

            if (_rightArm != null)
            {
                Vector3 rightRest = aimPos + aimRot * new Vector3(_restArmSpacing, _restArmHeight, _restArmForwardOffset);
                _rightArm.InitializeHandPos(rightRest, aimRot);
            }
        }

        private void HandleThrow()
        {
            _throwTimer = _throwThrustDuration;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            if (_throwTimer > 0f)
            {
                _throwTimer = Mathf.Max(0f, _throwTimer - dt);
            }

            bool isFused = _controller != null ? _controller.IsFused : true;
            bool isHolding = _controller != null ? _controller.IsHoldingObject : false;
            bool isMagnet = _controller != null ? _controller.IsMagnetActive : false;

            if (isFused || isHolding || isMagnet)
            {
                SolveFusedOrManipulatingPose(dt, isHolding, isMagnet);
            }
            else
            {
                SolveCrawlingPose(dt);
            }

            SolveArmSpline(_leftArm);
            SolveArmSpline(_rightArm);
        }

        private void SolveFusedOrManipulatingPose(float dt, bool isHolding, bool isMagnet)
        {
            Transform aimTrans = _aimPivot != null ? _aimPivot : transform;
            Vector3 aimPos = aimTrans.position;
            Vector3 aimForward = aimTrans.forward;
            Vector3 aimRight = aimTrans.right;
            Vector3 aimUp = aimTrans.up;
            Quaternion aimRot = aimTrans.rotation;

            float targetFingerCurl = _idleFingerCurl;
            Vector3 targetLeftHandPos;
            Vector3 targetRightHandPos;
            Quaternion targetLeftHandRot;
            Quaternion targetRightHandRot;

            if (isHolding)
            {
                Transform socket = _controller != null && _controller.HoldSocket != null ? _controller.HoldSocket : aimTrans;
                Vector3 socketPos = socket.position;
                Vector3 socketRight = socket.right;
                Vector3 socketForward = socket.forward;

                targetLeftHandPos = socketPos - (socketRight * _holdingHandSpacing) + (socketForward * _holdingForwardOffset);
                targetRightHandPos = socketPos + (socketRight * _holdingHandSpacing) + (socketForward * _holdingForwardOffset);

                Vector3 toCenterLeft = (socketPos - targetLeftHandPos);
                Vector3 toCenterRight = (socketPos - targetRightHandPos);

                targetLeftHandRot = toCenterLeft.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(toCenterLeft.normalized, socket.up)
                    : socket.rotation;

                targetRightHandRot = toCenterRight.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(toCenterRight.normalized, socket.up)
                    : socket.rotation;

                targetFingerCurl = _holdingCurlAngle;
            }
            else if (isMagnet)
            {
                Transform origin = _controller != null && _controller.MagnetOrigin != null ? _controller.MagnetOrigin : aimTrans;
                Vector3 centerTarget = origin.position + (origin.forward * _magnetReachDistance);

                float jitterTime = Time.time * _magnetJitterFrequency;
                Vector3 jitter = new Vector3(
                    Mathf.Sin(jitterTime),
                    Mathf.Cos(jitterTime * 1.25f),
                    Mathf.Sin(jitterTime * 0.75f)
                ) * _magnetJitterAmplitude;

                targetLeftHandPos = centerTarget - (origin.right * _magnetHandSpacing) + jitter;
                targetRightHandPos = centerTarget + (origin.right * _magnetHandSpacing) + jitter;

                targetLeftHandRot = Quaternion.LookRotation(origin.forward, origin.up);
                targetRightHandRot = targetLeftHandRot;

                targetFingerCurl = _magnetCurlAngle;
            }
            else
            {
                float breath = Mathf.Sin(Time.time * _idleBreathingFrequency) * _idleBreathingAmplitude;
                Vector3 vel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
                Vector3 inertiaLag = -vel * _inertiaLagAmount;
                if (inertiaLag.sqrMagnitude > _maxInertiaOffset * _maxInertiaOffset)
                {
                    inertiaLag = inertiaLag.normalized * _maxInertiaOffset;
                }

                Vector3 angVel = _rigidbody != null ? _rigidbody.angularVelocity : Vector3.zero;
                Vector3 centrifugalSway = aimRight * (angVel.y * _centrifugalSwayAmount);

                Vector3 leftRest = aimPos + aimRot * new Vector3(-_restArmSpacing, _restArmHeight + breath, _restArmForwardOffset);
                Vector3 rightRest = aimPos + aimRot * new Vector3(_restArmSpacing, _restArmHeight + breath, _restArmForwardOffset);

                targetLeftHandPos = leftRest + inertiaLag + centrifugalSway;
                targetRightHandPos = rightRest + inertiaLag + centrifugalSway;

                if (_throwTimer > 0f)
                {
                    float throwProgress = 1f - (_throwTimer / Mathf.Max(0.01f, _throwThrustDuration));
                    float throwThrust = Mathf.Sin(throwProgress * Mathf.PI) * _throwThrustDistance;
                    targetLeftHandPos += aimForward * throwThrust;
                    targetRightHandPos += aimForward * throwThrust;
                }

                targetLeftHandRot = Quaternion.LookRotation(aimForward * 0.5f + Vector3.down * 0.5f, aimUp);
                targetRightHandRot = targetLeftHandRot;

                targetFingerCurl = _idleFingerCurl;
            }

            UpdateArmTarget(_leftArm, targetLeftHandPos, targetLeftHandRot, targetFingerCurl, dt);
            UpdateArmTarget(_rightArm, targetRightHandPos, targetRightHandRot, targetFingerCurl, dt);
        }

        private void SolveCrawlingPose(float dt)
        {
            Vector2 moveInput = _controller != null ? _controller.MoveInput : Vector2.zero;
            Vector3 vel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
            bool isMoving = moveInput.sqrMagnitude > 0.04f || vel.sqrMagnitude > 0.04f;

            Transform aimTrans = _aimPivot != null ? _aimPivot : transform;
            Vector3 charPos = transform.position;
            Vector3 charForward = transform.forward;
            Vector3 charRight = transform.right;

            if (!isMoving)
            {
                _crawlTimer = 0f;
                Vector3 leftGround = FindGroundPoint(charPos - (charRight * _crawlHandSpread) + (charForward * 0.25f));
                Vector3 rightGround = FindGroundPoint(charPos + (charRight * _crawlHandSpread) + (charForward * 0.25f));

                Quaternion leftRot = Quaternion.LookRotation(charForward, Vector3.up);
                Quaternion rightRot = Quaternion.LookRotation(charForward, Vector3.up);

                UpdateArmTarget(_leftArm, leftGround, leftRot, 0f, dt);
                UpdateArmTarget(_rightArm, rightGround, rightRot, 0f, dt);
                return;
            }

            _crawlTimer += dt;
            float cycleDuration = Mathf.Max(0.05f, _crawlCycleDuration);
            float normalizedCycle = (_crawlTimer % cycleDuration) / cycleDuration;

            bool isLeftSwing = normalizedCycle < 0.5f;
            float stepProgress = isLeftSwing ? (normalizedCycle * 2f) : ((normalizedCycle - 0.5f) * 2f);

            Vector3 leadVelocity = vel * 0.25f;
            if (leadVelocity.sqrMagnitude > _crawlStrideForward * _crawlStrideForward)
            {
                leadVelocity = leadVelocity.normalized * _crawlStrideForward;
            }

            Vector3 leftAnchor = charPos - (charRight * _crawlHandSpread) + (charForward * 0.20f) + leadVelocity;
            Vector3 rightAnchor = charPos + (charRight * _crawlHandSpread) + (charForward * 0.20f) + leadVelocity;

            if (isLeftSwing)
            {
                float arcHeight = Mathf.Sin(stepProgress * Mathf.PI) * _crawlStepHeight;
                Vector3 groundTarget = FindGroundPoint(leftAnchor);
                Vector3 leftPos = Vector3.Lerp(_leftArm.StepStartPos, groundTarget, stepProgress) + Vector3.up * arcHeight;
                Quaternion leftRot = Quaternion.LookRotation(charForward, Vector3.up);
                UpdateArmTarget(_leftArm, leftPos, leftRot, 25f, dt);

                Vector3 rightPlanted = FindGroundPoint(charPos + (charRight * _crawlHandSpread) - (charForward * 0.15f));
                UpdateArmTarget(_rightArm, rightPlanted, Quaternion.LookRotation(charForward, Vector3.up), -5f, dt);

                _leftArm.StepStartPos = leftPos;
                _rightArm.StepStartPos = rightPlanted;
            }
            else
            {
                float arcHeight = Mathf.Sin(stepProgress * Mathf.PI) * _crawlStepHeight;
                Vector3 groundTarget = FindGroundPoint(rightAnchor);
                Vector3 rightPos = Vector3.Lerp(_rightArm.StepStartPos, groundTarget, stepProgress) + Vector3.up * arcHeight;
                Quaternion rightRot = Quaternion.LookRotation(charForward, Vector3.up);
                UpdateArmTarget(_rightArm, rightPos, rightRot, 25f, dt);

                Vector3 leftPlanted = FindGroundPoint(charPos - (charRight * _crawlHandSpread) - (charForward * 0.15f));
                UpdateArmTarget(_leftArm, leftPlanted, Quaternion.LookRotation(charForward, Vector3.up), -5f, dt);

                _rightArm.StepStartPos = rightPos;
                _leftArm.StepStartPos = leftPlanted;
            }
        }

        private Vector3 FindGroundPoint(Vector3 samplePoint)
        {
            Vector3 origin = samplePoint + Vector3.up * 0.6f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _crawlRayHits, 1.4f, _crawlGroundLayer, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _crawlRayHits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform.root == transform.root) continue;
                return hit.point;
            }

            return samplePoint + Vector3.down * 0.2f;
        }

        private void UpdateArmTarget(ProceduralArm arm, Vector3 targetPos, Quaternion targetRot, float targetCurl, float dt)
        {
            if (arm == null) return;

            arm.TargetHandPos = targetPos;
            Vector3 handVelocity = arm.HandVelocity;
            arm.CurrentHandPos = Vector3.SmoothDamp(arm.CurrentHandPos, targetPos, ref handVelocity, _handPositionSmoothTime, 25f, dt);
            arm.HandVelocity = handVelocity;

            arm.CurrentHandRot = Quaternion.Slerp(arm.CurrentHandRot, targetRot, 18f * dt);

            float currentCurl = Mathf.MoveTowards(arm.CurrentFingerCurl, targetCurl, _fingerCurlSpeed * dt);
            arm.ApplyFingerCurl(currentCurl, _distalCurlMultiplier);
        }

        private void SolveArmSpline(ProceduralArm arm)
        {
            if (arm == null || arm.Shoulder == null || arm.Bones == null || arm.Bones.Length < 8) return;

            Vector3 p0 = arm.Bones[0] != null ? arm.Bones[0].position : arm.Shoulder.position;
            Vector3 p3 = arm.CurrentHandPos;

            Vector3 shoulderToHand = p3 - p0;
            float dist = shoulderToHand.magnitude;
            float maxReach = arm.TotalChainLength > 0.05f ? arm.TotalChainLength * 0.98f : 0.85f;

            if (dist > maxReach && dist > 0.0001f)
            {
                p3 = p0 + (shoulderToHand / dist) * maxReach;
                shoulderToHand = p3 - p0;
            }

            bool isLeft = arm.Side == ArmSide.Left;
            Vector3 sideDir = isLeft ? -transform.right : transform.right;

            Vector3 p1 = p0 + (sideDir * _hoseArchOutward) - (transform.up * _hoseArchDown) + (shoulderToHand * 0.22f);
            Vector3 handForward = arm.CurrentHandRot * Vector3.forward;
            Vector3 p2 = p3 - (handForward * _wristApproachDistance) + (Vector3.up * _wristApproachHeight);

            arm.SolveSpline(p0, p1, p2, p3, transform);
        }

        [Button("Auto-Find Armature Bones")]
        public void AutoFindArmatureBones()
        {
            if (_controller == null) _controller = GetComponent<TorsoController>();
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_aimPivot == null && _controller != null) _aimPivot = _controller.AimPivot;
            if (_aimPivot == null) _aimPivot = transform;

            Transform shoulderL = FindChildRecursive(transform, "Shoulder.L");
            Transform shoulderR = FindChildRecursive(transform, "Shoulder.R");

            PopulateArm(ref _leftArm, ArmSide.Left, shoulderL, ".L");
            PopulateArm(ref _rightArm, ArmSide.Right, shoulderR, ".R");

            CacheRestPoses(forceRecache: true);
        }

        private void PopulateArm(ref ProceduralArm arm, ArmSide side, Transform shoulder, string suffix)
        {
            if (arm == null)
            {
                arm = new ProceduralArm();
            }

            Transform[] bones = new Transform[8];
            for (int i = 1; i <= 8; i++)
            {
                string boneName = $"Bone.{i:D3}{suffix}";
                bones[i - 1] = FindChildRecursive(transform, boneName);
            }

            ProceduralFinger[] fingers = new ProceduralFinger[3];
            for (int f = 1; f <= 3; f++)
            {
                Transform prox = FindChildRecursive(transform, $"Finger_{f}_Proximal{suffix}");
                Transform dist = FindChildRecursive(transform, $"Finger_{f}_Distal{suffix}");
                fingers[f - 1] = new ProceduralFinger(prox, dist);
            }

            arm.Configure(side, shoulder, bones, fingers);
        }

        [Button("Cache Rest Poses")]
        public void CacheRestPoses(bool forceRecache = false)
        {
            if (_leftArm != null)
            {
                _leftArm.EnsureBuffers();
                _leftArm.CacheRestPose(transform, forceRecache);
            }

            if (_rightArm != null)
            {
                _rightArm.EnsureBuffers();
                _rightArm.CacheRestPose(transform, forceRecache);
            }
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            if (parent.name == targetName) return parent;

            int count = parent.childCount;
            for (int i = 0; i < count; i++)
            {
                Transform result = FindChildRecursive(parent.GetChild(i), targetName);
                if (result != null) return result;
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (_leftArm != null && _leftArm.Bones != null && _leftArm.Bones[0] != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_leftArm.CurrentHandPos, 0.05f);
                Gizmos.DrawLine(_leftArm.Bones[0].position, _leftArm.CurrentHandPos);
            }

            if (_rightArm != null && _rightArm.Bones != null && _rightArm.Bones[0] != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.2f);
                Gizmos.DrawWireSphere(_rightArm.CurrentHandPos, 0.05f);
                Gizmos.DrawLine(_rightArm.Bones[0].position, _rightArm.CurrentHandPos);
            }
        }
    }
}
