#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for coordinating and broadcasting PlayMode and AssemblyReload transitions.
    /// Manages an ordered set of subscribers to ensure deterministic state save and restore.
    /// </summary>
    public interface IPlayModeTransitionObserver
    {
        /// <summary>
        /// Registers a lifecycle subscriber, sorted by its ExecutionOrder.
        /// </summary>
        void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber);

        /// <summary>
        /// Unregisters an existing lifecycle subscriber.
        /// </summary>
        void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber);
    }
}
#endif
