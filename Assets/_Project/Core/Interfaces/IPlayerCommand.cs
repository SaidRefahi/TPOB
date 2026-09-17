using Game.Core.Commands;
using Game.Core.Structs;

namespace Game.Core.Interfaces
{
    public interface IPlayerCommand
    {
        byte CommandId { get; }
        bool IsReliable { get; }
        void Execute(PlayerContext context);
        PlayerCommandPacket ToPacket();
    }
}
