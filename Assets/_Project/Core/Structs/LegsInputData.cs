using PurrNet.Packing;
using UnityEngine;

namespace Game.Core.Structs
{
    public struct LegsInputData : IPackedAuto
    {
        public Vector2 MoveDirection;
        public bool JumpTriggered;
        public bool KickTriggered;
        public bool SprintHeld;

        public LegsInputData(Vector2 moveDirection, bool jumpTriggered, bool kickTriggered, bool sprintHeld)
        {
            MoveDirection = moveDirection;
            JumpTriggered = jumpTriggered;
            KickTriggered = kickTriggered;
            SprintHeld = sprintHeld;
        }
    }
}
