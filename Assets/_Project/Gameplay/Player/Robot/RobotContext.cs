using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
using UnityEngine;

namespace Game.Gameplay.Player.Robot
{
    public sealed class RobotContext
    {
        public LegsController Legs { get; }
        public TorsoController Torso { get; }
        public FusionSocket Socket { get; }
        public Rigidbody LegsRigidbody { get; }
        public Rigidbody TorsoRigidbody { get; }
        public float LegsBaseMass { get; }
        public float TorsoBaseMass { get; }
        public bool IsServer { get; }

        public RobotContext(
            LegsController legs,
            TorsoController torso,
            FusionSocket socket,
            Rigidbody legsRigidbody,
            Rigidbody torsoRigidbody,
            float legsBaseMass,
            float torsoBaseMass,
            bool isServer)
        {
            Legs = legs;
            Torso = torso;
            Socket = socket;
            LegsRigidbody = legsRigidbody;
            TorsoRigidbody = torsoRigidbody;
            LegsBaseMass = legsBaseMass;
            TorsoBaseMass = torsoBaseMass;
            IsServer = isServer;
        }
    }
}
