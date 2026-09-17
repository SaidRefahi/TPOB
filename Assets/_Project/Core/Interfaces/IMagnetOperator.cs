using System;

namespace Game.Core.Interfaces
{
    public interface IMagnetOperator
    {
        bool IsMagnetActive { get; }
        void SetMagnetActive(bool active);
        event Action<bool> OnMagnetStateChanged;
    }
}
