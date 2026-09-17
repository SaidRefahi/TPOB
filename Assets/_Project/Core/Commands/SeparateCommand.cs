using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct SeparateCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Separate;
        public bool IsReliable => true;

        public void Execute(PlayerContext context)
        {
            if (context.FusionOperator != null && context.FusionOperator.IsFused)
            {
                context.FusionOperator.RequestSeparation();
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Separate);
        }
    }
}
