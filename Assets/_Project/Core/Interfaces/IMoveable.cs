using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IMoveable
    {
        Vector2 MoveInput { get; }
        bool IsGrounded { get; }
        bool IsSprinting { get; }
        void SetMoveInput(Vector2 input);
        void SetSprint(bool isSprinting);
        void Jump();
    }
}
