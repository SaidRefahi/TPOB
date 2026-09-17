using System;

namespace Game.Core.Interfaces
{
    public interface IThrower
    {
        float ThrowForce { get; }
        void Throw();
        event Action OnThrow;
    }
}
