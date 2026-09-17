using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface ICameraCoordinator
    {
        bool IsFused { get; }
        void RegisterTargets(Transform legs, Transform torso);
        void SetFused(bool isFused);
        void TriggerImpulse(Vector3 velocity, float force = 1f);
    }
}
