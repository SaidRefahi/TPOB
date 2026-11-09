#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service contract for sanitizing Unity Inspector windows, active editor trackers,
    /// and orphaned Editor ScriptableObjects before Play Mode transitions and scene unloads.
    /// </summary>
    public interface IInspectorSanitizerService
    {
        /// <summary>
        /// Purges all open InspectorWindow and PropertyEditor instances by destroying their
        /// active editors, clearing locked target references, resetting UI Toolkit element caches,
        /// and triggering a clean UI rebuild.
        /// </summary>
        void PurgeAllInspectors();

        /// <summary>
        /// Safely purges the engine-wide ActiveEditorTracker.sharedTracker singleton by destroying
        /// its active editors and rebuilding against empty selection without invoking native Destroy().
        /// </summary>
        void PurgeSharedTracker();

        /// <summary>
        /// Destroys all Editor ScriptableObjects targeting scene objects or null references
        /// (!EditorUtility.IsPersistent), preventing deserialization of destroyed native targets.
        /// </summary>
        void DestroyOrphanAndSceneEditors();

        /// <summary>
        /// Unlocks all open InspectorWindow instances to prevent retention of locked targets across scene transitions.
        /// </summary>
        void UnlockAllInspectors();
    }
}
#endif
