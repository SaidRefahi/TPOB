#if UNITY_EDITOR
using UnityEngine.SceneManagement;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for subscribers responding to Unity Editor lifecycle transitions,
    /// including Play Mode state changes, assembly reloads, and scene closing events.
    /// </summary>
    public interface IPlayModeTransitionSubscriber
    {
        /// <summary>
        /// Defines dispatch priority. Lower numbers execute first during pre-transition phases (e.g. ExitingEditMode, BeforeAssemblyReload, SceneClosing).
        /// Standard execution orders:
        /// <list type="bullet">
        ///   <item><description>SelectionMementoService: 100 (Captures &amp; clears selection)</description></item>
        ///   <item><description>InspectorSanitizerService: 200 (Purges inspectors, trackers, and orphan editors)</description></item>
        ///   <item><description>Default/User subscribers: 500</description></item>
        /// </list>
        /// </summary>
        int ExecutionOrder { get; }

        /// <summary>
        /// Invoked when entering Play Mode, before the Edit Mode scene is unloaded or domain reloaded.
        /// </summary>
        void OnExitingEditMode();

        /// <summary>
        /// Invoked after Play Mode has fully initialized and the play scene is loaded.
        /// </summary>
        void OnEnteredPlayMode();

        /// <summary>
        /// Invoked when exiting Play Mode, before the Play Mode scene is unloaded.
        /// </summary>
        void OnExitingPlayMode();

        /// <summary>
        /// Invoked after exiting Play Mode and returning to Edit Mode.
        /// </summary>
        void OnEnteredEditMode();

        /// <summary>
        /// Invoked immediately before an assembly or domain reload begins.
        /// </summary>
        void OnBeforeAssemblyReload();

        /// <summary>
        /// Invoked immediately after an assembly or domain reload has completed.
        /// </summary>
        void OnAfterAssemblyReload();

        /// <summary>
        /// Invoked right before an open scene is closed or unloaded.
        /// </summary>
        /// <param name="scene">The scene being closed.</param>
        void OnSceneClosing(Scene scene);
    }
}
#endif
