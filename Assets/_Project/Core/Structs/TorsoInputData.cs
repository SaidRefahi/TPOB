using PurrNet.Packing;
using UnityEngine;

namespace Game.Core.Structs
{
    public struct TorsoInputData : IPackedAuto
    {
        public Vector2 MoveDirection;
        public Vector2 AimDirection;
        public bool GrabTriggered;
        public bool ThrowTriggered;
        public bool MagnetHeld;
        public bool InteractTriggered;

        public TorsoInputData(
            Vector2 moveDirection,
            Vector2 aimDirection,
            bool grabTriggered,
            bool throwTriggered,
            bool magnetHeld,
            bool interactTriggered)
        {
            MoveDirection = moveDirection;
            AimDirection = aimDirection;
            GrabTriggered = grabTriggered;
            ThrowTriggered = throwTriggered;
            MagnetHeld = magnetHeld;
            InteractTriggered = interactTriggered;
        }
    }
}
