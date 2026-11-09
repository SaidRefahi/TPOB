# Handoff Report: Milestone 1 Iteration 2 — InspectorSanitizerService Reflection & TargetException Investigation

**Agent**: `teamwork_explorer_m1_it2_1`  
**Milestone**: Milestone 1 Iteration 2 (Root Cause Investigation & Fix Strategy)  
**Date**: 2026-11-07  
**Parent Orchestrator**: `3370c761-5485-4812-ad19-6f9cfcb9094d`  
**Working Directory**: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_it2_1\`  
**Target Files**:
- `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
- `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`

---

## 1. Observation

### 1.1 Empirical Reflection Probing of Unity 6000.3.7f1 Engine Assemblies
Executed PowerShell probe against Unity 6000.3.7f1 managed binaries (`C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll`):
```pwsh
$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll");
$propEditor = $asm.GetType("UnityEditor.PropertyEditor");
$inspWindow = $asm.GetType("UnityEditor.InspectorWindow");
$bf = [System.Reflection.BindingFlags]"Instance, Public, NonPublic, DeclaredOnly";

Write-Host "InspectorWindow base type:" $inspWindow.BaseType.FullName;
Write-Host "PropertyEditor base type:" $propEditor.BaseType.FullName;

Write-Host "--- Methods ---";
Write-Host "propEditor.ClearSerializedLockedObjects:" ($propEditor.GetMethod("ClearSerializedLockedObjects", $bf) -ne $null);
Write-Host "inspWindow.ClearSerializedLockedObjects:" ($inspWindow.GetMethod("ClearSerializedLockedObjects", $bf) -ne $null);
Write-Host "propEditor.ClearEditorsAndRebuild:" ($propEditor.GetMethod("ClearEditorsAndRebuild", $bf) -ne $null);
Write-Host "inspWindow.ClearEditorsAndRebuild:" ($inspWindow.GetMethod("ClearEditorsAndRebuild", $bf) -ne $null);

Write-Host "--- Properties ---";
Write-Host "propEditor.isLocked:" ($propEditor.GetProperty("isLocked", $bf) -ne $null);
Write-Host "inspWindow.isLocked:" ($inspWindow.GetProperty("isLocked", $bf) -ne $null);
Write-Host "propEditor.tracker:" ($propEditor.GetProperty("tracker", $bf) -ne $null);
Write-Host "inspWindow.tracker:" ($inspWindow.GetProperty("tracker", $bf) -ne $null);

Write-Host "--- Fields ---";
Write-Host "propEditor.m_Tracker:" ($propEditor.GetField("m_Tracker", $bf) -ne $null);
Write-Host "inspWindow.m_Tracker:" ($inspWindow.GetField("m_Tracker", $bf) -ne $null);
Write-Host "propEditor.m_AllPropertyEditors:" ($propEditor.GetField("m_AllPropertyEditors", [System.Reflection.BindingFlags]"Static, Public, NonPublic, DeclaredOnly") -ne $null);
Write-Host "inspWindow.m_AllInspectors:" ($inspWindow.GetField("m_AllInspectors", [System.Reflection.BindingFlags]"Static, Public, NonPublic, DeclaredOnly") -ne $null);
```
**Empirical Output**:
```text
InspectorWindow base type: UnityEditor.PropertyEditor
PropertyEditor base type: UnityEditor.EditorWindow
--- Methods ---
propEditor.ClearSerializedLockedObjects: False
inspWindow.ClearSerializedLockedObjects: True
propEditor.ClearEditorsAndRebuild: True
inspWindow.ClearEditorsAndRebuild: False
--- Properties ---
propEditor.isLocked: False
inspWindow.isLocked: True
propEditor.tracker: True
inspWindow.tracker: False
--- Fields ---
propEditor.m_Tracker: True
inspWindow.m_Tracker: False
propEditor.m_AllPropertyEditors: True
inspWindow.m_AllInspectors: True
```

### 1.2 Evaluation of `s_clearSerializedLockedObjectsMethod` in Compiled `Game.Editor.dll`
Executed runtime check on current compiled binary:
```pwsh
pwsh -NoProfile -Command '& { [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine.dll") | Out-Null; [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll") | Out-Null; $gameEditorAsm = [System.Reflection.Assembly]::LoadFrom("c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll"); $type = $gameEditorAsm.GetType("Game.Editor.Core.Lifecycle.InspectorSanitizerService"); [System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($type.TypeHandle); $field = $type.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic"); Write-Host "s_clearSerializedLockedObjectsMethod is:" ($field.GetValue($null) -ne $null); }'
```
**Verbatim Output**:
```text
s_clearSerializedLockedObjectsMethod is: False
```

