#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for capturing and resolving GameObject identifiers across scenes and domain reloads,
    /// supporting active, inactive, and duplicate sibling GameObjects.
    /// </summary>
    public interface IHierarchyPathResolver
    {
        /// <summary>
        /// Captures persistent and hierarchical metadata for a GameObject into a SceneObjectIdentifier.
        /// </summary>
        SceneObjectIdentifier CaptureIdentifier(GameObject go);

        /// <summary>
        /// Resolves a SceneObjectIdentifier back to a live GameObject instance in the scene.
        /// </summary>
        GameObject ResolveIdentifier(SceneObjectIdentifier id);
    }
}
#endif
