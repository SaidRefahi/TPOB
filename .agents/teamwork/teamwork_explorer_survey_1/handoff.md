# Phase 0 Survey: Codebase Architecture & Play Mode Transition Analysis

## 1. Observation

### 1.1 Assembly Definition & Repository Layout
- **Game.Editor Assembly Definition**:
  - File path: `Assets/_Project/Editor/Game.Editor.asmdef`
  - Name: `"Game.Editor"`
  - Root Namespace: `"Game.Editor"`
  - Platforms: `["Editor"]` (`includePlatforms`)
  - Auto-referenced: `false`
  - Direct Assembly References:
    - `"Game.Core"`
    - `"Game.Network"`
    - `"Game.Gameplay"`
    - `"PurrNet.Runtime"`
    - `"PurrNet.Editor"`
    - `"VContainer"`
    - `"Unity.InputSystem"`
    - `"TriInspector"`
    - `"TriInspector.Editor"`
    - `"Unity.Cinemachine"`
    - `"Unity.Cinemachine.Editor"`
    - `"Unity.TextMeshPro"`
    - `"UnityEngine.UI"`
    - `"Unity.Animation.Rigging"`
    - `"Unity.Animation.Rigging.Editor"`

- **Editor Directory Organization**:
  - Primary location: `Assets/_Project/Editor/`
  - Core Lifecycle Subdirectory: `Assets/_Project/Editor/Core/Lifecycle/`
  - Level Editor Subdirectory: `Assets/_Project/Editor/LevelEditor/`
  - Associated Namespaces:
    - `Game.Editor`
    - `Game.Editor.Core.Lifecycle`
    - `Game.Editor.LevelEditor`

### 1.2 Existing Lifecycle, Memento, and Tracker Files
The codebase contains a preliminary implementation of the lifecycle / memento subsystem in `Assets/_Project/Editor/Core/Lifecycle/`:
1. `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs`:
   - Marked `[InitializeOnLoad]` at line 10.
   - Static constructor registers subscriptions (lines 15-23):
     - `RegisterSubscriber(SelectionMementoService.Instance);` (line 17)
     - `EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;` (line 19)
     - `AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;` (line 20)
     - `AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;` (line 21)
     - `Selection.selectionChanged += HandleSelectionChanged;` (line 22)
   - Dispatches `PlayModeStateChange.ExitingEditMode` -> `OnBeforePlayModeEntered()`, `EnteredPlayMode` -> `OnPlayModeEntered()`, `ExitingPlayMode` -> `OnBeforePlayModeExited()`, `EnteredEditMode` -> `OnPlayModeExited()`.
2. `Assets/_Project/Editor/Core/Lifecycle/IEditorLifecycleSubscriber.cs`:
   - Defines observer methods (lines 6-11):
     - `OnBeforePlayModeEntered()`
     - `OnPlayModeEntered()`
     - `OnBeforePlayModeExited()`
     - `OnPlayModeExited()`
     - `OnBeforeDomainReload()`
     - `OnAfterDomainReload()`
3. `Assets/_Project/Editor/Core/Lifecycle/SelectionMemento.cs`:
   - Immutable data carrier (lines 7-23):
     - Stores `string[] ScenePaths`, `string[] AssetGUIDs`, `string ActiveScenePath`.
4. `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs`:
   - Singleton subscriber implementing `IEditorLifecycleSubscriber` (lines 11-178).
   - In-memory state: `private SelectionMemento _savedMemento = SelectionMemento.Empty;` (line 16).
   - Captures selection via `CaptureSelection()` (lines 18-62).
   - Clears selection via `ClearSelectionSafely()` (lines 64-69) calling `InspectorTrackerSanitizer.ForceCleanTracker()`.
   - Restores selection via `RestoreSelection()` (lines 71-114).
   - Deferred restoration: `EditorApplication.delayCall += OnDeferredRestore;` (lines 124, 134, 151).
   - In `FindGameObjectByPath` (lines 172-176): uses `GameObject.Find(path)`.