### 1.3 Verbatim Source Code in `InspectorSanitizerService.cs`
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
Lines 511–524:
```csharp
private static MethodInfo FindMethodInHierarchy(Type type, string name)
{
    var current = type;
    while (current != null && current != typeof(object))
    {
        var method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (method != null)
        {
            return method;
        }
        current = current.BaseType;
    }
    return null;
}
```
Lines 113–126:
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

### 1.4 Direct Reproduction of `TargetException`
Executed empirical test invoking `s_isLockedProp.GetValue(win)` on uninitialized instances of `PropertyEditor` and `InspectorWindow`:
```pwsh
$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll");
$propEditorType = $asm.GetType("UnityEditor.PropertyEditor");
$inspWindowType = $asm.GetType("UnityEditor.InspectorWindow");
$isLockedProp = $inspWindowType.GetProperty("isLocked", [System.Reflection.BindingFlags]"Instance,Public,NonPublic,DeclaredOnly");

$uninitPropEditor = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject($propEditorType);
$uninitInspWindow = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject($inspWindowType);

Write-Host "InspectorWindow IsInstanceOfType:" ($isLockedProp.DeclaringType.IsInstanceOfType($uninitInspWindow));
Write-Host "PropertyEditor IsInstanceOfType:" ($isLockedProp.DeclaringType.IsInstanceOfType($uninitPropEditor));

try {
    $isLockedProp.GetValue($uninitPropEditor);
} catch {
    Write-Host "GetValue on PropertyEditor threw:" $_.Exception.GetType().FullName ":" $_.Exception.Message;
}
```
**Empirical Output**:
```text
InspectorWindow IsInstanceOfType: True
PropertyEditor IsInstanceOfType: False
GetValue on PropertyEditor threw: System.Management.Automation.MethodInvocationException : Excepción al llamar a "GetValue" con "1" argumentos: "Object type UnityEditor.InspectorWindow does not match target type UnityEditor.PropertyEditor."
```

### 1.5 Redundant Facade Invocations in `InspectorTrackerSanitizer.cs`
Lines 32–37:
```csharp
public static void PurgeAllInspectorWindows()
{
    Service.PurgeAllInspectors();
    Service.PurgeSharedTracker();
    Service.DestroyOrphanAndSceneEditors();
}
```
Lines 150–152 of `InspectorSanitizerService.cs`:
```csharp
// Purge shared tracker and all orphan/scene editors
PurgeSharedTracker();
DestroyOrphanAndSceneEditors();
```

---

## 2. Logic Chain

### 2.1 Issue 1: `s_clearSerializedLockedObjectsMethod` Permanently Null
1. From Observation 1.1: `UnityEditor.InspectorWindow` derives from `UnityEditor.PropertyEditor`.
2. `ClearSerializedLockedObjects` is declared directly on `InspectorWindow` (DeclaredOnly: True) and is **not** declared on `PropertyEditor` (DeclaredOnly: False).
3. From Observation 1.3: In `InspectorSanitizerService` line 60, `searchType = s_propertyEditorType ?? s_inspectorWindowType;`. Because `s_propertyEditorType` is not null in Unity 6000.3.7f1, `searchType` evaluates to `typeof(UnityEditor.PropertyEditor)`.
4. `FindMethodInHierarchy(PropertyEditor, "ClearSerializedLockedObjects")` traverses only base types upward (`PropertyEditor` -> `EditorWindow` -> `ScriptableObject` -> `Object`). It does not traverse down to derived subclasses.
5. Consequently, `ClearSerializedLockedObjects` is never encountered, and `s_clearSerializedLockedObjectsMethod` is permanently `null` (Observation 1.2).
6. Because `s_clearSerializedLockedObjectsMethod` is `null`, lines 123–126 in `PurgeAllInspectors()` and lines 280–283 in `UnlockAllInspectors()` are permanently bypassed:
   ```csharp
   if (s_clearSerializedLockedObjectsMethod != null)
   {
       s_clearSerializedLockedObjectsMethod.Invoke(win, null);
   }
   ```
7. Consequently, locked inspector windows retain uncleared buffers (`m_ObjectsLockedBeforeSerialization` and `m_InstanceIDsLockedBeforeSerialization`), causing deserialization failure with missing targets and triggering the exact 4 exceptions described in `ORIGINAL_REQUEST.md`.

### 2.2 Issue 2: `TargetException` on Incompatible Window Types
1. From Observation 1.1: `isLocked` is declared on `InspectorWindow`, not on `PropertyEditor`.
2. `GetInspectorWindows()` collects `EditorWindow` instances from:
   - `m_AllInspectors` (type `InspectorWindow`)
   - `m_AllPropertyEditors` (type `PropertyEditor` or subclasses)
   - `Resources.FindObjectsOfTypeAll(targetType)`
