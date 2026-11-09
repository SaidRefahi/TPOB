# Explorer Investigation Report: Milestone 1 Iteration 2 (Root Cause & Fix Strategy)

**Agent**: `teamwork_explorer_m1_it2_3`  
**Milestone**: Milestone 1: Inspector & Editor Sanitizer Service (Iteration 2)  
**Parent Orchestrator**: `3370c761-5485-4812-ad19-6f9cfcb9094d`  
**Target File**: `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`  
**Auxiliary File**: `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`  

---

## 1. Observation

### 1.1 Type Hierarchy & Member Declaration in Unity 6000.3.7f1 Engine Assembly
Empirical reflection probing against `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll`:
```pwsh
$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll");
$propEditor = $asm.GetType("UnityEditor.PropertyEditor");
$inspWindow = $asm.GetType("UnityEditor.InspectorWindow");
$bf = [System.Reflection.BindingFlags]"Instance, Public, NonPublic, DeclaredOnly";
```
Empirical output:
```text
=== HIERARCHY ===
PropertyEditor base: UnityEditor.EditorWindow
InspectorWindow base: UnityEditor.PropertyEditor
InspectorWindow is PropertyEditor: True

=== ClearSerializedLockedObjects ===
PropertyEditor Declared: False
InspectorWindow Declared: True

=== isLocked ===
PropertyEditor Declared: False
InspectorWindow Declared: True

=== tracker ===
PropertyEditor Declared: True
InspectorWindow Declared: False

=== ClearEditorsAndRebuild ===
PropertyEditor Declared: True
InspectorWindow Declared: False
```

### 1.2 Reflection Initialization Defect in `InspectorSanitizerService.cs`
File: `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
Lines 60–67:
```csharp
var searchType = s_propertyEditorType ?? s_inspectorWindowType;
if (searchType != null)
{
    s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
    s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
    s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
}
```
Empirical evaluation in compiled assembly `Game.Editor.dll`:
```text
s_clearSerializedLockedObjectsMethod is: False
```
`s_clearSerializedLockedObjectsMethod` evaluates unconditionally to `null`.

### 1.3 `TargetException` on Non-`InspectorWindow` `PropertyEditor`
In `InspectorSanitizerService.cs` lines 112–126:
```csharp
// 1. Force unlock if locked
if (s_isLockedProp != null)
{
    var isLockedVal = s_isLockedProp.GetValue(win);
    if (isLockedVal is bool isLocked && isLocked)
    {
        s_isLockedProp.SetValue(win, BoxedFalse);
    }
}

