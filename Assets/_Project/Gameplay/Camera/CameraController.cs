using Game.Core.Events;
using Game.Core.Interfaces;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Camera
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Cinemachine 3.x")]
    [DeclareBoxGroup("Configuración de Encuadre")]
    [DeclareBoxGroup("Impulsos")]
    [DeclareBoxGroup("Acciones")]
    public sealed class CameraController : MonoBehaviour, ICameraCoordinator
    {
        [Group("Cinemachine 3.x")]
        [SerializeField] private CinemachineCamera _cinemachineCamera;

        [Group("Cinemachine 3.x")]
        [SerializeField] private CinemachineGroupFraming _groupFraming;

        [Group("Cinemachine 3.x")]
        [SerializeField] private TargetGroupCoordinator _targetCoordinator;

        [Group("Impulsos")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Group("Configuración de Encuadre")]
        [SerializeField] private float _separatedFramingSize = 0.52f;

        [Group("Configuración de Encuadre")]
        [SerializeField] private float _fusedFramingSize = 0.42f;

        [Group("Configuración de Encuadre")]
        [SerializeField] private float _framingTransitionSpeed = 2.5f;

        private IGameEventBus _eventBus;
        private IRobotCoordinator _robotCoordinator;
        private bool _isFused;
        private float _currentFramingSize = 0.52f;

        public bool IsFused => _isFused;
        public CinemachineCamera CinemachineCamera => _cinemachineCamera;
        public TargetGroupCoordinator TargetCoordinator => _targetCoordinator;

        [Inject]
        public void Construct(IGameEventBus eventBus = null, IRobotCoordinator robotCoordinator = null)
        {
            _eventBus = eventBus;
            _robotCoordinator = robotCoordinator;
        }

        private void Awake()
        {
            if (_cinemachineCamera == null)
            {
                _cinemachineCamera = GetComponentInChildren<CinemachineCamera>();
            }

            if (_groupFraming == null && _cinemachineCamera != null)
            {
                _groupFraming = _cinemachineCamera.GetComponent<CinemachineGroupFraming>();
            }

            if (_targetCoordinator == null)
            {
                _targetCoordinator = GetComponentInChildren<TargetGroupCoordinator>();
            }

            if (_impulseSource == null)
            {
                _impulseSource = GetComponentInChildren<CinemachineImpulseSource>();
            }

            _currentFramingSize = _separatedFramingSize;
            if (_groupFraming != null)
            {
                _groupFraming.FramingSize = _currentFramingSize;
            }
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe<RobotFusedEvent>(OnRobotFused);
                _eventBus.Subscribe<RobotSeparatedEvent>(OnRobotSeparated);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<RobotFusedEvent>(OnRobotFused);
                _eventBus.Unsubscribe<RobotSeparatedEvent>(OnRobotSeparated);
            }
        }

        private void Start()
        {
            if (_robotCoordinator != null)
            {
                SetFused(_robotCoordinator.IsFused);
            }
        }

        private void Update()
        {
            if (_groupFraming == null)
            {
                return;
            }

            float targetFraming = _isFused ? _fusedFramingSize : _separatedFramingSize;
            if (Mathf.Abs(_currentFramingSize - targetFraming) > 0.001f)
            {
                _currentFramingSize = Mathf.MoveTowards(_currentFramingSize, targetFraming, _framingTransitionSpeed * Time.deltaTime);
                _groupFraming.FramingSize = _currentFramingSize;
            }
        }

        public void RegisterTargets(Transform legs, Transform torso)
        {
            if (_targetCoordinator != null)
            {
                _targetCoordinator.SetTargets(legs, torso);
            }
        }

        public void SetFused(bool isFused)
        {
            _isFused = isFused;

            if (_targetCoordinator != null)
            {
                _targetCoordinator.SetFused(isFused);
            }
        }

        public void TriggerImpulse(Vector3 velocity, float force = 1f)
        {
            if (_impulseSource != null)
            {
                _impulseSource.GenerateImpulse(velocity * force);
            }
        }

        private void OnRobotFused(RobotFusedEvent evt)
        {
            SetFused(true);
        }

        private void OnRobotSeparated(RobotSeparatedEvent evt)
        {
            SetFused(false);
        }

        [Group("Acciones")]
        [Button(ButtonSizes.Medium, "Simular Fusión (Cámara)")]
        public void SimulateFusion()
        {
            SetFused(true);
        }

        [Group("Acciones")]
        [Button(ButtonSizes.Medium, "Simular Separación (Cámara)")]
        public void SimulateSeparation()
        {
            SetFused(false);
        }

        [Group("Acciones")]
        [Button(ButtonSizes.Medium, "Test Impulse")]
        public void TestImpulse()
        {
            TriggerImpulse(Vector3.down, 1.2f);
        }
    }
}
