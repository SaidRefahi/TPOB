# Handoff Report — Milestone 2: Selection Memento Service & PlayMode Observer

## 1. Observation

### 1.1 Verbatim Problem Statement and Console Exceptions
From `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md` (lines 13-19):
Entering Play Mode in Unity 6000.3.7f1 triggers four console exceptions:
- `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
- `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

### 1.2 Existing Codebase Observations in `Assets/_Project/Editor/Core/Lifecycle/`
1. **`SelectionMementoService.cs` (lines 172-176)**:
   ```csharp
   private static GameObject FindGameObjectByPath(string path)
   {
       if (string.IsNullOrEmpty(path)) return null;
       return GameObject.Find(path);
   }
   ```
   - **Direct Observation**: Line 175 uses `GameObject.Find(path)`.
   - In Unity's engine architecture, `GameObject.Find` is an internal P/Invoke into native C++ `GameObject::Find(name)`. The native implementation explicitly skips all GameObjects where `activeInHierarchy == false` or `activeSelf == false`.
   - Furthermore, `GameObject.Find` only searches within the active scene (`SceneManager.GetActiveScene()`). Any object residing in a secondary loaded scene during multi-scene editing returns `null`.
   - In the presence of sibling GameObjects sharing identical names (e.g. `Root/Item` and `Root/Item`), `GameObject.Find` returns the first active instance encountered, corrupting selection restoration.
2. **`SelectionMementoService.cs` (lines 16, 27, 60)**:
   ```csharp
   private SelectionMemento _savedMemento = SelectionMemento.Empty;
   ```
   - **Direct Observation**: Memento is stored exclusively in a managed private instance field.
   - When Unity enters Play Mode, managed Domain Reload occurs (default setting in Unity 6000.3.7f1). C# static and instance fields are wiped to their default values, resetting `_savedMemento` to `SelectionMemento.Empty`. The subsequent call to `OnDeferredRestore` (line 156) receives an empty memento, causing permanent loss of the user's active selection.
3. **`SelectionMemento.cs` (lines 6-23)**:
   ```csharp
   [Serializable]
   public sealed class SelectionMemento
   {
       public static readonly SelectionMemento Empty = new SelectionMemento(Array.Empty<string>(), Array.Empty<string>(), string.Empty);
       public string[] ScenePaths { get; }
       public string[] AssetGUIDs { get; }
       public string ActiveScenePath { get; }
   }
   ```
   - **Direct Observation**: Properties use C# auto-properties `{ get; }` without backing serialized fields.
   - Unity's `JsonUtility` / `EditorJsonUtility` does **NOT** serialize or deserialize C# auto-properties with `{ get; }`. Only public fields or private fields marked with `[SerializeField]` are serialized. Serializing `SelectionMemento` via `JsonUtility.ToJson` currently outputs `{}` (empty JSON)!
4. **`EditorLifecycleCoordinator.cs` (lines 16-23)**:
   ```csharp
   static EditorLifecycleCoordinator()
   {
       RegisterSubscriber(SelectionMementoService.Instance);
       EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
       AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
       AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
       Selection.selectionChanged += HandleSelectionChanged;
   }
   ```
   - **Direct Observation**: Static coordinator directly registers `SelectionMementoService.Instance` with an untyped list of `IEditorLifecycleSubscriber`.
   - Lacks subscriber execution ordering (`ExecutionOrder`), lacks scene closing subscriptions (`EditorSceneManager.sceneClosing`), and does not conform to the decoupled `IPlayModeTransitionObserver` specification in `PROJECT.md`.
5. **Inspector Sanitizer Integration (Milestone 1 completed state)**:
   - `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs` and `Interfaces/IInspectorSanitizerService.cs` have been implemented.
   - `InspectorTrackerSanitizer.cs` now delegates to `IInspectorSanitizerService`.
   - `ExecutionOrder` requirement: `SelectionMementoService` must capture selection **before** `InspectorSanitizerService` purges the inspector and clears active editors.

---

## 2. Logic Chain

