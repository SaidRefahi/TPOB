# Handoff Report: Milestone 1 — Rules, Static Analysis & Verification Guidelines

**Agent**: `teamwork_explorer_m1_3`  
**Milestone**: Milestone 1: Inspector & Editor Sanitizer Service  
**Target Components**: `IInspectorSanitizerService`, `InspectorSanitizerService`, `InspectorTrackerSanitizer`

---

## 1. Observation

### 1.1 Context and Problem Statement
From `ORIGINAL_REQUEST.md` (lines 13-19):
Entering Play Mode in Unity 6000.3.7f1 triggers four console exceptions:
- `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
- `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

### 1.2 Repository Rule Directives
Direct inspection of repository rules reveals strict constraints enforced at build and analysis time:

1. **`AGENTS.md` and `.editorconfig`**:
   - **`AGY0001` (Severity: `error`)**: Shallow inheritance depth (max 1 level from base domain classes).
   - **`AGY0002` (Severity: `error`)**: Encapsulate serialized fields (`[SerializeField] private`). All fields must be `private`.
   - **`AGY0003` (Severity: `error`)**: Read-only property external access (no non-private setters on component state).
   - **`AGY0011` (Severity: `error`)**: Prohibit LINQ methods and queries in hot paths.
   - **`AGY0012` (Severity: `error`)**: Prohibit `GetComponent*` calls in hot paths (cache in `Awake`).
   - **`AGY0013` (Severity: `error`)**: Prohibit reference type instantiation (`new`) in hot paths.
   - **`AGY0014` (Severity: `error`)**: Prohibit string manipulation/interpolation/`ToString` and logging in hot paths.
   - **`AGY0015` (Severity: `error`)**: Prohibit value type boxing in hot paths.
   - **`AGY0016` (Severity: `error`)**: Prohibit closures and lambdas in hot paths.
   - **`AGY0021` (Severity: `error`)**: Named event handlers required in `+=` subscriptions (no inline lambdas).
   - **`UNT0007`, `UNT0008`, `UNT0023` (Severity: `error`)**: Prohibit `?.`, `??`, and `??=` null-coalescing/null-conditional operators on `UnityEngine.Object` types.

2. **`.agents/rules/core-architecture.md`**:
   - **Rule 1.3 (Symmetrical Lifecycle Cleanup)**: "Always clean up event subscriptions, stop running coroutines, and cancel `CancellationTokenSource` instances symmetrically."
   - **Rule 2.1 (Narrow Capability Interfaces)**: "Inter-system communication and cross-entity interactions should depend on focused capability interfaces."
   - **Rule 4.1 (Shallow Inheritance)**: "Keep inheritance trees shallow: maximum 1 level of domain inheritance from abstract base classes."
   - **Rule 5.1 & 5.2 (Encapsulation & Access)**: "All component fields must be `private` by default... Expose component state to external callers strictly through public read-only properties or expression-bodied getters."

3. **`.agents/rules/performance-hotpaths.md`**:
   - **Rule 1.1**: "Scope Clarification: One-time setup routines (`Awake()`, `Start()`, scene loaders, asset setup) and Editor tools are explicitly excluded from the zero-allocation restriction when readability benefits from standard C# constructs."
   - However, for Editor lifecycle routines executed during scene unload / Play Mode transition, minimizing GC overhead and eliminating repeated reflection queries is critical to prevent editor stutter and serialization delays.

4. **Existing Code Violations in `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`**:
   - Lines 47 & 79: `tracker.Destroy()` and `shared.Destroy()` invoke native C++ handle destruction, invalidating native pointers and causing subsequent null-pointer crashes or exceptions.
   - Lines 86-88, 122-124, 144-146, 175-177, 191-193: Silent `catch {}` blocks swallow critical errors and abort subsequent cleanup routines.
   - Lines 16-25: Re-running reflection queries (`GetType`, `GetMethod`, `GetField`, `GetProperty`) on every single call without caching.
   - Lines 106-114: Checks only `if (ed.target == null) isDead = true;`, failing to destroy scene-bound editors (`!EditorUtility.IsPersistent(ed.target)`) before scene unload.

---

## 2. Logic Chain

### 2.1 Static Analysis Rule Compliance Mechanics

