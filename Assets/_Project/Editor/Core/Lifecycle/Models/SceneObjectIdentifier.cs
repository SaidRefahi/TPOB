#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Serializable compound identifier capturing persistent and hierarchical metadata
    /// for scene GameObjects, supporting inactive objects, duplicate sibling names, and multi-scene contexts.
    /// Complies strictly with AGY0001, AGY0002, AGY0003, and UNT0007/8/23.
    /// </summary>
    [Serializable]
    public sealed class SceneObjectIdentifier : IEquatable<SceneObjectIdentifier>
    {
        [SerializeField] private string _sceneName;
        [SerializeField] private string _scenePath;
        [SerializeField] private string _globalObjectIdString;
        [SerializeField] private string _hierarchyPath;
        [SerializeField] private string[] _pathSegments;
        [SerializeField] private int[] _siblingIndices;

        public string SceneName => _sceneName ?? string.Empty;
        public string ScenePath => _scenePath ?? string.Empty;
        public string GlobalObjectIdString => _globalObjectIdString ?? string.Empty;
        public string HierarchyPath => _hierarchyPath ?? string.Empty;
        public string[] PathSegments => _pathSegments ?? Array.Empty<string>();
        public int[] SiblingIndices => _siblingIndices ?? Array.Empty<int>();

        public SceneObjectIdentifier()
        {
            _sceneName = string.Empty;
            _scenePath = string.Empty;
            _globalObjectIdString = string.Empty;
            _hierarchyPath = string.Empty;
            _pathSegments = Array.Empty<string>();
            _siblingIndices = Array.Empty<int>();
        }

        public SceneObjectIdentifier(
            string sceneName,
            string scenePath,
            string globalObjectIdString,
            string hierarchyPath,
            string[] pathSegments,
            int[] siblingIndices)
        {
            _sceneName = sceneName ?? string.Empty;
            _scenePath = scenePath ?? string.Empty;
            _globalObjectIdString = globalObjectIdString ?? string.Empty;
            _hierarchyPath = hierarchyPath ?? string.Empty;
            _pathSegments = pathSegments ?? (string.IsNullOrEmpty(hierarchyPath) ? Array.Empty<string>() : hierarchyPath.Split('/'));
            _siblingIndices = siblingIndices ?? Array.Empty<int>();
        }

        public SceneObjectIdentifier(
            string sceneName,
            string globalObjectIdString,
            string hierarchyPath,
            int[] siblingIndices)
            : this(sceneName, string.Empty, globalObjectIdString, hierarchyPath, null, siblingIndices)
        {
        }

        public bool Equals(SceneObjectIdentifier other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_globalObjectIdString, other._globalObjectIdString, StringComparison.Ordinal)
                && string.Equals(_hierarchyPath, other._hierarchyPath, StringComparison.Ordinal)
                && string.Equals(_sceneName, other._sceneName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SceneObjectIdentifier other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (!string.IsNullOrEmpty(_globalObjectIdString) ? _globalObjectIdString.GetHashCode() : 0);
                hash = hash * 23 + (!string.IsNullOrEmpty(_hierarchyPath) ? _hierarchyPath.GetHashCode() : 0);
                hash = hash * 23 + (!string.IsNullOrEmpty(_sceneName) ? _sceneName.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
#endif