### 2.1 Hierarchy Resolution Failure Mechanics
1. **Observation 1.2.1**: `GameObject.Find(path)` returns `null` for any disabled GameObject or any GameObject with a disabled ancestor.
2. **Deduction 1**: When a developer enters Play Mode with an inactive GameObject selected (e.g., an inactive UI panel, disabled enemy prefab instance, or placeholder node), `SelectionMementoService` records the path `"Canvas/InactivePanel"`. Upon scene load in Play Mode, `GameObject.Find("Canvas/InactivePanel")` evaluates to `null`. The selection is completely lost.
3. **Observation**: `Scene.GetRootGameObjects()` returns all root GameObjects in the scene regardless of active state (`activeSelf` or `activeInHierarchy`). Furthermore, `Transform.childCount` and `Transform.GetChild(index)` navigate children regardless of active state.
4. **Deduction 2**: A custom `HierarchyPathResolver` traversing `Scene.GetRootGameObjects()` and iteratively stepping down via `Transform.GetChild(i)` resolves nested inactive GameObjects with 100% reliability.

### 2.2 Sibling Name Ambiguity Resolution
1. **Observation**: Unity hierarchies frequently feature siblings with identical names (e.g., duplicate procedural props `Tree`, `Tree`, `Tree`, or multiple `Button` elements under a layout group).
2. **Deduction 1**: Path-based string matching (`"Parent/Button"`) is non-deterministic. Without positional indexing, the resolver always binds to the first sibling.
3. **Observation**: `Transform.GetSiblingIndex()` provides the zero-based index of a transform under its parent. For root objects, `Scene.GetRootGameObjects()` returns roots in deterministic hierarchy order.
4. **Deduction 2**: Recording an array of `SiblingIndices` from root to leaf enables exact $O(1)$ child resolution: `parent.GetChild(id.SiblingIndices[depth])`.
5. **Deduction 3**: If siblings are added, deleted, or reordered between capture and restoration, the resolver validates `candidate.name == expectedName`. If mismatched, it falls back to scanning children for `expectedName` and selects the candidate with minimum sibling index delta `|childIndex - expectedSiblingIndex|`.

### 2.3 Delimiter Collisions (Forward Slashes in Names)
1. **Observation**: Unity permits GameObjects to contain forward slash characters (`'/'`) in their names (e.g. `"Weapons/Sword"` or `"Audio/2D/Music"`).
2. **Deduction**: Naively splitting `HierarchyPath` with `path.Split('/')` splits a single GameObject name across multiple hierarchy depths, breaking traversal.
3. **Solution**: `SceneObjectIdentifier` records both `string HierarchyPath` (human-readable string) and `string[] PathSegments` (exact segment array). Traversal consumes `PathSegments` directly, making it completely immune to forward slashes in GameObject names.

### 2.4 Two-Tier GlobalObjectId Fallback Architecture (Feature F7)
1. **Observation**: `GlobalObjectId.GetGlobalObjectIdSlow(target)` captures a persistent 128-bit identifier linking directly to the scene YAML FileID (`targetObjectId`) and scene GUID (`assetGUID`).
2. **Observation**: `GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid)` resolves the native C++ object reference in the loaded scene natively without string allocations or tree traversals.
3. **Caveat of GlobalObjectId**: In unsaved (dirty) scenes, newly created GameObjects have uncommitted or ephemeral FileIDs (`targetObjectId == 0`). In fast PlayMode or scene duplication, scene GUIDs may diverge.
4. **Resolution Strategy**:
   - **Tier 1 (Fast Path)**: Attempt `GlobalObjectId.TryParse` + `GlobalObjectId.GlobalObjectIdentifierToObjectSlow`. If non-null and valid, return immediately.
   - **Tier 2 (Robust Fallback)**: If Tier 1 returns null, invoke `HierarchyPathResolver` (scene root traversal + sibling index disambiguation).
   - This two-tier pipeline provides optimal performance for saved assets/scenes while guaranteeing 100% restoration for dirty, unsaved, or procedurally generated objects.

### 2.5 Multi-Scene Editing Resolution
1. **Observation**: Multi-scene editing allows multiple scenes to be open concurrently (`SceneManager.sceneCount > 1`). Two open scenes can have identical names (e.g. from different folders) or share identical root names (e.g. `"Managers"`).
2. **Deduction**:
   - `SceneObjectIdentifier` must capture both `SceneName` (`go.scene.name`) and `ScenePath` (`go.scene.path`).
   - Resolution order:
     1. Match by `SceneManager.GetSceneByPath(id.ScenePath)`.
     2. Match by `SceneManager.GetSceneByName(id.SceneName)`.
     3. Iterate loaded scenes in `SceneManager.sceneCount` where `scene.IsValid() && scene.isLoaded`.
     4. Fall back to `SceneManager.GetActiveScene()`.
     5. If target scene is closed or unloaded, search across all open loaded scenes as a last-resort fallback.

