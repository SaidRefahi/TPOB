using Game.Core.Interfaces;
using Game.Core.Structs;
using UnityEngine;

namespace Game.Core.Commands
{
    public readonly struct ClimbCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Climb;
        public bool IsReliable => false;

        public readonly Vector2 Direction;

        public ClimbCommand(Vector2 direction)
        {
            Direction = direction;
        }

        public void Execute(PlayerContext context)
        {
            if (context.Climber != null)
            {
                context.Climber.Climb(Direction);
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Climb, Direction);
        }
    }
}