// 2. Clear locked serialized objects
if (s_clearSerializedLockedObjectsMethod != null)
{
    s_clearSerializedLockedObjectsMethod.Invoke(win, null);
}
```
Empirical check of `s_isLockedProp.DeclaringType`:
- `s_isLockedProp.DeclaringType` is `UnityEditor.InspectorWindow`.
- `win` can be any `EditorWindow` returned by `GetInspectorWindows()`, including instances from `m_AllPropertyEditors` or `Resources.FindObjectsOfTypeAll(typeof(PropertyEditor))` that are pure `PropertyEditor` instances (not derived `InspectorWindow`).
- Calling `s_isLockedProp.GetValue(win)` when `!(win is InspectorWindow)` throws:
  `System.Reflection.TargetException: Object does not match target type.`
- Because lines 112–143 are inside a single `try-catch` block:
  ```csharp
  catch (Exception ex)
  {
      Debug.LogWarning($"[InspectorSanitizerService] Error purging inspector window: {ex.Message}");
  }
  ```
  The thrown `TargetException` aborts the window iteration before lines 128–143 can execute, entirely skipping tracker editor destruction (`DestroyTrackerEditors`), container rebuilding (`ClearEditorsAndRebuild`), and `Repaint()`.

---

## 2. Logic Chain

1. **Hierarchy Traversal Directionality (from Obs 1.1)**:
   - `FindMethodInHierarchy(Type type, string name)` starts at `type` and follows `current = current.BaseType` upward until `typeof(object)`.
   - In lines 60–67, `searchType = s_propertyEditorType ?? s_inspectorWindowType;`. Because `s_propertyEditorType` is `PropertyEditor`, `searchType` is assigned `PropertyEditor`.
   - The traversal chain from `PropertyEditor` is: `PropertyEditor` -> `EditorWindow` -> `ScriptableObject` -> `Object`.
   - Because `ClearSerializedLockedObjects` is declared on `InspectorWindow` (a child class), traversing upward from `PropertyEditor` never encounters `InspectorWindow`.
   - Thus, `s_clearSerializedLockedObjectsMethod` evaluates to `null`.

2. **Inverted Hierarchy Seed Resolution**:
   - Reversing the search order: `var searchType = s_inspectorWindowType ?? s_propertyEditorType;`.
   - Starting from `InspectorWindow`, the traversal chain is: `InspectorWindow` -> `PropertyEditor` -> `EditorWindow` -> `ScriptableObject` -> `Object`.
   - `ClearSerializedLockedObjects` is declared on `InspectorWindow` -> Found immediately on `InspectorWindow`.
   - `tracker`, `m_Tracker`, and `ClearEditorsAndRebuild` are declared on `PropertyEditor` -> Found on `current.BaseType` (`PropertyEditor`).
   - Probed and empirically verified: all 4 members resolve to non-null with their correct declaring types.

3. **`TargetException` Root Cause & Prevention (from Obs 1.3)**:
   - `s_isLockedProp` and `s_clearSerializedLockedObjectsMethod` both have declaring type `UnityEditor.InspectorWindow`.
   - In .NET Reflection, invoking a member declared on type $T$ against an object instance $O$ requires that $O$ is an instance of $T$ (or a subclass of $T$). If not, runtime throws `TargetException`.
   - Checking `prop.DeclaringType != null && prop.DeclaringType.IsInstanceOfType(win)` before calling `GetValue`, `SetValue`, or `Invoke`:
     - Evaluates to `true` if `win` is `InspectorWindow` (or derived from `InspectorWindow`).
     - Evaluates to `false` if `win` is a base `PropertyEditor` or any other `EditorWindow`.
     - Zero memory allocations: `win` is an `EditorWindow` reference type, so `IsInstanceOfType(object)` does not box.
     - Protects steps 3, 4, 5 (`DestroyTrackerEditors`, `ClearEditorsAndRebuild`, `Repaint`) so they are never aborted by `TargetException`.

4. **Zero-GC & Roslyn Analyzer Compliance**:
   - `UNT0007/8/23`: Prohibits `?.`, `??`, `??=` on `UnityEngine.Object`.
     - `win` is an `EditorWindow` (`UnityEngine.Object`).
     - Using `if (win != null)` with standard `operator !=` is strictly compliant.
     - `prop.DeclaringType` is `System.Type` (not `UnityEngine.Object`), so standard null checks apply.
   - `AGY0015`: Value type boxing prohibited.
     - `BoxedFalse` (`static readonly object BoxedFalse = false;`) is reused.
     - `IsInstanceOfType` takes `object`, passing `win` (class reference) performs an implicit reference upcast without boxing.
   - `AGY0021`: Named event handlers. No anonymous subscriptions introduced.
   - `AGY0001` / `AGY0002` / `AGY0003`: No architecture or encapsulation regressions.

---

## 3. Caveats

1. **Unity Engine Reflection Boundary**:
   Internal editor types (`UnityEditor.PropertyEditor`, `UnityEditor.InspectorWindow`) are not part of Unity's public API. While `FindPropertyInHierarchy` and `FindMethodInHierarchy` make the lookup resilient across Unity versions, fallback guards (`??`) and `IsInstanceOfType` checks ensure that if Unity further alters internal inheritance, the service degrades gracefully with warnings rather than crashing Play Mode transitions.
2. **Milestone Boundary**:
   This report provides the exact fix strategy and concrete code edits. Under Milestone 1 Iteration 2 rules, this investigation agent is strictly read-only and does not modify the source files directly.

---

## 4. Conclusion

The failures identified in Milestone 1 Challenger reports are completely verified and understood:
1. `s_clearSerializedLockedObjectsMethod` was null due to searching upward from base class `PropertyEditor` instead of derived class `InspectorWindow`.
2. Calling `s_isLockedProp.GetValue(win)` threw `TargetException` when encountering non-`InspectorWindow` `PropertyEditor` instances.

Both issues are cleanly resolved by:
- Setting `searchType = s_inspectorWindowType ?? s_propertyEditorType` in the static constructor.
- Adding `DeclaringType.IsInstanceOfType(win)` guards around `s_isLockedProp` and `s_clearSerializedLockedObjectsMethod` invocations.
- Guarding `s_trackerProp` and `s_trackerField` with `DeclaringType.IsInstanceOfType(window)` in `GetTracker()`.
- Setting `targetType = s_propertyEditorType ?? s_inspectorWindowType` in `GetInspectorWindows()` fallback query.
- Eliminating redundant tracker and editor purges in `InspectorTrackerSanitizer.PurgeAllInspectorWindows()`.

### Concrete Proposed Code Changes

#### Change 1: `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
**Lines 59–68** (Static Constructor reflection search):
```csharp
<<<< BEFORE
                var searchType = s_propertyEditorType ?? s_inspectorWindowType;
                if (searchType != null)
                {
                    s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
                    s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
                    s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
                }
==== AFTER
                var searchType = s_inspectorWindowType ?? s_propertyEditorType;
                if (searchType != null)
                {
                    s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
                    s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
                    s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
                }

                if (s_clearSerializedLockedObjectsMethod == null && s_propertyEditorType != null)
                {
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(s_propertyEditorType, "ClearSerializedLockedObjects");
                }
>>>>
```

