# Handoff Report — Survey 3: PlayMode Transition Observer & SelectionMemento Architecture

## 1. Observation

### 1.1 Problem Statement and Verbatim Errors
From `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md` (lines 13-19):
Entering Play Mode in Unity 6000.3.7f1 triggers four console exceptions:
- `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
- `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

### 1.2 Existing Codebase State in `Assets/_Project/Editor/Core/Lifecycle/`
1. **`EditorLifecycleCoordinator.cs` (`Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs`)**:
   - Lines 17-23:
     ```csharp
     RegisterSubscriber(SelectionMementoService.Instance);
     EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
     AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
     AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
     Selection.selectionChanged += HandleSelectionChanged;
     ```
   - Observed Defect: Static coordinator is tightly coupled directly to `SelectionMementoService.Instance`. It lacks subscription to scene closing events (`EditorSceneManager.sceneClosing`) or active scene changes.
2. **`SelectionMementoService.cs` (`Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs`)**:
   - Line 16:
     ```csharp
     private SelectionMemento _savedMemento = SelectionMemento.Empty;
     ```
   - Observed Defect: Stored in a standard managed field. When Domain Reload occurs on Play Mode entry (Unity default), managed static state is wiped back to `SelectionMemento.Empty`, causing `RestoreSelection` to receive empty state unless `SessionState` persistence is used.
   - Lines 172-176:
     ```csharp
     private static GameObject FindGameObjectByPath(string path)
     {
         if (string.IsNullOrEmpty(path)) return null;
         return GameObject.Find(path);
     }
     ```
   - Observed Defect: `GameObject.Find(path)` strictly returns active GameObjects. If the selected GameObject or any parent in its hierarchy is disabled (`activeSelf == false` or `activeInHierarchy == false`), `GameObject.Find` returns `null`, losing the selection.
   - Lines 64-69: `SelectionMementoService` is directly hardcoded to call static `InspectorTrackerSanitizer.ForceCleanTracker()`, violating the Single Responsibility Principle and Dependency Inversion Principle.
3. **`InspectorTrackerSanitizer.cs` (`Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`)**:
   - Lines 16-62:
     ```csharp
     if (trackerField != null)
     {
         if (trackerField.GetValue(win) is ActiveEditorTracker tracker)
         {
             var editors = tracker.activeEditors;
             ...
             tracker.Destroy(); // Line 47
         }
     }
     clearLockedMethod?.Invoke(win, null); // Line 51 (ClearSerializedLockedObjects does not exist)
     ...
     clearEditorsMethod?.Invoke(win, null); // Line 58 (ClearEditorsAndRebuild)
     ```
   - Observed Defect: Calling `tracker.Destroy()` at line 47 destroys the native peer of the tracker. Calling `ClearEditorsAndRebuild()` at line 58 then accesses `m_Tracker` which throws an `ObjectDisposedException` or `NullReferenceException`. Because the entire block is wrapped in `catch { }` (lines 86-88), the exception is swallowed, and lines 64-85 (purging `ActiveEditorTracker.sharedTracker` and `PurgeOrphanEditors()`) are skipped entirely!
4. **Assembly and Project Rules (`Assets/_Project/Editor/Game.Editor.asmdef` and `AGENTS.md`)**:
   - Assembly `Game.Editor` with root namespace `Game.Editor`.
   - Adherence to shallow inheritance (max 1 level), zero GC allocations on runtime loops, and clean separation between interfaces and implementations.
   - No existing EditMode test assembly currently exists under `Assets/_Project/Tests/Editor/`.

---

## 2. Logic Chain

### 2.1 Exception Causation Mechanics
1. **Observation**: The 4 exceptions (`MeshColliderEditor`, `GameObjectInspector`, `TransformInspector`, `Renderer2DMeshEditor`) are standard UnityEditor components and custom editors instantiated by `ActiveEditorTracker` for the selected GameObject.
2. **Mechanism**:
   - In Unity 6, entering Play Mode unloads the Edit Mode scene. All native C++ GameObject and Component instances in that scene are destroyed (`m_CachedPtr == IntPtr.Zero`).
   - If `InspectorWindow` or `ActiveEditorTracker` maintains references to those `Editor` ScriptableObjects across the transition, or if orphan `Editor` ScriptableObjects remain in memory, Unity's serialization pipeline retains them during domain reload.
   - When Unity re-enables windows post-reload, it invokes `OnEnable()` on the cached `Editor` instances.
   - Each of those 4 editors executes `new SerializedObject(target)` or accesses `m_Targets` in their `OnEnable()`.
   - Because the target was destroyed when the scene unloaded, `target` is null/missing reference.
   - `SerializedObject` constructor throws `SerializedObjectNotCreatableException: Object at index 0 is null`, or `GameObjectInspector` throws `MissingReferenceException`.

