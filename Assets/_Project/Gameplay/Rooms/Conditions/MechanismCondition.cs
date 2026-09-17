using System;
using Game.Core.Interfaces;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Rooms.Conditions
{
    [DeclareBoxGroup("Condición de Mecanismo")]
    public sealed class MechanismCondition : ConditionBase
    {
        [Group("Condición de Mecanismo")]
        [SerializeField] private MonoBehaviour _mechanismTarget;

        [Group("Condición de Mecanismo")]
        [SerializeField] private bool _requiredState = true;

        private IInteractableMechanism _mechanism;

        public override event Action<bool> OnConditionChanged;

        public override bool IsSatisfied => _mechanism != null && _mechanism.IsActivated == _requiredState;

        private void Awake()
        {
            ResolveMechanism();
        }

        private void OnEnable()
        {
            if (_mechanism == null)
            {
                ResolveMechanism();
            }

            if (_mechanism != null)
            {
                _mechanism.OnStateChanged += HandleMechanismStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_mechanism != null)
            {
                _mechanism.OnStateChanged -= HandleMechanismStateChanged;
            }
        }

        private void ResolveMechanism()
        {
            if (_mechanismTarget != null)
            {
                _mechanism = _mechanismTarget as IInteractableMechanism;
                if (_mechanism == null)
                {
                    _mechanism = _mechanismTarget.GetComponent<IInteractableMechanism>();
                }
            }
        }

        private void HandleMechanismStateChanged(bool _)
        {
            OnConditionChanged?.Invoke(IsSatisfied);
        }

        public void Configure(MonoBehaviour mechanismTarget, bool requiredState = true)
        {
            _mechanismTarget = mechanismTarget;
            _requiredState = requiredState;
            ResolveMechanism();
        }
    }
}
