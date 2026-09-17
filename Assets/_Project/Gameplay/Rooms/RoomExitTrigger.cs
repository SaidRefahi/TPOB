using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Gameplay.Interactables;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Player.Torso;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    [DeclareBoxGroup("Configuración de Salida")]
    [DeclareBoxGroup("Feedback Visual")]
    [DeclareBoxGroup("Estado")]
    public sealed class RoomExitTrigger : NetworkBehaviour
    {
        [Group("Configuración de Salida")]
        [SerializeField] private RoomController _roomController;

        [Group("Configuración de Salida")]
        [SerializeField] private SlidingDoor _exitDoor;

        [Group("Configuración de Salida")]
        [SerializeField] private bool _requireDoorOpened = true;

        [Group("Feedback Visual")]
        [SerializeField] private Renderer _gatewayIndicator;

        [Group("Feedback Visual")]
        [SerializeField] private Color _lockedColor = new Color(0.8f, 0.2f, 0.2f);

        [Group("Feedback Visual")]
        [SerializeField] private Color _readyColor = new Color(0.1f, 0.7f, 1f);

        [Group("Feedback Visual")]
        [SerializeField] private Color _activeTransitionColor = new Color(0.2f, 1f, 0.3f);

        [Group("Estado")]
        [ShowInInspector, ReadOnly]
        private bool _isTransitioning;

        [Group("Estado")]
        [ShowInInspector, ReadOnly]
        private bool _hasLegs;

        [Group("Estado")]
        [ShowInInspector, ReadOnly]
        private bool _hasTorso;

        private ILevelManager _levelManager;
        private IRobotCoordinator _robotCoordinator;
        private IPlayerRegistry _playerRegistry;
        private BoxCollider _triggerCollider;
        private MaterialPropertyBlock _propBlock;
        private CancellationTokenSource _cts;

        private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorPropId = Shader.PropertyToID("_EmissionColor");

        [Inject]
        public void Construct(ILevelManager levelManager = null, IRobotCoordinator robotCoordinator = null)
        {
            _levelManager = levelManager;
            _robotCoordinator = robotCoordinator;
        }

        private void Awake()
        {
            _triggerCollider = GetComponent<BoxCollider>();
            _triggerCollider.isTrigger = true;
            _propBlock = new MaterialPropertyBlock();
            _cts = new CancellationTokenSource();

            if (_roomController == null)
            {
                _roomController = GetComponentInParent<RoomController>();
                if (_roomController == null) _roomController = FindFirstObjectByType<RoomController>();
            }

            if (_exitDoor == null)
            {
                _exitDoor = GetComponentInParent<SlidingDoor>();
                if (_exitDoor == null) _exitDoor = FindFirstObjectByType<SlidingDoor>();
            }

            UpdateVisualState(_lockedColor);
        }

        protected override void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            base.OnDestroy();
        }

        private void Start()
        {
            if (_levelManager == null)
            {
                _levelManager = FindFirstObjectByType<Game.Network.Services.LevelManager>();
            }

            if (_robotCoordinator == null)
            {
                _robotCoordinator = FindFirstObjectByType<RobotCoordinator>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            EvaluatePlayer(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            EvaluatePlayer(other, false);
        }

        private void ResolvePlayerRegistry()
        {
            if (_playerRegistry != null) return;
            var scopes = UnityEngine.Object.FindObjectsByType<VContainer.Unity.LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] != null && scopes[i].Container != null)
                {
                    try
                    {
                        _playerRegistry = scopes[i].Container.Resolve<IPlayerRegistry>();
                        if (_playerRegistry != null) break;
                    }
                    catch { }
                }
            }
        }

        private void EvaluatePlayer(Collider col, bool entered)
        {
            if (_isTransitioning) return;

            if (_robotCoordinator == null)
            {
                _robotCoordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            bool isFused = _robotCoordinator != null && _robotCoordinator.IsFused;

            bool isLegs = col.GetComponent<LegsController>() != null || col.GetComponentInParent<LegsController>() != null;
            bool isTorso = col.GetComponent<TorsoController>() != null || col.GetComponentInParent<TorsoController>() != null;

            if (isLegs)
            {
                _hasLegs = entered;
                if (isFused) _hasTorso = entered;
            }

            if (isTorso)
            {
                _hasTorso = entered;
                if (isFused) _hasLegs = entered;
            }

            CheckAdvanceCondition();
        }

        private void CheckAdvanceCondition()
        {
            if (_isTransitioning) return;

            if (_roomController == null)
            {
                _roomController = FindFirstObjectByType<RoomController>();
            }

            if (_exitDoor == null)
            {
                _exitDoor = FindFirstObjectByType<SlidingDoor>();
            }

            if (_levelManager == null)
            {
                _levelManager = FindFirstObjectByType<Game.Network.Services.LevelManager>();
            }

            if (_robotCoordinator == null)
            {
                _robotCoordinator = FindFirstObjectByType<RobotCoordinator>();
            }

            bool isRoomCompleted = _roomController == null || _roomController.CurrentState == RoomState.Completed;
            bool isDoorOpen = !_requireDoorOpened || (_exitDoor != null && _exitDoor.IsOpened);

            if (!isRoomCompleted && !isDoorOpen)
            {
                UpdateVisualState(_lockedColor);
                return;
            }

            UpdateVisualState(_readyColor);

            ResolvePlayerRegistry();
            bool isSinglePlayer = _playerRegistry == null || _playerRegistry.ConnectedPlayers.Count <= 1;

            bool isFused = _robotCoordinator != null && _robotCoordinator.IsFused;
            bool bothPresent = isFused 
                ? (_hasLegs || _hasTorso) 
                : (isSinglePlayer ? (_hasLegs || _hasTorso) : (_hasLegs && _hasTorso));

            if (bothPresent)
            {
                TriggerAdvance();
            }
        }

        private void TriggerAdvance()
        {
            bool canExecute = !isSpawned || isServer;
            if (!canExecute) return;

            _isTransitioning = true;
            UpdateVisualState(_activeTransitionColor);
            Debug.Log("<color=green>[RoomExitTrigger] ¡Condición de salida cumplida! Avanzando a la siguiente sala...</color>");

            if (_levelManager == null)
            {
                _levelManager = FindFirstObjectByType<Game.Network.Services.LevelManager>();
            }

            if (_levelManager != null)
            {
                _levelManager.AdvanceToNextRoomAsync().Forget();
            }
            else
            {
                Debug.LogWarning("[RoomExitTrigger] No se encontró ILevelManager para avanzar.");
            }
        }

        private void UpdateVisualState(Color color)
        {
            if (_gatewayIndicator == null) return;

            _gatewayIndicator.GetPropertyBlock(_propBlock);
            _propBlock.SetColor(BaseColorPropId, color);
            _propBlock.SetColor(ColorPropId, color);
            _propBlock.SetColor(EmissionColorPropId, color * 1.5f);
            _gatewayIndicator.SetPropertyBlock(_propBlock);
        }

        public void Configure(RoomController roomController, SlidingDoor exitDoor, Renderer indicator = null)
        {
            _roomController = roomController;
            _exitDoor = exitDoor;
            if (indicator != null) _gatewayIndicator = indicator;
        }

        [Button("Forzar Avance de Sala")]
        private void DebugForceAdvance()
        {
            TriggerAdvance();
        }
    }
}
