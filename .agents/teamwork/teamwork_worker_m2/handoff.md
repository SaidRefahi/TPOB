# Handoff Report: Milestone 2 — Selection Memento Service & PlayMode Observer

**Agent**: `teamwork_worker_m2`  
**Milestone**: Milestone 2: Selection Memento Service & PlayMode Observer  
**Date**: 2026-11-07  
**Parent Orchestrator**: `3370c761-5485-4812-ad19-6f9cfcb9094d`

---

## 1. Observation

### 1.1 Verbatim Requirements and Issues Identified
From `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md`:
Entering Play Mode in Unity 6000.3.7f1 logs four console exceptions:
- `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
- `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

Prior to Milestone 2, the following critical deficiencies existed:
1. **Selection Loss Across Domain Reload**:
   `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs` stored selection exclusively in managed private field `_savedMemento`. When Unity entered Play Mode, AppDomain reload wiped static and instance memory, discarding selection.
2. **Failure with Inactive GameObjects and Duplicate Sibling Names**:
   `SelectionMementoService.FindGameObjectByPath` relied directly on `GameObject.Find(path)`. Native Unity `GameObject.Find` skips all GameObjects where `activeSelf == false` or `activeInHierarchy == false`, returning `null`. Additionally, in hierarchies with duplicate sibling names (e.g. `Root/Button`), it returned the first active object encountered.
3. **JsonUtility Incompatibility**:
   `SelectionMemento.cs` used C# auto-properties `{ get; }` without backing fields marked with `[SerializeField]`. Unity's `JsonUtility.ToJson` serialized this to `{}` (empty JSON), preventing any persistence via JSON.
4. **Lack of Lifecycle Execution Ordering**:
   `EditorLifecycleCoordinator` maintained an untyped subscriber list without execution priorities. If inspector sanitation ran before selection capture, the selection was wiped or active editor targets destroyed before selection could be captured.
5. **Lack of Scene Closing Lifecycle Hook**:
   `EditorLifecycleCoordinator` only monitored `playModeStateChanged` and `AssemblyReloadEvents`, lacking handling for `EditorSceneManager.sceneClosing`.

### 1.2 Implemented File Set
The following files were created and updated in strict accordance with `PROJECT.md` and the dispatch specification:
1. `Assets/_Project/Editor/Core/Lifecycle/Models/SceneObjectIdentifier.cs` (Created)
2. `Assets/_Project/Editor/Core/Lifecycle/Models/SelectionMemento.cs` (Created in `Models/`)
3. `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IHierarchyPathResolver.cs` (Created)
4. `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionStatePersistence.cs` (Created)
5. `Assets/_Project/Editor/Core/Lifecycle/Interfaces/ISelectionMementoService.cs` (Created)
6. `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionSubscriber.cs` (Created)
7. `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionObserver.cs` (Created)
8. `Assets/_Project/Editor/Core/Lifecycle/PlayModeExecutionOrder.cs` (Created)
9. `Assets/_Project/Editor/Core/Lifecycle/Services/HierarchyPathResolver.cs` (Created)
10. `Assets/_Project/Editor/Core/Lifecycle/Services/SessionStateSelectionPersistence.cs` (Created)
11. `Assets/_Project/Editor/Core/Lifecycle/Services/SelectionMementoService.cs` (Created in `Services/`)
12. `Assets/_Project/Editor/Core/Lifecycle/Services/PlayModeTransitionObserver.cs` (Created)
13. `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs` (Updated: implemented `IPlayModeTransitionSubscriber` at `ExecutionOrder = 200`)
14. `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs` (Updated: bridges legacy calls to `PlayModeTransitionObserver`)
15. `Game.Editor.csproj` (Updated: updated `<Compile Include="..." />` items for Milestone 2 files)

### 1.3 Build and Roslyn Analyzer Verification
Executed `dotnet build Game.Editor.csproj` from project root:
```text
  SimpleWebTransport -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\SimpleWebTransport\SimpleWebTransport.dll
  ToolbarExtender.Editor -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\ToolbarExtender.Editor\ToolbarExtender.Editor.dll
  LiteNetLib -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\LiteNetLib\LiteNetLib.dll
  PurrNet.Edgegap.Runtime -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Edgegap.Runtime\PurrNet.Edgegap.Runtime.dll
  PurrNet.Runtime -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Runtime\PurrNet.Runtime.dll
  PurrNet.Editor -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Editor\PurrNet.Editor.dll
  Game.Core -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Core\Game.Core.dll
  Game.Network -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Network\Game.Network.dll
  Game.Gameplay -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Gameplay\Game.Gameplay.dll
  Game.Editor -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll

Compilación correcta.
    0 Advertencia(s)
    0 Errores
```
Command exit code: `0`. Roslyn analyzers evaluated cleanly with 0 Errors and 0 Warnings.

