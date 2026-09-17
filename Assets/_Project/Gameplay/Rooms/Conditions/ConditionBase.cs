using System;
using Game.Core.Interfaces;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Rooms.Conditions
{
    [DeclareBoxGroup("Estado de Condición")]
    public abstract class ConditionBase : MonoBehaviour, ICompletionCondition
    {
        [Group("Estado de Condición")]
        [ShowInInspector]
        public abstract bool IsSatisfied { get; }

        public abstract event Action<bool> OnConditionChanged;
    }
}
