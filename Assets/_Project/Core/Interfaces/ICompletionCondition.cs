using System;

namespace Game.Core.Interfaces
{
    public interface ICompletionCondition
    {
        bool IsSatisfied { get; }
        event Action<bool> OnConditionChanged;
    }
}
