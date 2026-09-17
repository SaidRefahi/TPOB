using System;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Rooms.Conditions
{
    [RequireComponent(typeof(Collider))]
    [DeclareBoxGroup("Condición de Zona")]
    public sealed class ZoneTriggerCondition : ConditionBase
    {
        [Group("Condición de Zona")]
        [SerializeField] private string _targetTag = "Player";

        [Group("Condición de Zona")]
        [SerializeField] private int _requiredCount = 1;

        [Group("Condición de Zona")]
        [ShowInInspector]
        private int _currentCount;

        public override event Action<bool> OnConditionChanged;

        public override bool IsSatisfied => _currentCount >= _requiredCount;

        private void OnTriggerEnter(Collider other)
        {
            if (string.IsNullOrEmpty(_targetTag) || other.CompareTag(_targetTag))
            {
                _currentCount++;
                OnConditionChanged?.Invoke(IsSatisfied);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (string.IsNullOrEmpty(_targetTag) || other.CompareTag(_targetTag))
            {
                _currentCount = Mathf.Max(0, _currentCount - 1);
                OnConditionChanged?.Invoke(IsSatisfied);
            }
        }

        public void Configure(string targetTag, int requiredCount = 1)
        {
            _targetTag = targetTag;
            _requiredCount = requiredCount;
        }
    }
}
