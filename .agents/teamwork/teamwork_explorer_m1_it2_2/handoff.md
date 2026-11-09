# Investigation Report: InspectorSanitizerService Reflection Defects & Concrete Resolution Strategy

**Agent**: `teamwork_explorer_m1_it2_2`  
**Milestone**: Milestone 1 Iteration 2  
**Date**: 2026-11-07  
**Parent Orchestrator**: `3370c761-5485-4812-ad19-6f9cfcb9094d`  
**Status**: COMPLETE (Read-Only Investigation)  

---

## 1. Observation

### 1.1 Type & Member Inheritance in Unity 6000.3.7f1 Engine Assemblies
Direct reflection inspection against Unity 6000.3.7f1 managed assemblies (`C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll`):

```pwsh
$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll")
$propEditor = $asm.GetType("UnityEditor.PropertyEditor")
$inspWindow = $asm.GetType("UnityEditor.InspectorWindow")
```

**Empirical Output**:
- Inheritance Hierarchy: `UnityEditor.InspectorWindow` inherits directly from `UnityEditor.PropertyEditor` (`inspWindow.BaseType == typeof(UnityEditor.PropertyEditor)`).
- `UnityEditor.PropertyEditor` Declared Members (`BindingFlags.DeclaredOnly`):
  - `Property: tracker` -> **True**
  - `Field: m_Tracker` -> **True**
  - `Method: ClearEditorsAndRebuild()` -> **True**
  - `Method: ClearSerializedLockedObjects()` -> **False**
  - `Property: isLocked` -> **False**
  - `Field: m_AllPropertyEditors` -> **True**
- `UnityEditor.InspectorWindow` Declared Members (`BindingFlags.DeclaredOnly`):
  - `Method: ClearSerializedLockedObjects()` -> **True**
  - `Property: isLocked` -> **True**
  - `Field: m_AllInspectors` -> **True**
  - `Field: m_ObjectsLockedBeforeSerialization` -> **True**
  - `Field: m_InstanceIDsLockedBeforeSerialization` -> **True**

### 1.2 Inspection of `InspectorSanitizerService.cs` Reflection Initialization
In `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs` (lines 55–67):

```csharp
55: var editorAssembly = typeof(UnityEditor.Editor).Assembly;
56: 
57: s_propertyEditorType = editorAssembly.GetType("UnityEditor.PropertyEditor");
58: s_inspectorWindowType = editorAssembly.GetType("UnityEditor.InspectorWindow");
59: 
60: var searchType = s_propertyEditorType ?? s_inspectorWindowType;
61: if (searchType != null)
62: {
63:     s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
64:     s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
65:     s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
66:     s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
67: }
```

In `InspectorSanitizerService.cs` (lines 511–524):

```csharp
511: private static MethodInfo FindMethodInHierarchy(Type type, string name)
512: {
513:     var current = type;
514:     while (current != null && current != typeof(object))
515:     {
516:         var method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
517:         if (method != null)
518:         {
519:             return method;
520:         }
521:         current = current.BaseType;
522:     }
523:     return null;
524: }
```

Empirical runtime check on compiled `Temp/Bin/Debug/Game.Editor/Game.Editor.dll`:
```pwsh
$field = $type.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic")
Write-Host ($field.GetValue($null) -ne $null)
# Output: False (permanently null)
```

### 1.3 Inspection of `TargetException` Vulnerability in `PurgeAllInspectors` and `UnlockAllInspectors`
In `InspectorSanitizerService.cs` (lines 112–126 and 274–284):

```csharp
// PurgeAllInspectors (lines 112–126):
if (s_isLockedProp != null)
{
    var isLockedVal = s_isLockedProp.GetValue(win); // <--- TargetException if win is not InspectorWindow
    if (isLockedVal is bool isLocked && isLocked)
    {
        s_isLockedProp.SetValue(win, BoxedFalse);
    }
}

if (s_clearSerializedLockedObjectsMethod != null)
{
    s_clearSerializedLockedObjectsMethod.Invoke(win, null); // <--- TargetException if win is not InspectorWindow
}
```

