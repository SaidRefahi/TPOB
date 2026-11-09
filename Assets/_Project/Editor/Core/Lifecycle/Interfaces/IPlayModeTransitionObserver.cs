#if UNITY_EDITOR
using System.Collections.Generic;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for the observer service coordinating Unity Editor lifecycle transitions
    /// and dispatching ordered notifications to registered subscribers.
    /// </summary>
    public interface IPlayModeTransitionObserver
    {
        /// <summary>
        /// Read-only collection of registered subscribers, ordered by <see cref="IPlayModeTransitionSubscriber.ExecutionOrder"/>.
        /// </summary>
        IReadOnlyList<IPlayModeTransitionSubscriber> Subscribers { get; }

        /// <summary>
        /// Registers a subscriber to receive lifecycle notifications.
        /// Maintains sorted order by <see cref="IPlayModeTransitionSubscriber.ExecutionOrder"/>.
        /// </summary>
        /// <param name="subscriber">The subscriber to register.</param>
        void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber);

        /// <summary>
        /// Unregisters a subscriber from lifecycle notifications.
        /// </summary>
        /// <param name="subscriber">The subscriber to unregister.</param>
        void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber);
    }
}
#endif
