using UnityEngine;

namespace Game.Gameplay.Player.Robot
{
    public sealed class FusionStrategy : IRobotStrategy
    {
        public bool IsFused => true;

        public void OnEnter(RobotContext context)
        {
            if (context == null || context.Torso == null || context.Socket == null || context.Legs == null)
            {
                return;
            }

            var torsoColliders = context.Torso.GetComponentsInChildren<Collider>();
            var legsColliders = context.Legs.GetComponentsInChildren<Collider>();
            for (int i = 0; i < torsoColliders.Length; i++)
            {
                for (int j = 0; j < legsColliders.Length; j++)
                {
                    Physics.IgnoreCollision(torsoColliders[i], legsColliders[j], true);
                }
            }

            if (context.TorsoRigidbody != null)
            {
                if (!context.TorsoRigidbody.isKinematic)
                {
                    context.TorsoRigidbody.linearVelocity = Vector3.zero;
                    context.TorsoRigidbody.angularVelocity = Vector3.zero;
                }
                context.TorsoRigidbody.isKinematic = true;
                context.TorsoRigidbody.constraints = RigidbodyConstraints.FreezeAll;
            }

            Transform attachPoint = context.Socket.AttachPoint;
            if (attachPoint != null)
            {
                context.Torso.transform.SetParent(attachPoint, false);
                context.Torso.transform.localPosition = Vector3.zero;
                context.Torso.transform.localRotation = Quaternion.identity;
            }

            context.Torso.SetFused(true, context.Legs.transform.rotation);
            context.Legs.SetFused(true, context.TorsoBaseMass);
            context.Socket.PlayDockJuice();
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