In `GetInspectorWindows()` (lines 458–482 and 485):
When `m_AllInspectors` is null or empty, `GetInspectorWindows()` iterates `s_allPropertyEditorsField` (`m_AllPropertyEditors`). Any generic `PropertyEditor` instance that is not an `InspectorWindow` (e.g. preset pickers, sprite editor windows, or custom property editor windows) is returned in the `windows` list.
When passed to `s_isLockedProp.GetValue(win)`:
```text
System.Reflection.TargetException: Object does not match target type.
   at System.Reflection.RuntimePropertyInfo.GetterAdapterFrame[T,R](...)
   at System.Reflection.RuntimePropertyInfo.GetValue(Object obj, Object[] index)
```
Because lines 110–147 are enclosed in a single `try-catch` block:
```csharp
catch (Exception ex)
{
    Debug.LogWarning($"[InspectorSanitizerService] Error purging inspector window: {ex.Message}");
}
```
The `TargetException` immediately aborts the loop iteration for that window before lines 128–143 can execute. The tracker active editors are not destroyed, `ClearEditorsAndRebuild` is not called, and the window is not repainted.

### 1.4 Inspection of Redundant Invocations in `InspectorTrackerSanitizer.cs`
In `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs` (lines 32–37):
```csharp
public static void PurgeAllInspectorWindows()
{
    Service.PurgeAllInspectors();
    Service.PurgeSharedTracker();
    Service.DestroyOrphanAndSceneEditors();
}
```
In `InspectorSanitizerService.PurgeAllInspectors()` (lines 150–153):
```csharp
// Purge shared tracker and all orphan/scene editors
PurgeSharedTracker();
DestroyOrphanAndSceneEditors();
```
`PurgeAllInspectors()` already unconditionally invokes `PurgeSharedTracker()` and `DestroyOrphanAndSceneEditors()`. The subsequent calls in `InspectorTrackerSanitizer` are redundant.

---

## 2. Logic Chain

### 2.1 Why `s_clearSerializedLockedObjectsMethod` Evaluates to `null`
1. `InspectorWindow` is a derived subclass of `PropertyEditor`.
2. `ClearSerializedLockedObjects()` is declared specifically on `UnityEditor.InspectorWindow` (Observation 1.1).
3. In line 60, `searchType = s_propertyEditorType ?? s_inspectorWindowType;` evaluates to `s_propertyEditorType` because `s_propertyEditorType` (`UnityEditor.PropertyEditor`) is non-null.
4. `FindMethodInHierarchy(PropertyEditor, "ClearSerializedLockedObjects")` traverses `current.BaseType` upward: `PropertyEditor` -> `EditorWindow` -> `ScriptableObject` -> `Object`. It never searches downward into derived subclasses like `InspectorWindow`.
5. Therefore, the method lookup returns `null`, leaving `s_clearSerializedLockedObjectsMethod` permanently `null`.
6. Consequence: Lines 123–126 in `PurgeAllInspectors()` and lines 280–283 in `UnlockAllInspectors()` are permanently bypassed at runtime. Any locked `InspectorWindow` retains its internal serialized locked references (`m_ObjectsLockedBeforeSerialization`), triggering `SerializedObjectNotCreatableException` and `MissingReferenceException` upon scene unload and Play Mode domain reload.

### 2.2 Why `s_isLockedProp.GetValue(win)` Throws `TargetException`
1. `isLocked` is declared on `UnityEditor.InspectorWindow` (Observation 1.1).
2. Its `DeclaringType` is `typeof(UnityEditor.InspectorWindow)`.
3. `GetInspectorWindows()` can return non-`InspectorWindow` instances of `PropertyEditor` via `m_AllPropertyEditors` or `Resources.FindObjectsOfTypeAll(typeof(PropertyEditor))`.
4. In the .NET Reflection runtime, calling `PropertyInfo.GetValue(obj)` or `MethodInfo.Invoke(obj, null)` where `!prop.DeclaringType.IsAssignableFrom(obj.GetType())` unconditionally throws `TargetException: Object does not match target type.`
5. Because this call occurs at the beginning of the per-window loop in `PurgeAllInspectors()`, the exception terminates processing of that window, skipping tracker editor destruction and container rebuilding.

