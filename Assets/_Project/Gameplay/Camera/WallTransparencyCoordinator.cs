using Game.Gameplay.Player.Robot;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Camera
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración de Recorte")]
    [DeclareBoxGroup("Referencias de Jugadores")]
    public sealed class WallTransparencyCoordinator : MonoBehaviour
    {
        [Group("Configuración de Recorte")]
        [SerializeField] private float _defaultRadius = 2.2f;

        [Group("Configuración de Recorte")]
        [SerializeField] private float _softness = 0.8f;

        [Group("Configuración de Recorte")]
        [Range(0f, 1f)]
        [SerializeField] private float _minAlpha = 0.18f;

        [Group("Configuración de Recorte")]
        [SerializeField] private float _fusedRadius = 3.0f;

        [Group("Configuración de Recorte")]
        [SerializeField] private float _heightOffset = 0.85f;

        [Group("Configuración de Recorte")]
        [SerializeField] private float _transitionSpeed = 5f;

        [Group("Referencias de Jugadores")]
        [SerializeField] private Transform _player1Transform;

        [Group("Referencias de Jugadores")]
        [SerializeField] private Transform _player2Transform;

        [Group("Referencias de Jugadores")]
        [SerializeField] private RobotCoordinator _robotCoordinator;

        private int _player1PosId;
        private int _player2PosId;
        private int _cutoutParamsId;

        private float _currentRadius1;
        private float _currentRadius2;
        private Vector3 _currentPos1;
        private Vector3 _currentPos2;

        public void SetTargets(Transform player1, Transform player2)
        {
            _player1Transform = player1;
            _player2Transform = player2;
        }

        public void SetCoordinator(RobotCoordinator coordinator)
        {
            _robotCoordinator = coordinator;
        }

        private void Awake()
        {
            _player1PosId = Shader.PropertyToID("_Player1Pos");
            _player2PosId = Shader.PropertyToID("_Player2Pos");
            _cutoutParamsId = Shader.PropertyToID("_CutoutParams");

            if (_robotCoordinator == null)
            {
                _robotCoordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            ResolveTargets();
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(_player1PosId, Vector4.zero);
            Shader.SetGlobalVector(_player2PosId, Vector4.zero);
            Shader.SetGlobalVector(_cutoutParamsId, Vector4.zero);
        }

        private void LateUpdate()
        {
            ResolveTargets();

            bool isFused = _robotCoordinator != null && _robotCoordinator.IsFused;

            float targetRadius1 = 0f;
            float targetRadius2 = 0f;
            Vector3 targetPos1 = _currentPos1;
            Vector3 targetPos2 = _currentPos2;

            Vector3 offset = Vector3.up * _heightOffset;

            if (isFused)
            {
                if (_robotCoordinator != null && _robotCoordinator.Socket != null)
                {
                    targetPos1 = _robotCoordinator.Socket.transform.position + offset;
                }
                else if (_player1Transform != null)
                {
                    targetPos1 = _player1Transform.position + offset;
                }

                targetRadius1 = _fusedRadius;
                targetRadius2 = 0f;
            }
            else
            {
                if (_player1Transform != null)
                {
                    targetPos1 = _player1Transform.position + offset;
                    targetRadius1 = _defaultRadius;
                }

                if (_player2Transform != null)
                {
                    targetPos2 = _player2Transform.position + offset;
                    targetRadius2 = _defaultRadius;
                }
            }

            float dt = Time.deltaTime;
            _currentRadius1 = Mathf.MoveTowards(_currentRadius1, targetRadius1, _transitionSpeed * dt);
            _currentRadius2 = Mathf.MoveTowards(_currentRadius2, targetRadius2, _transitionSpeed * dt);

            _currentPos1 = targetPos1;
            _currentPos2 = targetPos2;

            Shader.SetGlobalVector(_player1PosId, new Vector4(_currentPos1.x, _currentPos1.y, _currentPos1.z, _currentRadius1));
            Shader.SetGlobalVector(_player2PosId, new Vector4(_currentPos2.x, _currentPos2.y, _currentPos2.z, _currentRadius2));
            Shader.SetGlobalVector(_cutoutParamsId, new Vector4(_softness, _minAlpha, 0f, 0f));
        }

        private void ResolveTargets()
        {
            if (_player1Transform != null && _player2Transform != null)
            {
                return;
            }

            if (_robotCoordinator == null)
            {
                _robotCoordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            if (_robotCoordinator != null)
            {
                if (_player1Transform == null && _robotCoordinator.Legs != null)
                {
                    _player1Transform = _robotCoordinator.Legs.transform;
                }

                if (_player2Transform == null && _robotCoordinator.Torso != null)
                {
                    _player2Transform = _robotCoordinator.Torso.transform;
                }
            }
        }
    }
}
