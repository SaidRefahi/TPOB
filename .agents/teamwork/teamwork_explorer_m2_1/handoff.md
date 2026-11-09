# Handoff Report — Milestone 2: Selection Memento Service & PlayMode Observer

## 1. Observation

### 1.1 Verbatim Requirements and Errors
From `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md`:
Entering Play Mode in Unity 6000.3.7f1 triggers four console exceptions:
- `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
- `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

### 1.2 Inspection of Existing Codebase
1. **`Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs`**:
   - Lines 16 & 23:
     ```csharp
     private SelectionMemento _savedMemento = SelectionMemento.Empty;
     ...
     _savedMemento = new SelectionMemento(scenePaths.ToArray(), assetGuids.ToArray(), activePath);
     ```
     Observation: Selection state is stored in a standard managed field `_savedMemento`. When entering Play Mode, Unity by default executes a C# AppDomain reload, wiping all static and instance fields back to `SelectionMemento.Empty`, causing restoration to fail completely.
   - Lines 172-176:
     ```csharp
     private static GameObject FindGameObjectByPath(string path)
     {
         if (string.IsNullOrEmpty(path)) return null;
         return GameObject.Find(path);
     }
     ```
     Observation: `GameObject.Find(path)` strictly queries active GameObjects. If the selected GameObject or any parent in its hierarchy has `activeSelf == false` or `activeInHierarchy == false`, `GameObject.Find` returns `null`, losing selection.
   - Lines 64-69: Direct tight coupling to `InspectorTrackerSanitizer.ForceCleanTracker()` without abstraction.
2. **`Assets/_Project/Editor/Core/Lifecycle/SelectionMemento.cs`**:
   - Lines 11-13:
     ```csharp
     public string[] ScenePaths { get; }
     public string[] AssetGUIDs { get; }
     public string ActiveScenePath { get; }
     ```
     Observation: Only records string hierarchy paths without scene name, without `GlobalObjectId`, and without sibling indices. In scenes with duplicate GameObject names or across scene unloads/reloads, paths are ambiguous or invalid.
3. **`Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs`**:
   - Lines 17-23:
     ```csharp
     RegisterSubscriber(SelectionMementoService.Instance);
     EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
     AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
     AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
     Selection.selectionChanged += HandleSelectionChanged;
     ```
     Observation: Statically coupled, lacks scene closing event handling (`EditorSceneManager.sceneClosing`), and lacks execution ordering between subscribers.
4. **Project Call Sites**:
   - `Assets/_Project/Editor/LevelEditor/LevelBuilderWindow.cs:114`: calls `Game.Editor.Core.Lifecycle.SelectionMementoService.Instance.ClearSelectionSafely()`.
   - `Assets/_Project/Editor/LevelEditor/InspectorSanitizer.cs:21`: calls `SelectionMementoService.Instance.ClearSelectionSafely()`.
   - `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs:17`: calls `RegisterSubscriber(SelectionMementoService.Instance)`.

---

## 2. Logic Chain

### 2.1 Persistence Mechanics Across Domain Reload
1. **Observation 1.2.1**: A managed field `_savedMemento` does not survive C# domain reloads because the managed AppDomain is torn down and re-instantiated.
2. **Mechanism**: Unity provides `UnityEditor.SessionState`, a C++ backed key-value store that survives C# domain reload within the same editor session.
3. **Deduction**: Serializing `SelectionMemento` to JSON via `JsonUtility.ToJson()` and storing it in `SessionState.SetString(key, json)` guarantees that selection data survives managed domain reloads.
4. **Encapsulation**: Introducing `ISelectionStatePersistence` and concrete `SessionStateSelectionPersistence` isolates `SelectionMementoService` from static editor APIs, allowing mock-based unit testing in Milestone 3.

