using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct ThrowCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Throw;
        public bool IsReliable => true;

        public void Execute(PlayerContext context)
        {
            if (context.Thrower != null)
            {
                context.Thrower.Throw();
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Throw);
        }
    }
}
