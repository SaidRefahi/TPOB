using UnityEngine;

namespace Game.Gameplay.Player.Robot
{
    public sealed class SeparationStrategy : IRobotStrategy
    {
        public bool IsFused => false;

        public void OnEnter(RobotContext context)
        {
            if (context == null || context.Torso == null || context.Legs == null)
            {
                return;
            }

            context.Torso.transform.SetParent(null, true);

            Vector3 offset = context.Socket != null ? context.Socket.SeparationOffset : new Vector3(0f, 0.25f, 1.5f);
            Vector3 targetPosition = context.Legs.transform.position
                + context.Legs.transform.forward * offset.z
                + Vector3.up * offset.y;

            context.Torso.transform.position = targetPosition;
            context.Torso.transform.rotation = context.Legs.transform.rotation;

            context.Torso.SetFused(false);
            context.Legs.SetFused(false, 0f);

            if (context.TorsoRigidbody != null)
            {
                bool canSimulate = !context.Torso.isSpawned || context.IsServer;
                context.TorsoRigidbody.isKinematic = !canSimulate;
                if (!context.TorsoRigidbody.isKinematic)
                {
                    context.TorsoRigidbody.linearVelocity = Vector3.zero;
                }
            }
        }

        public void OnExit(RobotContext context)
        {
        }

        public void OnTick(RobotContext context, float deltaTime)
        {
        }

        public void OnFixedTick(RobotContext context, float fixedDeltaTime)
        {
        }
    }
}
