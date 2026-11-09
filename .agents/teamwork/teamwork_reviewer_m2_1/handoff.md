# Review & Adversarial Critic Handoff Report: Milestone 2 — Selection Memento Service & PlayMode Observer

**Reviewer**: `teamwork_reviewer_m2_1`  
**Roles**: Reviewer, Adversarial Critic  
**Milestone**: Milestone 2: Selection Memento Service & PlayMode Observer  
**Date**: 2026-11-07  
**Parent Orchestrator**: `3370c761-5485-4812-ad19-6f9cfcb9094d`  
**Verdict**: **APPROVE**

---

## 1. Observation

### 1.1 Independent Compilation Check
Executed the build command from project root:
```pwsh
dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
```
Direct command output:
```text
  ToolbarExtender.Editor -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\ToolbarExtender.Editor\ToolbarExtender.Editor.dll
  PurrNet.Edgegap.Runtime -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Edgegap.Runtime\PurrNet.Edgegap.Runtime.dll
  LiteNetLib -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\LiteNetLib\LiteNetLib.dll
  SimpleWebTransport -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\SimpleWebTransport\SimpleWebTransport.dll
  PurrNet.Runtime -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Runtime\PurrNet.Runtime.dll
  PurrNet.Editor -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Editor\PurrNet.Editor.dll
  Game.Core -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Core\Game.Core.dll
  Game.Network -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Network\Game.Network.dll
  Game.Gameplay -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Gameplay\Game.Gameplay.dll
  Game.Editor -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll

Compilación correcta.
    0 Advertencia(s)
    0 Errores
```
Exit code: `0`. There are 0 compilation errors and 0 Roslyn analyzer warnings.

### 1.2 Integrity and Genuineness Verification
Audited implementation across all 14 files in `Assets/_Project/Editor/Core/Lifecycle/`:
- **No Hardcoding**: No hardcoded test outputs or dummy return values embedded in production logic.
- **No Facades**: All services (`HierarchyPathResolver`, `SessionStateSelectionPersistence`, `SelectionMementoService`, `PlayModeTransitionObserver`, `InspectorSanitizerService`) contain complete, functional logic.
- **No Unauthorized Shortcuts**: No delegating core work to external tools or bypasses.
- **No Fabricated Verifications**: Independent build and code inspection executed directly.
- **Integrity Status**: CLEAN. No integrity violations detected.

### 1.3 Roslyn & Repository Rules Conformance Check
- **`AGY0001` (Shallow Inheritance)**:
  - `SceneObjectIdentifier.cs:13`: inherits directly from `System.Object` (depth 0).
  - `SelectionMemento.cs:13`: inherits directly from `System.Object` (depth 0).
  - `HierarchyPathResolver.cs:17`: inherits directly from `System.Object` (depth 0).
  - `SessionStateSelectionPersistence.cs:14`: inherits directly from `System.Object` (depth 0).
  - `SelectionMementoService.cs:17`: inherits directly from `System.Object` (depth 0).
  - `PlayModeTransitionObserver.cs:17`: inherits directly from `System.Object` (depth 0).
  - `InspectorSanitizerService.cs:18`: inherits directly from `System.Object` (depth 0).
  - `EditorLifecycleCoordinator.LegacySubscriberAdapter:124`: inherits directly from `System.Object` (depth 0).
- **`AGY0002` (Encapsulate Serialized Fields)**:
  - All `[SerializeField]` attributes are `private`:
    - `SelectionMemento.cs:17-23`: `[SerializeField] private SceneObjectIdentifier[] _sceneObjects;`, `_assetGuids;`, `_assetGlobalObjectIds;`, `_activeSceneObject;`, `_activeAssetGuid;`, `_activeGlobalObjectId;`, `_timestampTicks;`.
    - `SceneObjectIdentifier.cs:15-20`: `[SerializeField] private string _sceneName;`, `_scenePath;`, `_globalObjectIdString;`, `_hierarchyPath;`, `_pathSegments;`, `_siblingIndices;`.
  - Zero occurrences of public or non-private `[SerializeField]`.
- **`AGY0003` (Read-only Property External Access)**:
  - All public properties on models and services have no public/internal setters.
  - Zero occurrences of `set;` across all 14 files in `Assets/_Project/Editor/Core/Lifecycle/`.