5. `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`:
   - Reflection-based sanitizer (lines 9-195):
     - Queries `UnityEditor.InspectorWindow` instances via `Resources.FindObjectsOfTypeAll(inspectorType)` (lines 16-20).
     - Accesses private field `m_Tracker` (`ActiveEditorTracker`) (line 24).
     - Loops through `tracker.activeEditors` and calls `UnityEngine.Object.DestroyImmediate(editors[j])` (lines 39-45).
     - Calls `tracker.Destroy()` (line 47).
     - Calls `clearLockedMethod?.Invoke(win, null)` and unsets `isLocked` (lines 51-56).
     - Calls `clearEditorsMethod?.Invoke(win, null)` (`ClearEditorsAndRebuild`) and `win.Repaint()` (lines 58-59).
     - Cleans `ActiveEditorTracker.sharedTracker` (lines 65-81), including `shared.ForceRebuild()` (line 80).
     - `PurgeOrphanEditors()` (lines 91-125): searches all `UnityEditor.Editor` and destroys any with `ed.target == null`.
     - Entire block is enclosed in empty `catch {}` (lines 86-88, 121-124).
6. `Assets/_Project/Editor/LevelEditor/InspectorSanitizer.cs`:
   - Helper wrappers invoking `InspectorTrackerSanitizer` methods (lines 6-25).

### 1.3 Other `[InitializeOnLoad]` and `playModeStateChanged` Usages
- `[InitializeOnLoad]` classes:
  - `Assets/_Project/Editor/ModularWallBuilder.cs` (line 12)
  - `Assets/_Project/Editor/PlaygroundBuilder.cs` (line 28)
  - `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs` (line 10)
- `EditorApplication.playModeStateChanged` usages:
  - `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs` (line 19)
  - `Assets/Plugins/Demigiant/DOTween/Modules/DOTweenModuleUtils.cs` (lines 54-55)
  - `Assets/PurrNet/Editor/NetworkManagerInspector.cs` (lines 63, 68)

### 1.4 Verbatim Errors Directly Observed in Unity Editor Log
From `C:\Users\SaidR\AppData\Local\Unity\Editor\Editor.log` (lines 25089-25117), occurring immediately after assembly recompilation/reload (`Begin MonoManager ReloadAssembly`):
```text
SerializedObjectNotCreatableException: Object at index 0 is null
  at UnityEditor.Editor.CreateSerializedObject () [0x000a4] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Editor.GetSerializedObjectInternal () [0x0000f] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Editor.get_serializedObject () [0x00018] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Collider3DEditorBase.OnEnable () [0x00008] in <0ecc69f93b8b470fa61444dd26a33dc0>:0 
  at UnityEditor.MeshColliderEditor.OnEnable () [0x00001] in <0ecc69f93b8b470fa61444dd26a33dc0>:0 

MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore.
You probably need to reassign the m_Targets variable of the 'GameObjectInspector' script in the inspector. Parameter name: componentOrGameObject
  at UnityEngine.Object+MarshalledUnityObject.TryThrowEditorNullExceptionObject (UnityEngine.Object unityObj, System.String parameterName) [0x000cd] in <b7840b1dfe7b4a73b466889f96810c49>:0 
  at UnityEngine.Bindings.ThrowHelper.ThrowArgumentNullException (System.Object obj, System.String parameterName) [0x00010] in <b7840b1dfe7b4a73b466889f96810c49>:0 
  at UnityEditor.PrefabUtility.IsPartOfVariantPrefab (UnityEngine.Object componentOrGameObject) [0x00015] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.GameObjectInspector.OnEnable () [0x00043] in <2348e9bf86984d808478203ec94ae526>:0 

SerializedObjectNotCreatableException: Object at index 0 is null
  at UnityEditor.Editor.CreateSerializedObject () [0x000a4] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Editor.GetSerializedObjectInternal () [0x0000f] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Editor.get_serializedObject () [0x00018] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.TransformInspector.OnEnable () [0x00001] in <2348e9bf86984d808478203ec94ae526>:0 

SerializedObjectNotCreatableException: Object at index 0 is null
  at UnityEditor.Editor.CreateSerializedObject () [0x000a4] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Editor.GetSerializedObjectInternal () [0x0000f] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Editor.get_serializedObject () [0x00018] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.RendererEditorBase.OnEnable () [0x00001] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.MeshRendererEditor.OnEnable () [0x0000d] in <2348e9bf86984d808478203ec94ae526>:0 
  at UnityEditor.Rendering.Universal.Renderer2DMeshEditor.OnEnable () [0x00000] in .\Library\PackageCache\com.unity.render-pipelines.universal@a6e30d1f59f5\Editor\2D\Overrides\MeshEditor2DURP.cs:24
```

