using Game.Core.Interfaces;
using Game.Core.Structs;
using UnityEngine;

namespace Game.Core.Commands
{
    public readonly struct MoveCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Move;
        public bool IsReliable => false;

        public readonly Vector2 Direction;
        public readonly bool Sprint;

        public MoveCommand(Vector2 direction, bool sprint = false)
        {
            Direction = direction;
            Sprint = sprint;
        }

        public void Execute(PlayerContext context)
        {
            if (context.Moveable != null)
            {
                context.Moveable.SetMoveInput(Direction);
                context.Moveable.SetSprint(Sprint);
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Move, Direction, boolData: Sprint);
        }
    }
}