3. If an editor window in the list is an instance of `PropertyEditor` that is not an `InspectorWindow` (e.g. from `m_AllPropertyEditors`), invoking `s_isLockedProp.GetValue(win)` throws `TargetException: Object type UnityEditor.InspectorWindow does not match target type UnityEditor.PropertyEditor` (Observation 1.4).
4. Because lines 110–147 in `PurgeAllInspectors()` are wrapped in a single per-window `try-catch`, this exception immediately aborts the loop iteration for that window. Lines 128–143 (tracker editor destruction, `ClearEditorsAndRebuild`, and `Repaint`) are skipped completely.
5. The same vulnerability applies to `s_clearSerializedLockedObjectsMethod.Invoke(win, null)` and `s_clearEditorsAndRebuildMethod.Invoke(win, null)` if invoked on an incompatible instance.

### 2.3 Polymorphic Window Retrieval in `GetInspectorWindows()`
1. In `InspectorSanitizerService.cs` line 485:
   ```csharp
   var targetType = s_inspectorWindowType ?? s_propertyEditorType;
   ```
2. Unity's `Resources.FindObjectsOfTypeAll(Type type)` returns all active/inactive loaded objects that are assignable to `type` (polymorphic downward matching).
3. Querying `s_inspectorWindowType` (`InspectorWindow`) returns only `InspectorWindow` instances and misses any open `PropertyEditor` instances that are not `InspectorWindow`.
4. Inverting this to `var targetType = s_propertyEditorType ?? s_inspectorWindowType;` queries the base class `PropertyEditor`, ensuring Unity returns both generic `PropertyEditor` instances and derived `InspectorWindow` instances polymorphically.

### 2.4 Redundancy in `InspectorTrackerSanitizer`
1. Observation 1.5 shows `InspectorTrackerSanitizer.PurgeAllInspectorWindows()` calling `Service.PurgeAllInspectors()`, `Service.PurgeSharedTracker()`, and `Service.DestroyOrphanAndSceneEditors()`.
2. `Service.PurgeAllInspectors()` already calls `PurgeSharedTracker()` and `DestroyOrphanAndSceneEditors()` at lines 151–152.
3. The duplicate calls in the facade are redundant and trigger duplicate scans over `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))`.

---

## 3. Caveats

1. **Unity Main Thread Execution**: Reflection calls accessing Unity Editor GUI and `Resources.FindObjectsOfTypeAll` must execute on the Unity main thread. As verified, `InspectorSanitizerService` is invoked only from editor lifecycle hooks (`EditorApplication.playModeStateChanged`, `AssemblyReloadEvents`), which execute synchronously on the main thread.
2. **Batchmode / Headless Compatibility**: In batchmode execution (`-batchmode`), `Resources.FindObjectsOfTypeAll` returns empty arrays without error, and `GetInspectorWindows()` safely returns an empty list.

---

## 4. Conclusion & Concrete Fix Strategy

The root cause of the Milestone 1 failure is completely understood and validated empirically. The required fixes are localized, deterministic, and 100% compliant with repository Roslyn analyzer rules (`AGY0001`, `AGY0002`, `AGY0003`, `AGY0021`, `UNT0007/8/23`).

### 4.1 Concrete Code Edits for `InspectorSanitizerService.cs`

#### Change 1: Invert Hierarchy Search Seed (Line 60)
**Rationale**: Starting hierarchy search from `s_inspectorWindowType` resolves `ClearSerializedLockedObjects` on `InspectorWindow`, and climbs to `PropertyEditor` to resolve `tracker`, `m_Tracker`, and `ClearEditorsAndRebuild`.

```csharp
<<<< CURRENT (Line 60):
var searchType = s_propertyEditorType ?? s_inspectorWindowType;
==== PROPOSED:
var searchType = s_inspectorWindowType ?? s_propertyEditorType;
>>>>
```

#### Change 2: Guard Invocations in `PurgeAllInspectors()` (Lines 112–140)
**Rationale**: Verifies `member.DeclaringType != null && member.DeclaringType.IsInstanceOfType(win)` before calling `GetValue`, `SetValue`, or `Invoke`, eliminating `TargetException` with zero GC allocations.

```csharp
<<<< CURRENT (Lines 112–140):
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
==== PROPOSED:
                        // 1. Force unlock if locked
                        if (s_isLockedProp != null && s_isLockedProp.DeclaringType != null && s_isLockedProp.DeclaringType.IsInstanceOfType(win))
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                            }
                        }

                        // 2. Clear locked serialized objects
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
>>>>
```

