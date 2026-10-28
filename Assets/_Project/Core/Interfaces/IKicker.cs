using System;

namespace Game.Core.Interfaces
{
    public interface IKicker
    {
        bool CanKick { get; }
        void Kick();
        void Kick(float chargeRatio);
        event Action OnKicked;
    }
}
