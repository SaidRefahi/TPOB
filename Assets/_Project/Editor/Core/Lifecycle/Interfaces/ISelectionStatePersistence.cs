#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for persisting selection mementos across domain reloads and editor sessions.
    /// Encapsulates lifetime storage to decouple SelectionMementoService from UnityEditor.SessionState.
    /// </summary>
    public interface ISelectionStatePersistence
    {
        /// <summary>
        /// Saves a selection memento into persistent session storage.
        /// If memento is null or empty, the persistence store is cleared.
        /// </summary>
        void SaveMemento(SelectionMemento memento);

        /// <summary>
        /// Loads the persisted selection memento, or returns SelectionMemento.Empty if none is stored.
        /// </summary>
        SelectionMemento LoadMemento();

        /// <summary>
        /// Clears any persisted memento from storage.
        /// </summary>
        void ClearMemento();

        /// <summary>
        /// Returns true if a valid memento currently resides in the persistence store.
        /// </summary>
        bool HasMemento { get; }
    }
}
#endif
