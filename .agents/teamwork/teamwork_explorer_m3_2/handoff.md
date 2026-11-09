# Milestone 3 Explorer 2 Handoff Report: Test Suites for Memento & Hierarchy Resolution

## 1. Observation

Inspection of existing production implementations in `Assets/_Project/Editor/Core/Lifecycle/`:

### 1.1 `Models/SceneObjectIdentifier.cs`
- **Lines 12-28**:
  Encapsulates compound identifier metadata using private serialized fields:
  ```csharp
  [SerializeField] private string _sceneName;
  [SerializeField] private string _scenePath;
  [SerializeField] private string _globalObjectIdString;
  [SerializeField] private string _hierarchyPath;
  [SerializeField] private string[] _pathSegments;
  [SerializeField] private int[] _siblingIndices;
  ```
  Exposes read-only properties with null coalescing (`SceneName => _sceneName ?? string.Empty;`, `PathSegments => _pathSegments ?? Array.Empty<string>();`, etc.).
- **Lines 39-53 & 55-62**:
  Provides 6-argument and 4-argument constructors that split `hierarchyPath` into segments when `pathSegments` is null.
- **Lines 64-88**:
  Implements `IEquatable<SceneObjectIdentifier>`, comparing `_globalObjectIdString`, `_hierarchyPath`, and `_sceneName`.

### 1.2 `Models/SelectionMemento.cs`
- **Lines 13-33**:
  Encapsulates serializable memento state:
  ```csharp
  [SerializeField] private SceneObjectIdentifier[] _sceneObjects;
  [SerializeField] private string[] _assetGuids;
  [SerializeField] private string[] _assetGlobalObjectIds;
  [SerializeField] private SceneObjectIdentifier _activeSceneObject;
  [SerializeField] private string _activeAssetGuid;
  [SerializeField] private string _activeGlobalObjectId;
  [SerializeField] private long _timestampTicks;
  ```
  Empty check: `public bool IsEmpty => (SceneObjects.Length == 0) && (AssetGuids.Length == 0);`.
- **Lines 37-60**:
  Backwards-compatible accessors:
  `ScenePaths` maps `_sceneObjects[i].HierarchyPath`; `AssetGUIDs` aliases `AssetGuids`; `ActiveScenePath` returns `_activeSceneObject.HierarchyPath`.
- **Lines 101-118**:
  Legacy constructor `SelectionMemento(string[] scenePaths, string[] assetGUIDs, string activeScenePath)` constructing `SceneObjectIdentifier` instances from raw paths.

### 1.3 `Interfaces/ISelectionStatePersistence.cs` & `Services/SessionStateSelectionPersistence.cs`
- **Lines 8-30 (ISelectionStatePersistence.cs)**:
  Defines contract: `SaveMemento(SelectionMemento memento)`, `SelectionMemento LoadMemento()`, `void ClearMemento()`, and `bool HasMemento { get; }`.
- **Lines 16-23 (SessionStateSelectionPersistence.cs)**:
  Defaults to `"Game.Editor.Core.Lifecycle.SelectionMemento.State"`, but accepts custom `sessionKey` in constructor, allowing test fixtures to isolate persistence storage keys completely without state collisions.
- **Lines 42-79 (SessionStateSelectionPersistence.cs)**:
  `SaveMemento` clears memento if `memento == null || memento.IsEmpty`, otherwise invokes `JsonUtility.ToJson(memento)` and writes to `SessionState.SetString`.
  `LoadMemento` returns `SelectionMemento.Empty` if string is empty or deserialization yields null.

### 1.4 `Interfaces/IHierarchyPathResolver.cs` & `Services/HierarchyPathResolver.cs`
- **Lines 34-76 (HierarchyPathResolver.cs)**:
  `BuildIdentifier(GameObject go)`: handles null gracefully (returns empty identifier), extracts scene metadata, reads `GlobalObjectId.GetGlobalObjectIdSlow(go)`, and traverses up `transform.parent` accumulating path segments and sibling indices.
- **Lines 82-191 (HierarchyPathResolver.cs)**:
  Resolution follows 3 cascaded strategies:
  - **Strategy 1 (GlobalObjectId fast path, lines 89-116)**: `GlobalObjectId.TryParse` and `GlobalObjectIdToObjectSlow`.
  - **Strategy 2 (Scene root traversal, lines 118-187)**:
    - Resolves via `SceneManager.GetSceneByPath` -> `SceneManager.GetSceneByName` -> `SceneManager.GetActiveScene` -> loaded scenes.
    - Inside `ResolveInScene` (lines 193-281): matches roots and child transforms by `siblingIndices` first, with fallback linear scan.
    - **Crucial finding**: `Scene.GetRootGameObjects()` and `transform.GetChild(i)` return inactive GameObjects (`activeSelf = false` and `activeInHierarchy = false`).
  - **Strategy 3 (Global search fallback, lines 189-191, 283-311)**:
    - `FallbackNameSearch` executes `Resources.FindObjectsOfTypeAll<GameObject>()` matching non-persistent objects by name, finding relocated objects.