### 2.3 Why Inverting Search Root to `InspectorWindow` Resolves Both Base & Derived Members
1. In C# inheritance, derived classes inherit all public and non-public instance members of their base classes (except constructors).
2. `InspectorWindow` inherits `tracker`, `m_Tracker`, and `ClearEditorsAndRebuild()` from `PropertyEditor`.
3. If `searchType` begins at `s_inspectorWindowType ?? s_propertyEditorType`:
   - `ClearSerializedLockedObjects`: found on `InspectorWindow` (at step 0 of traversal).
   - `tracker`: found on `PropertyEditor` (at step 1 of traversal, via `current.BaseType`).
   - `m_Tracker`: found on `PropertyEditor` (at step 1 of traversal, via `current.BaseType`).
   - `ClearEditorsAndRebuild`: found on `PropertyEditor` (at step 1 of traversal, via `current.BaseType`).
4. All four reflection metadata tokens resolve successfully to valid `MemberInfo` objects with zero misses.

---

## 3. Caveats

1. **Unity Version Variations**: In some earlier or future minor revisions of Unity, Unity could move `ClearSerializedLockedObjects` up into `PropertyEditor` or deprecate it. To make the fix robust against all variations, the initialization must search `s_inspectorWindowType ?? s_propertyEditorType` first, with an explicit fallback to `s_propertyEditorType`.
2. **Type Compatibility Checks**: Rather than using `is` keyword with internal engine types (which are inaccessible without internals visible), the code must check `member.DeclaringType != null && member.DeclaringType.IsAssignableFrom(win.GetType())` prior to any `GetValue`, `SetValue`, or `Invoke` call. This incurs zero heap allocations and is completely type-safe.

---

## 4. Conclusion & Concrete Resolution Strategy

The defects are fully diagnosed and can be cleanly resolved by updating `InspectorSanitizerService.cs` and `InspectorTrackerSanitizer.cs`.

### Exact Code Edits Required for Worker

#### File 1: `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`

##### Diff 1: Static Constructor Reflection Lookup (Lines 60–67)
```csharp
<<<< CURRENT (Lines 60–67):
                var searchType = s_propertyEditorType ?? s_inspectorWindowType;
                if (searchType != null)
                {
                    s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
                    s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
                    s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
                }
==== REPLACEMENT:
                // Initialize hierarchy search starting from InspectorWindow (derived class) down into PropertyEditor (base class)
                // so that ClearSerializedLockedObjects (declared on InspectorWindow) and tracker / ClearEditorsAndRebuild
                // (declared on PropertyEditor) are both resolved via upward inheritance traversal.
                var searchType = s_inspectorWindowType ?? s_propertyEditorType;
                if (searchType != null)
                {
                    s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
                    s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
                    s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
                }

                // Defensive fallback: if ClearSerializedLockedObjects was not declared on InspectorWindow in this version, attempt PropertyEditor
                if (s_clearSerializedLockedObjectsMethod == null && s_propertyEditorType != null)
                {
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(s_propertyEditorType, "ClearSerializedLockedObjects");
                }
>>>>
```

