using System;
using Game.Gameplay.Player.Legs;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Player.Legs
{
    [Serializable]
    public sealed class SplineLeg : ISerializationCallbackReceiver
    {
        [SerializeField] private LegID _id;
        [SerializeField] private Transform _restAnchor;
        [SerializeField] private Transform[] _bones = new Transform[13];

        [SerializeField, HideInInspector] private Quaternion[] _restLocalRotations;
        [SerializeField, HideInInspector] private Vector3[] _restLocalSegmentDirs;
        [SerializeField, HideInInspector] private float[] _restSegmentLengths;
        [SerializeField, HideInInspector] private float _totalChainLength;
        [SerializeField, HideInInspector] private Quaternion _footRestRotation = Quaternion.identity;
        [SerializeField, HideInInspector] private Vector3[] _restBoneLocalPositions;
        [SerializeField, HideInInspector] private Quaternion[] _restBoneLocalRotations;
        [SerializeField, HideInInspector] private bool _hasValidBindPose;

        [NonSerialized] private Vector3 _currentFootPos;
        [NonSerialized] private Vector3 _plantedFootPos;
        [NonSerialized] private Vector3 _stepStartFootPos;
        [NonSerialized] private Vector3 _stepTargetFootPos;
        [NonSerialized] private Quaternion _currentFootRot = Quaternion.identity;
        [NonSerialized] private Quaternion _plantedFootRot = Quaternion.identity;
        [NonSerialized] private Vector3 _groundNormal = Vector3.up;
        [NonSerialized] private float _stepTimer;
        [NonSerialized] private LegActionState _state = LegActionState.Planted;
        [NonSerialized] private RaycastHit[] _rayHits;
        [NonSerialized] private Vector3[] _splinePoints;

        // Pre-allocated buffers for zero-GC arc-length sampling
        [NonSerialized] private Vector3[] _arcFineSamples;
        [NonSerialized] private float[] _arcCumulativeLengths;

        public LegID ID => _id;
        public Transform RestAnchor => _restAnchor;
        public Transform[] Bones => _bones;
        public float TotalChainLength => _totalChainLength;
        public bool HasValidBindPose => _hasValidBindPose;
        public Vector3 CurrentFootPos { get => _currentFootPos; set => _currentFootPos = value; }
        public Vector3 PlantedFootPos { get => _plantedFootPos; set => _plantedFootPos = value; }
        public Vector3 StepStartFootPos { get => _stepStartFootPos; set => _stepStartFootPos = value; }
        public Vector3 StepTargetFootPos { get => _stepTargetFootPos; set => _stepTargetFootPos = value; }
        public Quaternion CurrentFootRot { get => _currentFootRot; set => _currentFootRot = value; }
        public Quaternion PlantedFootRot { get => _plantedFootRot; set => _plantedFootRot = value; }
        public Vector3 GroundNormal { get => _groundNormal; set => _groundNormal = value; }
        public float StepTimer { get => _stepTimer; set => _stepTimer = value; }
        public LegActionState State { get => _state; set => _state = value; }

        public RaycastHit[] RayHits
        {
            get
            {
                if (_rayHits == null || _rayHits.Length != 4) _rayHits = new RaycastHit[4];
                return _rayHits;
            }
        }

        public Vector3[] SplinePoints
        {
            get
            {
                if (_splinePoints == null || _splinePoints.Length != 13) _splinePoints = new Vector3[13];
                return _splinePoints;
            }
        }

        public SplineLeg(LegID id, Transform restAnchor, Transform[] bones)
        {
            _id = id;
            _restAnchor = restAnchor;
            _bones = bones;
            EnsureBuffers();
        }

        public void EnsureBuffers()
        {
            if (_restLocalRotations == null || _restLocalRotations.Length != 13)
                _restLocalRotations = new Quaternion[13];
            if (_restLocalSegmentDirs == null || _restLocalSegmentDirs.Length != 12)
                _restLocalSegmentDirs = new Vector3[12];
            if (_restSegmentLengths == null || _restSegmentLengths.Length != 12)
                _restSegmentLengths = new float[12];
            if (_restBoneLocalPositions == null || _restBoneLocalPositions.Length != 13)
                _restBoneLocalPositions = new Vector3[13];
            if (_restBoneLocalRotations == null || _restBoneLocalRotations.Length != 13)
                _restBoneLocalRotations = new Quaternion[13];
            if (_rayHits == null || _rayHits.Length != 4)
                _rayHits = new RaycastHit[4];
            if (_splinePoints == null || _splinePoints.Length != 13)
                _splinePoints = new Vector3[13];
            if (_arcFineSamples == null || _arcFineSamples.Length != 25)
                _arcFineSamples = new Vector3[25];
            if (_arcCumulativeLengths == null || _arcCumulativeLengths.Length != 25)
                _arcCumulativeLengths = new float[25];

            if (_groundNormal == Vector3.zero)
                _groundNormal = Vector3.up;
            if (_footRestRotation.w == 0f && _footRestRotation.x == 0f && _footRestRotation.y == 0f && _footRestRotation.z == 0f)
                _footRestRotation = Quaternion.identity;
            if (_currentFootRot.w == 0f && _currentFootRot.x == 0f && _currentFootRot.y == 0f && _currentFootRot.z == 0f)
                _currentFootRot = Quaternion.identity;
            if (_plantedFootRot.w == 0f && _plantedFootRot.x == 0f && _plantedFootRot.y == 0f && _plantedFootRot.z == 0f)
                _plantedFootRot = Quaternion.identity;
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize()
        {
            EnsureBuffers();
        }

        public void CacheRestPose(Transform characterTransform, bool forceRecache = false)
        {
            EnsureBuffers();
            if (_bones == null || _bones.Length < 13) return;

            // Never overwrite pristine bind pose with dynamically posed or airborne bones from scene!
            if (_hasValidBindPose && !forceRecache && _totalChainLength > 0.1f)
            {
                return;
            }

            Quaternion invCharRot = characterTransform != null ? Quaternion.Inverse(characterTransform.rotation) : Quaternion.identity;

            for (int i = 0; i < 13; i++)
            {
                if (_bones[i] != null)
                {
                    _restBoneLocalPositions[i] = _bones[i].localPosition;
                    _restBoneLocalRotations[i] = _bones[i].localRotation;
                    _restLocalRotations[i] = invCharRot * _bones[i].rotation;
                }
            }

            _totalChainLength = 0f;
            for (int i = 0; i < 12; i++)
            {
                if (_bones[i] != null && _bones[i + 1] != null)
                {
                    Vector3 worldDir = (_bones[i + 1].position - _bones[i].position);
                    float segLen = worldDir.magnitude;
                    _restSegmentLengths[i] = segLen;
                    _totalChainLength += segLen;
                    _restLocalSegmentDirs[i] = invCharRot * (segLen > 0.0001f ? worldDir / segLen : Vector3.down);
                }
            }

            if (_bones[12] != null && characterTransform != null)
            {
                _footRestRotation = invCharRot * _bones[12].rotation;
            }

            if (_totalChainLength > 0.1f)
            {
                _hasValidBindPose = true;
            }
        }

        public void RestoreRestBones()
        {
            if (_bones == null || _restBoneLocalPositions == null || _restBoneLocalRotations == null) return;
            for (int i = 0; i < 13; i++)
            {
                if (_bones[i] != null && i < _restBoneLocalPositions.Length && i < _restBoneLocalRotations.Length)
                {
                    if (_restBoneLocalRotations[i].w != 0f || _restBoneLocalRotations[i].x != 0f || _restBoneLocalRotations[i].y != 0f || _restBoneLocalRotations[i].z != 0f)
                    {
                        _bones[i].localPosition = _restBoneLocalPositions[i];
                        _bones[i].localRotation = _restBoneLocalRotations[i];
                    }
                }
            }
        }

        public void Initialize(Vector3 initialFootWorldPos, Transform characterTransform)
        {
            EnsureBuffers();
            _currentFootPos = initialFootWorldPos;
            _plantedFootPos = initialFootWorldPos;
            _stepStartFootPos = initialFootWorldPos;
            _stepTargetFootPos = initialFootWorldPos;
            _groundNormal = Vector3.up;
            _stepTimer = 0f;
            _state = LegActionState.Planted;

            if (characterTransform != null)
            {
                _currentFootRot = characterTransform.rotation * _footRestRotation;
                _plantedFootRot = _currentFootRot;
            }
        }

        public void SolveSplineHose(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 groundNorm, Transform characterTransform)
        {
            EnsureBuffers();
            if (_bones == null || _bones.Length < 13) return;

            Quaternion charRot = characterTransform != null ? characterTransform.rotation : Quaternion.identity;

            // 1. Fine-sample the Cubic Bezier curve to measure true arc-length (Zero GC)
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

            // 2. Place all 13 bones at exact physical arc-length distances (prevents mesh spacing / stretching!)
            _splinePoints[0] = p0;
            float lengthScale = (_totalChainLength > 0.001f && totalCurveLength > 0.001f)
                ? (totalCurveLength / _totalChainLength)
                : 1f;

            float accumulatedDist = 0f;
            for (int i = 1; i < 13; i++)
            {
                accumulatedDist += _restSegmentLengths[i - 1] * lengthScale;
                _splinePoints[i] = SamplePointAtDistance(accumulatedDist, totalCurveLength);
            }

            // 3. Position and rotate bones 0..11 along the curve segments
            for (int i = 0; i < 12; i++)
            {
                Transform bone = _bones[i];
                if (bone == null) continue;

                Vector3 currentPos = _splinePoints[i];
                Vector3 nextPos = _splinePoints[i + 1];
                Vector3 newDir = (nextPos - currentPos);
                if (newDir.sqrMagnitude > 0.0001f) newDir.Normalize();
                else newDir = Vector3.down;

                Vector3 currentRestDir = charRot * _restLocalSegmentDirs[i];
                Quaternion currentRestRot = charRot * _restLocalRotations[i];

                Quaternion deltaRot = Quaternion.FromToRotation(currentRestDir, newDir);

                bone.position = currentPos;
                bone.rotation = deltaRot * currentRestRot;
            }

            // 4. Position and rotate terminal foot bone (12)
            Transform footBone = _bones[12];
            if (footBone != null && characterTransform != null)
            {
                footBone.position = _splinePoints[12];

                // Align foot with ground normal and character facing direction
                Quaternion groundAlignment = Quaternion.FromToRotation(Vector3.up, groundNorm);
                footBone.rotation = groundAlignment * charRot * _footRestRotation;
            }
        }

        private Vector3 SamplePointAtDistance(float targetDist, float totalCurveLength)
        {
            if (targetDist <= 0f) return _arcFineSamples[0];
            if (targetDist >= totalCurveLength) return _arcFineSamples[24];

            for (int k = 0; k < 24; k++)
            {
                float d0 = _arcCumulativeLengths[k];
                float d1 = _arcCumulativeLengths[k + 1];
                if (targetDist >= d0 && targetDist <= d1)
                {
                    float segLen = d1 - d0;
                    float frac = segLen > 0.0001f ? (targetDist - d0) / segLen : 0f;
                    return Vector3.Lerp(_arcFineSamples[k], _arcFineSamples[k + 1], frac);
                }
            }
            return _arcFineSamples[24];
        }
    }

    [DisallowMultipleComponent]
    [DeclareBoxGroup("Referencias")]
    [DeclareBoxGroup("Curvatura de Manguera (Bézier Arch)")]
    [DeclareBoxGroup("Marcha Torpe y Caída de Golpe")]
    [DeclareBoxGroup("Esquive Inteligente de Extremidades")]
    [DeclareBoxGroup("Detección de Terreno")]
    [DeclareBoxGroup("Bamboleo y Amortiguación")]
    [DeclareBoxGroup("Salto y Patada")]
    [DeclareBoxGroup("Simulación y Pruebas en Editor (Odin/TriInspector)")]
    public sealed class LegsSplineHoseAnimator : MonoBehaviour
    {
        [Group("Referencias")]
        [SerializeField] private LegsController _controller;

        [Group("Referencias")]
        [SerializeField] private Rigidbody _rigidbody;

        [Group("Referencias")]
        [SerializeField] private Transform _bodyRoot;

        [Group("Referencias")]
        [SerializeField] private SplineLeg[] _legs = new SplineLeg[4];

        [Group("Curvatura de Manguera (Bézier Arch)")]
        [Tooltip("Apertura lateral máxima del arco hacia afuera")]
        [SerializeField] private float _archOutwardWidth = 0.30f;

        [Group("Curvatura de Manguera (Bézier Arch)")]
        [Tooltip("Altura del arco sobre el pie para caída vertical (P2)")]
        [SerializeField] private float _ankleArchHeight = 0.20f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [SerializeField] private float _stepDistanceThreshold = 0.35f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Duración base del paso al caminar lento o detenerse")]
        [SerializeField] private float _stepDuration = 0.20f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Duración mínima del paso a máxima velocidad")]
        [SerializeField] private float _minStepDuration = 0.11f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Pausa obligatoria con las 4 patas firmemente apoyadas en el suelo al caminar lento")]
        [Range(0f, 0.25f)]
        [SerializeField] private float _interStepPause = 0.08f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Pausa mínima de apoyo firme a máxima velocidad")]
        [Range(0f, 0.10f)]
        [SerializeField] private float _minInterStepPause = 0.02f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Distancia máxima de zancada proyectada hacia adelante")]
        [SerializeField] private float _maxForwardStride = 0.55f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Distancia máxima atrás en apoyo antes de acompañar suavemente al chasis")]
        [SerializeField] private float _maxRearStanceDistance = 0.45f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Altura máxima del paso en Y (zancada vertical pronunciada)")]
        [SerializeField] private float _maxStepHeight = 0.55f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Arco lateral hacia afuera (0 para zancada limpia hacia adelante sin revoleo lateral)")]
        [SerializeField] private float _lateralSwayArc = 0f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [SerializeField] private float _stompSquashPunch = 0.10f;

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Avance horizontal: elevación en Y primero, traslación al frente y llegada firme al destino")]
        [SerializeField] private AnimationCurve _horizontalProgressCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.25f, 0.15f, 1.2f, 1.2f),
            new Keyframe(0.65f, 0.95f, 0.8f, 0.8f),
            new Keyframe(0.85f, 1.0f, 0f, 0f),
            new Keyframe(1f, 1.0f, 0f, 0f)
        );

        [Group("Marcha Torpe y Caída de Golpe")]
        [Tooltip("Elevación vertical: subida rápida al cenit (0.3), vuelo alto y caída en picado al suelo (0.85)")]
        [SerializeField] private AnimationCurve _stepHeightCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 5f, 5f),
            new Keyframe(0.30f, 1.0f, 0f, 0f),
            new Keyframe(0.60f, 0.65f, -1.8f, -1.8f),
            new Keyframe(0.85f, 0f, -1.5f, 0f),
            new Keyframe(1f, 0f, 0f, 0f)
        );

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Curvatura interna permanente de la manguera delantera (m) en reposo y apoyo")]
        [Range(0.04f, 0.35f)]
        [SerializeField] private float _frontLegInwardFlex = 0.14f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Flexión interna dinámica adicional de la manguera delantera al levantar el pie en vuelo (m)")]
        [Range(0f, 0.25f)]
        [SerializeField] private float _frontLegStepInwardBoost = 0.06f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Desviación lateral hacia adentro del pie delantero en vuelo para no invadir el carril exterior (m)")]
        [Range(0f, 0.20f)]
        [SerializeField] private float _frontFootInwardArc = 0.05f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Amplitud del arco lateral hacia afuera del pie trasero en vuelo (m) para rodear limpiamente la pata delantera")]
        [Range(0.05f, 0.50f)]
        [SerializeField] private float _flankClearanceArc = 0.25f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Apertura lateral mínima garantizada del arco Bézier de la manguera trasera durante el paso (m)")]
        [Range(0.15f, 0.60f)]
        [SerializeField] private float _steppingFlankBowWidth = 0.38f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Proporción de apertura lateral en el tobillo P2 de la pata trasera durante el vuelo (0.3 = sutil, 0.7 = amplio)")]
        [Range(0.30f, 0.90f)]
        [SerializeField] private float _ankleBowRatio = 0.70f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Repliegue adicional hacia adentro de la manguera delantera plantada cuando pasa la pata trasera (m)")]
        [Range(0f, 0.20f)]
        [SerializeField] private float _companionHoseTuck = 0.08f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Radio de proximidad horizontal de seguridad entre patas del mismo flanco")]
        [Range(0.20f, 0.60f)]
        [SerializeField] private float _avoidanceRadius = 0.38f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Deflexión lateral de emergencia hacia afuera/adentro para el pie en vuelo")]
        [Range(0.05f, 0.40f)]
        [SerializeField] private float _maxAvoidancePush = 0.15f;

        [Group("Esquive Inteligente de Extremidades")]
        [Tooltip("Apertura adicional del arco Bézier de la manguera durante el esquive")]
        [Range(0f, 0.30f)]
        [SerializeField] private float _avoidanceHoseBulge = 0.15f;

        [Group("Detección de Terreno")]
        [SerializeField] private float _raycastOriginHeight = 0.8f;

        [Group("Detección de Terreno")]
        [SerializeField] private float _raycastDistance = 1.6f;

        [Group("Detección de Terreno")]
        [SerializeField] private LayerMask _groundLayer = ~0;

        [Group("Bamboleo y Amortiguación")]
        [SerializeField] private float _wobbleRollAngle = 5f;

        [Group("Bamboleo y Amortiguación")]
        [SerializeField] private float _wobblePitchAngle = 4f;

        [Group("Bamboleo y Amortiguación")]
        [SerializeField] private float _wobbleSmoothSpeed = 14f;

        [Group("Bamboleo y Amortiguación")]
        [SerializeField] private float _landingSquashAmount = 0.20f;

        [Group("Bamboleo y Amortiguación")]
        [SerializeField] private float _squashRecoverySpeed = 12f;

        [Group("Salto y Patada")]
        [SerializeField] private float _airborneTuckHeight = 0.28f;

        [Group("Salto y Patada")]
        [SerializeField] private float _airborneInwardOffset = 0.15f;

        [Group("Salto y Patada")]
        [SerializeField] private float _kickWindupDuration = 0.08f;

        [Group("Salto y Patada")]
        [SerializeField] private float _kickThrustDuration = 0.08f;

        [Group("Salto y Patada")]
        [SerializeField] private float _kickHoldDuration = 0.04f;

        [Group("Salto y Patada")]
        [SerializeField] private float _kickReturnDuration = 0.16f;

        [Group("Salto y Patada")]
        [SerializeField] private float _kickReachDistance = 1.5f;

        [Group("Simulación y Pruebas en Editor (Odin/TriInspector)")]
        [Tooltip("Velocidad horizontal simulada (m/s) para la marcha")]
        [Range(0.5f, 8.5f)]
        [SerializeField] private float _simulatedSpeed = 4.5f;

        [Group("Simulación y Pruebas en Editor (Odin/TriInspector)")]
        [Tooltip("Escala de velocidad de la animación para análisis milisegundo a milisegundo (0.0 = pausa, 0.05 = cámara 20x lenta, 1.0 = normal)")]
        [Range(0f, 2.0f)]
        [SerializeField] private float _animationTimeScale = 1.0f;

        [Group("Simulación y Pruebas en Editor (Odin/TriInspector)")]
        [Button(ButtonSizes.Medium, "▶ Simular Caminata Infinita (Toggle)")]
        public void ToggleSimulateWalk()
        {
            if (Application.isPlaying)
            {
                _isSimulatingWalk = !_isSimulatingWalk;
                if (_isSimulatingWalk)
                {
                    _isSimulatingAirborne = false;
                    _isKicking = false;
                }
                else
                {
                    SnapAllPlantedLegsToGround();
                    SolveAllLegSplines();
                }
            }
            else
            {
#if UNITY_EDITOR
                _isEditorSimulatingWalk = !_isEditorSimulatingWalk;
                if (_isEditorSimulatingWalk)
                {
                    EnsureEditorInitialized();
                    _isEditorAirborne = false;
                    _isKicking = false;
                    StartEditorSimulation();
                }
                else
                {
                    ResetToRestPose();
                }
#endif
            }
        }

        [Group("Simulación y Pruebas en Editor (Odin/TriInspector)")]
        [Button(ButtonSizes.Medium, "⏭ Avanzar 1 Fotograma (+16ms)")]
        public void StepForwardOneFrame()
        {
            float stepDt = 0.01666f;
            if (Application.isPlaying)
            {
                bool isGrounded = (_controller == null || _controller.IsGrounded) && !_isSimulatingAirborne;
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();
                Vector3 horizontalVelocity = fwd * _simulatedSpeed;
                StepSimulation(horizontalVelocity, isGrounded, stepDt);
            }
            else
            {
#if UNITY_EDITOR
                EnsureEditorInitialized();
                if (!_isEditorSimulatingWalk && !_isEditorAirborne && !_isKicking)
                {
                    _isEditorSimulatingWalk = true;
                }
                bool isGrounded = !_isEditorAirborne;
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();
                Vector3 horizontalVelocity = _isEditorSimulatingWalk ? (fwd * _simulatedSpeed) : Vector3.zero;

                StepSimulation(horizontalVelocity, isGrounded, stepDt);
                UnityEditor.SceneView.RepaintAll();
#endif
            }
        }

        [Group("Simulación y Pruebas en Editor (Odin/TriInspector)")]
        [Button(ButtonSizes.Medium, "⚡ Probar Patada")]
        public void TriggerSimulateKick()
        {
            if (Application.isPlaying)
            {
                HandleKicked();
            }
            else
            {
#if UNITY_EDITOR
                EnsureEditorInitialized();
                _isEditorAirborne = false;
                _isEditorSimulatingWalk = false;
                HandleKicked();
                StartEditorSimulation();
#endif
            }
        }

        [Group("Simulación y Pruebas en Editor (Odin/TriInspector)")]
        [Button(ButtonSizes.Medium, "🦘 Probar Salto / En el Aire (Toggle)")]
        public void ToggleSimulateAirborne()
        {
            if (Application.isPlaying)
            {
                _isSimulatingAirborne = !_isSimulatingAirborne;
                if (_isSimulatingAirborne)
                {
                    _isSimulatingWalk = false;
                }
                else
                {
                    _currentSquash = _landingSquashAmount;
                    SnapAllPlantedLegsToGround();
                    SolveAllLegSplines();
                }
            }
            else
            {
#if UNITY_EDITOR
                _isEditorAirborne = !_isEditorAirborne;
                _isEditorSimulatingWalk = false;
                _isKicking = false;

                if (_isEditorAirborne)
                {
                    EnsureEditorInitialized();
                    StartEditorSimulation();
                }
                else
                {
                    _currentSquash = _landingSquashAmount;
                    SnapAllPlantedLegsToGround();
                    SolveAllLegSplines();
                    StopEditorSimulation();
                    UnityEditor.SceneView.RepaintAll();
                }
#endif
            }
        }

        [Group("Simulación y Pruebas en Editor (Odin/TriInspector)")]
        [Button(ButtonSizes.Medium, "🔄 Resetear a Pose de Reposo")]
        public void ResetToRestPose()
        {
            _isSimulatingWalk = false;
            _isSimulatingAirborne = false;
            _isKicking = false;
            _kickingLegIndex = -1;
            _activeSteppingIndex = -1;
            _interStepTimer = 0f;
            _currentSquash = 0f;
            _currentWobbleRotation = Quaternion.identity;

#if UNITY_EDITOR
            StopEditorSimulation();
#endif

            EnsureEditorInitialized();

            if (_bodyRoot != null && _defaultBodyTransformCached)
            {
                _bodyRoot.localPosition = _defaultBodyPosition;
                _bodyRoot.localRotation = _defaultBodyRotation;
            }

            if (_legs != null)
            {
                for (int i = 0; i < _legs.Length; i++)
                {
                    SplineLeg leg = _legs[i];
                    if (leg == null || leg.RestAnchor == null) continue;

                    leg.RestoreRestBones();

                    Vector3 groundPoint = EvaluateIdealGroundPosition(leg, Vector3.zero);
                    leg.Initialize(groundPoint, transform);
                }
            }

            SolveAllLegSplines();

#if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
#endif
        }

        // Runtime states
        private Vector3 _defaultBodyPosition;
        private Quaternion _defaultBodyRotation;
        private bool _defaultBodyTransformCached;
        private float _currentSquash;
        private Quaternion _currentWobbleRotation = Quaternion.identity;

        private int _activeSteppingIndex = -1;
        private int _nextGaitIndex;
        private float _interStepTimer;
        private bool _wasGrounded = true;

        // Proximity Avoidance runtime states
        private float _currentAvoidanceHoseBulge;
        private Vector3 _debugAvoidanceSteppingPos;
        private Vector3 _debugAvoidanceOffset;
        private Vector3 _debugCompanionPos;
        private bool _debugHasAvoidance;

        public event Action<LegID, Vector3, Vector3> OnFootstepLanded;

        // Kick state
        private bool _isKicking;
        private int _kickingLegIndex = -1;
        private float _kickTimer;
        private Vector3 _kickStartPos;
        private Vector3 _kickTargetPos;
        private bool _lastKickWasLeft;

        private bool _restPoseCached;

        // Simulation flags
        private bool _isSimulatingWalk;
        private bool _isSimulatingAirborne;

