using System;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Rooms.Conditions
{
    [DeclareBoxGroup("Composición OR")]
    public sealed class OrCondition : ConditionBase
    {
        [Group("Composición OR")]
        [SerializeField] private ConditionBase[] _conditions;

        public override event Action<bool> OnConditionChanged;

        public override bool IsSatisfied
        {
            get
            {
                if (_conditions == null || _conditions.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < _conditions.Length; i++)
                {
                    ConditionBase condition = _conditions[i];
                    if (condition != null && condition.IsSatisfied)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void OnEnable()
        {
            if (_conditions == null) return;

            for (int i = 0; i < _conditions.Length; i++)
            {
                ConditionBase condition = _conditions[i];
                if (condition != null)
                {
                    condition.OnConditionChanged += HandleChildConditionChanged;
                }
            }
        }

        private void OnDisable()
        {
            if (_conditions == null) return;

            for (int i = 0; i < _conditions.Length; i++)
            {
                ConditionBase condition = _conditions[i];
                if (condition != null)
                {
                    condition.OnConditionChanged -= HandleChildConditionChanged;
                }
            }
        }

        private void HandleChildConditionChanged(bool _)
        {
            OnConditionChanged?.Invoke(IsSatisfied);
        }

        public void SetConditions(ConditionBase[] conditions)
        {
            _conditions = conditions;
        }
    }
}