#### 1. Roslyn Rule `AGY0001` (Shallow Inheritance)
- **Constraint**: No class may inherit more than 1 level of domain inheritance.
- **Application**:
  - `InspectorSanitizerService` derives directly from `System.Object` (implicit) and implements `IInspectorSanitizerService`. Class inheritance depth is 0.
  - `InspectorTrackerSanitizer` is a `static class` (derives from `System.Object`). Inheritance depth is 0.
  - `IInspectorSanitizerService` is an interface.
- **Worker Mandate**: Declare `public sealed class InspectorSanitizerService : IInspectorSanitizerService`.

#### 2. Roslyn Rule `AGY0002` (Serialized / Field Encapsulation)
- **Constraint**: All fields must be `private`. No public or internal fields.
- **Application**:
  - All cached reflection fields (`Type`, `MethodInfo`, `FieldInfo`, `PropertyInfo`) in `InspectorSanitizerService` must be `private static readonly`.
  - The singleton instance in `InspectorSanitizerService` must be stored in a `private static readonly Lazy<InspectorSanitizerService>` or `private static readonly InspectorSanitizerService`.
  - In `InspectorTrackerSanitizer`, the backing service reference must be `private static IInspectorSanitizerService s_service`.
- **Worker Mandate**: Verify that ZERO fields are declared `public`, `protected`, or `internal`.

#### 3. Roslyn Rule `AGY0003` (Read-Only Property Access)
- **Constraint**: No non-private setters on properties. External access strictly through read-only getters (`get;` or expression-bodied `=>`).
- **Application**:
  - `InspectorSanitizerService.Instance` must be `public static InspectorSanitizerService Instance => s_instance.Value;` (no setter).
  - In `InspectorTrackerSanitizer`, `public static IInspectorSanitizerService Service => s_service ??= InspectorSanitizerService.Instance;` must NOT expose a public setter.
  - For testing or dependency substitution, provide an explicit method instead of a property setter: `public static void SetServiceForTesting(IInspectorSanitizerService service)`.
- **Worker Mandate**: Ensure zero properties have `public set;` or `internal set;`.

#### 4. Roslyn Rule `AGY0021` (Named Event Handlers Required)
- **Constraint**: Prohibit inline anonymous lambdas in `+=` subscriptions (`action += () => ...`).
- **Application**:
  - If `InspectorSanitizerService` or `InspectorTrackerSanitizer` subscribes to any Unity Editor events (e.g., `EditorApplication.delayCall`, `Selection.selectionChanged`), it MUST use a named method:
    ```csharp
    // VIOLATION:
    EditorApplication.delayCall += () => PurgeAllInspectors();
    // COMPLIANT:
    EditorApplication.delayCall += OnDelayCallPurge;
    private static void OnDelayCallPurge() { ... }
    ```
- **Worker Mandate**: Any `+=` binding must reference a declared private static or instance method.

#### 5. Native Roslyn Rules `UNT0007`, `UNT0008`, `UNT0023` (`UnityEngine.Object` Null Checks)
- **Constraint**: Prohibit `?.`, `??`, and `??=` on any type deriving from `UnityEngine.Object`.
- **Underlying Reason**: Unity overrides `operator ==` and `operator !=` on `UnityEngine.Object` to check if the underlying native C++ object has been destroyed. In C#, the operators `?.`, `??`, and `??=` emit IL instructions (`ldnull`, `callvirt`) that check only managed reference nullness, bypassing Unity's overloaded `==` operator. If a native object is destroyed (e.g. after scene unload or `DestroyImmediate`), `?.` treats the object as valid, triggering `MissingReferenceException` or engine crashes!
- **Application**:
  - `UnityEditor.Editor` inherits from `ScriptableObject` -> `UnityEngine.Object`.
  - `UnityEditor.EditorWindow` inherits from `ScriptableObject` -> `UnityEngine.Object`.
  - Any inspected target `UnityEngine.Object` inherits from `UnityEngine.Object`.
- **Worker Mandate**: NEVER write `win?.Repaint()`, `ed?.target`, `target?.name`, or `obj ?? fallback`. ALWAYS write:
  ```csharp
  if (win != null)
  {
      win.Repaint();
  }
  if (ed != null)
  {
      var target = ed.target;
      if (target != null && !EditorUtility.IsPersistent(target))
      {
          UnityEngine.Object.DestroyImmediate(ed);
      }
  }
  ```

---

### 2.2 Performance & Zero-GC in Editor Context

1. **Reflection Caching**:
   - Reflection lookups (`GetType`, `GetMethod`, `GetField`, `GetProperty`) are CPU-intensive and allocate strings and member arrays.
   - Cache all reflection metadata once in `static readonly` fields (e.g., in a static constructor) in `InspectorSanitizerService`.
