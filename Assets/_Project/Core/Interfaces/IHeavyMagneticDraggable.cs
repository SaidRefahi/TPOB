using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IHeavyMagneticDraggable : IMagnetic
    {
        bool IsMagnetLocked { get; }
        float OpenProgress { get; }
        void LockMagnet(Transform operatorTransform);
        void UnlockMagnet();
        void ApplyDragMotion(Vector3 displacement);
    }
}
