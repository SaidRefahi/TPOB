using System;
using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IInteractableMechanism
    {
        bool IsActivated { get; }
        void Toggle(GameObject user);
        event Action<bool> OnStateChanged;
    }
}