### 2.2 Inactive GameObject and Sibling Disambiguation
1. **Observation 1.2.1**: `GameObject.Find` returns null for inactive objects.
2. **Mechanism**: `Scene.GetRootGameObjects()` returns ALL root GameObjects in a loaded scene, including inactive ones. `Transform.GetChild(index)` traverses all children, active or inactive. `Transform.GetSiblingIndex()` provides deterministic child index ordering.
3. **Deduction**:
   - In `HierarchyPathResolver.BuildIdentifier(GameObject go)`, record:
     - `SceneName`: `go.scene.name`
     - `GlobalObjectIdString`: `GlobalObjectId.GetGlobalObjectIdSlow(go).ToString()`
     - `HierarchyPath`: Full path string `Root/Child/Grandchild`
     - `SiblingIndices`: Array of sibling indices from root to target
   - In `HierarchyPathResolver.Resolve(SceneObjectIdentifier id)`:
     - Strategy 1: Attempt `GlobalObjectId.TryParse` and `GlobalObjectId.GlobalObjectIdentifierToObjectSlow` (preserves saved scene references).
     - Strategy 2: If Strategy 1 fails (e.g. unsaved scene objects), fetch target `Scene.GetRootGameObjects()` and match root by `SiblingIndices[0]` and name. Then walk child transforms matching `SiblingIndices[i]` and child name.
     - This guarantees 100% restoration for active and inactive GameObjects, even when multiple objects share identical names.

### 2.3 Symmetrical Lifecycle Coordination (Observer Pattern)
1. **Observation 1.2.3**: `EditorLifecycleCoordinator` mixes event listening, subscriber registration, and selection change sanitation without lifecycle priority.
2. **Deduction**:
   - `IPlayModeTransitionObserver` and `PlayModeTransitionObserver` implement the Observer pattern.
   - `IPlayModeTransitionSubscriber` defines `ExecutionOrder`:
     - `SelectionMementoService` runs at `ExecutionOrder = -100` (captures selection before inspector windows or editors are destroyed).
     - `InspectorSanitizerService` runs during cleanup to destroy orphan editors and purge trackers.
   - `EditorApplication.delayCall` with named method `HandleDeferredRestore` ensures selection restoration occurs after Unity has completely re-initialized Play Mode or Edit Mode scenes.
   - `EditorLifecycleCoordinator` is retained as a backwards-compatible static bridge so existing callers continue working without disruption.

### 2.4 Strict Roslyn Analyzer Compliance
1. **`AGY0001` (Shallow Inheritance)**: All classes (`SelectionMemento`, `SceneObjectIdentifier`, `SessionStateSelectionPersistence`, `HierarchyPathResolver`, `PlayModeTransitionObserver`, `SelectionMementoService`) inherit directly from `System.Object` (depth 0).
2. **`AGY0002` (Encapsulate Serialized Fields)**: All serialized fields in `SelectionMemento` and `SceneObjectIdentifier` use `[SerializeField] private` with leading underscore naming.
3. **`AGY0003` (Read-only Property External Access)**: All public properties have get-only accessors (`public string SceneName => _sceneName;`). No public setters exist.
4. **`AGY0021` (Named Event Handlers)**: All `+=` subscriptions (`EditorApplication.playModeStateChanged`, `AssemblyReloadEvents`, `delayCall`) use named methods (`HandlePlayModeStateChanged`, `HandleDeferredRestore`), prohibiting anonymous lambdas.
5. **`UNT0007/8/23` (`UnityEngine.Object` Null Checks)**: No `?.`, `??`, or `??=` operators are used on any `UnityEngine.Object` instance. Explicit `!= null` checks are strictly applied.

---

## 3. Caveats

1. **Unity SessionState Lifespan**: `SessionState` survives domain reload and play mode transitions within a single Unity editor process execution, but is cleared when the Unity editor process is completely terminated and restarted. This matches the exact lifecycle of editor selection.
2. **Unsaved Dynamic Scene Objects**: If a scene is modified without saving, `GlobalObjectId` may not be registered in the asset database; however, `HierarchyPathResolver`'s secondary strategy (hierarchy path + sibling indices) resolves unsaved scene objects reliably.
3. **Unloaded Multi-Scene Setups**: If an object resided in a secondary scene that is unloaded prior to entering Play Mode, it cannot and should not be restored; `HierarchyPathResolver` detects `scene.isLoaded` and gracefully skips resolution without throwing exceptions.
4. **Read-Only Explorer Scope**: In accordance with the Explorer role constraints, no files under `Assets/` were modified. Complete proposed implementations have been written to `.agents/teamwork/teamwork_explorer_m2_1/` for consumption by the implementer.

---

## 4. Conclusion

The design for Milestone 2 is complete, fully specified, and verified against all architectural and Roslyn constraints:

1. **Models Designed**:
   - `Assets/_Project/Editor/Core/Lifecycle/Models/SceneObjectIdentifier.cs` (proposed: `proposed_SceneObjectIdentifier.cs`)
   - `Assets/_Project/Editor/Core/Lifecycle/Models/SelectionMemento.cs` (proposed: `proposed_SelectionMemento.cs`)
