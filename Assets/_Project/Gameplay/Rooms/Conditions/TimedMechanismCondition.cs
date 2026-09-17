using System;
using Game.Core.Interfaces;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Rooms.Conditions
{
    [DeclareBoxGroup("Configuración Sincronizada")]
    public sealed class TimedMechanismCondition : ConditionBase
    {
        [Group("Configuración Sincronizada")]
        [SerializeField] private ConditionBase[] _conditions;

        [Group("Configuración Sincronizada")]
        [SerializeField] private float _timeWindow = 3f;

        [Group("Configuración Sincronizada")]
        [ShowInInspector, ReadOnly]
        public override bool IsSatisfied => EvaluateSatisfaction();

        public override event Action<bool> OnConditionChanged;

        private float[] _activationTimes;
        private bool _lastSatisfiedState;

        private void Awake()
        {
            if (_conditions != null && _conditions.Length > 0)
            {
                _activationTimes = new float[_conditions.Length];
                for (int i = 0; i < _activationTimes.Length; i++)
                {
                    _activationTimes[i] = -999f;
                }
            }
        }

        private void OnEnable()
        {
            if (_conditions == null) return;

            for (int i = 0; i < _conditions.Length; i++)
            {
                if (_conditions[i] != null)
                {
                    int index = i;
                    _conditions[i].OnConditionChanged += _ => HandleSubConditionChanged(index);
                }
            }
        }

        private void OnDisable()
        {
            if (_conditions == null) return;

            for (int i = 0; i < _conditions.Length; i++)
            {
                if (_conditions[i] != null)
                {
                    _conditions[i].OnConditionChanged -= _ => { };
                }
            }
        }

        private void HandleSubConditionChanged(int index)
        {
            if (index >= 0 && index < _activationTimes.Length)
            {
                if (_conditions[index].IsSatisfied)
                {
                    _activationTimes[index] = Time.time;
                }
                else
                {
                    _activationTimes[index] = -999f;
                }
            }

            bool satisfied = EvaluateSatisfaction();
            if (satisfied != _lastSatisfiedState)
            {
                _lastSatisfiedState = satisfied;
                OnConditionChanged?.Invoke(satisfied);
            }
        }

        private bool EvaluateSatisfaction()
        {
            if (_conditions == null || _conditions.Length == 0) return false;

            float now = Time.time;
            for (int i = 0; i < _conditions.Length; i++)
            {
                if (_conditions[i] == null || !_conditions[i].IsSatisfied)
                {
                    return false;
                }

                if (_timeWindow > 0f && (now - _activationTimes[i]) > _timeWindow)
                {
                    return false;
                }
            }

            return true;
        }

        public void Configure(ConditionBase[] conditions, float timeWindow = 3f)
        {
            _conditions = conditions;
            _timeWindow = timeWindow;
            _activationTimes = new float[conditions.Length];
            for (int i = 0; i < _activationTimes.Length; i++)
            {
                _activationTimes[i] = -999f;
            }
        }
    }
}