- **`AGY0021` (Named Event Handlers)**:
  - Every single `+=` and `-=` subscription binds exclusively to a named method:
    - `SelectionMementoService.cs:338, 348, 371`: `EditorApplication.delayCall += HandleDeferredRestore;`
    - `PlayModeTransitionObserver.cs:57-60`: `EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;`, `AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;`, `AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;`, `EditorSceneManager.sceneClosing += HandleSceneClosing;`
    - `PlayModeTransitionObserver.cs:68-71`: Symmetrical `-=` unbinding with identical named methods.
    - `EditorLifecycleCoordinator.cs:32-33`: Symmetrical unbind/bind with named method `HandleSelectionChanged`.
  - Zero occurrences of inline lambdas or anonymous delegates attached to events.
- **`UNT0007`/`UNT0008`/`UNT0023` (No `?.`, `??`, `??=` on `UnityEngine.Object`)**:
  - Found 0 occurrences of `?.` in `Assets/_Project/Editor/Core/Lifecycle/`.
  - All `??` and `??=` occurrences are strictly confined to C# managed objects (`string`, `string[]`, `int[]`, `System.Type`, interface references).
  - All `UnityEngine.Object` null checks use explicit `== null`, `!= null`, or type pattern matching (`is GameObject`).

### 1.4 JsonUtility Serialization Compatibility
- `SelectionMemento` is marked `[Serializable]`.
- Backing fields (`_sceneObjects`, `_assetGuids`, `_assetGlobalObjectIds`, `_activeSceneObject`, `_activeAssetGuid`, `_activeGlobalObjectId`, `_timestampTicks`) are decorated with `[SerializeField] private`.
- `SceneObjectIdentifier` is marked `[Serializable]` with `[SerializeField] private` fields of primitive/string/array types.
- Auto-properties are not used for serialized data; get-only properties read from the private serialized backing fields.
- `JsonUtility.ToJson` properly extracts and serializes these backing fields into JSON payload, which is persisted in `SessionState.SetString`.

### 1.5 HierarchyPathResolver Capabilities
- Zero calls to `GameObject.Find`.
- Traverses `Scene.GetRootGameObjects()` in `HierarchyPathResolver.cs:195`.
- Inactive GameObjects: `scene.GetRootGameObjects()` and `transform.GetChild()` include both active and inactive GameObjects.
- Disambiguation: Sibling indices (`_siblingIndices`) are captured during `BuildIdentifier` (root to leaf) and matched first in `ResolveInScene` (lines 206-216 for root, lines 244-255 for children). If sibling names are identical, distinct sibling indices ensure the correct instance is resolved.
- Two-Tier pipeline: Fast path uses `GlobalObjectId.TryParse` and `GlobalObjectId.GlobalObjectIdentifierToObjectSlow`; fallback path uses scene root traversal; global fallback uses `Resources.FindObjectsOfTypeAll<GameObject>()`.

### 1.6 Execution Ordering in PlayModeTransitionObserver
- Defined priority order:
  - `PlayModeExecutionOrder.SelectionMemento = 100` (`PlayModeExecutionOrder.cs:13`)
  - `PlayModeExecutionOrder.InspectorSanitizer = 200` (`PlayModeExecutionOrder.cs:18`)
- In `PlayModeTransitionObserver.RegisterDefaultSubscribers()`:
  - `SelectionMementoService.Instance` registered at order 100.
  - `InspectorSanitizerService.Instance` registered at order 200.
- `PlayModeTransitionObserver.RegisterSubscriber` maintains sorted insertion order (`_subscribers[insertIdx].ExecutionOrder <= subscriber.ExecutionOrder`).
- Pre-transition execution order: Priority 100 (`SelectionMementoService`) captures selection, saves memento to `SessionState`, and safely clears selection. Priority 200 (`InspectorSanitizerService`) subsequently clears locked buffers, destroys tracker active editors, and destroys orphan/scene editors.

---

## 2. Logic Chain

1. **Premise 1 (Integrity & Roslyn Compliance)**:
   Observations 1.1, 1.2, and 1.3 show `dotnet build Game.Editor.csproj` compiles with 0 errors and 0 warnings, and manual AST review confirms adherence to `AGY0001`, `AGY0002`, `AGY0003`, `AGY0021`, and `UNT0007`/`8`/`23`.
2. **Premise 2 (JsonUtility Serialization)**:
   Observation 1.4 confirms `SelectionMemento` and `SceneObjectIdentifier` use `[SerializeField] private` fields of supported serializable types rather than bare auto-properties. Thus, `JsonUtility.ToJson` generates a valid JSON representation that survives domain reload when stored in native `SessionState`.
