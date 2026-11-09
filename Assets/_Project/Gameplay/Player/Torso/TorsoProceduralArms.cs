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
                _proximal.localRotation = _proximalRestLocalRot * Quaternion.Euler(0f, 0f, -curlAngle);
            }
            if (_distal != null)
            {
                _distal.localRotation = _distalRestLocalRot * Quaternion.Euler(0f, 0f, -curlAngle * distalMultiplier);
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
        [NonSerialized] private Vector3 _groundNormal = Vector3.up;
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
        public Vector3 GroundNormal { get => _groundNormal; set => _groundNormal = value; }
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
            _groundNormal = Vector3.up;
            _crawlStepTimer = 0f;
            _isCrawlingSwing = false;
            _handVelocity = Vector3.zero;
        }

        public void SolveSpline(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Transform rootTransform, float minFloorY = float.NegativeInfinity)
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
                if (minFloorY > -99999f && pt.y < minFloorY)
                {
                    pt.y = minFloorY;
                }
                _arcFineSamples[k] = pt;
                totalCurveLength += (_arcFineSamples[k - 1] - pt).magnitude;
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
                Vector3 sampled = SamplePointAtDistance(accumulatedDist, totalCurveLength);
                if (i < 7 && minFloorY > -99999f && sampled.y < minFloorY)
                {
                    sampled.y = minFloorY;
                }
                _splinePoints[i] = sampled;
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

        public Quaternion ComputeWorldHandRotation(Vector3 worldPointingDir, Transform rootTransform)
        {
            Quaternion rootRot = rootTransform != null ? rootTransform.rotation : Quaternion.identity;
            Quaternion restRot = (_restLocalRotations != null && _restLocalRotations.Length > 7 &&
                                  (_restLocalRotations[7].w != 0f || _restLocalRotations[7].x != 0f || _restLocalRotations[7].y != 0f || _restLocalRotations[7].z != 0f))
                ? _restLocalRotations[7]
                : Quaternion.Euler(180f, 0f, 0f);

            Vector3 restWorldDir = rootRot * Vector3.down;
            Vector3 safeTargetDir = worldPointingDir.sqrMagnitude > 0.0001f ? worldPointingDir.normalized : restWorldDir;
            Quaternion alignDelta = Quaternion.FromToRotation(restWorldDir, safeTargetDir);
            return alignDelta * (rootRot * restRot);
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

        [Group("Referencias")]
        [SerializeField] private Transform _bodyRoot;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlCycleDuration = 0.48f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlMinStepDuration = 0.14f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlInterStepPause = 0.06f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlMinInterStepPause = 0.02f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlStepDistanceThreshold = 0.30f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlStrideForward = 0.50f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlRestForwardOffset = 0.24f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _maxRearHandDistance = 0.62f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlStepHeight = 0.24f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlHandSpread = 0.46f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _palmGroundOffset = 0.18f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlPlantedFingerCurl = -45f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlSwingFingerCurl = 10f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlSlapSquash = 0.055f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlWobbleRollAngle = 4.5f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlWobblePitchAngle = 3.5f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlWobbleSmoothSpeed = 14f;

        [Group("Marcha Gateo")]
        [SerializeField] private float _crawlSquashRecoverySpeed = 11f;

        [Group("Marcha Gateo")]
        [SerializeField] private AnimationCurve _crawlHorizontalCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.25f, 0.15f, 1.2f, 1.2f),
            new Keyframe(0.65f, 0.95f, 0.8f, 0.8f),
            new Keyframe(0.85f, 1.0f, 0f, 0f),
            new Keyframe(1f, 1.0f, 0f, 0f)
        );

        [Group("Marcha Gateo")]
        [SerializeField] private AnimationCurve _crawlHeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 5f, 5f),
            new Keyframe(0.30f, 1.0f, 0f, 0f),
            new Keyframe(0.60f, 0.65f, -1.8f, -1.8f),
            new Keyframe(0.85f, 0f, -1.5f, 0f),
            new Keyframe(1f, 0f, 0f, 0f)
        );

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
        [SerializeField] private float _wristApproachHeight = 0.14f;

        [Group("Dedos Articulados")]
        [SerializeField] private float _idleFingerCurl = 15f;

        [Group("Dedos Articulados")]
        [SerializeField] private float _fingerCurlSpeed = 240f;

        [Group("Dedos Articulados")]
        [SerializeField] private float _distalCurlMultiplier = 1.25f;

        private readonly RaycastHit[] _crawlRayHits = new RaycastHit[4];
        private float _throwTimer;
        private float _interStepTimer;
        private int _activeSteppingArmIndex = -1;
        private int _nextArmGaitIndex;
        private bool _isRestPoseSettled;
        private bool _wasCrawling;
        private Vector3 _defaultBodyPosition;
        private Quaternion _defaultBodyRotation = Quaternion.identity;
        private bool _defaultBodyTransformCached;
        private float _currentSquash;
        private Quaternion _currentWobbleRotation = Quaternion.identity;
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

            if (_bodyRoot == null)
            {
                _bodyRoot = FindChildRecursive(transform, "Armature");
                if (_bodyRoot == null) _bodyRoot = FindChildRecursive(transform, "Torso_Unity");
            }

            if (_bodyRoot != null && !_defaultBodyTransformCached)
            {
                _defaultBodyPosition = _bodyRoot.localPosition;
                _defaultBodyRotation = _bodyRoot.localRotation;
                _defaultBodyTransformCached = true;
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

            if (_crawlGroundLayer.value == 0 || _crawlGroundLayer.value == ~0)
            {
                _crawlGroundLayer = LayerMask.GetMask("Default", "Ground", "Environment");
                if (_crawlGroundLayer.value == 0) _crawlGroundLayer = ~0;
            }

            if (_crawlPlantedFingerCurl > -25f)
            {
                _crawlPlantedFingerCurl = -45f;
            }

            if (_palmGroundOffset < 0.14f)
            {
                _palmGroundOffset = 0.18f;
            }

            if (_crawlStepDistanceThreshold < 0.24f)
            {
                _crawlStepDistanceThreshold = 0.30f;
            }

            if (_crawlCycleDuration < 0.42f)
            {
                _crawlCycleDuration = 0.48f;
            }

            if (_crawlInterStepPause < 0.045f)
            {
                _crawlInterStepPause = 0.06f;
            }

            if (_wristApproachHeight < 0.22f)
            {
                _wristApproachHeight = 0.26f;
            }

            _isInitialized = true;
        }

        public void ResetHandPositions()
        {
            Transform aimTrans = _aimPivot != null ? _aimPivot : transform;
            Vector3 aimPos = aimTrans.position;
            Quaternion aimRot = aimTrans.rotation;
            Vector3 downDir = (Vector3.down + aimTrans.forward * 0.25f).normalized;

            if (_leftArm != null)
            {
                Vector3 leftRest = aimPos + aimRot * new Vector3(-_restArmSpacing, _restArmHeight, _restArmForwardOffset);
                Quaternion leftRot = _leftArm.ComputeWorldHandRotation(downDir, transform);
                _leftArm.InitializeHandPos(leftRest, leftRot);
            }

            if (_rightArm != null)
            {
                Vector3 rightRest = aimPos + aimRot * new Vector3(_restArmSpacing, _restArmHeight, _restArmForwardOffset);
                Quaternion rightRot = _rightArm.ComputeWorldHandRotation(downDir, transform);
                _rightArm.InitializeHandPos(rightRest, rightRot);
            }

            _activeSteppingArmIndex = -1;
            _interStepTimer = 0f;
            _isRestPoseSettled = false;
            _wasCrawling = false;
        }

        public void SnapAllHandsToGround()
        {
            float baseStepDur = Mathf.Max(0.08f, _crawlCycleDuration * 0.5f);
            if (_leftArm != null)
            {
                Vector3 leftGround = EvaluateIdealHandGroundPosition(_leftArm, Vector3.zero, baseStepDur, _crawlInterStepPause, out Vector3 leftNorm);
                Quaternion leftRot = ComputePlantedHandRotation(_leftArm, leftNorm);
                _leftArm.InitializeHandPos(leftGround, leftRot);
                _leftArm.GroundNormal = leftNorm;
                _leftArm.ApplyFingerCurl(_crawlPlantedFingerCurl, _distalCurlMultiplier);
            }

            if (_rightArm != null)
            {
                Vector3 rightGround = EvaluateIdealHandGroundPosition(_rightArm, Vector3.zero, baseStepDur, _crawlInterStepPause, out Vector3 rightNorm);
                Quaternion rightRot = ComputePlantedHandRotation(_rightArm, rightNorm);
                _rightArm.InitializeHandPos(rightGround, rightRot);
                _rightArm.GroundNormal = rightNorm;
                _rightArm.ApplyFingerCurl(_crawlPlantedFingerCurl, _distalCurlMultiplier);
            }

            _activeSteppingArmIndex = -1;
            _interStepTimer = 0f;
            _isRestPoseSettled = true;
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
            bool isCrawling = !isFused && !isHolding && !isMagnet;

            if (isCrawling)
            {
                if (!_wasCrawling)
                {
                    SnapAllHandsToGround();
                }
                _wasCrawling = true;

                SolveCrawlingPose(dt);
                UpdateCrawlBodyWobble(dt, true);
            }
            else
            {
                _wasCrawling = false;
                _activeSteppingArmIndex = -1;
                _interStepTimer = 0f;
                _isRestPoseSettled = false;

                SolveFusedOrManipulatingPose(dt, isHolding, isMagnet);
                UpdateCrawlBodyWobble(dt, false);
            }

            SolveArmSpline(_leftArm, isCrawling);
            SolveArmSpline(_rightArm, isCrawling);
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

                targetLeftHandRot = _leftArm != null
                    ? _leftArm.ComputeWorldHandRotation(toCenterLeft, transform)
                    : socket.rotation;

                targetRightHandRot = _rightArm != null
                    ? _rightArm.ComputeWorldHandRotation(toCenterRight, transform)
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

                targetLeftHandRot = _leftArm != null
                    ? _leftArm.ComputeWorldHandRotation(origin.forward, transform)
                    : Quaternion.LookRotation(origin.forward, origin.up);
                targetRightHandRot = _rightArm != null
                    ? _rightArm.ComputeWorldHandRotation(origin.forward, transform)
                    : targetLeftHandRot;

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

                Vector3 idlePointingDir = (Vector3.down + aimForward * 0.25f).normalized;
                targetLeftHandRot = _leftArm != null
                    ? _leftArm.ComputeWorldHandRotation(idlePointingDir, transform)
                    : Quaternion.LookRotation(aimForward, aimUp);
                targetRightHandRot = _rightArm != null
                    ? _rightArm.ComputeWorldHandRotation(idlePointingDir, transform)
                    : targetLeftHandRot;

                targetFingerCurl = _idleFingerCurl;
            }

            UpdateArmTarget(_leftArm, targetLeftHandPos, targetLeftHandRot, targetFingerCurl, dt);
            UpdateArmTarget(_rightArm, targetRightHandPos, targetRightHandRot, targetFingerCurl, dt);
        }

        private void SolveCrawlingPose(float dt)
        {
            if (_currentSquash > 0.001f)
            {
                _currentSquash = Mathf.MoveTowards(_currentSquash, 0f, _crawlSquashRecoverySpeed * dt);
            }

            Vector3 linearVelocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(linearVelocity, transform.up);
            float speedSqr = horizontalVelocity.sqrMagnitude;
            float speed = Mathf.Sqrt(speedSqr);
            bool isMoving = speedSqr > 0.20f;

            Vector2 moveInput = _controller != null ? _controller.MoveInput : Vector2.zero;
            float inputSqr = moveInput.sqrMagnitude;
            bool hasInput = inputSqr > 0.01f;
            float inputMag = Mathf.Sqrt(inputSqr);
            bool hasLocomotionIntent = hasInput || isMoving;

            float maxSpeed = _controller != null ? _controller.CrawlSpeed : 3.5f;
            float speedRatio = Mathf.Clamp01(Mathf.Max(speed, inputMag * maxSpeed) / Mathf.Max(maxSpeed, 0.1f));

            float baseStepDuration = Mathf.Max(0.12f, _crawlCycleDuration * 0.45f);
            float currentStepDuration = Mathf.Lerp(baseStepDuration, _crawlMinStepDuration, speedRatio);
            float currentInterStepPause = Mathf.Lerp(_crawlInterStepPause, _crawlMinInterStepPause, speedRatio);

            // 1. Advance currently stepping hand along non-linear gait curves and plant firmly on ground
            if (_activeSteppingArmIndex != -1)
            {
                ProceduralArm steppingArm = GetArmByIndex(_activeSteppingArmIndex);
                if (steppingArm != null)
                {
                    steppingArm.CrawlStepTimer += dt;
                    float progress = Mathf.Clamp01(steppingArm.CrawlStepTimer / currentStepDuration);

                    if (hasLocomotionIntent)
                    {
                        Vector3 dynamicTarget = EvaluateIdealHandGroundPosition(steppingArm, horizontalVelocity, currentStepDuration, currentInterStepPause, out Vector3 targetNormal);
                        steppingArm.StepTargetPos = dynamicTarget;
                        steppingArm.GroundNormal = targetNormal;
                    }

                    float horizProgress = _crawlHorizontalCurve.Evaluate(progress);
                    Vector3 baseHoriz = Vector3.LerpUnclamped(steppingArm.StepStartPos, steppingArm.StepTargetPos, horizProgress);

                    float lateralProgress = Mathf.Clamp01(progress / 0.85f);
                    float lateralSample = Mathf.Sin(lateralProgress * Mathf.PI);
                    Vector3 outwardDir = steppingArm.Side == ArmSide.Left ? -transform.right : transform.right;
                    Vector3 lateralOffset = outwardDir * (lateralSample * 0.05f);

                    float heightSample = _crawlHeightCurve.Evaluate(progress);
                    Vector3 liftOffset = transform.up * (heightSample * _crawlStepHeight);

                    steppingArm.CurrentHandPos = baseHoriz + lateralOffset + liftOffset;
                    steppingArm.TargetHandPos = steppingArm.CurrentHandPos;

                    Quaternion plantedRot = ComputePlantedHandRotation(steppingArm, steppingArm.GroundNormal);
                    Vector3 swingPointingDir = (-steppingArm.GroundNormal + transform.forward * (0.35f * heightSample)).normalized;
                    Quaternion swingRot = steppingArm.ComputeWorldHandRotation(swingPointingDir, transform);
                    steppingArm.CurrentHandRot = Quaternion.Slerp(steppingArm.CurrentHandRot, swingRot, 24f * dt);

                    float flattenBlend = Mathf.Clamp01((progress - 0.55f) / 0.30f);
                    float targetCurl = Mathf.Lerp(_crawlSwingFingerCurl, _crawlPlantedFingerCurl, flattenBlend);
                    float nextCurl = Mathf.MoveTowards(steppingArm.CurrentFingerCurl, targetCurl, _fingerCurlSpeed * dt);
                    steppingArm.ApplyFingerCurl(nextCurl, _distalCurlMultiplier);

                    if (progress >= 1f)
                    {
                        steppingArm.CurrentHandPos = steppingArm.StepTargetPos;
                        steppingArm.PlantedGroundPos = steppingArm.StepTargetPos;
                        steppingArm.CurrentHandRot = plantedRot;
                        steppingArm.IsCrawlingSwing = false;
                        steppingArm.ApplyFingerCurl(_crawlPlantedFingerCurl, _distalCurlMultiplier);

                        _currentSquash = Mathf.Max(_currentSquash, _crawlSlapSquash);
                        _activeSteppingArmIndex = -1;
                        _interStepTimer = currentInterStepPause;
                        _nextArmGaitIndex = (_nextArmGaitIndex + 1) % 2;
                    }
                }
                else
                {
                    _activeSteppingArmIndex = -1;
                }
            }

            // 2. Maintain planted hands locked in world space on the floor with rear leash limit
            for (int i = 0; i < 2; i++)
            {
                if (i == _activeSteppingArmIndex) continue;

                ProceduralArm arm = GetArmByIndex(i);
                if (arm == null) continue;

                arm.IsCrawlingSwing = false;
                Vector3 anchorPos = GetHandRestAnchorWorld(arm);
                Vector3 deltaTotal = arm.PlantedGroundPos - anchorPos;
                Vector3 deltaOnPlane = Vector3.ProjectOnPlane(deltaTotal, transform.up);

                float distOnPlaneSqr = deltaOnPlane.sqrMagnitude;
                float maxRearSqr = _maxRearHandDistance * _maxRearHandDistance;
                if (distOnPlaneSqr > maxRearSqr && distOnPlaneSqr > 0.0001f)
                {
                    float distOnPlane = Mathf.Sqrt(distOnPlaneSqr);
                    Vector3 clampedWorld = anchorPos + (deltaOnPlane / distOnPlane) * _maxRearHandDistance;
                    arm.PlantedGroundPos = FindGroundPoint(clampedWorld, out Vector3 clampedNorm);
                    arm.GroundNormal = clampedNorm;
                }

                arm.CurrentHandPos = arm.PlantedGroundPos;
                arm.TargetHandPos = arm.PlantedGroundPos;

                Quaternion plantedRot = ComputePlantedHandRotation(arm, arm.GroundNormal);
                arm.CurrentHandRot = Quaternion.Slerp(arm.CurrentHandRot, plantedRot, 22f * dt);

                float nextCurl = Mathf.MoveTowards(arm.CurrentFingerCurl, _crawlPlantedFingerCurl, _fingerCurlSpeed * dt);
                arm.ApplyFingerCurl(nextCurl, _distalCurlMultiplier);
            }

            // 3. Initiate step on next candidate hand ONLY after inter-step pause (both hands planted first!)
            if (_activeSteppingArmIndex == -1)
            {
                if (_interStepTimer > 0f)
                {
                    _interStepTimer -= dt;
                    return;
                }

                if (hasLocomotionIntent)
                {
                    _isRestPoseSettled = false;
                }
                else
                {
                    if (_isRestPoseSettled)
                    {
                        return;
                    }

                    bool anyHandMisaligned = false;
                    for (int s = 0; s < 2; s++)
                    {
                        int checkIndex = (_nextArmGaitIndex + s) % 2;
                        ProceduralArm checkArm = GetArmByIndex(checkIndex);
                        if (checkArm == null) continue;

                        Vector3 restTarget = EvaluateIdealHandGroundPosition(checkArm, Vector3.zero, currentStepDuration, currentInterStepPause, out Vector3 restNormal);
                        float distSqr = (checkArm.PlantedGroundPos - restTarget).sqrMagnitude;

                        if (distSqr > 0.025f)
                        {
                            checkArm.IsCrawlingSwing = true;
                            checkArm.CrawlStepTimer = 0f;
                            checkArm.StepStartPos = checkArm.CurrentHandPos;
                            checkArm.StepTargetPos = restTarget;
                            checkArm.GroundNormal = restNormal;
                            _activeSteppingArmIndex = checkIndex;
                            _nextArmGaitIndex = (checkIndex + 1) % 2;
                            anyHandMisaligned = true;
                            return;
                        }
                    }

                    if (!anyHandMisaligned)
                    {
                        _isRestPoseSettled = true;
                        return;
                    }
                }

                ProceduralArm candidateArm = GetArmByIndex(_nextArmGaitIndex);
                if (candidateArm != null)
                {
                    Vector3 idealTarget = EvaluateIdealHandGroundPosition(candidateArm, horizontalVelocity, currentStepDuration, currentInterStepPause, out Vector3 idealNormal);
                    float distToIdealSqr = (candidateArm.PlantedGroundPos - idealTarget).sqrMagnitude;
                    float thresholdSqr = _crawlStepDistanceThreshold * _crawlStepDistanceThreshold;

                    if (distToIdealSqr > thresholdSqr || (hasInput && distToIdealSqr > 0.04f))
                    {
                        candidateArm.IsCrawlingSwing = true;
                        candidateArm.CrawlStepTimer = 0f;
                        candidateArm.StepStartPos = candidateArm.CurrentHandPos;
                        candidateArm.StepTargetPos = idealTarget;
                        candidateArm.GroundNormal = idealNormal;
                        _activeSteppingArmIndex = _nextArmGaitIndex;
                    }
                    else
                    {
                        int otherIndex = (_nextArmGaitIndex + 1) % 2;
                        ProceduralArm otherArm = GetArmByIndex(otherIndex);
                        if (otherArm != null)
                        {
                            Vector3 otherTarget = EvaluateIdealHandGroundPosition(otherArm, horizontalVelocity, currentStepDuration, currentInterStepPause, out Vector3 otherNormal);
                            if ((otherArm.PlantedGroundPos - otherTarget).sqrMagnitude > thresholdSqr)
                            {
                                _nextArmGaitIndex = otherIndex;
                            }
                        }
                    }
                }
            }
        }

        private ProceduralArm GetArmByIndex(int index)
        {
            return index == 0 ? _leftArm : _rightArm;
        }

        private Vector3 GetHandRestAnchorWorld(ProceduralArm arm)
        {
            float lateralSign = arm != null && arm.Side == ArmSide.Left ? -1f : 1f;
            return transform.position
                + (transform.right * (lateralSign * _crawlHandSpread))
                + (transform.forward * _crawlRestForwardOffset);
        }

        private Quaternion ComputePlantedHandRotation(ProceduralArm arm, Vector3 groundNormal)
        {
            Vector3 up = groundNormal.sqrMagnitude > 0.001f ? groundNormal.normalized : Vector3.up;
            if (arm != null)
            {
                return arm.ComputeWorldHandRotation(-up, transform);
            }

            Vector3 forwardOnGround = Vector3.ProjectOnPlane(transform.forward, up);
            if (forwardOnGround.sqrMagnitude < 0.0001f)
            {
                forwardOnGround = transform.forward;
            }
            else
            {
                forwardOnGround.Normalize();
            }

            return Quaternion.LookRotation(forwardOnGround, up);
        }

        private Vector3 EvaluateIdealHandGroundPosition(
            ProceduralArm arm,
            Vector3 horizontalVelocity,
            float currentStepDuration,
            float currentInterStepPause,
            out Vector3 groundNormal)
        {
            Vector3 anchorPos = GetHandRestAnchorWorld(arm);

            float stanceDuration = currentStepDuration + currentInterStepPause;
            float leadTime = (currentStepDuration * 0.85f) + (stanceDuration * 0.5f);

            Vector3 angularVel = _rigidbody != null ? _rigidbody.angularVelocity : Vector3.zero;
            Vector3 tangentialVel = Vector3.Cross(angularVel, anchorPos - transform.position);
            Vector3 tangentialOnPlane = Vector3.ProjectOnPlane(tangentialVel, transform.up);
            Vector3 totalVelocity = horizontalVelocity + tangentialOnPlane;

            if (_controller != null && _controller.MoveInput.sqrMagnitude > 0.01f)
            {
                Vector2 input = _controller.MoveInput;
                Vector3 inputDir = new Vector3(input.x, 0f, input.y);
                if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();
                Vector3 inputVelocity = inputDir * _controller.CrawlSpeed;
                if (totalVelocity.sqrMagnitude < (inputVelocity.sqrMagnitude * 0.5f))
                {
                    totalVelocity = Vector3.Lerp(totalVelocity, inputVelocity, 0.85f);
                }
            }

            Vector3 strideLead = totalVelocity * leadTime;
            float leadMagSqr = strideLead.sqrMagnitude;
            float maxStrideSqr = _crawlStrideForward * _crawlStrideForward;
            if (leadMagSqr > maxStrideSqr && leadMagSqr > 0.0001f)
            {
                strideLead = (strideLead / Mathf.Sqrt(leadMagSqr)) * _crawlStrideForward;
            }

            Vector3 projectedPos = anchorPos + strideLead;
            return FindGroundPoint(projectedPos, out groundNormal);
        }

        private Vector3 FindGroundPoint(Vector3 samplePoint, out Vector3 groundNormal)
        {
            Vector3 origin = samplePoint + Vector3.up * 0.75f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _crawlRayHits, 1.6f, _crawlGroundLayer, QueryTriggerInteraction.Ignore);

            float bestDist = float.MaxValue;
            Vector3 bestPoint = Vector3.zero;
            Vector3 bestNormal = Vector3.up;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _crawlRayHits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform.root == transform.root) continue;

                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    bestPoint = hit.point;
                    bestNormal = hit.normal.sqrMagnitude > 0.01f ? hit.normal : Vector3.up;
                    found = true;
                }
            }

            if (found)
            {
                groundNormal = bestNormal;
                return bestPoint + (bestNormal * _palmGroundOffset);
            }

            groundNormal = Vector3.up;
            return new Vector3(samplePoint.x, transform.position.y + _palmGroundOffset, samplePoint.z);
        }

        private void UpdateCrawlBodyWobble(float dt, bool isCrawling)
        {
            if (_bodyRoot == null || !_defaultBodyTransformCached) return;

            if (!isCrawling)
            {
                _currentSquash = Mathf.MoveTowards(_currentSquash, 0f, _crawlSquashRecoverySpeed * dt);
                _currentWobbleRotation = Quaternion.Slerp(_currentWobbleRotation, Quaternion.identity, _crawlWobbleSmoothSpeed * dt);
                _bodyRoot.localRotation = _defaultBodyRotation * _currentWobbleRotation;
                _bodyRoot.localPosition = _defaultBodyPosition - Vector3.up * _currentSquash;
                return;
            }

            Quaternion targetWobble = Quaternion.identity;
            if (_activeSteppingArmIndex == 0)
            {
                targetWobble = Quaternion.Euler(-_crawlWobblePitchAngle, 0f, _crawlWobbleRollAngle);
            }
            else if (_activeSteppingArmIndex == 1)
            {
                targetWobble = Quaternion.Euler(-_crawlWobblePitchAngle, 0f, -_crawlWobbleRollAngle);
            }

            Vector3 avgNormal = Vector3.zero;
            int normalCount = 0;
            if (_leftArm != null && _leftArm.GroundNormal.sqrMagnitude > 0.1f)
            {
                avgNormal += _leftArm.GroundNormal;
                normalCount++;
            }
            if (_rightArm != null && _rightArm.GroundNormal.sqrMagnitude > 0.1f)
            {
                avgNormal += _rightArm.GroundNormal;
                normalCount++;
            }

            if (normalCount > 0)
            {
                avgNormal /= normalCount;
                if (avgNormal.sqrMagnitude > 0.001f) avgNormal.Normalize();
                else avgNormal = transform.up;
            }
            else
            {
                avgNormal = transform.up;
            }

            Quaternion slopeOffset = Quaternion.FromToRotation(transform.up, avgNormal);
            Quaternion localSlopeOffset = Quaternion.Inverse(transform.rotation) * slopeOffset * transform.rotation;

            _currentWobbleRotation = Quaternion.Slerp(_currentWobbleRotation, targetWobble * localSlopeOffset, _crawlWobbleSmoothSpeed * dt);
            _bodyRoot.localRotation = _defaultBodyRotation * _currentWobbleRotation;
            _bodyRoot.localPosition = _defaultBodyPosition - Vector3.up * _currentSquash;
        }

        public bool ComputeCrawlSupportCentroid(out Vector3 centroid, out Vector3 avgNormal, out float pullSurgeMultiplier)
        {
            centroid = transform.position;
            avgNormal = transform.up;
            pullSurgeMultiplier = 1f;

            if (_leftArm == null || _rightArm == null)
            {
                return false;
            }

            Vector3 sumPos = Vector3.zero;
            Vector3 sumNormal = Vector3.zero;
            float totalWeight = 0f;

            for (int i = 0; i < 2; i++)
            {
                ProceduralArm arm = i == 0 ? _leftArm : _rightArm;
                Vector3 handPos;
                float weight;

                if (!arm.IsCrawlingSwing)
                {
                    handPos = arm.PlantedGroundPos;
                    weight = 1f;
                }
                else
                {
                    handPos = Vector3.Lerp(arm.StepStartPos, arm.StepTargetPos, 0.5f);
                    weight = 0.70f;
                }

                sumPos += handPos * weight;
                sumNormal += (arm.GroundNormal.sqrMagnitude > 0.01f ? arm.GroundNormal : transform.up) * weight;
                totalWeight += weight;
            }

            if (totalWeight <= 0.0001f)
            {
                return false;
            }

            centroid = sumPos / totalWeight;
            avgNormal = sumNormal.sqrMagnitude > 0.001f ? sumNormal.normalized : transform.up;

            // Rhythmic traction surge: while a hand is actively swinging/pulling, modulate crawl surge smoothly
            if (_activeSteppingArmIndex != -1)
            {
                ProceduralArm activeArm = GetArmByIndex(_activeSteppingArmIndex);
                float baseStepDuration = Mathf.Max(0.08f, _crawlCycleDuration * 0.45f);
                float prog = activeArm != null ? Mathf.Clamp01(activeArm.CrawlStepTimer / baseStepDuration) : 0.5f;
                pullSurgeMultiplier = 0.88f + 0.24f * Mathf.Sin(prog * Mathf.PI);
            }

            return true;
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

        private void SolveArmSpline(ProceduralArm arm, bool isCrawling)
        {
            if (arm == null || arm.Shoulder == null || arm.Bones == null || arm.Bones.Length < 8) return;

            Vector3 p0 = arm.Bones[0] != null ? arm.Bones[0].position : arm.Shoulder.position;
            Vector3 p3 = arm.CurrentHandPos;

            Vector3 shoulderToHand = p3 - p0;
            float distSqr = shoulderToHand.sqrMagnitude;
            float dist = Mathf.Sqrt(distSqr);
            float maxReach = arm.TotalChainLength > 0.05f ? arm.TotalChainLength * 0.98f : 0.85f;

            if (distSqr > maxReach * maxReach && distSqr > 0.000001f)
            {
                p3 = p0 + (shoulderToHand / dist) * maxReach;
                shoulderToHand = p3 - p0;
                dist = maxReach;
            }

            bool isLeft = arm.Side == ArmSide.Left;
            Vector3 sideDir = isLeft ? -transform.right : transform.right;

            Vector3 p1;
            Vector3 p2;
            float minFloorY = float.NegativeInfinity;

            if (isCrawling)
            {
                float slack = Mathf.Max(0f, maxReach - dist);
                float naturalBow = Mathf.Clamp(Mathf.Sqrt(slack * maxReach * 0.45f) + 0.10f, 0.12f, _hoseArchOutward + 0.14f);
                Vector3 groundUp = arm.GroundNormal.sqrMagnitude > 0.01f ? arm.GroundNormal : Vector3.up;
                Vector3 horizShoulderToHand = Vector3.ProjectOnPlane(shoulderToHand, groundUp);

                p1 = p0 + (sideDir * naturalBow) + (horizShoulderToHand * 0.25f) + (groundUp * 0.24f);
                p2 = p3 + (groundUp * Mathf.Max(0.28f, _wristApproachHeight));
                minFloorY = (arm.PlantedGroundPos.y - _palmGroundOffset) + 0.14f;
            }
            else
            {
                p1 = p0 + (sideDir * _hoseArchOutward) - (transform.up * _hoseArchDown) + (shoulderToHand * 0.22f);
                Vector3 handPointingDir = arm.CurrentHandRot * Vector3.up;
                p2 = p3 - (handPointingDir * _wristApproachDistance) + (Vector3.up * _wristApproachHeight);
            }

            arm.SolveSpline(p0, p1, p2, p3, transform, minFloorY);
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
