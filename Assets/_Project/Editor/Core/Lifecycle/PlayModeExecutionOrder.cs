#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Standard execution order constants for <see cref="IPlayModeTransitionSubscriber.ExecutionOrder"/>.
    /// Lower values execute earlier during pre-transition phases (cleanup, state capture).
    /// </summary>
    public static class PlayModeExecutionOrder
    {
        /// <summary>
        /// Captures and clears selection state prior to inspector/tracker purging.
        /// </summary>
        public const int SelectionMemento = 100;

        /// <summary>
        /// Purges inspector trackers, locked object buffers, and orphan editors after selection has been cleared.
        /// </summary>
        public const int InspectorSanitizer = 200;

        /// <summary>
        /// Default execution order for general editor lifecycle subscribers.
        /// </summary>
        public const int Default = 500;
    }
}
#endif