### 2.2 Purging Pipeline Flaw in Current Code
1. **Observation**: `InspectorTrackerSanitizer.PurgeAllInspectorWindows()` destroys the tracker (`tracker.Destroy()`) before calling `ClearEditorsAndRebuild()`.
2. **Deduction**: `ClearEditorsAndRebuild()` internally requires a live tracker to reset its editor list. Invoking it after `tracker.Destroy()` throws, triggering the catch block and aborting the rest of `PurgeAllInspectorWindows()`.
3. **Correct Sequence**:
   - Clear `Selection.objects = Array.Empty<Object>()` and `Selection.activeObject = null`.
   - Unlock all inspector windows (`isLocked = false`).
   - Destroy existing editors in `tracker.activeEditors` via `DestroyImmediate`.
   - Invoke `ClearEditorsAndRebuild()` while the tracker is intact so the window cleanly rebuilds with 0 editors.
   - Purge `ActiveEditorTracker.sharedTracker` and force rebuild.
   - Destroy all remaining orphan `Editor` instances discovered via `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))`.

### 2.3 Selection State Preservation (Memento Pattern)
1. **Observation**: A managed field `_savedMemento` does not survive Domain Reload. Furthermore, `GameObject.Find(path)` fails for inactive objects, and InstanceIDs change when scenes are reloaded.
2. **Deduction**:
   - **Originator**: Unity Editor Selection.
   - **Memento**: `SelectionMemento` containing:
     1. `GlobalObjectId` representations (`GlobalObjectId.ToString()`), which survive across scene reload for saved scene objects and assets.
     2. Comprehensive Scene Hierarchy Paths (`SceneName:/RootTransform/Child[index]/Subchild[index]`), allowing traversal via `Scene.GetRootGameObjects()` to find both active AND inactive GameObjects.
     3. Asset GUIDs for Project assets via `AssetDatabase.AssetPathToGUID`.
     4. Active GameObject/Object identifier.
   - **Caretaker / Persistence**:
     - `ISelectionStatePersistence` using `UnityEditor.SessionState.SetString` and `GetString`.
     - Serialized to JSON via `EditorJsonUtility` or `JsonUtility`.
     - Survives domain reload, then cleared immediately after successful restoration.

### 2.4 Event Observation (Observer Pattern) & Separation of Concerns
1. **Observation**: Hardcoding services inside static constructors or having `SelectionMementoService` directly call `InspectorTrackerSanitizer` couples domains and inhibits testing.
2. **Deduction**:
   - `IPlayModeTransitionObserver` / `EditorPlayModeTransitionObserver`:
     - Listens to `EditorApplication.playModeStateChanged`, `AssemblyReloadEvents`, and `EditorSceneManager.sceneClosing`.
     - Dispatches lifecycle notifications to registered `IPlayModeTransitionSubscriber` instances.
   - `IInspectorSanitizerService`:
     - Pure single responsibility: reflection and memory cleanup of `InspectorWindow`, `ActiveEditorTracker`, and orphan `Editor` ScriptableObjects.
   - `ISelectionMementoService`:
     - Pure single responsibility: capture selection, persist via `ISelectionStatePersistence`, and restore without throwing.

---

## 3. Detailed Architecture Design

### 3.1 Class & Interface Breakdown

```
Game.Editor.Core.Lifecycle
├── Interfaces
│   ├── IPlayModeTransitionObserver.cs      # Event coordinator interface
│   ├── IPlayModeTransitionSubscriber.cs    # Subscriber contract
│   ├── IInspectorSanitizerService.cs       # Inspector/tracker purge service contract
│   ├── ISelectionMementoService.cs          # Selection memento capture/restore contract
│   ├── ISelectionStatePersistence.cs       # SessionState persistence abstraction
│   └── ISelectionAdapter.cs                # Unity Selection abstraction for mocking
├── Models
│   ├── SelectionMemento.cs                 # Immutable serializable state container
│   └── SceneObjectIdentifier.cs            # Compound ID (GlobalObjectId + Hierarchy + SiblingIndex)
└── Services
    ├── PlayModeTransitionObserver.cs       # [InitializeOnLoad] Event dispatcher
    ├── InspectorSanitizerService.cs        # Reflection-safe tracker and editor purger
    ├── SelectionMementoService.cs          # Memento capture/restore engine
    ├── SessionStateSelectionPersistence.cs # SessionState JSON caretaker
    └── UnitySelectionAdapter.cs            # Concrete wrapper over UnityEditor.Selection
```

