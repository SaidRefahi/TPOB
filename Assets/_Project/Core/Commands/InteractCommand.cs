using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct InteractCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Interact;
        public bool IsReliable => true;

        public void Execute(PlayerContext context)
        {
            if (context.InteractOperator != null)
            {
                context.InteractOperator.TriggerInteract();
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Interact);
        }
    }
}