**Lines 111–143** (`PurgeAllInspectors` window loop):
```csharp
<<<< BEFORE
                    try
                    {
                        // 1. Force unlock if locked
                        if (s_isLockedProp != null)
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                            }
                        }

                        // 2. Clear locked serialized objects
                        if (s_clearSerializedLockedObjectsMethod != null)
                        {
                            s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                        }

                        // 3. Purge tracker active editors without calling tracker.Destroy()
                        var tracker = GetTracker(win);
                        if (tracker != null)
                        {
                            DestroyTrackerEditors(tracker);
                        }

                        // 4. Clear UI elements & rebuild containers
                        if (s_clearEditorsAndRebuildMethod != null)
                        {
                            s_clearEditorsAndRebuildMethod.Invoke(win, null);
                        }

                        // 5. Repaint window
                        win.Repaint();
                    }
==== AFTER
                    try
                    {
                        // 1. Force unlock if locked (safely check declaring type to prevent TargetException)
                        if (s_isLockedProp != null && s_isLockedProp.DeclaringType != null && s_isLockedProp.DeclaringType.IsInstanceOfType(win))
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                            }
                        }

                        // 2. Clear locked serialized objects (safely check declaring type to prevent TargetException)
                        if (s_clearSerializedLockedObjectsMethod != null && s_clearSerializedLockedObjectsMethod.DeclaringType != null && s_clearSerializedLockedObjectsMethod.DeclaringType.IsInstanceOfType(win))
                        {
                            s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                        }

                        // 3. Purge tracker active editors without calling tracker.Destroy()
                        var tracker = GetTracker(win);
                        if (tracker != null)
                        {
                            DestroyTrackerEditors(tracker);
                        }

                        // 4. Clear UI elements & rebuild containers
                        if (s_clearEditorsAndRebuildMethod != null && s_clearEditorsAndRebuildMethod.DeclaringType != null && s_clearEditorsAndRebuildMethod.DeclaringType.IsInstanceOfType(win))
                        {
                            s_clearEditorsAndRebuildMethod.Invoke(win, null);
                        }

                        // 5. Repaint window
                        win.Repaint();
                    }
>>>>
```