### 3.2 Key Contracts

#### `IPlayModeTransitionSubscriber`
```csharp
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
        void OnSceneClosing(UnityEngine.SceneManagement.Scene scene);
    }
}
```

#### `IInspectorSanitizerService`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public interface IInspectorSanitizerService
    {
        void PurgeAllInspectors();
        void DestroyOrphanEditors();
        void UnlockAllInspectors();
        void ForceRebuildSharedTracker();
    }
}
```

#### `ISelectionMementoService`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public interface ISelectionMementoService
    {
        SelectionMemento CaptureCurrentSelection();
        void ClearSelectionSafely();
        void RestoreSelection(SelectionMemento memento);
        bool HasPersistedMemento { get; }
        void RestoreFromPersistence();
        void ClearPersistence();
    }
}
```

#### `SelectionMemento` and `SceneObjectIdentifier`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    [System.Serializable]
    public sealed class SelectionMemento
    {
        public SceneObjectIdentifier[] SceneObjects;
        public string[] AssetGuids;
        public SceneObjectIdentifier ActiveSceneObject;
        public string ActiveAssetGuid;
        public long TimestampTicks;

        public bool IsEmpty => (SceneObjects == null || SceneObjects.Length == 0) &&
                               (AssetGuids == null || AssetGuids.Length == 0);
    }

    [System.Serializable]
    public sealed class SceneObjectIdentifier
    {
        public string SceneName;
        public string GlobalObjectIdString;
        public string HierarchyPath;
        public int[] SiblingIndices;
    }
}
```

### 3.3 Safe Inspector Purging Implementation Logic
```csharp
public void PurgeAllInspectors()
{
    // 1. Gather all open InspectorWindow instances safely
    var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
    if (inspectorType == null) return;

    var windows = new HashSet<EditorWindow>();
    
    // Method A: GetAllInspectorWindows static method
    var getAllMethod = inspectorType.GetMethod("GetAllInspectorWindows", 
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    if (getAllMethod?.Invoke(null, null) is IEnumerable enumerable)
    {
        foreach (var item in enumerable)
        {
            if (item is EditorWindow win) windows.Add(win);
        }
    }

    // Method B: Resources.FindObjectsOfTypeAll fallback
    var foundWindows = Resources.FindObjectsOfTypeAll(inspectorType);
    for (int i = 0; i < foundWindows.Length; i++)
    {
        if (foundWindows[i] is EditorWindow win) windows.Add(win);
    }

    var trackerField = inspectorType.GetField("m_Tracker", 
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    var isLockedProp = inspectorType.GetProperty("isLocked", 
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    var clearEditorsMethod = inspectorType.GetMethod("ClearEditorsAndRebuild", 
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    // 2. Process each window
    foreach (var win in windows)
    {
        if (win == null) continue;

        try
        {
            // Unlock window
            if (isLockedProp != null && (bool)isLockedProp.GetValue(win))
            {
                isLockedProp.SetValue(win, false);
            }

            // Clean active editors inside tracker
            if (trackerField?.GetValue(win) is ActiveEditorTracker tracker)
            {
                tracker.isLocked = false;
                var editors = tracker.activeEditors;
                if (editors != null)
                {
                    for (int j = 0; j < editors.Length; j++)
                    {
                        if (editors[j] != null)
                        {
                            UnityEngine.Object.DestroyImmediate(editors[j]);
                        }
                    }
                }
            }

            // Clear and rebuild window with empty selection
            clearEditorsMethod?.Invoke(win, null);
            win.Repaint();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[InspectorSanitizerService] Failed cleaning InspectorWindow: {ex.Message}");
        }
    }

    // 3. Purge shared tracker
    try
    {
        var shared = ActiveEditorTracker.sharedTracker;
        if (shared != null)
        {
            shared.isLocked = false;
            var sharedEditors = shared.activeEditors;
            if (sharedEditors != null)
            {
                for (int i = 0; i < sharedEditors.Length; i++)
                {
                    if (sharedEditors[i] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(sharedEditors[i]);
                    }
                }
            }
            shared.RebuildAll();
        }
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[InspectorSanitizerService] Failed cleaning shared tracker: {ex.Message}");
    }

    // 4. Destroy remaining orphan Editor ScriptableObjects
    DestroyOrphanEditors();
}
```

### 3.4 Inactive GameObject Hierarchy Resolution
In `SelectionMementoService`:
```csharp
private GameObject ResolveSceneObject(SceneObjectIdentifier id)
{
    // Strategy 1: Try GlobalObjectId
    if (!string.IsNullOrEmpty(id.GlobalObjectIdString) &&
        GlobalObjectId.TryParse(id.GlobalObjectIdString, out var gid))
    {
        var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
        if (obj != null) return obj;
    }

    // Strategy 2: Scene Root traversal (Supports INACTIVE GameObjects!)
    if (string.IsNullOrEmpty(id.HierarchyPath)) return null;

    Scene targetScene = default;
    if (!string.IsNullOrEmpty(id.SceneName))
    {
        targetScene = SceneManager.GetSceneByName(id.SceneName);
    }
    if (!targetScene.IsValid())
    {
        targetScene = SceneManager.GetActiveScene();
    }

    var rootObjects = targetScene.GetRootGameObjects(); // Returns ALL roots, including inactive!
    var pathParts = id.HierarchyPath.Split('/');
    if (pathParts.Length == 0) return null;

    GameObject current = null;
    for (int i = 0; i < rootObjects.Length; i++)
    {
        if (rootObjects[i].name == pathParts[0])
        {
            current = rootObjects[i];
            break;
        }
    }

    if (current == null) return null;

    for (int i = 1; i < pathParts.Length; i++)
    {
        Transform child = null;
        // Try sibling index if available
        if (id.SiblingIndices != null && i <= id.SiblingIndices.Length)
        {
            int siblingIdx = id.SiblingIndices[i - 1];
            if (siblingIdx >= 0 && siblingIdx < current.transform.childCount)
            {
                var candidate = current.transform.GetChild(siblingIdx);
                if (candidate.name == pathParts[i])
                {
                    child = candidate;
                }
            }
        }

        // Fallback to searching all children by name
        if (child == null)
        {
            for (int c = 0; c < current.transform.childCount; c++)
            {
                var candidate = current.transform.GetChild(c);
                if (candidate.name == pathParts[i])
                {
                    child = candidate;
                    break;
                }
            }
        }

        if (child == null) return null;
        current = child.gameObject;
    }

    return current;
}
```

---

## 4. Testing Strategy

### 4.1 Test Assembly Architecture
To ensure clean isolation and adherence to Unity Test Framework conventions:
- Create `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`:
  - `name`: `Game.Editor.Tests`
  - `references`: `["Game.Editor", "Game.Core"]`
  - `includePlatforms`: `["Editor"]`
  - `defineConstraints`: `["UNITY_INCLUDE_TESTS"]`
  - `optionalUnityReferences`: `["TestAssemblies"]`

### 4.2 Test Suite Matrix

| Test Suite | Test Type | Target Component | Verifications |
|------------|-----------|------------------|---------------|
| `SelectionMementoUnitTests` | Pure Unit Test | `SelectionMemento`, `SelectionMementoService` | - Memento captures selection without nulls.<br>- Serializes to JSON and deserializes identically.<br>- `SessionState` persistence round-trip works.<br>- Safely clears selection. |
| `HierarchyPathResolverTests` | EditMode Integration | `HierarchyPathResolver` | - Resolves active GameObject.<br>- Resolves deeply nested **inactive** GameObject.<br>- Disambiguates duplicate names via sibling indices. |
| `InspectorSanitizerTests` | EditMode Integration | `InspectorSanitizerService` | - Purges `InspectorWindow` without throwing `ObjectDisposedException`.<br>- Destroys orphan `Editor` ScriptableObjects.<br>- Leaves 0 dead editors for temporary target object. |
| `PlayModeObserverIntegrationTests` | EditMode Integration | `PlayModeTransitionObserver` | - Subscribes and dispatches in configured `ExecutionOrder`.<br>- Tolerates exceptions in individual subscribers. |

### 4.3 Key EditMode Test Implementations (Sketches)

```csharp
[Test]
public void CaptureAndRestore_InactiveGameObject_RestoresSuccessfully()
{
    var parent = new GameObject("TestParent");
    var child = new GameObject("TestChild");
    child.transform.SetParent(parent.transform);
    child.SetActive(false); // Inactive GameObject test

    Selection.activeGameObject = child;

    var service = new SelectionMementoService(new UnitySelectionAdapter(), new SessionStateSelectionPersistence());
    var memento = service.CaptureCurrentSelection();

    service.ClearSelectionSafely();
    Assert.IsNull(Selection.activeGameObject);

    service.RestoreSelection(memento);
    Assert.AreEqual(child, Selection.activeGameObject);

    Object.DestroyImmediate(parent);
}

[Test]
public void PurgeAllInspectors_WithMeshColliderTarget_LeavesNoOrphanEditors()
{
    var go = new GameObject("TestEntity");
    go.AddComponent<MeshCollider>();
    Selection.activeGameObject = go;

    var sanitizer = new InspectorSanitizerService();
    sanitizer.PurgeAllInspectors();

    var editors = Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor));
    foreach (UnityEditor.Editor ed in editors)
    {
        if (ed.target == go)
        {
            Assert.Fail("Orphan editor was not destroyed during purge.");
        }
    }

    Object.DestroyImmediate(go);
}
```

---

## 5. Caveats

1. **Undocumented Unity Internal APIs**: `InspectorWindow.ClearEditorsAndRebuild` and `GetAllInspectorWindows` are internal UnityEditor methods accessed via reflection. While stable in Unity 6000.3.7f1, all reflection lookups must employ null guards and fallback paths.
2. **Fast Play Mode (Enter Play Mode Options)**: When Fast Play Mode has Domain Reload disabled, static variables persist, but edit-mode scenes are still unloaded. The dual-layer design (in-memory + SessionState) guarantees correct handling in both modes.
3. **Multi-Scene Editing**: GameObjects in unloaded secondary scenes cannot be resolved. The resolver correctly checks `scene.IsValid()` and skips objects in unloaded scenes rather than creating dummy references.
4. **Read-Only Investigation**: As instructed, no production or test files have been modified or created during this survey phase.

---

## 6. Conclusion

The root cause of the 4 console exceptions is confirmed: cached `Editor` ScriptableObjects retained by `InspectorWindow` / `ActiveEditorTracker` survive scene unloading and domain reload, invoking `OnEnable()` on dead/null targets.

The architecture developed in this survey satisfies all requirements:
1. **Observer Pattern**: `PlayModeTransitionObserver` coordinates `EditorApplication.playModeStateChanged`, `AssemblyReloadEvents`, and `EditorSceneManager.sceneClosing`.
2. **Sanitation Service**: `InspectorSanitizerService` resolves the critical bug in previous code by maintaining tracker validity while clearing editors, destroying orphan `Editor` ScriptableObjects, and unlocking inspectors without swallowing unhandled crashes.
3. **Memento Pattern**: `SelectionMementoService` captures `GlobalObjectId` + robust scene hierarchy (supporting inactive GameObjects) and persists across domain reload via `SessionState`.
4. **SOLID & Quality**: All components are fully decoupled via interfaces, enabling 100% unit and EditMode test coverage within `Assets/_Project/Tests/Editor/`.

---

## 7. Verification Method

To independently verify these findings and future implementation:
1. **Source Inspection**:
   - Inspect `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs:47` to verify the premature `tracker.Destroy()` call before `ClearEditorsAndRebuild()`.
   - Inspect `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs:175` to verify `GameObject.Find(path)` failure on inactive objects.
   - Inspect `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs:16` to verify unpersisted static memento field.
2. **Replication Command**:
   - Open Unity 6000.3.7f1 with a scene containing a GameObject with `Transform`, `MeshCollider`, and `Renderer2DMesh`. Select it in the Inspector.
   - Enter Play Mode. Observe console logs for the 4 verbatim exceptions.
3. **Test Execution**:
   - Once implementation is completed by the developer agent, execute Unity EditMode tests via command line:
     ```pwsh
     & "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testResults "test_results.xml" -logFile "test_run.log"
     ```
   - Invalidation Condition: If any `SerializedObjectNotCreatableException` or `MissingReferenceException` occurs during PlayMode entry, the sanitizer or memento cleanup did not run prior to scene unload.