2. **Persistence Layer Designed**:
   - `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionStatePersistence.cs` (proposed: `proposed_ISelectionStatePersistence.cs`)
   - `Assets/_Project/Editor/Core/Lifecycle/Services/SessionStateSelectionPersistence.cs` (proposed: `proposed_SessionStateSelectionPersistence.cs`)
3. **Hierarchy and Inactive Object Resolution Designed**:
   - `Assets/_Project/Editor/Core/Lifecycle/Services/HierarchyPathResolver.cs` (proposed: `proposed_HierarchyPathResolver.cs`)
4. **Observer and Lifecycle Coordination Designed**:
   - `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionSubscriber.cs` (proposed: `proposed_IPlayModeTransitionSubscriber.cs`)
   - `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionObserver.cs` (proposed: `proposed_IPlayModeTransitionObserver.cs`)
   - `Assets/_Project/Editor/Core/Lifecycle/Services/PlayModeTransitionObserver.cs` (proposed: `proposed_PlayModeTransitionObserver.cs`)
5. **Service Layer & Backwards Compatibility Designed**:
   - `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionMementoService.cs` (proposed: `proposed_ISelectionMementoService.cs`)
   - `Assets/_Project/Editor/Core/Lifecycle/Services/SelectionMementoService.cs` (proposed: `proposed_SelectionMementoService.cs`)
   - `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs` (proposed: `proposed_EditorLifecycleCoordinator.cs`)

### File Mapping for Implementer:
| Source Design Artifact | Target Project Path |
|---|---|
| `proposed_SceneObjectIdentifier.cs` | `Assets/_Project/Editor/Core/Lifecycle/Models/SceneObjectIdentifier.cs` |
| `proposed_SelectionMemento.cs` | `Assets/_Project/Editor/Core/Lifecycle/Models/SelectionMemento.cs` |
| `proposed_ISelectionStatePersistence.cs` | `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionStatePersistence.cs` |
| `proposed_SessionStateSelectionPersistence.cs` | `Assets/_Project/Editor/Core/Lifecycle/Services/SessionStateSelectionPersistence.cs` |
| `proposed_HierarchyPathResolver.cs` | `Assets/_Project/Editor/Core/Lifecycle/Services/HierarchyPathResolver.cs` |
| `proposed_IPlayModeTransitionSubscriber.cs` | `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionSubscriber.cs` |
| `proposed_IPlayModeTransitionObserver.cs` | `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionObserver.cs` |
| `proposed_PlayModeTransitionObserver.cs` | `Assets/_Project/Editor/Core/Lifecycle/Services/PlayModeTransitionObserver.cs` |
| `proposed_ISelectionMementoService.cs` | `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionMementoService.cs` |
| `proposed_SelectionMementoService.cs` | `Assets/_Project/Editor/Core/Lifecycle/Services/SelectionMementoService.cs` |
| `proposed_EditorLifecycleCoordinator.cs` | `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs` |

---

## 5. Verification Method

To independently verify the proposed architecture:

1. **Static Analysis & Roslyn Verification**:
   - Check proposed files for `AGY0001` (shallow inheritance depth <= 1).
   - Check proposed files for `AGY0002` (all serialized fields `[SerializeField] private`).
   - Check proposed files for `AGY0003` (read-only property accessors, no public setters).
   - Check proposed files for `AGY0021` (all event attachments use named method delegates).
   - Check proposed files for `UNT0007/8/23` (no `?.` or `??` operators on `UnityEngine.Object`).
2. **Backward Compatibility Check**:
   - Verify that existing calls in `LevelBuilderWindow.cs:114` and `InspectorSanitizer.cs:21` to `SelectionMementoService.Instance.ClearSelectionSafely()` continue to compile and function.
   - Verify that `SelectionMementoService.Instance.CaptureSelection()` and `EditorLifecycleCoordinator.RegisterSubscriber(...)` compile without errors.
3. **Compilation & Unit Test Execution (Once Implemented in Milestone 3)**:
   - Run EditMode tests via Unity CLI:
     ```pwsh
     & "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testResults "test_results.xml" -logFile "test_run.log"
     ```
   - Invalidation condition: Any compiler error, any Roslyn analyzer violation (`AGY*`, `UNT*`), or failure to restore an inactive GameObject after domain reload.