3. **Premise 3 (Hierarchy Resolution Correctness)**:
   Observation 1.5 proves `HierarchyPathResolver` avoids `GameObject.Find`, searches `Scene.GetRootGameObjects()`, and traverses `Transform.GetChild()` using preserved sibling indices, correctly identifying inactive GameObjects and disambiguating duplicate sibling names.
4. **Premise 4 (Lifecycle Execution Ordering)**:
   Observation 1.6 verifies that `SelectionMementoService` executes at order 100 while `InspectorSanitizerService` executes at order 200. This ensures selection is captured into persistent storage before inspector trackers and scene editor objects are purged, eliminating race conditions and avoiding editor null target exceptions upon Play Mode entry.
5. **Premise 5 (Adversarial Stress-Testing)**:
   - *Scenario A (Subscriber Failure)*: `PlayModeTransitionObserver` wraps each subscriber invocation in an independent `try-catch` block. A failure in one subscriber cannot abort subsequent cleanup.
   - *Scenario B (Multi-Scene Conflicts)*: `SceneObjectIdentifier` records `_sceneName` and `_scenePath`, querying `SceneManager.GetSceneByPath` and `SceneManager.GetSceneByName` before attempting active or multi-scene traversal.
   - *Scenario C (Null Selection Entries)*: `EditorLifecycleCoordinator.HandleSelectionChanged` filters null elements out of `Selection.objects` and triggers sanitizer cleanup if destroyed objects linger in selection.

---

## 3. Caveats

1. **Unity Process Lifecycle**:
   `UnityEditor.SessionState` persists across domain reloads and Play Mode entries/exits within a running Unity Editor process session, but is cleared if the entire Unity Editor process is shut down and reopened. This is normal and expected for Editor selection state.
2. **Prefab Isolation Stage**:
   If entering Play Mode while inside Prefab Stage (Isolation Mode), Unity automatically closes the preview scene upon entering Play Mode. Objects located in the closed preview scene cannot be resolved in the main Play scene; `HierarchyPathResolver` handles this gracefully by returning `null` without throwing.
3. **Milestone 3 Test Infrastructure**:
   Formal automated unit/integration test fixtures (`Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`) are scheduled for implementation in Milestone 3. All service singletons provide `SetInstanceForTesting` / `SetServiceForTesting` hooks to facilitate dependency injection and mocking during M3.

---

## 4. Conclusion

The Milestone 2 implementation satisfies all technical, architectural, and repository requirements:
- The Memento pattern (`SelectionMementoService`, `SessionStateSelectionPersistence`, `HierarchyPathResolver`) and Observer pattern (`PlayModeTransitionObserver`, `PlayModeExecutionOrder`, `IPlayModeTransitionSubscriber`) are cleanly implemented according to SOLID principles.
- Backwards compatibility is preserved via `EditorLifecycleCoordinator` adapters and `InspectorTrackerSanitizer` facades.
- Roslyn analyzer rules and zero-GC principles are strictly respected.
- Build compiles cleanly with 0 Errors and 0 Warnings.

**Verdict**: **APPROVE**

---

## 5. Verification Method

### 5.1 Compilation Verification
Run from the repository root:
```pwsh
dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
```
**Expected Result**:
- Exit code: `0`
- `Compilación correcta.`
- `0 Advertencia(s)`
- `0 Errores`

### 5.2 Key Inspection Points
- `Assets/_Project/Editor/Core/Lifecycle/Models/SelectionMemento.cs`: Verify `[SerializeField] private` fields and get-only properties.
- `Assets/_Project/Editor/Core/Lifecycle/Services/HierarchyPathResolver.cs`: Verify zero calls to `GameObject.Find` and traversal of `Scene.GetRootGameObjects()`.
- `Assets/_Project/Editor/Core/Lifecycle/Services/PlayModeTransitionObserver.cs`: Verify execution order sorting and named event handlers (`AGY0021`).
- `Assets/_Project/Editor/Core/Lifecycle/PlayModeExecutionOrder.cs`: Verify `SelectionMemento = 100` and `InspectorSanitizer = 200`.

### 5.3 Invalidation Conditions
- Any occurrence of `GameObject.Find` in `HierarchyPathResolver`.
- Any Roslyn analyzer warning or error under `dotnet build Game.Editor.csproj`.
- Any inline lambda bound to `+=` or `-=` events.
- Any `?.`, `??`, or `??=` operator used on a `UnityEngine.Object`.
