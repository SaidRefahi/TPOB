using Game.Core.Interfaces;
using UnityEngine;

namespace Game.Core.Commands
{
    public sealed class PlayerContext
    {
        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public Rigidbody Rigidbody { get; }
        public IMoveable Moveable { get; }
        public IKicker Kicker { get; }
        public IGrabber Grabber { get; }
        public IThrower Thrower { get; }
        public IMagnetOperator MagnetOperator { get; }
        public IClimber Climber { get; }
        public IInteractOperator InteractOperator { get; }

        public PlayerContext(
            GameObject gameObject,
            Transform transform,
            Rigidbody rigidbody,
            IMoveable moveable,
            IKicker kicker,
            IGrabber grabber,
            IThrower thrower,
            IMagnetOperator magnetOperator,
            IClimber climber,
            IInteractOperator interactOperator = null)
        {
            GameObject = gameObject;
            Transform = transform;
            Rigidbody = rigidbody;
            Moveable = moveable;
            Kicker = kicker;
            Grabber = grabber;
            Thrower = thrower;
            MagnetOperator = magnetOperator;
            Climber = climber;
            InteractOperator = interactOperator;
        }
    }
}