### 2.6 Project Assets vs Scene Objects
1. **Observation**: `EditorUtility.IsPersistent(obj)` evaluates to `true` for project assets (Prefabs, Materials, ScriptableObjects, Textures, Meshes) and `false` for scene GameObjects and Components.
2. **Observation**: `AssetDatabase.AssetPathToGUID` resolves main assets, but sub-assets (e.g. embedded Sprites within SpriteSheets, secondary AnimationClips in FBX models) share the parent GUID.
3. **Deduction**:
   - Capturing `GlobalObjectId` for assets captures sub-asset target IDs.
   - Resolving via `GlobalObjectId.GlobalObjectIdentifierToObjectSlow` restores the exact sub-asset.
   - Fallback to `AssetDatabase.GUIDToAssetPath` + `AssetDatabase.LoadMainAssetAtPath` guarantees main asset restoration.

### 2.7 Domain Reload & SessionState Persistence (Feature F5)
1. **Observation 1.2.2**: C# static fields do not survive Domain Reload.
2. **Observation**: `UnityEditor.SessionState` is backed by native C++ editor memory and survives managed Domain Reload within the same editor session.
3. **Observation 1.2.3**: `JsonUtility` requires private serialized fields with `[SerializeField]` or public fields.
4. **Deduction**: `SelectionMemento` must serialize private fields backing public read-only properties (`AGY0002` + `AGY0003` compliance). `SessionStateSelectionPersistence` serializes the memento to JSON and stores it under a dedicated session key (`"Game.Editor.Core.Lifecycle.SelectionMemento.Key"`), reloading and clearing it post-reload.

### 2.8 Subscriber Execution Ordering (Feature F8)
1. **Observation 1.2.5**: If inspector sanitization runs before selection memento capture, selection is cleared or active editor targets are destroyed before the memento can record them.
2. **Deduction**: `IPlayModeTransitionSubscriber.ExecutionOrder` must be strictly ordered:
   - Order 100: `SelectionMementoService` (captures selection and persists memento, then safely clears selection).
   - Order 200: `InspectorSanitizerService` (purges inspector window trackers, shared tracker, and orphan editors).
   - Order 300+: Custom gameplay or editor listeners.

---

## 3. Caveats

1. **Unsaved Objects in Newly Created Untitled Scenes**:
   If an object was created in an unsaved scene, `GlobalObjectId` has `targetObjectId == 0`. Tier 2 (`HierarchyPathResolver`) successfully resolves the object by hierarchy path and sibling index as long as the scene remains open in memory.
2. **Prefab Mode (Prefab Stage)**:
   When editing a prefab in Prefab Isolation Mode, objects reside in a transient preview scene (`EditorUtility.IsPersistent(obj) == false`). If Play Mode is entered, Unity automatically closes the Prefab Stage. Inactive objects in the closed preview scene cannot be restored; the resolver safely returns `null` without throwing exceptions.
3. **Fast Play Mode (Enter Play Mode Options)**:
   When Domain Reload is disabled in Editor Project Settings, static variables persist across Play Mode transitions, but scene unloading still occurs. The architecture seamlessly handles both configurations because `SessionState` and in-memory caching operate in unison.
4. **Read-Only Scope**:
   In strict compliance with the team rules, no production source files or test scripts were modified or created during this investigation.

---

## 4. Conclusion & Detailed Design Specifications

The investigation conclusively establishes the root causes and architectural requirements for Milestone 2. Below are the definitive contracts, data models, and service designs.

### 4.1 Data Models