##### Diff 2: Type Guarding in `PurgeAllInspectors` (Lines 111–143)
```csharp
<<<< CURRENT (Lines 111–143):
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
==== REPLACEMENT:
                    try
                    {
                        var winType = win.GetType();

                        // 1. Force unlock if locked (verify win matches isLocked declaring type to prevent TargetException)
                        if (s_isLockedProp != null && s_isLockedProp.DeclaringType != null && s_isLockedProp.DeclaringType.IsAssignableFrom(winType))
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                            }
                        }

                        // 2. Clear locked serialized objects (verify win matches declaring type to prevent TargetException)
                        if (s_clearSerializedLockedObjectsMethod != null && s_clearSerializedLockedObjectsMethod.DeclaringType != null && s_clearSerializedLockedObjectsMethod.DeclaringType.IsAssignableFrom(winType))
                        {
                            s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                        }

                        // 3. Purge tracker active editors without calling tracker.Destroy()
                        var tracker = GetTracker(win);
                        if (tracker != null)
                        {
                            DestroyTrackerEditors(tracker);
                        }

                        // 4. Clear UI elements & rebuild containers (verify win matches declaring type to prevent TargetException)
                        if (s_clearEditorsAndRebuildMethod != null && s_clearEditorsAndRebuildMethod.DeclaringType != null && s_clearEditorsAndRebuildMethod.DeclaringType.IsAssignableFrom(winType))
                        {
                            s_clearEditorsAndRebuildMethod.Invoke(win, null);
                        }

                        // 5. Repaint window
                        win.Repaint();
                    }
>>>>
```

##### Diff 3: Type Guarding in `UnlockAllInspectors` (Lines 273–287)
```csharp
<<<< CURRENT (Lines 273–287):
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
==== REPLACEMENT:
                    try
                    {
                        var winType = win.GetType();
                        if (s_isLockedProp != null && s_isLockedProp.DeclaringType != null && s_isLockedProp.DeclaringType.IsAssignableFrom(winType))
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                                if (s_clearSerializedLockedObjectsMethod != null && s_clearSerializedLockedObjectsMethod.DeclaringType != null && s_clearSerializedLockedObjectsMethod.DeclaringType.IsAssignableFrom(winType))
                                {
                                    s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                                }
                                win.Repaint();
                            }
                        }
                    }
>>>>
```

##### Diff 4: Type Guarding in `GetTracker` (Lines 372–392)
```csharp
<<<< CURRENT (Lines 372–392):
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
==== REPLACEMENT:
                var windowType = window.GetType();

                // 1. Try cached PropertyInfo (guard against TargetException on non-matching windows)
                if (s_trackerProp != null && s_trackerProp.DeclaringType != null && s_trackerProp.DeclaringType.IsAssignableFrom(windowType))
                {
                    var tracker = s_trackerProp.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }

                // 2. Try cached FieldInfo (guard against TargetException on non-matching windows)
                if (s_trackerField != null && s_trackerField.DeclaringType != null && s_trackerField.DeclaringType.IsAssignableFrom(windowType))
                {
                    var tracker = s_trackerField.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }
>>>>
```

##### Diff 5: Polymorphic Query Type in `GetInspectorWindows` (Line 485)
```csharp
<<<< CURRENT (Line 485):
            // 3. Fallback via Resources.FindObjectsOfTypeAll
            var targetType = s_inspectorWindowType ?? s_propertyEditorType;
==== REPLACEMENT:
            // 3. Fallback via Resources.FindObjectsOfTypeAll (query base PropertyEditor first to polymorphically return all InspectorWindow and PropertyEditor instances)
            var targetType = s_propertyEditorType ?? s_inspectorWindowType;
>>>>
```

#### File 2: `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`

##### Diff 6: Remove Redundant Sub-Calls in `PurgeAllInspectorWindows` (Lines 32–37)
```csharp
<<<< CURRENT (Lines 32–37):
        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
            Service.PurgeSharedTracker();
            Service.DestroyOrphanAndSceneEditors();
        }
==== REPLACEMENT:
        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
        }
>>>>
```