2. **Zero LINQ (`AGY0011`)**:
   - Do NOT use LINQ (`.Where()`, `.Select()`, `.Cast<T>()`, `.ToList()`, `.ToArray()`).
   - Use standard `for (int i = 0; i < array.Length; i++)` loops.
3. **Parameterless `Invoke` Optimization**:
   - `MethodInfo.Invoke(instance, null)` takes `null` for parameterless methods without allocating an `object[]`.
   - Never write `method.Invoke(instance, new object[0])`. Pass `null`.
4. **Zero Boxing (`AGY0015`)**:
   - Avoid boxing value types. When setting boolean properties via reflection (`isLockedProp.SetValue(win, false)`), `false` is boxed by the reflection API, but caching `private static readonly object BoxedFalse = false;` eliminates repeated heap boxing!

---

### 2.3 Symmetrical Lifecycle & Error Recovery

1. **Non-Destructive Tracker Sanitation**:
   - `ActiveEditorTracker.Destroy()` delegates to native `Internal_Destroy(this)`. Freeing native tracker pointers causes subsequent calls to `ForceRebuild()` or inspector painting to crash.
   - Safe sequence:
     1. Iterate `tracker.activeEditors`.
     2. Call `UnityEngine.Object.DestroyImmediate(editor)` for each active editor.
     3. Call `tracker.ClearDirty()`.
     4. DO NOT call `tracker.Destroy()` or `shared.Destroy()`.
2. **Scene & Orphan Editor Purge (`!EditorUtility.IsPersistent`)**:
   - At `ExitingEditMode`, the Edit Mode scene is still loaded, so `ed.target` is not null.
   - To prevent `SerializedObjectNotCreatableException` and `MissingReferenceException` when the scene unloads seconds later, destroy any editor where:
     ```csharp
     bool shouldDestroy = false;
     try
     {
         if (ed.target == null || !EditorUtility.IsPersistent(ed.target))
         {
             shouldDestroy = true;
         }
     }
     catch
     {
         shouldDestroy = true;
     }
     if (shouldDestroy)
     {
         UnityEngine.Object.DestroyImmediate(ed);
     }
     ```
3. **Structured Warning Logging (Feature F4)**:
   - Replace empty `catch {}` with targeted exception handling and structured warning logs:
     ```csharp
     catch (Exception ex)
     {
         Debug.LogWarning($"[InspectorSanitizerService] Failed to sanitize inspector window: {ex.Message}");
     }
     ```
   - Individual window failures must never abort the purging of subsequent windows or trackers.

---

## 3. Caveats

1. **Unity 6 PropertyEditor Base Class Hierarchy**:
   `UnityEditor.InspectorWindow` inherits from `UnityEditor.PropertyEditor` in Unity 6000.3.7f1. `tracker`, `ClearEditorsAndRebuild`, and `ClearSerializedLockedObjects` are declared on `PropertyEditor`. The reflection caching logic should inspect `typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyEditor")` and fall back to `typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow")`.
2. **Read-Only Scope**:
   This report provides analytical guidance and verification criteria only; no production or test source files have been created or modified by this agent.
3. **Milestone 2/3 Interfaces**:
   `IPlayModeTransitionObserver` and `ISelectionMementoService` will be implemented in subsequent milestones. Milestone 1 must strictly focus on `IInspectorSanitizerService`, `InspectorSanitizerService`, and backwards-compatible delegation in `InspectorTrackerSanitizer`.

---

## 4. Conclusion & Worker Implementation Checklist

### 4.1 Interface Contract Definition

Create `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs`:
```csharp
#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service responsible for purging active inspector trackers, unlocking inspectors,
    /// and destroying orphan or scene-bound Editor ScriptableObjects before play mode transitions.
    /// </summary>
    public interface IInspectorSanitizerService
    {
        void PurgeAllInspectors();
        void PurgeSharedTracker();
        void DestroyOrphanAndSceneEditors();
        void UnlockAllInspectors();
    }
}
#endif
```

### 4.2 Worker Implementation Checklist (10 Strict Checkpoints)

The Worker agent must verify all 10 checkpoints before finalizing implementation:

