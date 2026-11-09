#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Utility service responsible for capturing and resolving GameObject identifiers across scenes and domain reloads.
    /// Supports active and inactive GameObjects, disambiguates sibling duplicates using SiblingIndices,
    /// and prioritizes GlobalObjectId resolution across scene reloads.
    /// Complies strictly with AGY0001, AGY0002, AGY0003, and UNT0007/8/23.
    /// </summary>
    public sealed class HierarchyPathResolver : IHierarchyPathResolver
    {
        public static readonly HierarchyPathResolver Default = new HierarchyPathResolver();

        public SceneObjectIdentifier CaptureIdentifier(GameObject go)
        {
            return BuildIdentifier(go);
        }

        public GameObject ResolveIdentifier(SceneObjectIdentifier id)
        {
            return Resolve(id);
        }

        /// <summary>
        /// Builds a compound <see cref="SceneObjectIdentifier"/> for a scene GameObject.
        /// </summary>
        public static SceneObjectIdentifier BuildIdentifier(GameObject go)
        {
            if (go == null)
            {
                return new SceneObjectIdentifier();
            }

            string sceneName = go.scene.IsValid() ? go.scene.name : string.Empty;
            string scenePath = go.scene.IsValid() ? go.scene.path : string.Empty;
            string globalIdString = string.Empty;

            try
            {
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
                globalIdString = gid.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HierarchyPathResolver] Failed reading GlobalObjectId for {go.name}: {ex.Message}");
            }

            var pathBuilder = new StringBuilder(go.name);
            var segmentList = new List<string> { go.name };
            var siblingList = new List<int> { go.transform.GetSiblingIndex() };

            var currentParent = go.transform.parent;
            while (currentParent != null)
            {
                pathBuilder.Insert(0, "/").Insert(0, currentParent.name);
                segmentList.Insert(0, currentParent.name);
                siblingList.Insert(0, currentParent.GetSiblingIndex());
                currentParent = currentParent.parent;
            }

            return new SceneObjectIdentifier(
                sceneName,
                scenePath,
                globalIdString,
                pathBuilder.ToString(),
                segmentList.ToArray(),
                siblingList.ToArray()
            );
        }

        /// <summary>
        /// Resolves a <see cref="SceneObjectIdentifier"/> back to a live GameObject in the scene.
        /// Finds inactive GameObjects and handles duplicate names via sibling indices.
        /// </summary>
        public static GameObject Resolve(SceneObjectIdentifier id)
        {
            if (id == null)
            {
                return null;
            }

            // Strategy 1: GlobalObjectId lookup (Fast Path)
            if (!string.IsNullOrEmpty(id.GlobalObjectIdString) &&
                GlobalObjectId.TryParse(id.GlobalObjectIdString, out var gid))
            {
                try
                {
                    var resolvedObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
                    if (resolvedObj != null)
                    {
                        if (resolvedObj is GameObject resolvedGo)
                        {
                            return resolvedGo;
                        }
                        if (resolvedObj is Component resolvedComp)
                        {
                            var compGo = resolvedComp.gameObject;
                            if (compGo != null)
                            {
                                return compGo;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[HierarchyPathResolver] GlobalObjectId resolution failed: {ex.Message}");
                }
            }

            // Strategy 2: Scene Root Traversal (Supports Inactive GameObjects and Sibling Disambiguation)
            string[] pathSegments = id.PathSegments;
            if (pathSegments == null || pathSegments.Length == 0)
            {
                if (string.IsNullOrEmpty(id.HierarchyPath))
                {
                    return null;
                }
                pathSegments = id.HierarchyPath.Split('/');
            }

            if (pathSegments.Length == 0)
            {
                return null;
            }

            // 2.1: Attempt to match by ScenePath if available
            if (!string.IsNullOrEmpty(id.ScenePath))
            {
                var sceneByPath = SceneManager.GetSceneByPath(id.ScenePath);
                if (sceneByPath.IsValid() && sceneByPath.isLoaded)
                {
                    var resolved = ResolveInScene(sceneByPath, pathSegments, id.SiblingIndices);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            // 2.2: Attempt to resolve within target SceneName
            Scene targetScene = default;
            if (!string.IsNullOrEmpty(id.SceneName))
            {
                targetScene = SceneManager.GetSceneByName(id.SceneName);
                if (targetScene.IsValid() && targetScene.isLoaded)
                {
                    var resolved = ResolveInScene(targetScene, pathSegments, id.SiblingIndices);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            // 2.3: Fallback to Active scene
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded && activeScene != targetScene)
            {
                var resolved = ResolveInScene(activeScene, pathSegments, id.SiblingIndices);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            // 2.4: Fallback across all loaded scenes
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && scene != targetScene && scene != activeScene)
                {
                    var resolved = ResolveInScene(scene, pathSegments, id.SiblingIndices);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
            }

            // Strategy 3: Global search fallback
            return FallbackNameSearch(pathSegments[pathSegments.Length - 1]);
        }

        private static GameObject ResolveInScene(Scene scene, string[] pathSegments, int[] siblingIndices)
        {
            var roots = scene.GetRootGameObjects();
            if (roots == null || roots.Length == 0)
            {
                return null;
            }

            GameObject current = null;
            string rootName = pathSegments[0];

            // 1. Try matching root by sibling index first
            if (siblingIndices != null && siblingIndices.Length > 0)
            {
                int rootIndex = siblingIndices[0];
                if (rootIndex >= 0 && rootIndex < roots.Length)
                {
                    var candidate = roots[rootIndex];
                    if (candidate != null && candidate.name == rootName)
                    {
                        current = candidate;
                    }
                }
            }

            // 2. Linear scan of roots if sibling index didn't match
            if (current == null)
            {
                for (int i = 0; i < roots.Length; i++)
                {
                    var candidate = roots[i];
                    if (candidate != null && candidate.name == rootName)
                    {
                        current = candidate;
                        break;
                    }
                }
            }

            if (current == null)
            {
                return null;
            }

            // 3. Traverse hierarchy down to leaf
            for (int segIdx = 1; segIdx < pathSegments.Length; segIdx++)
            {
                string targetSegmentName = pathSegments[segIdx];
                Transform childTransform = null;

                // Try sibling index
                if (siblingIndices != null && segIdx < siblingIndices.Length)
                {
                    int siblingIdx = siblingIndices[segIdx];
                    if (siblingIdx >= 0 && siblingIdx < current.transform.childCount)
                    {
                        var candidate = current.transform.GetChild(siblingIdx);
                        if (candidate != null && candidate.name == targetSegmentName)
                        {
                            childTransform = candidate;
                        }
                    }
                }

                // Fallback child scan
                if (childTransform == null)
                {
                    int childCount = current.transform.childCount;
                    for (int c = 0; c < childCount; c++)
                    {
                        var candidate = current.transform.GetChild(c);
                        if (candidate != null && candidate.name == targetSegmentName)
                        {
                            childTransform = candidate;
                            break;
                        }
                    }
                }

                if (childTransform == null)
                {
                    return null;
                }

                current = childTransform.gameObject;
            }

            return current;
        }

        private static GameObject FallbackNameSearch(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            try
            {
                var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                if (allGameObjects != null && allGameObjects.Length > 0)
                {
                    for (int i = 0; i < allGameObjects.Length; i++)
                    {
                        var go = allGameObjects[i];
                        if (go != null && !EditorUtility.IsPersistent(go) && go.name == targetName)
                        {
                            return go;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HierarchyPathResolver] Fallback search encountered error: {ex.Message}");
            }

            return null;
        }
    }
}
#endif