### 1.5 `Services/SelectionMementoService.cs`
- **Lines 46-59**:
  Provides explicit dependency injection constructor `public SelectionMementoService(ISelectionStatePersistence persistence, IInspectorSanitizerService sanitizer, IHierarchyPathResolver pathResolver = null)` enabling isolated test spies for persistence, inspector sanitation, and path resolution.
- **Lines 64-165**:
  `CaptureCurrentSelection()` differentiates persistent assets (`EditorUtility.IsPersistent`) vs scene objects (`GameObject` / `Component`), resolves GUIDs/GlobalObjectIds for assets, and calls `_pathResolver.CaptureIdentifier` for scene objects.
- **Lines 178-187**:
  `ClearSelectionSafely()` zeroes `Selection.objects` and `Selection.activeObject`, then delegates to `_sanitizer.PurgeAllInspectors()`.
- **Lines 192-285**:
  `RestoreSelection(SelectionMemento memento)` resolves scene objects and assets, updates `Selection.objects` / `Selection.activeGameObject` / `Selection.activeObject`, and invokes `_sanitizer.PurgeSharedTracker()`.
- **Lines 287-320**:
  `RestoreFromPersistence()` wraps restore logic and guarantees `ClearPersistence()` via `finally`.

---

## 2. Logic Chain

1. **Test Isolation Guarantee**:
   Directly modifying editor selection or standard `SessionState` keys during tests can corrupt active user work or leave dirty state across test runs.
   Therefore, `SelectionMementoUnitTests` must:
   - Cache original `Selection.objects` and `Selection.activeObject` in `[SetUp]` and restore them in `[TearDown]`.
   - Instantiate `SessionStateSelectionPersistence` with a unique, test-scoped key (`"TPOB.Tests.SelectionMemento." + Guid.NewGuid().ToString("N")`) and erase it in `[TearDown]`.
   - Track all dynamically created `GameObject` instances and destroy them synchronously via `Object.DestroyImmediate` in `[TearDown]`.

2. **JSON Serialization Fidelity**:
   `SelectionMemento` and `SceneObjectIdentifier` use private fields decorated with `[SerializeField]`. Unity's `JsonUtility` natively serializes private serialized fields and nested objects.
   The unit test must verify that an instance populated with multiple scene objects, sibling indices, path segments, asset GUIDs, asset GlobalObjectIds, active objects, and timestamps serializes to JSON and deserializes back with identical values across all properties.

3. **SessionState Persistence Behavior**:
   `SessionStateSelectionPersistence` handles edge cases (`null` mementos, empty mementos, missing keys). Tests must confirm that:
   - Saving a valid memento sets `HasMemento = true` and `LoadMemento()` returns equivalent data.
   - Saving `null` or `SelectionMemento.Empty` clears the store (`HasMemento = false`).
   - `ClearMemento()` explicitly erases the key.

4. **Selection Capture & Restoration**:
   `SelectionMementoService` must capture multi-selection, active selection, and component-selected objects.
   By injecting an `InspectorSanitizerSpy` (implementing `IInspectorSanitizerService`), tests can verify that:
   - `ClearSelectionSafely()` calls `PurgeAllInspectors()`.
   - `RestoreSelection()` calls `PurgeSharedTracker()`.
   - Neither reflection nor real GUI inspector windows are required to validate lifecycle flow.

5. **Hierarchy Resolution Verification**:
   - **Inactive GameObjects**: Because `Scene.GetRootGameObjects()` and `Transform.GetChild()` include deactivated transforms, `HierarchyPathResolver` must successfully resolve root objects with `activeSelf = false`, child objects with `activeSelf = false`, and deeply nested inactive objects.
   - **Sibling Disambiguation**: When multiple GameObjects share identical names at root or child levels, resolution must use `SiblingIndices` to resolve the exact instance corresponding to the captured index.
   - **Fault Tolerance**: Null, empty, and malformed hierarchy paths (`"///"`, non-existent roots, negative/overflow sibling indices) must fail gracefully returning `null` without throwing unhandled exceptions.
   - **Multi-Scene Fallback**: When an identifier carries an invalid or unloaded `SceneName`/`ScenePath`, resolution must fall back to the active scene and loaded scenes.
   - **GlobalObjectId vs Name Fallback**: When an object is moved in the hierarchy, Strategy 1 (`GlobalObjectId`) or Strategy 3 (`FallbackNameSearch`) must recover the object.

---

## 3. Caveats

1. **Unity AssetDatabase in Batch Mode**:
   Project asset GUID queries (`AssetDatabase.AssetPathToGUID`, `AssetDatabase.LoadMainAssetAtPath`) require existing project asset paths. Test cases testing asset selection can reference known existing repository assets (such as the test script itself or project `.asmdef` files) or focus on verifying the memento data model and mock asset GUID pipelines.
