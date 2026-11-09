#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service managing selection state capture, clean clearing, and restoration across editor transitions.
    /// Implements IPlayModeTransitionSubscriber to participate in lifecycle transitions.
    /// </summary>
    public interface ISelectionMementoService : IPlayModeTransitionSubscriber
    {
        /// <summary>
        /// Captures the current active and multi-selected objects into an immutable SelectionMemento.
        /// </summary>
        SelectionMemento CaptureCurrentSelection();

        /// <summary>
        /// Clears active selection and purges open inspector trackers safely before scene unloads.
        /// </summary>
        void ClearSelectionSafely();

        /// <summary>
        /// Restores selection from a previously captured SelectionMemento.
        /// </summary>
        void RestoreSelection(SelectionMemento memento);

        /// <summary>
        /// Indicates whether a valid SelectionMemento is currently stored in persistence.
        /// </summary>
        bool HasPersistedMemento { get; }

        /// <summary>
        /// Restores selection from persisted storage and clears the store upon completion.
        /// </summary>
        void RestoreFromPersistence();

        /// <summary>
        /// Clears any persisted selection memento from storage.
        /// </summary>
        void ClearPersistence();
    }
}
#endif
