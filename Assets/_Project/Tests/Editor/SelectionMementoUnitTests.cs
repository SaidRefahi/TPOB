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
    /// Unit test suite for SelectionMemento, SessionStateSelectionPersistence, and SelectionMementoService.
    /// Complies strictly with AGENTS.md, AGY0001, AGY0002, AGY0003, and UNT0007/8/23.
    /// </summary>
    [TestFixture]
    public sealed class SelectionMementoUnitTests
    {
        private string _testSessionKey;
        private SessionStateSelectionPersistence _persistence;
        private InspectorSanitizerSpy _sanitizerSpy;
        private SelectionMementoService _service;
        private readonly List<GameObject> _createdGameObjects = new List<GameObject>();

        private Object[] _savedSelectionObjects;
        private Object _savedActiveObject;

        [SetUp]
        public void SetUp()
        {
            _savedSelectionObjects = Selection.objects;
            _savedActiveObject = Selection.activeObject;

            _testSessionKey = "TPOB.Tests.SelectionMemento." + Guid.NewGuid().ToString("N");
            _persistence = new SessionStateSelectionPersistence(_testSessionKey);
            _sanitizerSpy = new InspectorSanitizerSpy();
            _service = new SelectionMementoService(_persistence, _sanitizerSpy, HierarchyPathResolver.Default);
        }

        [TearDown]
        public void TearDown()
        {
            // 1. Restore editor selection state
            Selection.objects = _savedSelectionObjects != null ? _savedSelectionObjects : Array.Empty<Object>();
            Selection.activeObject = _savedActiveObject;

            // 2. Erase isolated test persistence key
            if (_persistence != null)
            {
                _persistence.ClearMemento();
            }

            // 3. Destroy temporary test GameObjects cleanly
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

        #region 1. JSON Serialization Round-Trip Tests

        [Test]
        public void JsonSerialization_PopulatedMemento_RoundTripMatchesExactValues()
        {
            var sceneObjA = new SceneObjectIdentifier(
                sceneName: "MainScene",
                scenePath: "Assets/Scenes/MainScene.unity",
                globalObjectIdString: "GlobalObjectId_001",
                hierarchyPath: "Environment/Obstacle",
                pathSegments: new[] { "Environment", "Obstacle" },
                siblingIndices: new[] { 0, 3 }
            );

            var sceneObjB = new SceneObjectIdentifier(
                sceneName: "MainScene",
                scenePath: "Assets/Scenes/MainScene.unity",
                globalObjectIdString: "GlobalObjectId_002",
                hierarchyPath: "Environment/Door",
                pathSegments: new[] { "Environment", "Door" },
                siblingIndices: new[] { 0, 5 }
            );

            long expectedTimestamp = DateTime.UtcNow.Ticks;
            var originalMemento = new SelectionMemento(
                sceneObjects: new[] { sceneObjA, sceneObjB },
                assetGuids: new[] { "guid-asset-1", "guid-asset-2" },
                assetGlobalObjectIds: new[] { "asset-gid-1", "asset-gid-2" },
                activeSceneObject: sceneObjB,
                activeAssetGuid: "guid-asset-2",
                activeGlobalObjectId: "asset-gid-2",
                timestampTicks: expectedTimestamp
            );

            string json = JsonUtility.ToJson(originalMemento);
            Assert.IsFalse(string.IsNullOrEmpty(json), "Serialized JSON should not be empty.");

            var deserializedMemento = JsonUtility.FromJson<SelectionMemento>(json);
            Assert.IsNotNull(deserializedMemento, "Deserialized memento should not be null.");

            // Verify Collections
            Assert.AreEqual(2, deserializedMemento.SceneObjects.Length);
            Assert.AreEqual(2, deserializedMemento.AssetGuids.Length);
            Assert.AreEqual(2, deserializedMemento.AssetGlobalObjectIds.Length);

            // Verify Item Details
            Assert.AreEqual("Environment/Obstacle", deserializedMemento.SceneObjects[0].HierarchyPath);
            Assert.AreEqual("GlobalObjectId_001", deserializedMemento.SceneObjects[0].GlobalObjectIdString);
            Assert.AreEqual(2, deserializedMemento.SceneObjects[0].PathSegments.Length);
            Assert.AreEqual(3, deserializedMemento.SceneObjects[0].SiblingIndices[1]);

            Assert.AreEqual("guid-asset-1", deserializedMemento.AssetGuids[0]);
            Assert.AreEqual("guid-asset-2", deserializedMemento.AssetGuids[1]);

            // Verify Active References
            Assert.IsNotNull(deserializedMemento.ActiveSceneObject);
            Assert.AreEqual("Environment/Door", deserializedMemento.ActiveSceneObject.HierarchyPath);
            Assert.AreEqual("guid-asset-2", deserializedMemento.ActiveAssetGuid);
            Assert.AreEqual("asset-gid-2", deserializedMemento.ActiveGlobalObjectId);
            Assert.AreEqual(expectedTimestamp, deserializedMemento.TimestampTicks);
            Assert.IsFalse(deserializedMemento.IsEmpty);
        }

        [Test]
        public void JsonSerialization_EmptyMemento_PreservesEmptyState()
        {
            var emptyMemento = new SelectionMemento();
            string json = JsonUtility.ToJson(emptyMemento);

            var deserialized = JsonUtility.FromJson<SelectionMemento>(json);
            Assert.IsNotNull(deserialized);
            Assert.IsTrue(deserialized.IsEmpty);
            Assert.AreEqual(0, deserialized.SceneObjects.Length);
            Assert.AreEqual(0, deserialized.AssetGuids.Length);
            Assert.IsTrue(deserialized.ActiveSceneObject == null || string.IsNullOrEmpty(deserialized.ActiveSceneObject.HierarchyPath));
            Assert.AreEqual(string.Empty, deserialized.ActiveScenePath);
            Assert.AreEqual(string.Empty, deserialized.ActiveAssetGuid);
            Assert.AreEqual(string.Empty, deserialized.ActiveGlobalObjectId);
        }

        #endregion

        #region 2. SessionStateSelectionPersistence Tests

        [Test]
        public void Persistence_InitiallyEmpty_HasMementoIsFalse()
        {
            Assert.IsFalse(_persistence.HasMemento);
            var loaded = _persistence.LoadMemento();
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.IsEmpty);
        }

        [Test]
        public void Persistence_SaveMemento_SetsHasMementoTrueAndLoadsEquivalentData()
        {
            var sceneObj = new SceneObjectIdentifier("Scene", "gid", "Root/Item", new[] { 0, 1 });
            var memento = new SelectionMemento(
                sceneObjects: new[] { sceneObj },
                assetGuids: new[] { "asset-guid-test" },
                assetGlobalObjectIds: Array.Empty<string>(),
                activeSceneObject: sceneObj,
                activeAssetGuid: string.Empty,
                activeGlobalObjectId: string.Empty,
                timestampTicks: 123456789
            );

            _persistence.SaveMemento(memento);

            Assert.IsTrue(_persistence.HasMemento);
            var loaded = _persistence.LoadMemento();

            Assert.IsNotNull(loaded);
            Assert.IsFalse(loaded.IsEmpty);
            Assert.AreEqual(1, loaded.SceneObjects.Length);
            Assert.AreEqual("Root/Item", loaded.SceneObjects[0].HierarchyPath);
            Assert.AreEqual("asset-guid-test", loaded.AssetGuids[0]);
        }

        [Test]
        public void Persistence_ClearMemento_ErasesSessionState()
        {
            var sceneObj = new SceneObjectIdentifier("Scene", "gid", "Root/Item", new[] { 0 });
            var memento = new SelectionMemento(new[] { sceneObj }, Array.Empty<string>(), sceneObj, string.Empty, 100);

            _persistence.SaveMemento(memento);
            Assert.IsTrue(_persistence.HasMemento);

            _persistence.ClearMemento();
            Assert.IsFalse(_persistence.HasMemento);

            var loaded = _persistence.LoadMemento();
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.IsEmpty);
        }

        [Test]
        public void Persistence_SaveNullMemento_ClearsSessionState()
        {
            var sceneObj = new SceneObjectIdentifier("Scene", "gid", "Root", new[] { 0 });
            _persistence.SaveMemento(new SelectionMemento(new[] { sceneObj }, Array.Empty<string>(), sceneObj, string.Empty, 100));
            Assert.IsTrue(_persistence.HasMemento);

            _persistence.SaveMemento(null);
            Assert.IsFalse(_persistence.HasMemento);
        }

        [Test]
        public void Persistence_SaveEmptyMemento_ClearsSessionState()
        {
            var sceneObj = new SceneObjectIdentifier("Scene", "gid", "Root", new[] { 0 });
            _persistence.SaveMemento(new SelectionMemento(new[] { sceneObj }, Array.Empty<string>(), sceneObj, string.Empty, 100));
            Assert.IsTrue(_persistence.HasMemento);

            _persistence.SaveMemento(SelectionMemento.Empty);
            Assert.IsFalse(_persistence.HasMemento);
        }

        #endregion

        #region 3. Selection Capture Tests

        [Test]
        public void CaptureCurrentSelection_ActiveAndMultiSelection_CapturesCorrectSceneIdentifiers()
        {
            var root = CreateGameObject("TestRoot");
            var childA = CreateGameObject("ChildA", root);
            var childB = CreateGameObject("ChildB", root);

            Selection.objects = new Object[] { childB, childA };

            var memento = _service.CaptureCurrentSelection();

            Assert.IsNotNull(memento);
            Assert.IsFalse(memento.IsEmpty);
            Assert.AreEqual(2, memento.SceneObjects.Length);

            Assert.IsNotNull(memento.ActiveSceneObject);
            Assert.AreEqual("TestRoot/ChildB", memento.ActiveSceneObject.HierarchyPath);

            var path0 = memento.SceneObjects[0].HierarchyPath;
            var path1 = memento.SceneObjects[1].HierarchyPath;
            Assert.IsTrue((path0 == "TestRoot/ChildA" && path1 == "TestRoot/ChildB") ||
                          (path0 == "TestRoot/ChildB" && path1 == "TestRoot/ChildA"));
        }

        [Test]
        public void CaptureCurrentSelection_WhenSelectionEmpty_ReturnsEmptyMemento()
        {
            Selection.objects = Array.Empty<Object>();
            Selection.activeGameObject = null;

            var memento = _service.CaptureCurrentSelection();

            Assert.IsNotNull(memento);
            Assert.IsTrue(memento.IsEmpty);
            Assert.AreEqual(0, memento.SceneObjects.Length);
            Assert.AreEqual(0, memento.AssetGuids.Length);
        }

        [Test]
        public void CaptureCurrentSelection_WithComponentSelected_CapturesParentGameObject()
        {
            var root = CreateGameObject("ComponentRoot");
            var boxCollider = root.AddComponent<BoxCollider>();

            Selection.objects = new Object[] { boxCollider };
            Selection.activeObject = boxCollider;

            var memento = _service.CaptureCurrentSelection();

            Assert.IsNotNull(memento);
            Assert.AreEqual(1, memento.SceneObjects.Length);
            Assert.AreEqual("ComponentRoot", memento.SceneObjects[0].HierarchyPath);
        }

        #endregion

        #region 4. Selection Restoration & Clean Clearing Tests

        [Test]
        public void ClearSelectionSafely_EmptiesSelectionAndPurgesInspectors()
        {
            var root = CreateGameObject("TargetToClear");
            Selection.objects = new Object[] { root };
            Selection.activeGameObject = root;

            _service.ClearSelectionSafely();

            Assert.AreEqual(0, Selection.objects.Length);
            Assert.IsNull(Selection.activeObject);
            Assert.IsNull(Selection.activeGameObject);
            Assert.AreEqual(1, _sanitizerSpy.PurgeAllInspectorsCallCount);
        }

        [Test]
        public void RestoreSelection_NullOrEmptyMemento_ExecutesSafelyWithoutException()
        {
            Assert.DoesNotThrow(() => _service.RestoreSelection(null));
            Assert.DoesNotThrow(() => _service.RestoreSelection(SelectionMemento.Empty));
            Assert.AreEqual(0, _sanitizerSpy.PurgeSharedTrackerCallCount);
        }

        [Test]
        public void RestoreSelection_SceneGameObjects_RestoresActiveAndMultiSelection()
        {
            var root = CreateGameObject("RestoreRoot");
            var childA = CreateGameObject("ChildA", root);
            var childB = CreateGameObject("ChildB", root);

            Selection.objects = new Object[] { childB, childA };

            var memento = _service.CaptureCurrentSelection();
            _service.ClearSelectionSafely();
            Assert.AreEqual(0, Selection.objects.Length);

            _service.RestoreSelection(memento);

            Assert.AreEqual(2, Selection.objects.Length);
            Assert.AreEqual(childB, Selection.activeGameObject);
            Assert.Contains(childA, Selection.objects);
            Assert.Contains(childB, Selection.objects);
            Assert.AreEqual(1, _sanitizerSpy.PurgeSharedTrackerCallCount);
        }

        [Test]
        public void RestoreFromPersistence_RestoresAndClearsPersistentStore()
        {
            var root = CreateGameObject("PersistenceRoot");
            Selection.objects = new Object[] { root };
            Selection.activeGameObject = root;

            var memento = _service.CaptureCurrentSelection();
            _persistence.SaveMemento(memento);
            _service.ClearSelectionSafely();

            Assert.IsTrue(_service.HasPersistedMemento);

            _service.RestoreFromPersistence();

            Assert.AreEqual(1, Selection.objects.Length);
            Assert.AreEqual(root, Selection.activeGameObject);
            Assert.IsFalse(_service.HasPersistedMemento);
            Assert.IsFalse(_persistence.HasMemento);
        }

        #endregion

        #region 5. Backward Compatibility Tests

        [Test]
        public void BackwardCompatibility_ScenePathsAndAssetGUIDs_ExposeExpectedArrays()
        {
            var objA = new SceneObjectIdentifier("Scene", "gid1", "Root/ItemA", new[] { 0, 0 });
            var objB = new SceneObjectIdentifier("Scene", "gid2", "Root/ItemB", new[] { 0, 1 });

            var memento = new SelectionMemento(
                sceneObjects: new[] { objA, objB },
                assetGuids: new[] { "guid-alpha", "guid-beta" },
                activeSceneObject: objB,
                activeAssetGuid: "guid-beta",
                timestampTicks: 999
            );

            // Backward compatible ScenePaths
            var scenePaths = memento.ScenePaths;
            Assert.AreEqual(2, scenePaths.Length);
            Assert.AreEqual("Root/ItemA", scenePaths[0]);
            Assert.AreEqual("Root/ItemB", scenePaths[1]);

            // Backward compatible AssetGUIDs alias
            var assetGuids = memento.AssetGUIDs;
            Assert.AreEqual(2, assetGuids.Length);
            Assert.AreEqual("guid-alpha", assetGuids[0]);
            Assert.AreEqual("guid-beta", assetGuids[1]);

            // Backward compatible ActiveScenePath
            Assert.AreEqual("Root/ItemB", memento.ActiveScenePath);
        }

        [Test]
        public void BackwardCompatibility_LegacyConstructor_BuildsValidIdentifiers()
        {
            string[] legacyPaths = { "World/Player", "World/Enemy" };
            string[] legacyGuids = { "guid-1", "guid-2" };
            string legacyActive = "World/Player";

            var memento = new SelectionMemento(legacyPaths, legacyGuids, legacyActive);

            Assert.IsFalse(memento.IsEmpty);
            Assert.AreEqual(2, memento.SceneObjects.Length);
            Assert.AreEqual("World/Player", memento.SceneObjects[0].HierarchyPath);
            Assert.AreEqual("World/Enemy", memento.SceneObjects[1].HierarchyPath);
            Assert.AreEqual("World/Player", memento.ActiveScenePath);
            Assert.AreEqual(2, memento.AssetGUIDs.Length);
        }

        [Test]
        public void BackwardCompatibility_LegacyCaptureSelectionAlias_ReturnsIdenticalResult()
        {
            var root = CreateGameObject("LegacyCaptureRoot");
            Selection.objects = new Object[] { root };
            Selection.activeGameObject = root;

            var mementoCurrent = _service.CaptureCurrentSelection();
            var mementoLegacy = _service.CaptureSelection();

            Assert.AreEqual(mementoCurrent.SceneObjects.Length, mementoLegacy.SceneObjects.Length);
            Assert.AreEqual(mementoCurrent.ActiveScenePath, mementoLegacy.ActiveScenePath);
        }

        #endregion

        #region Test Doubles

        private sealed class InspectorSanitizerSpy : IInspectorSanitizerService
        {
            public int PurgeAllInspectorsCallCount { get; private set; }
            public int PurgeSharedTrackerCallCount { get; private set; }
            public int DestroyOrphanAndSceneEditorsCallCount { get; private set; }
            public int UnlockAllInspectorsCallCount { get; private set; }

            public void PurgeAllInspectors() => PurgeAllInspectorsCallCount++;
            public void PurgeSharedTracker() => PurgeSharedTrackerCallCount++;
            public void DestroyOrphanAndSceneEditors() => DestroyOrphanAndSceneEditorsCallCount++;
            public void UnlockAllInspectors() => UnlockAllInspectorsCallCount++;
        }

        #endregion
    }
}
#endif
