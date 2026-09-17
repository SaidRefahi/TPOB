using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct FuseCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Fuse;
        public bool IsReliable => true;

        public void Execute(PlayerContext context)
        {
            if (context.FusionOperator != null && context.FusionOperator.CanFuse)
            {
                context.FusionOperator.RequestFusion();
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Fuse);
        }
    }
}
