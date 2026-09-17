using Game.Core.Commands;
using Game.Core.Interfaces;
using Game.Core.Structs;
using UnityEngine;

namespace Game.Gameplay.Player.Commands
{
    /// <summary>
    /// Demonstrates acceptance criterion: A new gameplay ability created by implementing
    /// IPlayerCommand and registering its handler without modifying any network transport code.
    /// </summary>
    public readonly struct TestCustomAbilityCommand : IPlayerCommand
    {
        public const byte CustomCommandTypeId = 100;

        public byte CommandId => CustomCommandTypeId;
        public bool IsReliable => true;

        public readonly float BoostMultiplier;
        public readonly bool TriggerVfx;

        public TestCustomAbilityCommand(float boostMultiplier, bool triggerVfx = false)
        {
            BoostMultiplier = boostMultiplier;
            TriggerVfx = triggerVfx;
        }

        public void Execute(PlayerContext context)
        {
            if (context.Rigidbody != null)
            {
                context.Rigidbody.AddForce(Vector3.up * BoostMultiplier, ForceMode.Impulse);
            }
        }

        public PlayerCommandPacket ToPacket()
        {
            return new PlayerCommandPacket(CustomCommandTypeId, floatData: BoostMultiplier, boolData: TriggerVfx);
        }

        public static void RegisterSelf()
        {
            CommandInvoker.RegisterHandler(CustomCommandTypeId, HandleCustomPacket);
        }

        private static void HandleCustomPacket(in PlayerCommandPacket packet, PlayerContext context)
        {
            var command = new TestCustomAbilityCommand(packet.FloatData, packet.BoolData);
            command.Execute(context);
        }
    }
}
