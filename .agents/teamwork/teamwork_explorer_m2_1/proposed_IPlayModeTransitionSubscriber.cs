#if UNITY_EDITOR
using UnityEngine.SceneManagement;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for components subscribing to editor lifecycle transitions (Play Mode and Assembly Reload).
    /// Ordered by ExecutionOrder (lower values execute first during shutdown, higher during startup).
    /// </summary>
    public interface IPlayModeTransitionSubscriber
    {
        /// <summary>
        /// Relative execution priority. Lower values execute first.
        /// Selection capture should execute early (e.g. -100) before inspector purges (e.g. 100).
        /// </summary>
        int ExecutionOrder { get; }

        /// <summary>
        /// Invoked when the user triggers entering Play Mode, before Edit Mode scene is unloaded.
        /// </summary>
        void OnExitingEditMode();

        /// <summary>
        /// Invoked immediately after entering Play Mode and Play Mode scene has initialized.
        /// </summary>
        void OnEnteredPlayMode();

        /// <summary>
        /// Invoked when the user triggers exiting Play Mode, before Play Mode scene is destroyed.
        /// </summary>
        void OnExitingPlayMode();

        /// <summary>
        /// Invoked immediately after entering Edit Mode and Edit Mode scene has reloaded.
        /// </summary>
        void OnEnteredEditMode();

        /// <summary>
        /// Invoked immediately before C# domain reload / compilation begins.
        /// </summary>
        void OnBeforeAssemblyReload();

        /// <summary>
        /// Invoked after C# domain reload completes and static fields are re-initialized.
        /// </summary>
        void OnAfterAssemblyReload();

        /// <summary>
        /// Invoked before an editor scene closes.
        /// </summary>
        void OnSceneClosing(Scene scene);
    }
}
#endif