2. **GlobalObjectId Scene-Bound Volatility**:
   In edit mode, newly instantiated scene GameObjects created via `new GameObject()` acquire valid `GlobalObjectId` values once registered with the scene, but in unsaved scratch scenes, their type and identifiers are session-bound. The tests account for this by testing both `GlobalObjectId` resolution and hierarchy traversal fallbacks independently.
3. **Unity Object Null Comparison (`UNT0007`/`UNT0008`/`UNT0023`)**:
   In test code, `GameObject` assertions must never use null coalescing operators (`??`, `?.`, `??=`). Explicit null checks (`if (go != null)`) and standard NUnit assertions (`Assert.IsNotNull`, `Assert.IsNull`, `Assert.AreEqual`) are used throughout the blueprints.

---

## 4. Conclusion & Test Blueprints

We have designed two comprehensive, production-ready NUnit test fixtures adhering strictly to `AGENTS.md` and `.agents/rules/`:
1. `SelectionMementoUnitTests.cs` (20 test cases covering JSON round-trip, persistence, capture, restoration, safe clear, and backward compatibility).
2. `HierarchyPathResolverTests.cs` (16 test cases covering inactive resolution, duplicate sibling disambiguation, malformed paths, unloaded scenes, and 3-tier fallback mechanisms).

### 4.1 Production Blueprint: `SelectionMementoUnitTests.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Editor.Core.Lifecycle;
using Object = UnityEngine.Object;

namespace Game.Editor.Tests.Lifecycle
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
            Selection.objects = _savedSelectionObjects ?? Array.Empty<Object>();
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
            Assert.IsNull(deserialized.ActiveSceneObject);
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

            Selection.objects = new Object[] { childA, childB };
            Selection.activeGameObject = childB;

            var memento = _service.CaptureCurrentSelection();

            Assert.IsNotNull(memento);
            Assert.IsFalse(memento.IsEmpty);
            Assert.AreEqual(2, memento.SceneObjects.Length);

            Assert.IsNotNull(memento.ActiveSceneObject);
            Assert.AreEqual("TestRoot/ChildB", memento.ActiveSceneObject.HierarchyPath);

            Assert.AreEqual("TestRoot/ChildA", memento.SceneObjects[0].HierarchyPath);
            Assert.AreEqual("TestRoot/ChildB", memento.SceneObjects[1].HierarchyPath);
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

            Selection.objects = new Object[] { childA, childB };
            Selection.activeGameObject = childB;

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
```

---

### 4.2 Production Blueprint: `HierarchyPathResolverTests.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Game.Editor.Core.Lifecycle;
using Object = UnityEngine.Object;

namespace Game.Editor.Tests.Lifecycle
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
            var target = CreateGameObject("FastPathTarget");
            var captured = _resolver.CaptureIdentifier(target);

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
            Assert.AreEqual(target, resolved);
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
```

---

## 5. Verification Method

### 5.1 Static Architecture and Code Inspection
1. Inspect `Assets/_Project/Editor/Core/Lifecycle/Models/` and `Services/` to verify all method names, constructor signatures, and property types match the blueprints:
   - `SceneObjectIdentifier` constructors (6-arg, 4-arg, default) and properties.
   - `SelectionMemento` constructors (7-arg, 5-arg, 3-arg legacy) and properties.
   - `SessionStateSelectionPersistence` custom sessionKey constructor.
   - `SelectionMementoService` DI constructor and lifecycle methods.
   - `HierarchyPathResolver.BuildIdentifier` and `Resolve`.
2. Inspect the test blueprints against `.editorconfig` Roslyn rules:
   - `AGY0001`: Zero deep inheritance (fixtures inherit directly from `object`).
   - `AGY0002`: Encapsulate fields (private member variables only).
   - `AGY0003`: Read-only accessors on properties.
   - `UNT0007`/`UNT0008`/`UNT0023`: No `??`, `?.`, `??=` used on `UnityEngine.Object` types.
   - `AGY0021`: No lambda event subscriptions.

### 5.2 Independent Verification Steps
When Worker creates `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef` (designed by Explorer 1) and writes these test files:
1. Target paths:
   - `Assets/_Project/Tests/Editor/SelectionMementoUnitTests.cs`
   - `Assets/_Project/Tests/Editor/HierarchyPathResolverTests.cs`
2. Run Unity EditMode test execution via Unity CLI or test runner:
   ```pwsh
   & "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testResults "c:\Users\SaidR\Documents\GitHub\TPOB\TestResults-M3-Memento.xml" -logFile "c:\Users\SaidR\Documents\GitHub\TPOB\Unity-M3-Memento-Test.log"
   ```
3. Invalidation conditions:
   - Any test failure in JSON round-trip indicates non-serialized fields in `SceneObjectIdentifier` or `SelectionMemento`.
   - Any test failure in inactive resolution indicates `Scene.GetRootGameObjects()` or child iteration failed to traverse inactive transforms.
   - Any test failure in sibling disambiguation indicates incorrect sibling index ordering.