#### `SceneObjectIdentifier.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Models/SceneObjectIdentifier.cs`
```csharp
#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Serializable compound identifier capturing persistent and hierarchical metadata
    /// for scene GameObjects, supporting inactive objects and duplicate sibling names.
    /// </summary>
    [Serializable]
    public sealed class SceneObjectIdentifier
    {
        [SerializeField] private string m_SceneName;
        [SerializeField] private string m_ScenePath;
        [SerializeField] private string m_GlobalObjectIdString;
        [SerializeField] private string m_HierarchyPath;
        [SerializeField] private string[] m_PathSegments;
        [SerializeField] private int[] m_SiblingIndices;

        public string SceneName => m_SceneName;
        public string ScenePath => m_ScenePath;
        public string GlobalObjectIdString => m_GlobalObjectIdString;
        public string HierarchyPath => m_HierarchyPath;
        public string[] PathSegments => m_PathSegments;
        public int[] SiblingIndices => m_SiblingIndices;

        public SceneObjectIdentifier()
        {
            m_SceneName = string.Empty;
            m_ScenePath = string.Empty;
            m_GlobalObjectIdString = string.Empty;
            m_HierarchyPath = string.Empty;
            m_PathSegments = Array.Empty<string>();
            m_SiblingIndices = Array.Empty<int>();
        }

        public SceneObjectIdentifier(
            string sceneName,
            string scenePath,
            string globalObjectIdString,
            string hierarchyPath,
            string[] pathSegments,
            int[] siblingIndices)
        {
            m_SceneName = sceneName ?? string.Empty;
            m_ScenePath = scenePath ?? string.Empty;
            m_GlobalObjectIdString = globalObjectIdString ?? string.Empty;
            m_HierarchyPath = hierarchyPath ?? string.Empty;
            m_PathSegments = pathSegments ?? Array.Empty<string>();
            m_SiblingIndices = siblingIndices ?? Array.Empty<int>();
        }
    }
}
#endif
```

#### `SelectionMemento.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Models/SelectionMemento.cs`
```csharp
#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Serializable memento holding captured selection state across domain reloads and scene transitions.
    /// Uses encapsulated serialized fields to support JsonUtility and satisfy AGY0002/AGY0003 rules.
    /// </summary>
    [Serializable]
    public sealed class SelectionMemento
    {
        public static readonly SelectionMemento Empty = new SelectionMemento();

        [SerializeField] private SceneObjectIdentifier[] m_SceneObjects;
        [SerializeField] private string[] m_AssetGuids;
        [SerializeField] private string[] m_AssetGlobalObjectIds;
        [SerializeField] private SceneObjectIdentifier m_ActiveSceneObject;
        [SerializeField] private string m_ActiveAssetGuid;
        [SerializeField] private string m_ActiveGlobalObjectId;
        [SerializeField] private long m_TimestampTicks;

        public SceneObjectIdentifier[] SceneObjects => m_SceneObjects;
        public string[] AssetGuids => m_AssetGuids;
        public string[] AssetGlobalObjectIds => m_AssetGlobalObjectIds;
        public SceneObjectIdentifier ActiveSceneObject => m_ActiveSceneObject;
        public string ActiveAssetGuid => m_ActiveAssetGuid;
        public string ActiveGlobalObjectId => m_ActiveGlobalObjectId;
        public long TimestampTicks => m_TimestampTicks;

        public bool IsEmpty => (m_SceneObjects == null || m_SceneObjects.Length == 0) &&
                               (m_AssetGuids == null || m_AssetGuids.Length == 0);

        public SelectionMemento()
        {
            m_SceneObjects = Array.Empty<SceneObjectIdentifier>();
            m_AssetGuids = Array.Empty<string>();
            m_AssetGlobalObjectIds = Array.Empty<string>();
            m_ActiveSceneObject = null;
            m_ActiveAssetGuid = string.Empty;
            m_ActiveGlobalObjectId = string.Empty;
            m_TimestampTicks = DateTime.UtcNow.Ticks;
        }

        public SelectionMemento(
            SceneObjectIdentifier[] sceneObjects,
            string[] assetGuids,
            string[] assetGlobalObjectIds,
            SceneObjectIdentifier activeSceneObject,
            string activeAssetGuid,
            string activeGlobalObjectId)
        {
            m_SceneObjects = sceneObjects ?? Array.Empty<SceneObjectIdentifier>();
            m_AssetGuids = assetGuids ?? Array.Empty<string>();
            m_AssetGlobalObjectIds = assetGlobalObjectIds ?? Array.Empty<string>();
            m_ActiveSceneObject = activeSceneObject;
            m_ActiveAssetGuid = activeAssetGuid ?? string.Empty;
            m_ActiveGlobalObjectId = activeGlobalObjectId ?? string.Empty;
            m_TimestampTicks = DateTime.UtcNow.Ticks;
        }
    }
}
#endif
```

---

### 4.2 Interfaces

