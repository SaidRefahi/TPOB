#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Static facade providing backwards-compatible access to inspector sanitation routines.
    /// Delegates directly to <see cref="InspectorSanitizerService.Instance"/> to prevent breaking existing callers.
    /// </summary>
    public static class InspectorTrackerSanitizer
    {
        /// <summary>
        /// Purges all open inspector windows, the shared active editor tracker, and orphaned editors.
        /// </summary>
        public static void PurgeAllInspectorWindows()
        {
            InspectorSanitizerService.Instance.PurgeAllInspectors();
            InspectorSanitizerService.Instance.PurgeSharedTracker();
            InspectorSanitizerService.Instance.DestroyOrphanAndSceneEditors();
        }

        /// <summary>
        /// Purges orphan and scene-bound Editor ScriptableObjects.
        /// </summary>
        public static void PurgeOrphanEditors()
        {
            InspectorSanitizerService.Instance.DestroyOrphanAndSceneEditors();
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
            InspectorSanitizerService.Instance.SanitizeActiveEditors();
        }

        /// <summary>
        /// Conditionally unlocks all open inspector windows.
        /// </summary>
        /// <param name="forceUnlock">If true, forces unlock on all inspector windows.</param>
        public static void UnlockInspectors(bool forceUnlock = false)
        {
            if (forceUnlock)
            {
                InspectorSanitizerService.Instance.UnlockAllInspectors();
            }
        }

        /// <summary>
        /// Unconditionally unlocks all open inspector windows.
        /// </summary>
        public static void UnlockAllInspectors()
        {
            InspectorSanitizerService.Instance.UnlockAllInspectors();
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