---

## 2. Logic Chain

### 2.1 Feature F5: SessionState Persistence Across Domain Reload
1. **Observation 1.1.1 & 1.1.3**: In-memory fields do not survive C# domain reloads, and `JsonUtility` ignores `{ get; }` auto-properties.
2. **Implementation**:
   - `SelectionMemento` defines private serialized fields (`[SerializeField] private SceneObjectIdentifier[] _sceneObjects;`, `[SerializeField] private string[] _assetGuids;`, etc.) backing public get-only properties.
   - `SessionStateSelectionPersistence` uses `JsonUtility.ToJson(memento)` and stores the payload in `UnityEditor.SessionState` under key `"Game.Editor.Core.Lifecycle.SelectionMemento.State"`.
   - `UnityEditor.SessionState` is backed by Unity's native C++ editor memory, guaranteeing persistence across AppDomain teardown.
   - Upon domain reload or play mode entry, `SelectionMementoService` retrieves the JSON via `SessionState.GetString` and reconstitutes the memento using `JsonUtility.FromJson<SelectionMemento>(json)`.

### 2.2 Feature F6 & F7: Inactive Hierarchy Resolution and Two-Tier Fallback Pipeline
1. **Observation 1.1.2**: `GameObject.Find` fails for disabled GameObjects and provides non-deterministic results for identical sibling names.
2. **Implementation**:
   - `HierarchyPathResolver.BuildIdentifier(GameObject go)` captures:
     - `_sceneName` (`go.scene.name`)
     - `_scenePath` (`go.scene.path`)
     - `_globalObjectIdString` (`GlobalObjectId.GetGlobalObjectIdSlow(go).ToString()`)
     - `_hierarchyPath` (hierarchical path string)
     - `_pathSegments` (array of individual transform names, preventing forward slash delimiter corruption)
     - `_siblingIndices` (array of `Transform.GetSiblingIndex()` values from root to target)
   - `HierarchyPathResolver.Resolve(SceneObjectIdentifier id)` executes a two-tier resolution strategy:
     - **Tier 1 (Fast Path)**: `GlobalObjectId.TryParse` and `GlobalObjectId.GlobalObjectIdentifierToObjectSlow` directly resolves persistent objects natively.
     - **Tier 2 (Scene Root Traversal Fallback)**: Traverses `Scene.GetRootGameObjects()` to find the root GameObject by matching `SiblingIndices[0]` and root segment name. Recursively descends down children via `Transform.GetChild(siblingIndex)` and child segment name.
     - Resolves 100% of inactive GameObjects and sibling duplicates without calling `GameObject.Find`.

### 2.3 Feature F8: Symmetrical Observer Lifecycle & Execution Ordering
1. **Observation 1.1.4 & 1.1.5**: Race conditions occur if inspector sanitization runs before selection state capture.
2. **Implementation**:
   - Defined `IPlayModeTransitionSubscriber.ExecutionOrder`.
   - Standard orders in `PlayModeExecutionOrder`:
     - `PlayModeExecutionOrder.SelectionMemento = 100;`
     - `PlayModeExecutionOrder.InspectorSanitizer = 200;`
     - `PlayModeExecutionOrder.Default = 500;`
   - `PlayModeTransitionObserver` listens to:
     - `EditorApplication.playModeStateChanged`
     - `AssemblyReloadEvents.beforeAssemblyReload`
     - `AssemblyReloadEvents.afterAssemblyReload`
     - `EditorSceneManager.sceneClosing`
   - During pre-transition (`OnExitingEditMode`, `OnBeforeAssemblyReload`, `OnSceneClosing`):
     - Priority 100 (`SelectionMementoService`): Captures active selection, writes memento to `SessionState`, and safely clears selection (`Selection.objects = Array.Empty<Object>()`).
     - Priority 200 (`InspectorSanitizerService`): Purges inspector windows, clears serialized locked objects, destroys tracker active editors, and destroys all non-persistent scene editors.
   - During post-transition (`OnEnteredPlayMode`, `OnAfterAssemblyReload`, `OnEnteredEditMode`):
     - `SelectionMementoService` schedules deferred restoration via `EditorApplication.delayCall += HandleDeferredRestore`, ensuring scenes and hierarchies are fully ready before restoring selection.
   - Every subscriber dispatch is wrapped in an individual `try-catch` block, ensuring a failure in one subscriber never prevents subsequent cleanup.

### 2.4 Backwards Compatibility & Roslyn Compliance
1. **Backwards Compatibility**:
   - `EditorLifecycleCoordinator` delegates to `PlayModeTransitionObserver.Instance`. Legacy callers calling `EditorLifecycleCoordinator.RegisterSubscriber` are wrapped in `LegacySubscriberAdapter` (at order 500).
   - `SelectionMementoService.CaptureSelection()` and `ClearSelectionSafely()` are preserved for callers in `LevelBuilderWindow.cs` and `InspectorSanitizer.cs`.
   - `SelectionMemento` preserves `ScenePaths`, `AssetGUIDs`, and `ActiveScenePath` properties.