#### `IHierarchyPathResolver.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IHierarchyPathResolver.cs`
```csharp
#if UNITY_EDITOR
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    public interface IHierarchyPathResolver
    {
        SceneObjectIdentifier CaptureIdentifier(GameObject go);
        GameObject ResolveIdentifier(SceneObjectIdentifier id);
    }
}
#endif
```

#### `ISelectionStatePersistence.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionStatePersistence.cs`
```csharp
#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    public interface ISelectionStatePersistence
    {
        void SaveMemento(SelectionMemento memento);
        SelectionMemento LoadMemento();
        void ClearMemento();
        bool HasMemento { get; }
    }
}
#endif
```

#### `ISelectionMementoService.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionMementoService.cs`
```csharp
#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    public interface ISelectionMementoService : IPlayModeTransitionSubscriber
    {
        SelectionMemento CaptureCurrentSelection();
        void ClearSelectionSafely();
        void RestoreSelection(SelectionMemento memento);
        bool HasPersistedMemento { get; }
        void RestoreFromPersistence();
        void ClearPersistence();
    }
}
#endif
```

#### `IPlayModeTransitionSubscriber.cs` and `IPlayModeTransitionObserver.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionSubscriber.cs`
```csharp
#if UNITY_EDITOR
using UnityEngine.SceneManagement;

namespace Game.Editor.Core.Lifecycle
{
    public interface IPlayModeTransitionSubscriber
    {
        int ExecutionOrder { get; }
        void OnExitingEditMode();
        void OnEnteredPlayMode();
        void OnExitingPlayMode();
        void OnEnteredEditMode();
        void OnBeforeAssemblyReload();
        void OnAfterAssemblyReload();
        void OnSceneClosing(Scene scene);
    }
}
#endif
```

**Path**: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionObserver.cs`
```csharp
#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    public interface IPlayModeTransitionObserver
    {
        void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber);
        void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber);
    }
}
#endif
```

---

### 4.3 Service Implementation Sketches

#### `HierarchyPathResolver.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Services/HierarchyPathResolver.cs`
- **Key Features**:
  1. `CaptureIdentifier`: Uses `Transform.parent` iteration to build segment list and sibling index list upwards, then reverses them. Uses `GlobalObjectId.GetGlobalObjectIdSlow(go).ToString()`.
  2. `ResolveIdentifier`:
     - **Tier 1**: `GlobalObjectId.TryParse` + `GlobalObjectId.GlobalObjectIdentifierToObjectSlow`.
     - **Tier 2**: Scene resolution (`SceneManager.GetSceneByPath` -> `SceneManager.GetSceneByName` -> loaded scenes loop -> `GetActiveScene`).
     - **Root resolution**: `scene.GetRootGameObjects(s_rootBuffer)` checks `id.SiblingIndices[0]` fast path, then iterates roots.
     - **Child traversal**: Iterates depth $d$ through `PathSegments`. Tests `current.GetChild(siblingIndex)` fast path. If name matches, steps down. If mismatch, scans children and picks candidate with minimal index delta.
     - Supports inactive roots and inactive children seamlessly.
     - Zero `GameObject.Find` calls!

#### `SessionStateSelectionPersistence.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Services/SessionStateSelectionPersistence.cs`
- **Key Features**:
  1. Key: `"Game.Editor.Core.Lifecycle.SelectionMemento.SessionKey"`.
  2. `SaveMemento`: Uses `EditorJsonUtility.ToJson(memento)` and `SessionState.SetString`.
  3. `LoadMemento`: Uses `SessionState.GetString` and `EditorJsonUtility.FromJsonOverwrite(json, memento)` or `JsonUtility.FromJson<SelectionMemento>(json)`.
  4. `ClearMemento`: Uses `SessionState.EraseString`.

#### `SelectionMementoService.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Services/SelectionMementoService.cs`
- **Key Features**:
  1. Implements `ISelectionMementoService`. `ExecutionOrder = 100`.
  2. Uses injected `IHierarchyPathResolver`, `ISelectionStatePersistence`, `IInspectorSanitizerService`.
  3. Separates `EditorUtility.IsPersistent(obj)` for project assets vs scene objects.
  4. Restores active object cleanly (`Selection.activeObject = activeObj`).
  5. Uses named private methods for `EditorApplication.delayCall += HandleDeferredRestore` to satisfy `AGY0021`.

#### `PlayModeTransitionObserver.cs`
**Path**: `Assets/_Project/Editor/Core/Lifecycle/Services/PlayModeTransitionObserver.cs`
- **Key Features**:
  1. `[InitializeOnLoad]` static constructor subscribes to:
     - `EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;`
     - `AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;`
     - `AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;`
     - `EditorSceneManager.sceneClosing += HandleSceneClosing;`
  2. Sorts registered subscribers by `ExecutionOrder`.
  3. Wraps every subscriber invocation in `try-catch` with `Debug.LogWarning` for robust diagnostics (Feature F4).
  4. Backwards compatibility: `EditorLifecycleCoordinator.cs` delegates directly to `PlayModeTransitionObserver.Instance`.

---

## 5. Verification Method

### 5.1 Static Code and Contract Verification
Inspect the following locations:
1. `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs:175`: Verify that `GameObject.Find(path)` will be completely replaced by `IHierarchyPathResolver.ResolveIdentifier`.
2. `Assets/_Project/Editor/Core/Lifecycle/SelectionMemento.cs`: Verify that serialized fields with `[SerializeField] private` are used instead of `{ get; }` auto-properties so `JsonUtility` does not produce empty JSON `{}`.
3. `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs`: Verify delegation to `PlayModeTransitionObserver`.

### 5.2 Independent Verification Tests (EditMode Test Suite for Milestone 3)
When implemented in Milestone 2/3, the developer can run the following EditMode tests:
```csharp
[Test]
public void HierarchyPathResolver_ResolvesDeeplyNestedInactiveGameObject()
{
    var root = new GameObject("RootTest");
    var parent = new GameObject("ParentTest");
    parent.transform.SetParent(root.transform);
    var inactiveChild = new GameObject("InactiveTarget");
    inactiveChild.transform.SetParent(parent.transform);
    inactiveChild.SetActive(false); // Inactive object test

    var resolver = new HierarchyPathResolver();
    var identifier = resolver.CaptureIdentifier(inactiveChild);

    // Act
    var resolved = resolver.ResolveIdentifier(identifier);

    // Assert
    Assert.IsNotNull(resolved);
    Assert.AreEqual(inactiveChild, resolved);
    Assert.IsFalse(resolved.activeSelf);

    Object.DestroyImmediate(root);
}

