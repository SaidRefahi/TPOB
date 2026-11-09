#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Immutable serializable snapshot of the editor selection state.
    /// Supports both scene objects (active and inactive) and project asset references.
    /// Serializes cleanly with JsonUtility to survive Domain Reloads in SessionState.
    /// Complies strictly with AGY0001, AGY0002, AGY0003, and UNT0007/8/23.
    /// </summary>
    [Serializable]
    public sealed class SelectionMemento
    {
        public static readonly SelectionMemento Empty = new SelectionMemento(
            Array.Empty<SceneObjectIdentifier>(),
            Array.Empty<string>(),
            null,
            string.Empty,
            0
        );

        [SerializeField] private SceneObjectIdentifier[] _sceneObjects;
        [SerializeField] private string[] _assetGuids;
        [SerializeField] private SceneObjectIdentifier _activeSceneObject;
        [SerializeField] private string _activeAssetGuid;
        [SerializeField] private long _timestampTicks;

        public SceneObjectIdentifier[] SceneObjects => _sceneObjects ?? Array.Empty<SceneObjectIdentifier>();
        public string[] AssetGuids => _assetGuids ?? Array.Empty<string>();
        public SceneObjectIdentifier ActiveSceneObject => _activeSceneObject;
        public string ActiveAssetGuid => _activeAssetGuid ?? string.Empty;
        public long TimestampTicks => _timestampTicks;

        public bool IsEmpty => (SceneObjects.Length == 0) && (AssetGuids.Length == 0);

        // Backwards-compatible properties for existing consumers
        public string[] ScenePaths
        {
            get
            {
                if (_sceneObjects == null || _sceneObjects.Length == 0)
                {
                    return Array.Empty<string>();
                }
                var paths = new string[_sceneObjects.Length];
                for (int i = 0; i < _sceneObjects.Length; i++)
                {
                    paths[i] = _sceneObjects[i] != null ? _sceneObjects[i].HierarchyPath : string.Empty;
                }
                return paths;
            }
        }

        public string[] AssetGUIDs => AssetGuids;

        public string ActiveScenePath => _activeSceneObject != null ? _activeSceneObject.HierarchyPath : string.Empty;

        public SelectionMemento()
        {
            _sceneObjects = Array.Empty<SceneObjectIdentifier>();
            _assetGuids = Array.Empty<string>();
            _activeSceneObject = null;
            _activeAssetGuid = string.Empty;
            _timestampTicks = 0;
        }

        public SelectionMemento(
            SceneObjectIdentifier[] sceneObjects,
            string[] assetGuids,
            SceneObjectIdentifier activeSceneObject,
            string activeAssetGuid,
            long timestampTicks)
        {
            _sceneObjects = sceneObjects ?? Array.Empty<SceneObjectIdentifier>();
            _assetGuids = assetGuids ?? Array.Empty<string>();
            _activeSceneObject = activeSceneObject;
            _activeAssetGuid = activeAssetGuid ?? string.Empty;
            _timestampTicks = timestampTicks;
        }

        // Backwards-compatible constructor for existing tests and legacy call sites
        public SelectionMemento(string[] scenePaths, string[] assetGUIDs, string activeScenePath)
        {
            var paths = scenePaths ?? Array.Empty<string>();
            var objects = new SceneObjectIdentifier[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                objects[i] = new SceneObjectIdentifier(string.Empty, string.Empty, paths[i], Array.Empty<int>());
            }
            _sceneObjects = objects;
            _assetGuids = assetGUIDs ?? Array.Empty<string>();
            _activeSceneObject = !string.IsNullOrEmpty(activeScenePath)
                ? new SceneObjectIdentifier(string.Empty, string.Empty, activeScenePath, Array.Empty<int>())
                : null;
            _activeAssetGuid = string.Empty;
            _timestampTicks = DateTime.UtcNow.Ticks;
        }
    }
}
#endif
