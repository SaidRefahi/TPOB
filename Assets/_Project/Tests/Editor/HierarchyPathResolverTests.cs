#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Editor.Core.Lifecycle;
using Object = UnityEngine.Object;

namespace Game.Editor.Tests
{
    /// <summary>
    /// Comprehensive test suite for HierarchyPathResolver.
    /// Tests inactive GameObjects, sibling disambiguation, malformed inputs, unloaded scenes, and 3-tier fallbacks.
    /// Complies strictly with AGENTS.md, AGY0001, AGY0002, AGY0003, and UNT0007/8/23.
    /// </summary>
    [TestFixture]
    public sealed class HierarchyPathResolverTests
    {
        private HierarchyPathResolver _resolver;
        private readonly List<GameObject> _createdGameObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _resolver = HierarchyPathResolver.Default;
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy all dynamically created GameObjects cleanly
            for (int i = _createdGameObjects.Count - 1; i >= 0; i--)
            {
                var go = _createdGameObjects[i];
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _createdGameObjects.Clear();
        }

        private GameObject CreateGameObject(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent.transform, false);
            }
            _createdGameObjects.Add(go);
            return go;
        }

        #region 1. Inactive GameObject Resolution Tests

        [Test]
        public void ResolveIdentifier_InactiveRootGameObject_ResolvesSuccessfully()
        {
            var root = CreateGameObject("InactiveRoot");
            root.SetActive(false);

            Assert.IsFalse(root.activeSelf);
            Assert.IsFalse(root.activeInHierarchy);

            var id = _resolver.CaptureIdentifier(root);
            var resolved = _resolver.ResolveIdentifier(id);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(root, resolved);
            Assert.IsFalse(resolved.activeSelf);
        }

        [Test]
        public void ResolveIdentifier_InactiveChildGameObject_ResolvesSuccessfully()
        {
            var parent = CreateGameObject("ActiveParent");
            var child = CreateGameObject("InactiveChild", parent);
            child.SetActive(false);

            Assert.IsTrue(parent.activeSelf);
            Assert.IsFalse(child.activeSelf);
            Assert.IsFalse(child.activeInHierarchy);

            var id = _resolver.CaptureIdentifier(child);
            var resolved = _resolver.ResolveIdentifier(id);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(child, resolved);
        }

        [Test]
        public void ResolveIdentifier_DeeplyInactiveHierarchy_ResolvesSuccessfully()
        {
            var grandparent = CreateGameObject("InactiveGrandparent");
            var parent = CreateGameObject("Parent", grandparent);
            var child = CreateGameObject("Child", parent);

            grandparent.SetActive(false);

            // Parent and Child are activeSelf, but inactiveInHierarchy
            Assert.IsFalse(grandparent.activeInHierarchy);
            Assert.IsFalse(parent.activeInHierarchy);
            Assert.IsFalse(child.activeInHierarchy);
            Assert.IsTrue(child.activeSelf);

            var id = _resolver.CaptureIdentifier(child);
            var resolved = _resolver.ResolveIdentifier(id);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(child, resolved);
        }

        [Test]
        public void ResolveIdentifier_InactiveLeafUnderInactiveParent_ResolvesSuccessfully()
        {
            var parent = CreateGameObject("InactiveParent");
            var child = CreateGameObject("InactiveChildLeaf", parent);

            parent.SetActive(false);
            child.SetActive(false);

            Assert.IsFalse(parent.activeSelf);
            Assert.IsFalse(child.activeSelf);

            var id = _resolver.CaptureIdentifier(child);
            var resolved = _resolver.ResolveIdentifier(id);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(child, resolved);
        }

        #endregion

        #region 2. Identical Sibling Name Disambiguation Tests

        [Test]
        public void Disambiguation_DuplicateRootNames_ResolvesCorrectInstanceBySiblingIndex()
        {
            var root0 = CreateGameObject("DuplicateRoot");
            var root1 = CreateGameObject("DuplicateRoot");

            var id0 = _resolver.CaptureIdentifier(root0);
            var id1 = _resolver.CaptureIdentifier(root1);

            Assert.AreEqual("DuplicateRoot", id0.HierarchyPath);
            Assert.AreEqual("DuplicateRoot", id1.HierarchyPath);
            Assert.AreNotEqual(id0.SiblingIndices[0], id1.SiblingIndices[0]);

            var resolved0 = _resolver.ResolveIdentifier(id0);
            var resolved1 = _resolver.ResolveIdentifier(id1);

            Assert.IsNotNull(resolved0);
            Assert.IsNotNull(resolved1);
            Assert.AreEqual(root0, resolved0);
            Assert.AreEqual(root1, resolved1);
            Assert.AreNotEqual(resolved0, resolved1);
        }

        [Test]
        public void Disambiguation_DuplicateChildNames_ResolvesCorrectChildBySiblingIndex()
        {
            var parent = CreateGameObject("ContainerParent");
            var child0 = CreateGameObject("ItemSlot", parent);
            var child1 = CreateGameObject("ItemSlot", parent);

            child0.transform.SetSiblingIndex(0);
            child1.transform.SetSiblingIndex(1);

            var id0 = _resolver.CaptureIdentifier(child0);
            var id1 = _resolver.CaptureIdentifier(child1);

            Assert.AreEqual("ContainerParent/ItemSlot", id0.HierarchyPath);
            Assert.AreEqual("ContainerParent/ItemSlot", id1.HierarchyPath);
            Assert.AreEqual(0, id0.SiblingIndices[1]);
            Assert.AreEqual(1, id1.SiblingIndices[1]);

            var resolved0 = _resolver.ResolveIdentifier(id0);
            var resolved1 = _resolver.ResolveIdentifier(id1);

            Assert.IsNotNull(resolved0);
            Assert.IsNotNull(resolved1);
            Assert.AreEqual(child0, resolved0);
            Assert.AreEqual(child1, resolved1);
            Assert.AreNotEqual(resolved0, resolved1);
        }

        [Test]
        public void Disambiguation_MultiLevelDuplicateNames_ResolvesCorrectLeaf()
        {
            var group0 = CreateGameObject("Group");
            var leaf0 = CreateGameObject("Leaf", group0);

            var group1 = CreateGameObject("Group");
            var leaf1 = CreateGameObject("Leaf", group1);

            var id0 = _resolver.CaptureIdentifier(leaf0);
            var id1 = _resolver.CaptureIdentifier(leaf1);

            Assert.AreEqual("Group/Leaf", id0.HierarchyPath);
            Assert.AreEqual("Group/Leaf", id1.HierarchyPath);

            var resolved0 = _resolver.ResolveIdentifier(id0);
            var resolved1 = _resolver.ResolveIdentifier(id1);

            Assert.IsNotNull(resolved0);
            Assert.IsNotNull(resolved1);
            Assert.AreEqual(leaf0, resolved0);
            Assert.AreEqual(leaf1, resolved1);
        }

        #endregion

        #region 3. Null, Empty, and Malformed Input Tests

        [Test]
        public void ResolveIdentifier_NullIdentifier_ReturnsNull()
        {
            var resolved = _resolver.ResolveIdentifier(null);
            Assert.IsNull(resolved);
        }

        [Test]
        public void ResolveIdentifier_DefaultEmptyIdentifier_ReturnsNull()
        {
            var id = new SceneObjectIdentifier();
            var resolved = _resolver.ResolveIdentifier(id);
            Assert.IsNull(resolved);
        }

        [Test]
        public void ResolveIdentifier_EmptyHierarchyPathAndEmptySegments_ReturnsNull()
        {
            var id = new SceneObjectIdentifier(
                sceneName: "Main",
                scenePath: string.Empty,
                globalObjectIdString: string.Empty,
                hierarchyPath: string.Empty,
                pathSegments: Array.Empty<string>(),
                siblingIndices: Array.Empty<int>()
            );

            var resolved = _resolver.ResolveIdentifier(id);
            Assert.IsNull(resolved);
        }

        [Test]
        public void ResolveIdentifier_MalformedPathWithConsecutiveSlashes_HandlesGracefullyWithoutThrowing()
        {
            var id = new SceneObjectIdentifier(
                sceneName: "Main",
                scenePath: string.Empty,
                globalObjectIdString: string.Empty,
                hierarchyPath: "///Root//Child///",
                pathSegments: null,
                siblingIndices: new[] { 0, 0 }
            );

            Assert.DoesNotThrow(() =>
            {
                var resolved = _resolver.ResolveIdentifier(id);
                // Non-matching path returns null safely
                Assert.IsNull(resolved);
            });
        }

        [Test]
        public void ResolveIdentifier_SiblingIndexOutOfBounds_FallsBackToNameMatching()
        {
            var parent = CreateGameObject("ParentWithOneChild");
            var child = CreateGameObject("SoloChild", parent);

            // Specify out-of-bounds sibling index 999
            var id = new SceneObjectIdentifier(
                sceneName: child.scene.name,
                scenePath: child.scene.path,
                globalObjectIdString: string.Empty,
                hierarchyPath: "ParentWithOneChild/SoloChild",
                pathSegments: new[] { "ParentWithOneChild", "SoloChild" },
                siblingIndices: new[] { 0, 999 }
            );

            var resolved = _resolver.ResolveIdentifier(id);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(child, resolved);
        }

        #endregion

        #region 4. Missing and Unloaded Scene Tests

        [Test]
        public void ResolveIdentifier_UnloadedOrMissingScene_FallsBackToActiveScene()
        {
            var root = CreateGameObject("ActiveSceneTarget");

            // Point to a non-existent or unloaded scene name and path
            var id = new SceneObjectIdentifier(
                sceneName: "Scene_NonExistent_9999",
                scenePath: "Assets/Scenes/DoesNotExist.unity",
                globalObjectIdString: string.Empty,
                hierarchyPath: "ActiveSceneTarget",
                pathSegments: new[] { "ActiveSceneTarget" },
                siblingIndices: new[] { root.transform.GetSiblingIndex() }
            );

            var resolved = _resolver.ResolveIdentifier(id);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(root, resolved);
        }

        [Test]
        public void ResolveIdentifier_NonExistentObjectAcrossAllScenes_ReturnsNullSafely()
        {
            var id = new SceneObjectIdentifier(
                sceneName: "AnyScene",
                scenePath: string.Empty,
                globalObjectIdString: string.Empty,
                hierarchyPath: "GhostHierarchy/GhostObject_404",
                pathSegments: new[] { "GhostHierarchy", "GhostObject_404" },
                siblingIndices: new[] { 99, 99 }
            );

            var resolved = _resolver.ResolveIdentifier(id);
            Assert.IsNull(resolved);
        }

        #endregion

        #region 5. Multi-Tier Fallback Mechanism Tests

        [Test]
        public void Fallback_GlobalObjectIdStrategy_ResolvesFastPathEvenIfPathIsCorrupted()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/GrabbableBox.prefab");
            Assert.IsNotNull(prefab, "Prefab must exist to test persistent GlobalObjectId fast path.");

            var captured = _resolver.CaptureIdentifier(prefab);
            Assert.IsFalse(string.IsNullOrEmpty(captured.GlobalObjectIdString));

            // Corrupt hierarchy path but keep GlobalObjectIdString intact
            var corruptedId = new SceneObjectIdentifier(
                sceneName: captured.SceneName,
                scenePath: captured.ScenePath,
                globalObjectIdString: captured.GlobalObjectIdString,
                hierarchyPath: "TotallyWrong/Path/Corrupted",
                pathSegments: new[] { "TotallyWrong", "Path", "Corrupted" },
                siblingIndices: new[] { 99, 99, 99 }
            );

            var resolved = _resolver.ResolveIdentifier(corruptedId);

            // Fast path (Strategy 1) resolves directly via GlobalObjectId
            Assert.IsNotNull(resolved);
            Assert.AreEqual(prefab, resolved);
        }

        [Test]
        public void Fallback_NameSearchStrategy_ResolvesWhenHierarchyWasRestructured()
        {
            string uniqueLeafName = "UniqueMovedLeaf_" + Guid.NewGuid().ToString("N");
            var originalParent = CreateGameObject("OriginalFolder");
            var leaf = CreateGameObject(uniqueLeafName, originalParent);

            // Simulating an identifier recorded before restructuring:
            // Empty GlobalObjectIdString and old parent path "OldFolder/UniqueMovedLeaf_..."
            var id = new SceneObjectIdentifier(
                sceneName: leaf.scene.name,
                scenePath: leaf.scene.path,
                globalObjectIdString: string.Empty,
                hierarchyPath: "OldFolder/" + uniqueLeafName,
                pathSegments: new[] { "OldFolder", uniqueLeafName },
                siblingIndices: new[] { 0, 0 }
            );

            // Strategy 1 (empty GID) and Strategy 2 (wrong parent) will fail;
            // Strategy 3 (FallbackNameSearch) finds leaf by its unique name
            var resolved = _resolver.ResolveIdentifier(id);

            Assert.IsNotNull(resolved);
            Assert.AreEqual(leaf, resolved);
        }

        [Test]
        public void BuildIdentifier_NullGameObject_ReturnsDefaultIdentifierWithoutThrowing()
        {
            var id = HierarchyPathResolver.BuildIdentifier(null);
            Assert.IsNotNull(id);
            Assert.AreEqual(string.Empty, id.HierarchyPath);
            Assert.AreEqual(0, id.PathSegments.Length);
            Assert.AreEqual(0, id.SiblingIndices.Length);
        }

        #endregion
    }
}
#endif
