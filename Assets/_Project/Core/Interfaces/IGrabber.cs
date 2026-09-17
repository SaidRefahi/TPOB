using System;

namespace Game.Core.Interfaces
{
    public interface IGrabber
    {
        bool IsHoldingObject { get; }
        IGrabbable CurrentHeldObject { get; }
        void Grab(IGrabbable target);
        void ReleaseHeldObject();
        void TriggerGrab();
        event Action<IGrabbable> OnObjectGrabbed;
        event Action<IGrabbable> OnObjectReleased;
    }
}
