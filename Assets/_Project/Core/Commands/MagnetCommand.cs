using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct MagnetCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Magnet;
        public bool IsReliable => true;

        public readonly bool Active;

        public MagnetCommand(bool active)
        {
            Active = active;
        }

        public void Execute(PlayerContext context)
        {
            if (context.MagnetOperator != null)
            {
                context.MagnetOperator.SetMagnetActive(Active);
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Magnet, boolData: Active);
        }
    }
}
