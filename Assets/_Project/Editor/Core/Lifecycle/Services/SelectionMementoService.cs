#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service implementing selection capture, persistence, and restoration across Play Mode transitions and Domain Reloads.
    /// Implements ISelectionMementoService and legacy IEditorLifecycleSubscriber for full backward compatibility.
    /// Uses ISelectionStatePersistence (SessionState) to survive AppDomain teardowns.
    /// Complies strictly with AGY0001, AGY0002, AGY0003, AGY0021, and UNT0007/8/23.
    /// </summary>
    public sealed class SelectionMementoService : ISelectionMementoService, IEditorLifecycleSubscriber
    {
        private static SelectionMementoService s_instance;

        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static SelectionMementoService Instance => s_instance ??= new SelectionMementoService();

        /// <summary>
        /// Allows test suites to inject an instance or mock service.
        /// </summary>
        public static void SetInstanceForTesting(SelectionMementoService instance)
        {
            s_instance = instance;
        }

        private readonly ISelectionStatePersistence _persistence;
        private readonly IInspectorSanitizerService _sanitizer;
        private readonly IHierarchyPathResolver _pathResolver;
        private SelectionMemento _inMemoryMemento = SelectionMemento.Empty;

        /// <summary>
        /// Early execution order (100) ensures selection is captured before windows or trackers are purged (200).
        /// </summary>
        public int ExecutionOrder => PlayModeExecutionOrder.SelectionMemento;

        public bool HasPersistedMemento => _persistence != null && _persistence.HasMemento;

        public SelectionMementoService()
            : this(new SessionStateSelectionPersistence(), InspectorSanitizerService.Instance, HierarchyPathResolver.Default)
        {
        }

        public SelectionMementoService(
            ISelectionStatePersistence persistence,
            IInspectorSanitizerService sanitizer,
            IHierarchyPathResolver pathResolver = null)
        {
            _persistence = persistence ?? new SessionStateSelectionPersistence();
            _sanitizer = sanitizer ?? InspectorSanitizerService.Instance;
            _pathResolver = pathResolver ?? HierarchyPathResolver.Default;
        }

        /// <summary>
        /// Captures the current active and multi-selected objects into an immutable SelectionMemento.
        /// </summary>
        public SelectionMemento CaptureCurrentSelection()
        {
            var objects = Selection.objects;
            if (objects == null || objects.Length == 0)
            {
                _inMemoryMemento = SelectionMemento.Empty;
                return _inMemoryMemento;
            }

            var sceneObjects = new List<SceneObjectIdentifier>();
            var assetGuids = new List<string>();
            var assetGlobalObjectIds = new List<string>();
            SceneObjectIdentifier activeSceneObj = null;
            string activeAssetGuid = string.Empty;
            string activeAssetGlobalObjectId = string.Empty;

            var activeGo = Selection.activeGameObject;
            if (activeGo != null && !EditorUtility.IsPersistent(activeGo))
            {
                activeSceneObj = _pathResolver.CaptureIdentifier(activeGo);
            }

            var activeObj = Selection.activeObject;
            if (activeObj != null && EditorUtility.IsPersistent(activeObj))
            {
                string assetPath = AssetDatabase.GetAssetPath(activeObj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    activeAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                }

                try
                {
                    var gid = GlobalObjectId.GetGlobalObjectIdSlow(activeObj);
                    activeAssetGlobalObjectId = gid.ToString();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SelectionMementoService] Could not resolve GlobalObjectId for active object {activeObj.name}: {ex.Message}");
                }
            }

            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj == null)
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(obj))
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path))
                    {
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (!string.IsNullOrEmpty(guid) && !assetGuids.Contains(guid))
                        {
                            assetGuids.Add(guid);
                        }
                    }

                    try
                    {
                        var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
                        string gidStr = gid.ToString();
                        if (!string.IsNullOrEmpty(gidStr) && !assetGlobalObjectIds.Contains(gidStr))
                        {
                            assetGlobalObjectIds.Add(gidStr);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SelectionMementoService] Could not resolve GlobalObjectId for object {obj.name}: {ex.Message}");
                    }
                }
                else if (obj is GameObject go)
                {
                    sceneObjects.Add(_pathResolver.CaptureIdentifier(go));
                }
                else if (obj is Component comp)
                {
                    var compGo = comp.gameObject;
                    if (compGo != null)
                    {
                        sceneObjects.Add(_pathResolver.CaptureIdentifier(compGo));
                    }
                }
            }

            _inMemoryMemento = new SelectionMemento(
                sceneObjects.ToArray(),
                assetGuids.ToArray(),
                assetGlobalObjectIds.ToArray(),
                activeSceneObj,
                activeAssetGuid,
                activeAssetGlobalObjectId,
                DateTime.UtcNow.Ticks
            );

            return _inMemoryMemento;
        }

        /// <summary>
        /// Legacy alias for CaptureCurrentSelection.
        /// </summary>
        public SelectionMemento CaptureSelection()
        {
            return CaptureCurrentSelection();
        }

        /// <summary>
        /// Clears active selection and purges open inspector trackers safely before scene unloads.
        /// </summary>
        public void ClearSelectionSafely()
        {
            Selection.objects = Array.Empty<Object>();
            Selection.activeObject = null;

            if (_sanitizer != null)
            {
                _sanitizer.PurgeAllInspectors();
            }
        }

        /// <summary>
        /// Restores selection from a previously captured SelectionMemento.
        /// </summary>
        public void RestoreSelection(SelectionMemento memento)
        {
            if (memento == null || memento.IsEmpty)
            {
                return;
            }

            var restored = new List<Object>();
            GameObject activeGo = null;
            Object activeAsset = null;

            // 1. Resolve Scene GameObjects
            var sceneObjects = memento.SceneObjects;
            for (int i = 0; i < sceneObjects.Length; i++)
            {
                var id = sceneObjects[i];
                var go = _pathResolver.ResolveIdentifier(id);
                if (go != null)
                {
                    restored.Add(go);
                    if (memento.ActiveSceneObject != null && memento.ActiveSceneObject.Equals(id))
                    {
                        activeGo = go;
                    }
                }
            }

            // 2. Resolve Project Assets via GlobalObjectId or GUID
            var assetGids = memento.AssetGlobalObjectIds;
            if (assetGids != null && assetGids.Length > 0)
            {
                for (int i = 0; i < assetGids.Length; i++)
                {
                    if (GlobalObjectId.TryParse(assetGids[i], out var gid))
                    {
                        try
                        {
                            var asset = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                            if (asset != null && !restored.Contains(asset))
                            {
                                restored.Add(asset);
                                if (!string.IsNullOrEmpty(memento.ActiveGlobalObjectId) && memento.ActiveGlobalObjectId == assetGids[i])
                                {
                                    activeAsset = asset;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[SelectionMementoService] Asset GlobalObjectId resolution failed: {ex.Message}");
                        }
                    }
                }
            }

            var assetGuids = memento.AssetGuids;
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string guid = assetGuids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset != null && !restored.Contains(asset))
                    {
                        restored.Add(asset);
                        if (!string.IsNullOrEmpty(memento.ActiveAssetGuid) && memento.ActiveAssetGuid == guid)
                        {
                            activeAsset = asset;
                        }
                    }
                }
            }

            // 3. Apply Selection
            if (restored.Count > 0)
            {
                Selection.objects = restored.ToArray();
                if (activeGo != null)
                {
                    Selection.activeGameObject = activeGo;
                }
                else if (activeAsset != null)
                {
                    Selection.activeObject = activeAsset;
                }
            }

            // 4. Force shared tracker rebuild
            if (_sanitizer != null)
            {
                _sanitizer.PurgeSharedTracker();
            }
        }

        /// <summary>
        /// Restores selection from persisted storage and clears the store upon completion.
        /// </summary>
        public void RestoreFromPersistence()
        {
            try
            {
                if (!HasPersistedMemento)
                {
                    return;
                }

                var memento = _persistence.LoadMemento();
                if (memento != null && !memento.IsEmpty)
                {
                    RestoreSelection(memento);
                }
            }
            finally
            {
                ClearPersistence();
            }
        }

        /// <summary>
        /// Clears any persisted selection memento from storage.
        /// </summary>
        public void ClearPersistence()
        {
            if (_persistence != null)
            {
                _persistence.ClearMemento();
            }
        }

        // =========================================================================
        // IPlayModeTransitionSubscriber Lifecycle Implementation
        // =========================================================================

        public void OnExitingEditMode()
        {
            var memento = CaptureCurrentSelection();
            if (_persistence != null)
            {
                _persistence.SaveMemento(memento);
            }
            ClearSelectionSafely();
        }

        public void OnEnteredPlayMode()
        {
            EditorApplication.delayCall += HandleDeferredRestore;
        }

        public void OnExitingPlayMode()
        {
            ClearSelectionSafely();
        }

        public void OnEnteredEditMode()
        {
            EditorApplication.delayCall += HandleDeferredRestore;
        }

        public void OnBeforeAssemblyReload()
        {
            var objs = Selection.objects;
            if (objs != null && objs.Length > 0)
            {
                var memento = CaptureCurrentSelection();
                if (_persistence != null)
                {
                    _persistence.SaveMemento(memento);
                }
                ClearSelectionSafely();
            }
            else if (_sanitizer != null)
            {
                _sanitizer.PurgeAllInspectors();
            }
        }

        public void OnAfterAssemblyReload()
        {
            EditorApplication.delayCall += HandleDeferredRestore;
        }

        public void OnSceneClosing(Scene scene)
        {
            var activeGo = Selection.activeGameObject;
            if (activeGo != null && activeGo.scene == scene)
            {
                CaptureCurrentSelection();
                ClearSelectionSafely();
            }
        }

        private void HandleDeferredRestore()
        {
            try
            {
                if (HasPersistedMemento)
                {
                    RestoreFromPersistence();
                }
                else if (_inMemoryMemento != null && !_inMemoryMemento.IsEmpty)
                {
                    RestoreSelection(_inMemoryMemento);
                    _inMemoryMemento = SelectionMemento.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SelectionMementoService] Deferred restoration failed: {ex.Message}");
            }
        }

        // =========================================================================
        // IEditorLifecycleSubscriber Legacy Compatibility
        // =========================================================================

        void IEditorLifecycleSubscriber.OnBeforePlayModeEntered() => OnExitingEditMode();
        void IEditorLifecycleSubscriber.OnPlayModeEntered() => OnEnteredPlayMode();
        void IEditorLifecycleSubscriber.OnBeforePlayModeExited() => OnExitingPlayMode();
        void IEditorLifecycleSubscriber.OnPlayModeExited() => OnEnteredEditMode();
        void IEditorLifecycleSubscriber.OnBeforeDomainReload() => OnBeforeAssemblyReload();
        void IEditorLifecycleSubscriber.OnAfterDomainReload() => OnAfterAssemblyReload();
    }
}
#endif