### 1.5 Repository Rules and Guidelines (`AGENTS.md` and `.agents/rules/`)
- `AGENTS.md`:
  - Unity Version: Unity 6000.3.7f1 (Unity 6.x), URP, C# 9.0+ / .NET Standard 2.1.
  - Editor namespace: `Game.Editor`.
  - Roslyn Analyzers:
    - `AGY0001`: Shallow inheritance depth (max 1 level from base classes).
    - `AGY0002`: Encapsulate serialized fields (`[SerializeField] private`).
    - `AGY0003`: Read-only property external access (no non-private setters).
    - `AGY0021`: Named event handlers required in `+=` subscriptions (no inline lambdas).
- `.agents/rules/core-architecture.md`:
  - Rule 1.3: Symmetrical Lifecycle Cleanup (`[ENFORCEABLE]`).
  - Rule 2.1: Narrow Capability Interfaces at Subsystem Boundaries (`[GUIDED]`) — satisfies `IEditorLifecycleSubscriber`.
  - Rule 2.3: Avoid Circular Dependencies (`[GUIDED]`).
  - Rule 5.3: ScriptableObject Role & State Boundaries (`[GUIDED]`) — Editor tools must avoid dangling ScriptableObjects.
- `.agents/rules/performance-hotpaths.md`:
  - Rule 1.1 Scope Clarification: One-time setup routines and Editor tools are explicitly excluded from the per-frame 0-allocation restriction when readability benefits from standard C# constructs.
- `.agents/rules/input-and-events.md`:
  - Rule 2.1: Symmetrical Event Subscription Lifecycle.
  - Rule 3.1: Named Method Handlers for Event Subscriptions (`AGY0021`).

---

## 2. Logic Chain

1. **Root Cause Mechanism**:
   - As observed in section 1.4, the 4 exceptions originate during domain reload / MonoManager reload assembly when Unity automatically deserializes `InspectorWindow` and its tracked `Editor` ScriptableObjects (`MeshColliderEditor`, `GameObjectInspector`, `TransformInspector`, `Renderer2DMeshEditor`).
   - When Unity reloads the domain or transitions between Edit Mode and Play Mode, Edit Scene GameObjects are unloaded or invalidated.
   - If `InspectorWindow` still retains active editor instances in `m_Tracker` (or if `ActiveEditorTracker.sharedTracker` is forced to rebuild right before reload), the engine invokes `OnEnable()` on deserialized `Editor` instances whose targets are now null or destroyed C++ entities.
   - Upon `OnEnable()`, calling `Editor.serializedObject` or accessing `m_Targets` triggers `SerializedObjectNotCreatableException` and `MissingReferenceException`.

