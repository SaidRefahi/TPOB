using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IClimber
    {
        bool IsClimbing { get; }
        void Climb(Vector2 direction);
        void StopClimbing();
    }
}
