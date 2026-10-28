using Game.Gameplay.Rooms.Conditions;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración del Conector")]
    public sealed class DoorMechanismConnector : MonoBehaviour
    {
        [Group("Configuración del Conector")]
        [SerializeField] private ConditionBase _triggerCondition;

        [Group("Configuración del Conector")]
        [SerializeField] private SlidingDoor _targetDoor;

        [Group("Configuración del Conector")]
        [SerializeField] private bool _invertSignal;

        public void Configure(ConditionBase condition, SlidingDoor door, bool invert = false)
        {
            _triggerCondition = condition;
            _targetDoor = door;
            _invertSignal = invert;
            EvaluateState();
        }

        private void OnEnable()
        {
            if (_triggerCondition != null)
            {
                _triggerCondition.OnConditionChanged += HandleConditionChanged;
            }
            EvaluateState();
        }

        private void OnDisable()
        {
            if (_triggerCondition != null)
            {
                _triggerCondition.OnConditionChanged -= HandleConditionChanged;
            }
        }

        private void HandleConditionChanged(bool _)
        {
            EvaluateState();
        }

        public void EvaluateState()
        {
            if (_triggerCondition == null || _targetDoor == null)
            {
                return;
            }

            bool isSatisfied = _triggerCondition.IsSatisfied;
            bool shouldOpen = _invertSignal ? !isSatisfied : isSatisfied;

            if (shouldOpen)
            {
                if (!_targetDoor.IsOpened)
                {
                    _targetDoor.OpenDoor();
                }
            }
            else
            {
                if (_targetDoor.IsOpened)
                {
                    _targetDoor.CloseDoor();
                }
            }
        }
    }
}
