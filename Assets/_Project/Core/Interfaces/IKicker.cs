using System;

namespace Game.Core.Interfaces
{
    public interface IKicker
    {
        bool CanKick { get; }
        void Kick();
        event Action OnKicked;
    }
}
