using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Gameplay.Interactables;
using Game.Gameplay.Rooms.Conditions;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración del Evaluador")]
    [DeclareBoxGroup("Estado del Árbol")]
    public sealed class RoomCompletion : NetworkBehaviour
    {
        [Group("Configuración del Evaluador")]
        [SerializeField] private ConditionBase _rootCondition;

        [Group("Configuración del Evaluador")]
        [SerializeField] private SlidingDoor _exitDoor;

        [Group("Configuración del Evaluador")]
        [SerializeField] private bool _autoCompleteRoom = true;

        [Group("Configuración del Evaluador")]
        [SerializeField] private RoomController _roomController;

        [Group("Estado del Árbol")]
        [ShowInInspector]
        public bool IsSatisfied => _rootCondition != null && _rootCondition.IsSatisfied;

        private IRoomController _injectedRoomController;

        [Inject]
        public void Construct(IRoomController roomController = null)
        {
            _injectedRoomController = roomController;
        }

        private void Awake()
        {
            if (_roomController == null)
            {
                _roomController = GetComponent<RoomController>();
                if (_roomController == null)
                {
                    _roomController = GetComponentInParent<RoomController>();
                }
            }
        }

        private void OnEnable()
        {
            if (_rootCondition != null)
            {
                _rootCondition.OnConditionChanged += HandleConditionChanged;
            }

            EvaluateConditions();
        }

        private void OnDisable()
        {
            if (_rootCondition != null)
            {
                _rootCondition.OnConditionChanged -= HandleConditionChanged;
            }
        }

        private void HandleConditionChanged(bool _)
        {
            EvaluateConditions();
        }

        public void EvaluateConditions()
        {
            if (_rootCondition == null) return;

            if (_rootCondition.IsSatisfied)
            {
                OnPuzzleSolved();
            }
        }

        private void OnPuzzleSolved()
        {
            bool canSimulate = !isSpawned || isServer;
            if (!canSimulate) return;

            if (_exitDoor != null && !_exitDoor.IsOpened)
            {
                _exitDoor.OpenDoor();
            }

            if (_autoCompleteRoom)
            {
                var controller = _injectedRoomController != null ? _injectedRoomController : _roomController;
                if (controller != null && controller.CurrentState != RoomState.Completed)
                {
                    controller.CompleteRoom();
                }
            }
        }

        public void Configure(ConditionBase rootCondition, SlidingDoor exitDoor, RoomController roomController = null)
        {
            if (_rootCondition != null)
            {
                _rootCondition.OnConditionChanged -= HandleConditionChanged;
            }

            _rootCondition = rootCondition;
            _exitDoor = exitDoor;
            if (roomController != null) _roomController = roomController;

            if (_rootCondition != null && enabled)
            {
                _rootCondition.OnConditionChanged += HandleConditionChanged;
            }
        }

        [Button("Evaluar Condiciones")]
        private void DebugEvaluate() => EvaluateConditions();

        [Button("Forzar Abrir Salida")]
        private void DebugForceOpen()
        {
            if (_exitDoor != null) _exitDoor.OpenDoor();
            var controller = _injectedRoomController != null ? _injectedRoomController : _roomController;
            controller?.CompleteRoom();
        }
    }
}