#if UNITY_EDITOR
        private bool _isEditorSimulatingWalk;
        private bool _isEditorAirborne;
        private double _lastEditorTime;

        private void StartEditorSimulation()
        {
            _lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update -= EditorSimulationLoop;
            UnityEditor.EditorApplication.update += EditorSimulationLoop;
        }

        private void StopEditorSimulation()
        {
            _isEditorSimulatingWalk = false;
            _isEditorAirborne = false;
            _isKicking = false;
            UnityEditor.EditorApplication.update -= EditorSimulationLoop;
        }

        private void EditorSimulationLoop()
        {
            if (this == null || gameObject == null)
            {
                UnityEditor.EditorApplication.update -= EditorSimulationLoop;
                return;
            }

            if (Application.isPlaying)
            {
                StopEditorSimulation();
                return;
            }

            double currentTime = UnityEditor.EditorApplication.timeSinceStartup;
            float rawDt = (float)(currentTime - _lastEditorTime);
            _lastEditorTime = currentTime;

            if (rawDt > 0.05f) rawDt = 0.05f;
            if (rawDt < 0.0001f) return;

            float dt = rawDt * _animationTimeScale;

            bool isGrounded = !_isEditorAirborne;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();
            Vector3 horizontalVelocity = _isEditorSimulatingWalk ? (fwd * _simulatedSpeed) : Vector3.zero;

            if (dt > 0.000001f)
            {
                StepSimulation(horizontalVelocity, isGrounded, dt);
            }

            if (!_isEditorSimulatingWalk && !_isEditorAirborne && !_isKicking && _currentSquash <= 0.001f)
            {
                StopEditorSimulation();
            }

            UnityEditor.SceneView.RepaintAll();
        }
