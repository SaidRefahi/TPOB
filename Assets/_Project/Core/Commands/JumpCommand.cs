using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct JumpCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Jump;
        public bool IsReliable => true;

        public void Execute(PlayerContext context)
        {
            if (context.Moveable != null)
            {
                context.Moveable.Jump();
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Jump);
        }
    }
}
