#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Static facade providing backwards-compatible access to inspector sanitation routines.
    /// Delegates directly to <see cref="IInspectorSanitizerService"/> (<see cref="InspectorSanitizerService.Instance"/>)
    /// to preserve existing call sites across LevelEditor, SelectionMementoService, and EditorLifecycleCoordinator.
    /// </summary>
    public static class InspectorTrackerSanitizer
    {
        private static IInspectorSanitizerService s_service;

        /// <summary>
        /// Active inspector sanitizer service. Defaults to <see cref="InspectorSanitizerService.Instance"/>.
        /// </summary>
        public static IInspectorSanitizerService Service => s_service ??= InspectorSanitizerService.Instance;

        /// <summary>
        /// Allows test suites to inject an alternate <see cref="IInspectorSanitizerService"/> implementation.
        /// </summary>
        public static void SetServiceForTesting(IInspectorSanitizerService service)
        {
            s_service = service;
        }

        /// <summary>
        /// Purges all open inspector windows, the shared active editor tracker, and orphaned editors.
        /// </summary>
        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
        }

        /// <summary>
        /// Purges orphan and scene-bound Editor ScriptableObjects.
        /// </summary>
        public static void PurgeOrphanEditors()
        {
            Service.DestroyOrphanAndSceneEditors();
        }

        /// <summary>
        /// Forces a clean purge of all trackers and inspector editors.
        /// </summary>
        public static void ForceCleanTracker()
        {
            PurgeAllInspectorWindows();
        }

        /// <summary>
        /// Destroys dead scene editors and rebuilds the shared active editor tracker.
        /// </summary>
        public static void SanitizeActiveEditors()
        {
            Service.DestroyOrphanAndSceneEditors();
            Service.PurgeSharedTracker();
        }

        /// <summary>
        /// Conditionally unlocks all open inspector windows.
        /// </summary>
        /// <param name="forceUnlock">If true, forces unlock on all inspector windows.</param>
        public static void UnlockInspectors(bool forceUnlock = false)
        {
            if (forceUnlock)
            {
                Service.UnlockAllInspectors();
            }
        }

        /// <summary>
        /// Unconditionally unlocks all open inspector windows.
        /// </summary>
        public static void UnlockAllInspectors()
        {
            Service.UnlockAllInspectors();
        }

        /// <summary>
        /// Clears the Unity Editor Console.
        /// </summary>
        public static void ClearConsole()
        {
            InspectorSanitizerService.Instance.ClearConsole();
        }
    }
}
#endif