#endif

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<LegsController>();
                if (_controller == null) _controller = GetComponentInParent<LegsController>();
            }

            if (_rigidbody == null && _controller != null)
            {
                _rigidbody = _controller.GetComponent<Rigidbody>();
            }
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
                if (_rigidbody == null) _rigidbody = GetComponentInParent<Rigidbody>();
            }

            if (_bodyRoot != null && !_defaultBodyTransformCached)
            {
                _defaultBodyPosition = _bodyRoot.localPosition;
                _defaultBodyRotation = _bodyRoot.localRotation;
                _defaultBodyTransformCached = true;
            }

            if (_groundLayer.value == ~0 || _groundLayer.value == 0)
            {
                _groundLayer = LayerMask.GetMask("Default", "Ground", "Environment");
                if (_groundLayer.value == 0) _groundLayer = ~0;
            }

            CacheRestPoses();
            InitializeLegs();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnKicked += HandleKicked;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnKicked -= HandleKicked;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ResetToRestPose();
            }
            StopEditorSimulation();
#endif
        }

#if UNITY_EDITOR
        private void OnDestroy()
        {
            StopEditorSimulation();
        }
#endif

        private void Start()
        {
            CacheRestPoses();
            InitializeLegs();
        }

        private void EnsureEditorInitialized()
        {
            if (_bodyRoot != null && !_defaultBodyTransformCached)
            {
                _defaultBodyPosition = _bodyRoot.localPosition;
                _defaultBodyRotation = _bodyRoot.localRotation;
                _defaultBodyTransformCached = true;
            }

            if (_groundLayer.value == 0)
            {
                _groundLayer = LayerMask.GetMask("Default", "Ground", "Environment");
                if (_groundLayer.value == 0) _groundLayer = ~0;
            }

            if (_legs != null)
            {
                for (int i = 0; i < _legs.Length; i++)
                {
                    if (_legs[i] != null)
                    {
                        _legs[i].EnsureBuffers();
                    }
                }
            }

            if (_animationTimeScale <= 0f)
            {
                _animationTimeScale = 1.0f;
            }

            CacheRestPoses();
            InitializeLegs();
        }

        public void SetLegReferences(SplineLeg[] legs, Transform bodyRoot)
        {
            _legs = legs;
            _bodyRoot = bodyRoot;

            if (_bodyRoot != null)
            {
                _defaultBodyPosition = _bodyRoot.localPosition;
                _defaultBodyRotation = _bodyRoot.localRotation;
                _defaultBodyTransformCached = true;
            }

            _restPoseCached = false;
            CacheRestPoses(forceRecache: true);
        }

        private void CacheRestPoses(bool forceRecache = false)
        {
            if ((_restPoseCached && !forceRecache) || _legs == null) return;

            for (int i = 0; i < _legs.Length; i++)
            {
                if (_legs[i] != null)
                {
                    _legs[i].CacheRestPose(transform, forceRecache);
                }
            }
            _restPoseCached = true;
        }

        private void InitializeLegs()
        {
            if (_legs == null) return;

            for (int i = 0; i < _legs.Length; i++)
            {
                SplineLeg leg = _legs[i];
                if (leg == null || leg.RestAnchor == null) continue;

                Vector3 startFootPos = EvaluateIdealGroundPosition(leg, Vector3.zero);
                leg.Initialize(startFootPos, transform);
            }
        }

        private bool CheckGroundedFallback()
        {
            Vector3 origin = transform.position + Vector3.up * 0.15f;
            return Physics.Raycast(origin, Vector3.down, 0.40f, _groundLayer, QueryTriggerInteraction.Ignore);
        }

        private void LateUpdate()
        {
            // If remote proxy on network, let NetworkBones drive the bones without local solver conflict
            if (_controller != null && _controller.isSpawned && !_controller.isOwner)
            {
                return;
            }

            float dt = Time.deltaTime * _animationTimeScale;
            bool isGrounded = (_controller == null || _controller.IsGrounded || CheckGroundedFallback()) && !_isSimulatingAirborne;
            Vector3 linearVelocity = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
            Vector3 horizontalVelocity = new Vector3(linearVelocity.x, 0f, linearVelocity.z);

            if (_isSimulatingWalk && horizontalVelocity.sqrMagnitude < 0.01f)
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f) fwd.Normalize();
                horizontalVelocity = fwd * _simulatedSpeed;
            }

            if (dt > 0.000001f)
            {
                StepSimulation(horizontalVelocity, isGrounded, dt);
            }
        }

        private void StepSimulation(Vector3 horizontalVelocity, bool isGrounded, float dt)
        {
            UpdateLandingAndSquash(isGrounded, dt);

            if (_isKicking)
            {
                UpdateKickSequence(dt);
            }
            else if (!isGrounded)
            {
                UpdateAirbornePose(dt);
            }
            else
            {
                UpdateGaitSequence(horizontalVelocity, dt);
            }

            UpdateBodyWobble(dt);
            SolveAllLegSplines();

            _wasGrounded = isGrounded;
        }

        private void UpdateLandingAndSquash(bool isGrounded, float dt)
        {
            if (!_wasGrounded && isGrounded)
            {
                _currentSquash = _landingSquashAmount;
                SnapAllPlantedLegsToGround();
            }

            if (_currentSquash > 0.001f)
            {
                _currentSquash = Mathf.MoveTowards(_currentSquash, 0f, _squashRecoverySpeed * dt);
            }
        }

        private void UpdateAirbornePose(float dt)
        {
            _interStepTimer = 0f;
            if (_activeSteppingIndex != -1)
            {
                if (_activeSteppingIndex < _legs.Length && _legs[_activeSteppingIndex] != null)
                {
                    _legs[_activeSteppingIndex].State = LegActionState.Airborne;
                }
                _activeSteppingIndex = -1;
            }

            for (int i = 0; i < _legs.Length; i++)
            {
                SplineLeg leg = _legs[i];
                if (leg == null || leg.RestAnchor == null) continue;

                leg.State = LegActionState.Airborne;

                Vector3 inwardDir = (transform.position - leg.RestAnchor.position);
                inwardDir.y = 0f;
                inwardDir = inwardDir.normalized * _airborneInwardOffset;

                Vector3 targetAirbornePos = leg.RestAnchor.position + Vector3.up * _airborneTuckHeight + inwardDir;
                leg.CurrentFootPos = Vector3.MoveTowards(leg.CurrentFootPos, targetAirbornePos, 14f * dt);
                leg.PlantedFootPos = leg.CurrentFootPos;
            }
        }

        private void UpdateGaitSequence(Vector3 horizontalVelocity, float dt)
        {
            float speed = horizontalVelocity.magnitude;
            bool isMoving = speed > 0.2f;
            bool isStationary = !Application.isPlaying || (_rigidbody == null || _rigidbody.linearVelocity.sqrMagnitude < 0.05f);

            float maxSpeed = _controller != null ? _controller.WalkSpeed : 5.0f;
            float speedRatio = Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.1f));

            float currentStepDuration = Mathf.Lerp(_stepDuration, _minStepDuration, speedRatio);
            float currentInterStepPause = Mathf.Lerp(_interStepPause, _minInterStepPause, speedRatio);

            // 1. Advance stepping leg with caricature curve & sudden slam
            if (_activeSteppingIndex != -1)
            {
                SplineLeg steppingLeg = _legs[_activeSteppingIndex];
                if (steppingLeg != null)
                {
                    steppingLeg.StepTimer += dt;
                    float progress = Mathf.Clamp01(steppingLeg.StepTimer / currentStepDuration);

                    Vector3 dynamicTarget = EvaluateIdealGroundPosition(steppingLeg, horizontalVelocity, currentStepDuration, currentInterStepPause);
                    steppingLeg.StepTargetFootPos = dynamicTarget;

                    bool isLeft = steppingLeg.ID == LegID.FrontLeft || steppingLeg.ID == LegID.BackLeft;
                    bool isFront = steppingLeg.ID == LegID.FrontLeft || steppingLeg.ID == LegID.FrontRight;
                    Vector3 outwardDir = isLeft ? -transform.right : transform.right;
                    Vector3 inwardDir = isLeft ? transform.right : -transform.right;

                    // Non-linear horizontal progress (coils back briefly, reaches destination by 0.85)
                    float horizProgress = _horizontalProgressCurve.Evaluate(progress);
                    Vector3 baseHoriz = Vector3.LerpUnclamped(steppingLeg.StepStartFootPos, steppingLeg.StepTargetFootPos, horizProgress);

                    // Lateral C-curve bowing (settles to 0 as foot approaches ground at 0.85)
                    float lateralProgress = Mathf.Clamp01(progress / 0.85f);
                    float lateralSample = Mathf.Sin(lateralProgress * Mathf.PI);

                    // Differentiated lane foot swing:
                    // Rear legs flare OUTWARD into the outer lane to clear the front legs.
                    // Front legs swing along inner track with inward bias to keep outer lane completely open.
                    Vector3 lateralOffset = isFront
                        ? inwardDir * (lateralSample * _frontFootInwardArc)
                        : outwardDir * (lateralSample * (_lateralSwayArc + _flankClearanceArc));

                    // Vertical height curve: energetic rise to peak (0.3), continuous descent, and sudden stomp down (0.85)
                    float heightSample = _stepHeightCurve.Evaluate(progress);
                    float currentY = Mathf.Lerp(steppingLeg.StepStartFootPos.y, steppingLeg.StepTargetFootPos.y, progress) + heightSample * _maxStepHeight;

                    Vector3 rawSwingPos = new Vector3(baseHoriz.x + lateralOffset.x, currentY, baseHoriz.z + lateralOffset.z);

                    // Dynamic collision avoidance with companion leg on same flank
                    Vector3 avoidanceOffset = CalculateDynamicAvoidanceOffset(_activeSteppingIndex, rawSwingPos, progress, out _currentAvoidanceHoseBulge);

                    steppingLeg.CurrentFootPos = rawSwingPos + avoidanceOffset;

                    if (progress >= 1f)
                    {
                        // SLAM: Hits ground abruptly!
                        steppingLeg.CurrentFootPos = steppingLeg.StepTargetFootPos;
                        steppingLeg.PlantedFootPos = steppingLeg.StepTargetFootPos;
                        steppingLeg.State = LegActionState.Planted;

                        // Mechanical stomp punch
                        _currentSquash = Mathf.Max(_currentSquash, _stompSquashPunch);
                        OnFootstepLanded?.Invoke(steppingLeg.ID, steppingLeg.PlantedFootPos, steppingLeg.GroundNormal);

                        _activeSteppingIndex = -1;
                        _currentAvoidanceHoseBulge = 0f;
                        _debugHasAvoidance = false;
                        _interStepTimer = currentInterStepPause; // Obligatory pause with ALL 4 legs planted before next step!
                        _nextGaitIndex = (_nextGaitIndex + 1) % _legs.Length;
                    }
                }
                else
                {
                    _activeSteppingIndex = -1;
                    _currentAvoidanceHoseBulge = 0f;
                    _debugHasAvoidance = false;
                }
            }

            // 2. Firmly maintain planted legs in world space with rear leash limit
            for (int i = 0; i < _legs.Length; i++)
            {
                if (i == _activeSteppingIndex) continue;

                SplineLeg leg = _legs[i];
                if (leg == null || leg.RestAnchor == null) continue;

                // Treadmill conveyor: if robot is stationary in world space (e.g. editor preview or standing still),
                // drift planted feet backward relative to body so walking in place cycles infinitely
                if (isStationary && isMoving)
                {
                    leg.PlantedFootPos -= horizontalVelocity * dt;
                }

                Vector3 anchorPos = leg.RestAnchor.position;
                Vector3 delta = leg.PlantedFootPos - anchorPos;
                delta.y = 0f; // Only horizontal displacement on the ground plane

                float distHorizontal = delta.magnitude;
                if (distHorizontal > _maxRearStanceDistance && distHorizontal > 0.0001f)
                {
                    // Leash planted foot so it never falls further behind than _maxRearStanceDistance
                    Vector3 clampedOffset = (delta / distHorizontal) * _maxRearStanceDistance;
                    leg.PlantedFootPos = new Vector3(anchorPos.x + clampedOffset.x, leg.PlantedFootPos.y, anchorPos.z + clampedOffset.z);
                }

                leg.CurrentFootPos = leg.PlantedFootPos;
            }

            // 3. Initiate step on next candidate leg ONLY after inter-step pause has elapsed (guarantees foot is planted first!)
            if (_activeSteppingIndex == -1)
            {
                if (_interStepTimer > 0f)
                {
                    _interStepTimer -= dt;
                    return;
                }

                Vector3 angularVel = _rigidbody != null ? _rigidbody.angularVelocity : Vector3.zero;
                bool isTurning = Mathf.Abs(angularVel.y) > 0.15f;
                bool hasLocomotionIntent = isMoving || isTurning;

                SplineLeg candidateLeg = _legs[_nextGaitIndex];
                if (candidateLeg != null && candidateLeg.RestAnchor != null)
                {
                    Vector3 idealTarget = EvaluateIdealGroundPosition(candidateLeg, horizontalVelocity, currentStepDuration, currentInterStepPause);
                    float distanceToIdealSqr = (candidateLeg.PlantedFootPos - idealTarget).sqrMagnitude;

                    if (distanceToIdealSqr > _stepDistanceThreshold * _stepDistanceThreshold || (hasLocomotionIntent && distanceToIdealSqr > 0.03f))
                    {
                        candidateLeg.State = LegActionState.Stepping;
                        candidateLeg.StepTimer = 0f;
                        candidateLeg.StepStartFootPos = candidateLeg.CurrentFootPos;
                        candidateLeg.StepTargetFootPos = idealTarget;
                        _activeSteppingIndex = _nextGaitIndex;
                    }
                    else
                    {
                        // Check if ANY other leg is over-extended and urgently needs a step
                        bool anyOtherLegNeedsStep = false;
                        for (int k = 0; k < _legs.Length; k++)
                        {
                            if (k == _nextGaitIndex || _legs[k] == null || _legs[k].RestAnchor == null) continue;
                            Vector3 otherTarget = EvaluateIdealGroundPosition(_legs[k], horizontalVelocity, currentStepDuration, currentInterStepPause);
                            if ((_legs[k].PlantedFootPos - otherTarget).sqrMagnitude > _stepDistanceThreshold * _stepDistanceThreshold)
                            {
                                anyOtherLegNeedsStep = true;
                                break;
                            }
                        }

                        if (hasLocomotionIntent || anyOtherLegNeedsStep)
                        {
                            _nextGaitIndex = (_nextGaitIndex + 1) % _legs.Length;
                        }
                    }
                }
            }
        }

        private int GetCompanionLegIndex(int legIndex)
        {
            if (_legs == null || legIndex < 0 || legIndex >= _legs.Length || _legs[legIndex] == null) return -1;
            LegID id = _legs[legIndex].ID;
            switch (id)
            {
                case LegID.FrontLeft:
                    return FindLegIndexByID(LegID.BackLeft);
                case LegID.BackLeft:
                    return FindLegIndexByID(LegID.FrontLeft);
                case LegID.FrontRight:
                    return FindLegIndexByID(LegID.BackRight);
                case LegID.BackRight:
                    return FindLegIndexByID(LegID.FrontRight);
                default:
                    return -1;
            }
        }

        private int FindLegIndexByID(LegID targetID)
        {
            if (_legs == null) return -1;
            for (int i = 0; i < _legs.Length; i++)
            {
                if (_legs[i] != null && _legs[i].ID == targetID) return i;
            }
            return -1;
        }

        private Vector3 CalculateDynamicAvoidanceOffset(int steppingIndex, Vector3 currentSwingFootPos, float progress, out float hoseBulge)
        {
            hoseBulge = 0f;
            int companionIndex = GetCompanionLegIndex(steppingIndex);
            if (companionIndex == -1 || companionIndex >= _legs.Length)
            {
                _debugHasAvoidance = false;
                return Vector3.zero;
            }

            SplineLeg companionLeg = _legs[companionIndex];
            if (companionLeg == null)
            {
                _debugHasAvoidance = false;
                return Vector3.zero;
            }

            // Obstacle is companion foot in world XZ space
            Vector3 obstaclePos = companionLeg.CurrentFootPos;
            Vector3 delta = currentSwingFootPos - obstaclePos;
            delta.y = 0f;
            float dist = delta.magnitude;

            if (dist >= _avoidanceRadius || dist < 0.0001f)
            {
                _debugHasAvoidance = false;
                return Vector3.zero;
            }

            // Hermite smooth factor (0 at boundary, 1 at full proximity)
            float u = Mathf.Clamp01(1f - (dist / Mathf.Max(_avoidanceRadius, 0.01f)));
            float smoothFactor = Mathf.SmoothStep(0f, 1f, u);

            // Flight envelope: zero at takeoff (0.0), maximum at mid-flight (0.42), zero at landing (0.85 to 1.0)
            float flightTimeNorm = Mathf.Clamp01(progress / 0.85f);
            float flightEnvelope = Mathf.Sin(flightTimeNorm * Mathf.PI);

            // Direction is strictly outward from robot body on lateral axis
            bool isLeft = _legs[steppingIndex].ID == LegID.FrontLeft || _legs[steppingIndex].ID == LegID.BackLeft;
            bool isFront = _legs[steppingIndex].ID == LegID.FrontLeft || _legs[steppingIndex].ID == LegID.FrontRight;
            Vector3 outwardDir = isLeft ? -transform.right : transform.right;
            Vector3 inwardDir = isLeft ? transform.right : -transform.right;
            Vector3 avoidDir = isFront ? inwardDir : outwardDir;

            float pushMagnitude = smoothFactor * _maxAvoidancePush * flightEnvelope;
            hoseBulge = smoothFactor * _avoidanceHoseBulge * flightEnvelope;

            Vector3 offset = avoidDir * pushMagnitude;

            _debugAvoidanceSteppingPos = currentSwingFootPos;
            _debugAvoidanceOffset = offset;
            _debugCompanionPos = obstaclePos;
            _debugHasAvoidance = pushMagnitude > 0.001f;

            return offset;
        }

        private void UpdateKickSequence(float dt)
        {
            if (_kickingLegIndex < 0 || _kickingLegIndex >= _legs.Length)
            {
                _isKicking = false;
                return;
            }

            SplineLeg kickingLeg = _legs[_kickingLegIndex];
            if (kickingLeg == null)
            {
                _isKicking = false;
                return;
            }

            _kickTimer += dt;
            float tWindup = _kickWindupDuration;
            float tThrust = tWindup + _kickThrustDuration;
            float tHold = tThrust + _kickHoldDuration;
            float tTotal = tHold + _kickReturnDuration;

            if (_kickTimer <= tWindup)
            {
                float p = Mathf.Clamp01(_kickTimer / tWindup);
                Vector3 windupPos = _kickStartPos - transform.forward * 0.4f + Vector3.up * 0.45f;
                kickingLeg.CurrentFootPos = Vector3.Lerp(_kickStartPos, windupPos, p);
            }
            else if (_kickTimer <= tThrust)
            {
                float p = Mathf.Clamp01((_kickTimer - tWindup) / _kickThrustDuration);
                Vector3 windupPos = _kickStartPos - transform.forward * 0.4f + Vector3.up * 0.45f;
                kickingLeg.CurrentFootPos = Vector3.Lerp(windupPos, _kickTargetPos, p);
            }
            else if (_kickTimer <= tHold)
            {
                kickingLeg.CurrentFootPos = _kickTargetPos;
            }
            else if (_kickTimer <= tTotal)
            {
                float p = Mathf.Clamp01((_kickTimer - tHold) / _kickReturnDuration);
                Vector3 returnGroundPos = EvaluateIdealGroundPosition(kickingLeg, Vector3.zero);
                kickingLeg.CurrentFootPos = Vector3.Lerp(_kickTargetPos, returnGroundPos, p);
            }
            else
            {
                Vector3 finalGroundPos = EvaluateIdealGroundPosition(kickingLeg, Vector3.zero);
                kickingLeg.Initialize(finalGroundPos, transform);
                _isKicking = false;
                _kickingLegIndex = -1;
            }

            for (int i = 0; i < _legs.Length; i++)
            {
                if (i == _kickingLegIndex) continue;
                if (_legs[i] != null)
                {
                    _legs[i].CurrentFootPos = _legs[i].PlantedFootPos;
                }
            }
        }

        private void HandleKicked()
        {
            if (_isKicking) return;

            _kickingLegIndex = _lastKickWasLeft ? 2 : 0;
            _lastKickWasLeft = !_lastKickWasLeft;

            SplineLeg kickingLeg = _legs[_kickingLegIndex];
            if (kickingLeg == null) return;

            _isKicking = true;
            _kickTimer = 0f;
            _interStepTimer = 0f;
            _kickStartPos = kickingLeg.CurrentFootPos;
            _kickTargetPos = transform.position + transform.forward * _kickReachDistance + Vector3.up * 0.4f;

            kickingLeg.State = LegActionState.Kicking;
        }

        private void UpdateBodyWobble(float dt)
        {
            if (_bodyRoot == null) return;

            Quaternion targetWobble = Quaternion.identity;

            if (_isKicking)
            {
                targetWobble = Quaternion.Euler(-8f, 0f, _kickingLegIndex == 0 ? 6f : -6f);
            }
            else if (_activeSteppingIndex != -1)
            {
                switch (_activeSteppingIndex)
                {
                    case 0:
                        targetWobble = Quaternion.Euler(-_wobblePitchAngle, 0f, _wobbleRollAngle);
                        break;
                    case 1:
                        targetWobble = Quaternion.Euler(_wobblePitchAngle, 0f, -_wobbleRollAngle);
                        break;
                    case 2:
                        targetWobble = Quaternion.Euler(-_wobblePitchAngle, 0f, -_wobbleRollAngle);
                        break;
                    case 3:
                        targetWobble = Quaternion.Euler(_wobblePitchAngle, 0f, _wobbleRollAngle);
                        break;
                }
            }

            // Ground slope adaptation: tilt body to match average ground normal of planted legs
            Vector3 avgNormal = Vector3.zero;
            int normalCount = 0;
            if (_legs != null)
            {
                for (int i = 0; i < _legs.Length; i++)
                {
                    if (_legs[i] != null && _legs[i].GroundNormal.sqrMagnitude > 0.1f)
                    {
                        avgNormal += _legs[i].GroundNormal;
                        normalCount++;
                    }
                }
            }

            if (normalCount > 0)
            {
                avgNormal /= normalCount;
                if (avgNormal.sqrMagnitude > 0.001f) avgNormal.Normalize();
                else avgNormal = Vector3.up;
            }
            else
            {
                avgNormal = Vector3.up;
            }

            Quaternion slopeOffset = Quaternion.FromToRotation(transform.up, avgNormal);
            Quaternion localSlopeOffset = Quaternion.Inverse(transform.rotation) * slopeOffset * transform.rotation;

            _currentWobbleRotation = Quaternion.Slerp(_currentWobbleRotation, targetWobble * localSlopeOffset, _wobbleSmoothSpeed * dt);
            _bodyRoot.localRotation = _defaultBodyRotation * _currentWobbleRotation;

            Vector3 targetLocalPos = _defaultBodyPosition - Vector3.up * _currentSquash;
            _bodyRoot.localPosition = targetLocalPos;
        }

        private void SolveAllLegSplines()
        {
            int companionOfActive = GetCompanionLegIndex(_activeSteppingIndex);

            for (int i = 0; i < _legs.Length; i++)
            {
                SplineLeg leg = _legs[i];
                if (leg == null || leg.Bones == null || leg.Bones.Length < 13 || leg.Bones[0] == null) continue;

                // Control Point 0: Hip socket position
                Vector3 p0 = leg.Bones[0].position;

                // Control Point 3: Foot position
                Vector3 p3 = leg.CurrentFootPos;

                Vector3 hipToFoot = p3 - p0;
                float dist = hipToFoot.magnitude;

                // Ensure minimum separation to prevent zero-distance degenerate tangent collapse
                if (dist < 0.05f)
                {
                    p3 = p0 + Vector3.down * 0.05f;
                    hipToFoot = p3 - p0;
                    dist = 0.05f;
                }

                // Clamp foot target if over-stretched beyond physical leg length
                float maxReach = leg.TotalChainLength > 0.1f ? leg.TotalChainLength : 0.85f;
                if (dist > maxReach * 0.98f && dist > 0.0001f)
                {
                    p3 = p0 + (hipToFoot / dist) * (maxReach * 0.98f);
                    hipToFoot = p3 - p0;
                    dist = maxReach * 0.98f;
                }

                // Direction vector of the leg:
                // Front legs ALWAYS project inward towards the chassis (+ slight forward bias)
                // Rear legs ALWAYS project outward into the outer flank (+ slight backward bias)
                bool isLeft = leg.ID == LegID.FrontLeft || leg.ID == LegID.BackLeft;
                bool isFront = leg.ID == LegID.FrontLeft || leg.ID == LegID.FrontRight;
                Vector3 sideDir = isLeft ? -transform.right : transform.right;
                Vector3 inwardDir = isLeft ? transform.right : -transform.right;
                Vector3 baseLateralDir = isFront
                    ? (inwardDir * 0.88f + transform.forward * 0.15f).normalized
                    : (sideDir * 0.82f - transform.forward * 0.28f).normalized;

                // Flight state of active stepping leg
                bool isStepping = (i == _activeSteppingIndex);
                float stepProgress = isStepping ? (leg.StepTimer / Mathf.Max(_stepDuration, 0.01f)) : 0f;
                float flightFactor = isStepping ? Mathf.Sin(Mathf.Clamp01(stepProgress / 0.85f) * Mathf.PI) : 0f;

                // Natural adaptive bow width: the more slack, the more the hose bows!
                float slack = Mathf.Max(0f, maxReach - dist);
                float naturalBow = Mathf.Clamp(Mathf.Sqrt(slack * maxReach * 0.45f) + 0.08f, 0.06f, _archOutwardWidth);

                Vector3 lateralVectorP1;
                Vector3 lateralVectorP2;

                if (isFront)
                {
                    // FRONT LEGS: PERMANENT INTERNAL CURVATURE (Never resets/flips to external!)
                    // Stance inward curvature adapts naturally with slack, bounded by _frontLegInwardFlex
                    float frontInwardWidth = Mathf.Max(_frontLegInwardFlex, Mathf.Sqrt(slack * maxReach * 0.35f) + 0.06f);

                    if (isStepping)
                    {
                        // In flight: smooth dynamic inward pulse as foot lifts and lands
                        frontInwardWidth += _frontLegStepInwardBoost * flightFactor;
                    }
                    else if (i == companionOfActive && companionOfActive != -1 && _activeSteppingIndex >= 0 && _activeSteppingIndex < _legs.Length)
                    {
                        // Planted while rear sister leg flies past: deepens inward tuck
                        SplineLeg activeLeg = _legs[_activeSteppingIndex];
                        float activeProg = activeLeg != null ? (activeLeg.StepTimer / Mathf.Max(_stepDuration, 0.01f)) : 0f;
                        float activeFlight = Mathf.Sin(Mathf.Clamp01(activeProg / 0.85f) * Mathf.PI);
                        frontInwardWidth += _companionHoseTuck * activeFlight;
                    }

                    lateralVectorP1 = baseLateralDir * frontInwardWidth;
                    lateralVectorP2 = baseLateralDir * (frontInwardWidth * 0.35f);
                }
                else
                {
                    // REAR LEGS: PERMANENT EXTERNAL CURVATURE (Monopolizes outer clearance lane)
                    if (isStepping)
                    {
                        float extraBulge = _currentAvoidanceHoseBulge;
                        float minSteppingBow = Mathf.Max(_archOutwardWidth, _steppingFlankBowWidth) * flightFactor;
                        float effectiveBowWidth = Mathf.Max(naturalBow + extraBulge, minSteppingBow);

                        Vector3 outwardVector = (flightFactor > 0.05f) ? sideDir : baseLateralDir;
                        float ankleRatio = Mathf.Lerp(0.30f, _ankleBowRatio, flightFactor);

                        lateralVectorP1 = outwardVector * effectiveBowWidth;
                        lateralVectorP2 = outwardVector * (effectiveBowWidth * ankleRatio);
                    }
                    else if (i == companionOfActive && companionOfActive != -1 && _activeSteppingIndex >= 0 && _activeSteppingIndex < _legs.Length)
                    {
                        // Rear leg planted while front leg steps: maintain stable stance
                        SplineLeg activeLeg = _legs[_activeSteppingIndex];
                        float activeProg = activeLeg != null ? (activeLeg.StepTimer / Mathf.Max(_stepDuration, 0.01f)) : 0f;
                        float activeFlight = Mathf.Sin(Mathf.Clamp01(activeProg / 0.85f) * Mathf.PI);
                        float effectiveBowWidth = Mathf.Max(0.06f, naturalBow - (_companionHoseTuck * activeFlight));

                        lateralVectorP1 = baseLateralDir * effectiveBowWidth;
                        lateralVectorP2 = baseLateralDir * (effectiveBowWidth * 0.30f);
                    }
                    else
                    {
                        // Normal planted rear stance
                        lateralVectorP1 = baseLateralDir * naturalBow;
                        lateralVectorP2 = baseLateralDir * (naturalBow * 0.30f);
                    }
                }

                // Control Point 1: Hip shoulder.
                // Exits hip going with lateral vector and DOWNWARD along the hip-to-foot vector!
                // NO upward spike! Zero Vector3.up added here!
                Vector3 p1 = p0 + lateralVectorP1 + (hipToFoot * 0.28f);

                // Control Point 2: Ankle arch.
                // Descends vertically into the foot on the ground with outward/inward clearance.
                Vector3 p2 = p3 + (Vector3.up * _ankleArchHeight) + lateralVectorP2;

                // Solve the 13 bones along the Cubic Bezier spline with arc-length parameterization!
                leg.SolveSplineHose(p0, p1, p2, p3, leg.GroundNormal, transform);
            }
        }

        private Vector3 EvaluateIdealGroundPosition(SplineLeg leg, Vector3 horizontalVelocity, float currentStepDuration = -1f, float currentInterStepPause = -1f)
        {
            if (currentStepDuration < 0f) currentStepDuration = _stepDuration;
            if (currentInterStepPause < 0f) currentInterStepPause = _interStepPause;

            Vector3 anchorPos = leg.RestAnchor != null ? leg.RestAnchor.position : transform.position;

            // Forward stride prediction:
            // Stance time while 3 other legs step: 3 * (currentStepDuration + currentInterStepPause)
            // Lead time = swing time to landing (currentStepDuration * 0.85f) + half stance time (0.5f * stanceDuration)
            float stanceDuration = 3f * (currentStepDuration + currentInterStepPause);
            float leadTime = (currentStepDuration * 0.85f) + (stanceDuration * 0.5f);

            Vector3 angularVel = _rigidbody != null ? _rigidbody.angularVelocity : Vector3.zero;
            Vector3 tangentialVel = Vector3.Cross(angularVel, anchorPos - transform.position);
            Vector3 totalVelocity = horizontalVelocity + new Vector3(tangentialVel.x, 0f, tangentialVel.z);

            Vector3 strideLead = totalVelocity * leadTime;
            float leadMag = strideLead.magnitude;
            if (leadMag > _maxForwardStride && leadMag > 0.0001f)
            {
                strideLead = (strideLead / leadMag) * _maxForwardStride;
            }

            Vector3 projectedPos = anchorPos + strideLead;
            Vector3 origin = projectedPos + Vector3.up * _raycastOriginHeight;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, leg.RayHits, _raycastDistance, _groundLayer, QueryTriggerInteraction.Ignore);

            Vector3 groundPoint = projectedPos;
            bool foundGround = false;

            for (int h = 0; h < hitCount; h++)
            {
                RaycastHit hit = leg.RayHits[h];
                if (hit.collider == null) continue;
                if (hit.collider.transform.root == transform.root) continue;

                groundPoint = hit.point;
                leg.GroundNormal = hit.normal;
                foundGround = true;
                break;
            }

            if (!foundGround)
            {
                // Fallback: If no floor collider detected (e.g. in Prefab Stage / editor preview or mid-air void),
                // ground feet on the character base contact plane (transform.position.y)
                groundPoint = new Vector3(projectedPos.x, transform.position.y, projectedPos.z);
                leg.GroundNormal = Vector3.up;
            }

            return groundPoint;
        }

        private void SnapAllPlantedLegsToGround()
        {
            for (int i = 0; i < _legs.Length; i++)
            {
                SplineLeg leg = _legs[i];
                if (leg == null || leg.RestAnchor == null) continue;

                Vector3 target = EvaluateIdealGroundPosition(leg, Vector3.zero);
                leg.PlantedFootPos = target;
                leg.CurrentFootPos = target;
                leg.State = LegActionState.Planted;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_legs == null) return;

            for (int i = 0; i < _legs.Length; i++)
            {
                SplineLeg leg = _legs[i];
                if (leg == null) continue;

                // Draw Spline curve
                Gizmos.color = leg.State == LegActionState.Stepping ? Color.green : Color.cyan;
                Vector3[] pts = leg.SplinePoints;
                if (pts != null && pts.Length == 13)
                {
                    for (int s = 0; s < 12; s++)
                    {
                        Gizmos.DrawLine(pts[s], pts[s + 1]);
                        Gizmos.DrawSphere(pts[s], 0.02f);
                    }
                    Gizmos.DrawSphere(pts[12], 0.04f);
                }

                if (leg.RestAnchor != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(leg.RestAnchor.position, 0.08f);
                }

                // Avoidance clearance bubble around each foot
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
                Gizmos.DrawWireSphere(leg.CurrentFootPos, _avoidanceRadius * 0.5f);
            }

            // Draw active dynamic avoidance vectors if occurring
            if (_debugHasAvoidance)
            {
                // Line connecting stepping foot and companion obstacle
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_debugAvoidanceSteppingPos, _debugCompanionPos);

                // Deflection push vector (Magenta)
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(_debugAvoidanceSteppingPos, _debugAvoidanceSteppingPos + _debugAvoidanceOffset);
                Gizmos.DrawSphere(_debugAvoidanceSteppingPos + _debugAvoidanceOffset, 0.035f);
            }
        }
#endif
    }
}
