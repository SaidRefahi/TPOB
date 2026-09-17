using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct GrabCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Grab;
        public bool IsReliable => true;

        public void Execute(PlayerContext context)
        {
            if (context.Grabber != null)
            {
                context.Grabber.TriggerGrab();
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Grab);
        }
    }
}
