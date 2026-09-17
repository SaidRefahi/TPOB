using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct KickCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Kick;
        public bool IsReliable => true;

        public void Execute(PlayerContext context)
        {
            if (context.Kicker != null && context.Kicker.CanKick)
            {
                context.Kicker.Kick();
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Kick);
        }
    }
}
