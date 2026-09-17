using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IGrabbable
    {
        bool IsGrabbed { get; }
        Transform Transform { get; }
        void OnGrabbed(Transform holdSocket);
        void OnReleased(Vector3 throwVelocity);
    }
}