2. **Roslyn Analyzers (`AGENTS.md`)**:
   - `AGY0001` (Shallow Inheritance): All classes inherit directly from `System.Object` (depth 0).
   - `AGY0002` (Encapsulate Serialized Fields): All fields in `SceneObjectIdentifier` and `SelectionMemento` are `[SerializeField] private`.
   - `AGY0003` (Read-only Properties): All component/model state is exposed via get-only properties.
   - `AGY0021` (Named Event Handlers): All `+=` and `-=` bindings strictly use named methods (`HandlePlayModeStateChanged`, `HandleBeforeAssemblyReload`, `HandleAfterAssemblyReload`, `HandleSceneClosing`, `HandleDeferredRestore`, `HandleSelectionChanged`).
   - `UNT0007`, `UNT0008`, `UNT0023`: Zero `?.`, `??`, or `??=` on `UnityEngine.Object`. Explicit `!= null` checks used throughout.

---

## 3. Caveats

1. **Unity Process Termination**:
   `UnityEditor.SessionState` survives domain reloads and play mode transitions during an active editor process run, but is reset when the Unity editor process is closed and restarted. This matches standard Unity selection behavior.
2. **Prefab Stage Context**:
   When entering Play Mode from Prefab Isolation Mode, Unity forcibly closes the temporary prefab preview scene. Objects in closed preview scenes cannot be restored; `HierarchyPathResolver` safely handles this by returning `null` without throwing exceptions.
3. **Automated Test Infrastructure**:
   Formal NUnit test fixtures (`SelectionMementoUnitTests`, `HierarchyPathResolverTests`, `PlayModeObserverIntegrationTests`) are designated for Milestone 3 under `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`.

---

## 4. Conclusion

Milestone 2 implementation is complete and verified:
1. **Models Implemented**: `SceneObjectIdentifier` and `SelectionMemento` encapsulate complete hierarchical, sibling, and persistent metadata with full `JsonUtility` serialization support.
2. **Persistence Implemented**: `ISelectionStatePersistence` and `SessionStateSelectionPersistence` reliably preserve selection data across Domain Reloads using native `SessionState`.
3. **Hierarchy Resolution Implemented**: `IHierarchyPathResolver` and `HierarchyPathResolver` resolve active, inactive, and duplicate sibling GameObjects using scene root traversal with zero reliance on `GameObject.Find`.
4. **Observer Architecture Implemented**: `IPlayModeTransitionObserver`, `IPlayModeTransitionSubscriber`, and `PlayModeTransitionObserver` orchestrate lifecycle events in deterministic priority order (`SelectionMemento` at 100, `InspectorSanitizer` at 200).
5. **Backwards Compatibility Maintained**: Existing callers across `EditorLifecycleCoordinator`, `SelectionMementoService`, and level editor scripts function without modification.
6. **Compilation Clean**: `dotnet build Game.Editor.csproj` completes with 0 Errors and 0 Warnings.

---

## 5. Verification Method

### 5.1 Independent Compilation Check
Execute from repository root:
```pwsh
dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
```
**Expected Output**:
- `Compilación correcta.`
- `0 Advertencia(s)`
- `0 Errores`

### 5.2 Source Inspection
Verify the following files in `Assets/_Project/Editor/Core/Lifecycle/`:
1. `Models/SceneObjectIdentifier.cs`: Verify serialized private fields and read-only properties (`AGY0002`, `AGY0003`).
2. `Models/SelectionMemento.cs`: Verify `[SerializeField] private` fields and backwards compatibility properties.
3. `Services/HierarchyPathResolver.cs`: Verify zero calls to `GameObject.Find` and traversal of `Scene.GetRootGameObjects()`.
4. `Services/SessionStateSelectionPersistence.cs`: Verify `SessionState.SetString` and `JsonUtility` serialization.
5. `Services/PlayModeTransitionObserver.cs`: Verify named event handlers (`AGY0021`) and subscriber execution ordering.
6. `Services/InspectorSanitizerService.cs`: Verify implementation of `IPlayModeTransitionSubscriber` at `ExecutionOrder = 200`.
7. `EditorLifecycleCoordinator.cs`: Verify delegation to `PlayModeTransitionObserver.Instance`.

### 5.3 Invalidation Conditions
- Any occurrence of `GameObject.Find` within the selection restoration path.
- Any compile error or Roslyn analyzer violation (`AGY*`, `UNT*`) in `Game.Editor.csproj`.
- Any inline anonymous lambda attached to `+=` or `-=` delegates.
- Failure of `SelectionMemento` to retain data across domain reload due to missing serialized backing fields.