**Lines 272–289** (`UnlockAllInspectors` window loop):
```csharp
<<<< BEFORE
                    try
                    {
                        if (s_isLockedProp != null)
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                                if (s_clearSerializedLockedObjectsMethod != null)
                                {
                                    s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                                }
                                win.Repaint();
                            }
                        }
                    }
==== AFTER
                    try
                    {
                        if (s_isLockedProp != null && s_isLockedProp.DeclaringType != null && s_isLockedProp.DeclaringType.IsInstanceOfType(win))
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                                if (s_clearSerializedLockedObjectsMethod != null && s_clearSerializedLockedObjectsMethod.DeclaringType != null && s_clearSerializedLockedObjectsMethod.DeclaringType.IsInstanceOfType(win))
                                {
                                    s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                                }
                                win.Repaint();
                            }
                        }
                    }
>>>>
```

**Lines 371–392** (`GetTracker` cached resolution):
```csharp
<<<< BEFORE
                // 1. Try cached PropertyInfo
                if (s_trackerProp != null)
                {
                    var tracker = s_trackerProp.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }

                // 2. Try cached FieldInfo
                if (s_trackerField != null)
                {
                    var tracker = s_trackerField.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }
==== AFTER
                // 1. Try cached PropertyInfo
                if (s_trackerProp != null && s_trackerProp.DeclaringType != null && s_trackerProp.DeclaringType.IsInstanceOfType(window))
                {
                    var tracker = s_trackerProp.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }

                // 2. Try cached FieldInfo
                if (s_trackerField != null && s_trackerField.DeclaringType != null && s_trackerField.DeclaringType.IsInstanceOfType(window))
                {
                    var tracker = s_trackerField.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }
>>>>
```

**Lines 484–487** (`GetInspectorWindows` fallback type):
```csharp
<<<< BEFORE
            // 3. Fallback via Resources.FindObjectsOfTypeAll
            var targetType = s_inspectorWindowType ?? s_propertyEditorType;
==== AFTER
            // 3. Fallback via Resources.FindObjectsOfTypeAll (query base PropertyEditor to capture all derived InspectorWindow instances)
            var targetType = s_propertyEditorType ?? s_inspectorWindowType;
>>>>
```

#### Change 2: `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`
**Lines 32–37** (`PurgeAllInspectorWindows` simplification):
```csharp
<<<< BEFORE
        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
            Service.PurgeSharedTracker();
            Service.DestroyOrphanAndSceneEditors();
        }
==== AFTER
        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
        }
>>>>
```

---

## 5. Verification Method

### 5.1 Verification Commands

1. **Reflection Non-Null Field Check**:
   Run in PowerShell:
   ```pwsh
   pwsh -NoProfile -Command '& {
       [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine.dll") | Out-Null;
       [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll") | Out-Null;
       $gameEditorAsm = [System.Reflection.Assembly]::LoadFrom("c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll");
       $type = $gameEditorAsm.GetType("Game.Editor.Core.Lifecycle.InspectorSanitizerService");
       [System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($type.TypeHandle);
       $field = $type.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic");
       $val = $field.GetValue($null);
       Write-Host "s_clearSerializedLockedObjectsMethod is non-null:" ($val -ne $null) "-> DeclaredOn:" $val.DeclaringType.Name;
       if ($val -eq $null) { exit 1 }
   }'
   ```
   **Expected Output**:
   ```text
   s_clearSerializedLockedObjectsMethod is non-null: True -> DeclaredOn: InspectorWindow
   ```

2. **Roslyn Analyzers & C# Compilation**:
   ```pwsh
   dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
   ```
   **Expected Output**:
   ```text
   Compilación correcta.
       0 Advertencia(s)
       0 Errores
   ```

### 5.2 Invalidation Conditions
- `s_clearSerializedLockedObjectsMethod` evaluating to null at runtime.
- Any `System.Reflection.TargetException` logged during `PurgeAllInspectors()` or `UnlockAllInspectors()`.
- Any Roslyn analyzer warning or error under `Game.Editor.csproj`.