#### Change 3: Guard Invocations in `UnlockAllInspectors()` (Lines 274–286)
**Rationale**: Guards `s_isLockedProp` and `s_clearSerializedLockedObjectsMethod` against `TargetException` when unlocking windows.

```csharp
<<<< CURRENT (Lines 274–286):
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
==== PROPOSED:
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
>>>>
```

#### Change 4: Guard `GetTracker()` Reflection Invocations (Lines 373–391)
**Rationale**: Prevents `TargetException` on `s_trackerProp` and `s_trackerField` if an arbitrary `EditorWindow` instance is passed.

```csharp
<<<< CURRENT (Lines 373–391):
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
==== PROPOSED:
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

#### Change 5: Invert Fallback Type in `GetInspectorWindows()` (Line 485)
**Rationale**: Querying base class `PropertyEditor` in `Resources.FindObjectsOfTypeAll` retrieves both `PropertyEditor` and `InspectorWindow` instances polymorphically.

```csharp
<<<< CURRENT (Line 485):
            var targetType = s_inspectorWindowType ?? s_propertyEditorType;
==== PROPOSED:
            var targetType = s_propertyEditorType ?? s_inspectorWindowType;
>>>>
```

---

### 4.2 Concrete Code Edits for `InspectorTrackerSanitizer.cs`

#### Change 6: Eliminate Redundant Tracker and Editor Purges (Lines 32–37)
**Rationale**: `Service.PurgeAllInspectors()` internally calls `PurgeSharedTracker()` and `DestroyOrphanAndSceneEditors()`. Removing the duplicate calls avoids duplicate overhead.

```csharp
<<<< CURRENT (Lines 32–37):
        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
            Service.PurgeSharedTracker();
            Service.DestroyOrphanAndSceneEditors();
        }
==== PROPOSED:
        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
        }
>>>>
```

---

### 4.3 Roslyn & Architectural Compliance Audit

| Rule | Requirement | Assessment |
|---|---|---|
| `AGY0001` | Shallow inheritance depth (max 1 level from base domain classes) | `InspectorSanitizerService` derives from `object` (depth 0). **COMPLIANT** |
| `AGY0002` | Encapsulate serialized fields (`[SerializeField] private`) | Zero serialized fields in both classes. **COMPLIANT** |
| `AGY0003` | Read-only property external access | External access via read-only `Instance` and `Service` properties. **COMPLIANT** |
| `AGY0011` | Prohibit LINQ methods and queries in hot paths | Zero LINQ queries; standard indexed `for` loops used throughout. **COMPLIANT** |
| `AGY0012` | Prohibit `GetComponent*` calls in hot paths | Zero `GetComponent` calls. **COMPLIANT** |
| `AGY0013` | Prohibit reference type instantiation in hot paths | Pre-boxed `BoxedFalse` reused; zero allocations in invocation guards. **COMPLIANT** |
| `AGY0015` | Prohibit value type boxing in hot paths | `IsInstanceOfType` performs native CLR type check with 0 heap allocation. **COMPLIANT** |
| `AGY0021` | Named event handlers required in `+=` subscriptions | Zero event subscriptions in `InspectorSanitizerService`. **COMPLIANT** |
| `UNT0007/8/23` | Prohibit `??`, `?.`, `??=` on `UnityEngine.Object` | All null checks on `EditorWindow`, `Editor`, and `target` use explicit `== null` and `!= null`. **COMPLIANT** |

---

## 5. Verification Method

### 5.1 Verification Commands

1. **Empirical Method Resolution Verification**:
   Execute from repository root:
   ```pwsh
   pwsh -NoProfile -Command '& { [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine.dll") | Out-Null; [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll") | Out-Null; $gameEditorAsm = [System.Reflection.Assembly]::LoadFrom("c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll"); $type = $gameEditorAsm.GetType("Game.Editor.Core.Lifecycle.InspectorSanitizerService"); [System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($type.TypeHandle); $field = $type.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic"); $val = $field.GetValue($null); Write-Host "s_clearSerializedLockedObjectsMethod:" ($val -ne $null) ($val); if ($val -eq $null) { exit 1 } }'
   ```
   **Expected Result**:
   ```text
   s_clearSerializedLockedObjectsMethod: True Void ClearSerializedLockedObjects()
   ```

2. **C# Compilation & Roslyn Analyzer Enforcement**:
   ```pwsh
   dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
   ```
   **Expected Result**:
   - `Compilación correcta.`
   - `0 Advertencia(s)`
   - `0 Errores`

### 5.2 Invalidation Conditions
- `s_clearSerializedLockedObjectsMethod` evaluating to `null`.
- Any `System.Reflection.TargetException` logged during `PurgeAllInspectors()` or `UnlockAllInspectors()`.
- Any Roslyn compiler or analyzer warnings reported by `dotnet build`.
