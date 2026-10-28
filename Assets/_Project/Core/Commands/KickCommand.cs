using Game.Core.Interfaces;
using Game.Core.Structs;

namespace Game.Core.Commands
{
    public readonly struct KickCommand : IPlayerCommand
    {
        public byte CommandId => PlayerCommandType.Kick;
        public bool IsReliable => true;
        private readonly float _chargeRatio;

        public KickCommand(float chargeRatio = 1f)
        {
            _chargeRatio = chargeRatio;
        }

        public void Execute(PlayerContext context)
        {
            if (context.Kicker != null && context.Kicker.CanKick)
            {
                context.Kicker.Kick(_chargeRatio);
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(PlayerCommandType.Kick, floatData: _chargeRatio);
        }
    }
}
