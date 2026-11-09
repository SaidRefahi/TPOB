#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Serializable memento holding captured selection state across domain reloads and scene transitions.
    /// Uses encapsulated serialized fields to support JsonUtility and satisfy AGY0001, AGY0002, and AGY0003 rules.
    /// Preserves full backwards-compatible accessors for legacy callers.
    /// </summary>
    [Serializable]
    public sealed class SelectionMemento
    {
        public static readonly SelectionMemento Empty = new SelectionMemento();

        [SerializeField] private SceneObjectIdentifier[] _sceneObjects;
        [SerializeField] private string[] _assetGuids;
        [SerializeField] private string[] _assetGlobalObjectIds;
        [SerializeField] private SceneObjectIdentifier _activeSceneObject;
        [SerializeField] private string _activeAssetGuid;
        [SerializeField] private string _activeGlobalObjectId;
        [SerializeField] private long _timestampTicks;

        public SceneObjectIdentifier[] SceneObjects => _sceneObjects ?? Array.Empty<SceneObjectIdentifier>();
        public string[] AssetGuids => _assetGuids ?? Array.Empty<string>();
        public string[] AssetGlobalObjectIds => _assetGlobalObjectIds ?? Array.Empty<string>();
        public SceneObjectIdentifier ActiveSceneObject => _activeSceneObject;
        public string ActiveAssetGuid => _activeAssetGuid ?? string.Empty;
        public string ActiveGlobalObjectId => _activeGlobalObjectId ?? string.Empty;
        public long TimestampTicks => _timestampTicks;

        public bool IsEmpty => (SceneObjects.Length == 0) && (AssetGuids.Length == 0);

        #region Backwards-Compatible Accessors

        public string[] ScenePaths
        {
            get
            {
                var objects = SceneObjects;
                if (objects.Length == 0)
                {
                    return Array.Empty<string>();
                }

                var paths = new string[objects.Length];
                for (int i = 0; i < objects.Length; i++)
                {
                    paths[i] = objects[i] != null ? objects[i].HierarchyPath : string.Empty;
                }
                return paths;
            }
        }

        public string[] AssetGUIDs => AssetGuids;

        public string ActiveScenePath => _activeSceneObject != null ? _activeSceneObject.HierarchyPath : string.Empty;

        #endregion

        public SelectionMemento()
        {
            _sceneObjects = Array.Empty<SceneObjectIdentifier>();
            _assetGuids = Array.Empty<string>();
            _assetGlobalObjectIds = Array.Empty<string>();
            _activeSceneObject = null;
            _activeAssetGuid = string.Empty;
            _activeGlobalObjectId = string.Empty;
            _timestampTicks = 0;
        }

        public SelectionMemento(
            SceneObjectIdentifier[] sceneObjects,
            string[] assetGuids,
            string[] assetGlobalObjectIds,
            SceneObjectIdentifier activeSceneObject,
            string activeAssetGuid,
            string activeGlobalObjectId,
            long timestampTicks)
        {
            _sceneObjects = sceneObjects ?? Array.Empty<SceneObjectIdentifier>();
            _assetGuids = assetGuids ?? Array.Empty<string>();
            _assetGlobalObjectIds = assetGlobalObjectIds ?? Array.Empty<string>();
            _activeSceneObject = activeSceneObject;
            _activeAssetGuid = activeAssetGuid ?? string.Empty;
            _activeGlobalObjectId = activeGlobalObjectId ?? string.Empty;
            _timestampTicks = timestampTicks;
        }

        public SelectionMemento(
            SceneObjectIdentifier[] sceneObjects,
            string[] assetGuids,
            SceneObjectIdentifier activeSceneObject,
            string activeAssetGuid,
            long timestampTicks)
            : this(sceneObjects, assetGuids, Array.Empty<string>(), activeSceneObject, activeAssetGuid, string.Empty, timestampTicks)
        {
        }

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
            _assetGlobalObjectIds = Array.Empty<string>();
            _activeSceneObject = !string.IsNullOrEmpty(activeScenePath)
                ? new SceneObjectIdentifier(string.Empty, string.Empty, activeScenePath, Array.Empty<int>())
                : null;
            _activeAssetGuid = string.Empty;
            _activeGlobalObjectId = string.Empty;
            _timestampTicks = DateTime.UtcNow.Ticks;
        }
    }
}
#endif
