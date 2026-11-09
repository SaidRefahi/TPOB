#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service responsible for sanitizing Unity Inspector windows, active editor trackers,
    /// and destroying orphan or scene-bound Editor ScriptableObjects before Play Mode transitions and Domain Reloads.
    /// </summary>
    public interface IInspectorSanitizerService
    {
        /// <summary>
        /// Purges all open Inspector/PropertyEditor windows: unlocks them, clears serialized locked objects,
        /// destroys tracker active editors, clears UI elements, and requests repaint.
        /// Also purges the shared tracker and destroys orphan/scene editors.
        /// </summary>
        void PurgeAllInspectors();

        /// <summary>
        /// Purges the engine-wide ActiveEditorTracker.sharedTracker by destroying its active editors,
        /// clearing dirty state, and forcing rebuild against an empty selection without calling native Destroy().
        /// </summary>
        void PurgeSharedTracker();

        /// <summary>
        /// Identifies and destroys all Editor ScriptableObjects in memory whose target is null or non-persistent (scene objects),
        /// preventing them from surviving across scene unload and domain reload.
        /// </summary>
        void DestroyOrphanAndSceneEditors();

        /// <summary>
        /// Unlocks all open Inspector windows and clears their serialized locked objects buffer.
        /// </summary>
        void UnlockAllInspectors();
    }
}
#endif