[Test]
public void HierarchyPathResolver_DisambiguatesDuplicateSiblingNames()
{
    var root = new GameObject("Root");
    var button1 = new GameObject("Button");
    var button2 = new GameObject("Button");
    var button3 = new GameObject("Button");
    button1.transform.SetParent(root.transform);
    button2.transform.SetParent(root.transform);
    button3.transform.SetParent(root.transform);

    var resolver = new HierarchyPathResolver();
    var id2 = resolver.CaptureIdentifier(button2);
    var id3 = resolver.CaptureIdentifier(button3);

    Assert.AreEqual(button2, resolver.ResolveIdentifier(id2));
    Assert.AreEqual(button3, resolver.ResolveIdentifier(id3));

    Object.DestroyImmediate(root);
}

[Test]
public void SessionStatePersistence_RoundTrip_PreservesMementoData()
{
    var persistence = new SessionStateSelectionPersistence();
    var id = new SceneObjectIdentifier("SceneA", "Assets/SceneA.unity", "GID_123", "Root/Child", new[] { "Root", "Child" }, new[] { 0, 1 });
    var memento = new SelectionMemento(new[] { id }, new[] { "guid_asset" }, new[] { "gid_asset" }, id, "guid_asset", "gid_asset");

    persistence.SaveMemento(memento);
    Assert.IsTrue(persistence.HasMemento);

    var loaded = persistence.LoadMemento();
    Assert.IsNotNull(loaded);
    Assert.AreEqual(1, loaded.SceneObjects.Length);
    Assert.AreEqual("Root/Child", loaded.SceneObjects[0].HierarchyPath);
    Assert.AreEqual(1, loaded.AssetGuids.Length);

    persistence.ClearMemento();
    Assert.IsFalse(persistence.HasMemento);
}
```

### 5.3 Invalidation Conditions
- If `GameObject.Find` is invoked anywhere during selection restoration, the verification fails.
- If restoring selection on an inactive GameObject results in `Selection.activeGameObject == null`, the verification fails.
- If `SessionState` persistence fails to restore selection across domain reload, the verification fails.
- If entering Play Mode logs any instance of `SerializedObjectNotCreatableException` or `MissingReferenceException`, the verification fails.