- [ ] **Check 1 (`AGY0001` - Shallow Inheritance)**: `InspectorSanitizerService` is declared `public sealed class InspectorSanitizerService : IInspectorSanitizerService` (0 class inheritance).
- [ ] **Check 2 (`AGY0002` - Field Encapsulation)**: Every field in `InspectorSanitizerService` and `InspectorTrackerSanitizer` is `private`. Zero `public` or `internal` fields.
- [ ] **Check 3 (`AGY0003` - Read-Only Properties)**: Properties (`Instance`, `Service`) have only `get;` accessors. Zero public or internal setters.
- [ ] **Check 4 (`UNT0007`/`UNT0008`/`UNT0023` - UnityEngine.Object Null Checks)**: Zero usage of `?.`, `??`, or `??=` on `Editor`, `EditorWindow`, `GameObject`, or `Component`. All checks use explicit `if (obj != null)`.
- [ ] **Check 5 (`AGY0021` - Named Event Handlers)**: Any event subscription (`+=`) references a named static or instance method. Zero inline lambdas in event bindings.
- [ ] **Check 6 (Performance - Reflection Caching & Zero LINQ)**: All reflection types (`Type`, `MethodInfo`, `FieldInfo`, `PropertyInfo`) are cached in `private static readonly` fields. Zero LINQ imports or method calls (`using System.Linq;` omitted). Zero allocations on parameterless `Invoke(win, null)`. Boxed booleans cached if used.
- [ ] **Check 7 (Feature F2 - Non-Destructive Tracker Sanitation)**: `tracker.Destroy()` and `shared.Destroy()` are NEVER called. Only `activeEditors` are destroyed via `UnityEngine.Object.DestroyImmediate(editor)`.
- [ ] **Check 8 (Feature F3 - Scene & Orphan Editor Purge)**: `DestroyOrphanAndSceneEditors()` checks `ed.target == null || !EditorUtility.IsPersistent(ed.target)` to destroy editors before scene unload destroys native targets.
- [ ] **Check 9 (Feature F4 - Structured Diagnostics)**: Zero empty `catch {}` blocks. Replaced with `Debug.LogWarning($"[InspectorSanitizerService] ...: {ex.Message}")`. Granular try-catch per window ensures fault isolation.
- [ ] **Check 10 (Backwards Compatibility)**: `InspectorTrackerSanitizer` maintains all existing static methods (`PurgeAllInspectorWindows`, `PurgeOrphanEditors`, `ForceCleanTracker`, `SanitizeActiveEditors`, `UnlockInspectors`, `ClearConsole`) delegating to `InspectorSanitizerService.Instance` without breaking existing callers.

---

## 5. Verification Method

### 5.1 Static Analysis Verification Commands
To verify zero Roslyn analyzer rule violations, execute via CLI:
```pwsh
# 1. Verify compilation and analyzer diagnostic output
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -quit -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -logFile "compile.log"

# 2. Inspect compile.log for analyzer violations:
Get-Content "compile.log" | Select-String "AGY0001", "AGY0002", "AGY0003", "AGY0011", "AGY0021", "UNT0007", "UNT0008", "UNT0023", "error CS"
```

### 5.2 Source Code Inspection Checks
1. **Inspect `IInspectorSanitizerService.cs`**:
   - Location: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs`
   - Verify namespace is `Game.Editor.Core.Lifecycle`.
   - Verify the 4 method signatures match specification.
2. **Inspect `InspectorSanitizerService.cs`**:
   - Location: `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
   - Verify class is `sealed` and implements `IInspectorSanitizerService`.
   - Verify static constructor or field initializers cache all reflection members.
   - Verify zero occurrences of `?.` on `Editor` or `EditorWindow`.
   - Verify `tracker.Destroy()` does NOT appear anywhere in the file.
   - Verify `!EditorUtility.IsPersistent(ed.target)` check is present.
3. **Inspect `InspectorTrackerSanitizer.cs`**:
   - Location: `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`
   - Verify delegation to `InspectorSanitizerService.Instance`.
   - Verify `ClearConsole()` is retained.

### 5.3 Invalidation Conditions
- If any `UNT0008` error is reported by Roslyn analyzers, an illegal null-conditional operator (`?.`) was used on `Editor` or `EditorWindow`.
- If `ActiveEditorTracker.Destroy()` is called, invalid native C++ pointer crashes will occur during Play Mode transition.
- If scene editor purge does not check `!EditorUtility.IsPersistent(target)`, the 4 target exceptions (`MeshColliderEditor`, `GameObjectInspector`, `TransformInspector`, `Renderer2DMeshEditor`) will continue to occur upon Play Mode entry.
