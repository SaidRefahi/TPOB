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
    /// Utility service responsible for capturing and resolving GameObject identifiers.
    /// Supports active and inactive GameObjects, disambiguates sibling duplicates using SiblingIndices,
    /// and prioritizes GlobalObjectId resolution across scene reloads.
    /// Complies strictly with AGY0001, AGY0002, AGY0003, and UNT0007/8/23.
    /// </summary>
    public static class HierarchyPathResolver
    {
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
            var siblingList = new List<int>();

            siblingList.Add(go.transform.GetSiblingIndex());

            var currentParent = go.transform.parent;
            while (currentParent != null)
            {
                pathBuilder.Insert(0, "/").Insert(0, currentParent.name);
                siblingList.Insert(0, currentParent.GetSiblingIndex());
                currentParent = currentParent.parent;
            }

            return new SceneObjectIdentifier(
                sceneName,
                globalIdString,
                pathBuilder.ToString(),
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

            // Strategy 1: GlobalObjectId lookup
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
            if (string.IsNullOrEmpty(id.HierarchyPath))
            {
                return null;
            }

            var pathSegments = id.HierarchyPath.Split('/');
            if (pathSegments.Length == 0)
            {
                return null;
            }

            // Attempt to resolve within the target scene first
            Scene targetScene = default;
            if (!string.IsNullOrEmpty(id.SceneName))
            {
                targetScene = SceneManager.GetSceneByName(id.SceneName);
            }

            if (targetScene.IsValid() && targetScene.isLoaded)
            {
                var resolved = ResolveInScene(targetScene, pathSegments, id.SiblingIndices);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            // Fallback: Active scene
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene != targetScene)
            {
                var resolved = ResolveInScene(activeScene, pathSegments, id.SiblingIndices);
                if (resolved != null)
                {
                    return resolved;
                }
            }

            // Fallback: Check all other loaded scenes
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
                for (int i = 0; i < allGameObjects.Length; i++)
                {
                    var go = allGameObjects[i];
                    if (go != null && !EditorUtility.IsPersistent(go) && go.name == targetName)
                    {
                        return go;
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