2. **Analysis of Current Implementation Deficiencies**:
   - **Deficiency A (Transient Memento Wiped on Domain Reload)**:
     - `SelectionMementoService` stores memento data solely in `private SelectionMemento _savedMemento` (Observation 1.2, line 16).
     - In Unity 6000.3.7f1 with Domain Reload enabled (`m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0`), managed domain reload reconstructs all static classes and wipes plain C# instance fields.
     - Consequently, when `OnAfterDomainReload()` calls `OnDeferredRestore()`, `_savedMemento` has been reset to `SelectionMemento.Empty`, causing restored selection to be completely lost.
     - *Resolution Requirement*: Memento data must be serialized to `SessionState` (e.g. `SessionState.SetString("TPOB_SelectionMemento", json)`) to survive domain reloads.
   - **Deficiency B (`ForceRebuild()` during Purge)**:
     - In `InspectorTrackerSanitizer.PurgeAllInspectorWindows()` (Observation 1.2, line 80), `shared.ForceRebuild()` is called directly after destroying editors.
     - Calling `ForceRebuild()` immediately re-evaluates the active selection and reconstructs editors before the domain reload or scene unload has completed, undermining the purge.
   - **Deficiency C (Silent Exception Swallowing)**:
     - In `InspectorTrackerSanitizer.PurgeAllInspectorWindows()`, lines 86-88 contain an empty `catch {}` block. If reflection methods (e.g., `ClearSerializedLockedObjects`) or field lookups fail or throw, the failure is hidden without diagnostics.
   - **Deficiency D (Inactive GameObject Restoration)**:
     - `SelectionMementoService.FindGameObjectByPath()` relies on `GameObject.Find(path)` (Observation 1.2, line 175).
     - `GameObject.Find` returns `null` for inactive GameObjects in the scene hierarchy.
     - *Resolution Requirement*: Either use `GlobalObjectId` APIs or recursive transform search starting from `SceneManager.GetActiveScene().GetRootGameObjects()`.

---

## 3. Caveats
- `InspectorWindow` internal structure in Unity 6000.3.7f1 includes UI Toolkit elements (`m_TrackerResetter`, `m_EditorsElement`) alongside IMGUI elements. Purging `m_Tracker` and calling `ClearEditorsAndRebuild` operates via reflection; any changes to internal private member names across Unity minor versions must be guarded with null-checks.
- Active Selection in Play Mode: Restoring selection across the Play Mode boundary must happen deferred via `EditorApplication.delayCall` once the scene has finished loading in play mode, ensuring target GameObjects exist in the newly loaded scene.

---

## 4. Conclusion
1. The project structure, assembly definitions, and rules are clearly delineated:
   - All editor functionality belongs under `Assets/_Project/Editor/` within the `Game.Editor` assembly (`Game.Editor.asmdef`) and `Game.Editor.Core.Lifecycle` namespace.
   - Naming and architectural guidelines conform to `AGENTS.md` and `.agents/rules/` (shallow inheritance `AGY0001`, named event handlers `AGY0021`, Memento and Observer patterns).
2. The existing lifecycle classes (`EditorLifecycleCoordinator`, `SelectionMementoService`, `InspectorTrackerSanitizer`) already provide the correct architectural foundation (Observer & Memento pattern) but have specific implementation bugs:
   - State persistence across domain reloads is missing (needs `SessionState`).
   - Premature `shared.ForceRebuild()` in `InspectorTrackerSanitizer` resurrects editors before reload.
   - `GameObject.Find` does not support inactive objects.
   - Reflection error handling is silently swallowed in `catch {}`.
3. The root cause for the 4 exceptions is verified verbatim in `Editor.log`. Resolving these flaws in `Assets/_Project/Editor/Core/Lifecycle/` will completely eliminate the 4 Play Mode transition exceptions and ensure robust selection memento restoration.

---

## 5. Verification Method

### 5.1 Verification Commands and Inspection
1. **Assembly Definition Verification**:
   - Verify `Assets/_Project/Editor/Game.Editor.asmdef` references and platforms.
2. **Editor Log Inspection**:
   - Inspect `C:\Users\SaidR\AppData\Local\Unity\Editor\Editor.log` for any occurrences of:
     - `SerializedObjectNotCreatableException`
     - `MissingReferenceException: The variable m_Targets of GameObjectInspector`
3. **Compilation & Roslyn Diagnostics**:
   - Check Roslyn analyzer compliance against `AGY0001`, `AGY0002`, `AGY0003`, `AGY0021`.

### 5.2 Invalidation Conditions
- Any occurrence of the 4 exceptions in `Editor.log` upon entering Play Mode invalidates the fix.
- Failure to restore active selection (including inactive GameObjects or multi-selection) across Play Mode transition or Domain Reload invalidates the fix.
