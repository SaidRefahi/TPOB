using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IMagnetic
    {
        bool CanBeAttracted { get; }
        Transform Transform { get; }
        void ApplyMagneticForce(Vector3 magnetOrigin, float force, float deltaTime);
    }
}