### Roslyn Analyzer Rule Compliance Matrix
| Rule | Status | Evidence |
|------|--------|----------|
| `AGY0001` (Shallow inheritance) | COMPLIANT | `InspectorSanitizerService` is a sealed class implementing `IInspectorSanitizerService`. Inheritance depth: 0. |
| `AGY0002` (Serialized field encapsulation) | COMPLIANT | Zero serialized fields in service classes. |
| `AGY0003` (Read-only property external access) | COMPLIANT | `Instance` property has public getter only (`private static InspectorSanitizerService s_instance;`). |
| `AGY0011` (Prohibit LINQ in hot paths) | COMPLIANT | All collections traversed via indexer `for (int i = 0; i < list.Count; i++)`. Zero LINQ queries. |
| `AGY0012` (Prohibit GetComponent in hot paths) | COMPLIANT | Zero `GetComponent` calls. |
| `AGY0013` (Prohibit new in hot paths) | COMPLIANT | All reflection metadata is cached once in static constructor. Zero per-purge allocations. |
| `AGY0014` (Prohibit string formatting in hot paths) | COMPLIANT | Logging is restricted to catch blocks only. |
| `AGY0015` (Prohibit boxing in hot paths) | COMPLIANT | Pre-boxed `BoxedFalse` constant used for `SetValue(win, BoxedFalse)`. |
| `AGY0016` (Prohibit closures/lambdas in hot paths) | COMPLIANT | Zero closures or inline delegates. |
| `AGY0021` (Named event handlers in `+=`) | COMPLIANT | Zero `+=` subscriptions in `InspectorSanitizerService`. |
| `UNT0007`/`UNT0008`/`UNT0023` (No `??`/`?.`/`??=` on `UnityEngine.Object`) | COMPLIANT | All `UnityEngine.Object` null checks use explicit `== null` or `!= null`. Null-coalescing operators are applied exclusively to `System.Type` and interface references. |

---

## 5. Verification Method

### 5.1 Independent Verification Commands
To verify the fix after application by worker:

1. **Reflection Metadata Non-Null Probe**:
   ```pwsh
   pwsh -NoProfile -Command '& {
       [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine.dll") | Out-Null;
       [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll") | Out-Null;
       $gameEditorAsm = [System.Reflection.Assembly]::LoadFrom("c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll");
       $type = $gameEditorAsm.GetType("Game.Editor.Core.Lifecycle.InspectorSanitizerService");
       [System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($type.TypeHandle);
       $f1 = $type.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic").GetValue($null);
       $f2 = $type.GetField("s_clearEditorsAndRebuildMethod", [System.Reflection.BindingFlags]"Static,NonPublic").GetValue($null);
       $f3 = $type.GetField("s_trackerProp", [System.Reflection.BindingFlags]"Static,NonPublic").GetValue($null);
       $f4 = $type.GetField("s_isLockedProp", [System.Reflection.BindingFlags]"Static,NonPublic").GetValue($null);
       Write-Host "ClearSerializedLockedObjects:" ($f1 -ne $null) ($f1.DeclaringType.Name);
       Write-Host "ClearEditorsAndRebuild:" ($f2 -ne $null) ($f2.DeclaringType.Name);
       Write-Host "tracker:" ($f3 -ne $null) ($f3.DeclaringType.Name);
       Write-Host "isLocked:" ($f4 -ne $null) ($f4.DeclaringType.Name);
       if ($f1 -eq $null -or $f2 -eq $null -or $f3 -eq $null -or $f4 -eq $null) { exit 1; }
   }'
   ```
   **Expected Result**:
   - `ClearSerializedLockedObjects: True InspectorWindow`
   - `ClearEditorsAndRebuild: True PropertyEditor`
   - `tracker: True PropertyEditor`
   - `isLocked: True InspectorWindow`
   - Exit Code: 0

2. **C# Roslyn Build & Analyzer Check**:
   ```pwsh
   dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
   ```
   **Expected Result**:
   - `0 Advertencia(s)`
   - `0 Errores`
   - Exit Code: 0

### 5.2 Invalidation Conditions
- `s_clearSerializedLockedObjectsMethod` evaluating to `null`.
- Any `System.Reflection.TargetException` thrown during `PurgeAllInspectors()` or `UnlockAllInspectors()`.
- Any compiler warning or Roslyn analyzer violation (`AGY*`, `UNT*`).
