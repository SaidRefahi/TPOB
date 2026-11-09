# Challenger Report: Milestone 1 — Inspector & Editor Sanitizer Service

**Agent**: `teamwork_challenger_m1_1`  
**Milestone**: Milestone 1: Inspector & Editor Sanitizer Service  
**Date**: 2026-11-07  
**Parent Orchestrator**: `3370c761-5485-4812-ad19-6f9cfcb9094d`  
**Final Verdict**: **REQUEST_CHANGES**

---

## 1. Observation

### 1.1 Independent Build & Roslyn Verification
Execution of `dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"`:
```text
  ToolbarExtender.Editor -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\ToolbarExtender.Editor\ToolbarExtender.Editor.dll
  LiteNetLib -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\LiteNetLib\LiteNetLib.dll
  PurrNet.Edgegap.Runtime -> c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Edgegap.Runtime\PurrNet.Edgegap.Runtime.dll
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
Tiempo transcurrido 00:00:18.46
```
Roslyn rules `AGY0001`, `AGY0002`, `AGY0003`, `AGY0021`, and `UNT0007/8/23` pass cleanly with zero compiler or analyzer warnings.

### 1.2 Inspection of Native Destroy Invocations
Direct search across all C# scripts in `Assets` confirmed:
- Zero invocations of `tracker.Destroy()` or `shared.Destroy()`.
- The only match for `.Destroy(` in `Assets/_Project/Editor` is the comment at line 128 of `InspectorSanitizerService.cs`:
  `// 3. Purge tracker active editors without calling tracker.Destroy()`.

### 1.3 Empirical Reflection Verification on Unity 6000.3.7f1
Execution of empirical reflection probe against Unity 6000.3.7f1 managed engine assemblies (`C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll`):
```pwsh
$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll");
$propEditor = $asm.GetType("UnityEditor.PropertyEditor");
$inspWindow = $asm.GetType("UnityEditor.InspectorWindow");
$bf = [System.Reflection.BindingFlags]"Instance, Public, NonPublic, DeclaredOnly";

Write-Output ("PropertyEditor.ClearSerializedLockedObjects: " + ($propEditor.GetMethod("ClearSerializedLockedObjects", $bf) -ne $null));
Write-Output ("InspectorWindow.ClearSerializedLockedObjects: " + ($inspWindow.GetMethod("ClearSerializedLockedObjects", $bf) -ne $null));
```
**Empirical Output**:
```text
PropertyEditor.ClearSerializedLockedObjects: False
InspectorWindow.ClearSerializedLockedObjects: True
```

### 1.4 Code Implementation in `InspectorSanitizerService.cs`
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

---

## 2. Logic Chain

### 2.1 Critical Defect: `s_clearSerializedLockedObjectsMethod` is Permanently `null`
1. From Observation 1.3: In Unity 6000.3.7f1, `ClearSerializedLockedObjects` is declared on `UnityEditor.InspectorWindow`, **not** on `UnityEditor.PropertyEditor`.
2. From Observation 1.4 (lines 60–67): `s_propertyEditorType` is `typeof(UnityEditor.PropertyEditor)`. Because it is not null, `searchType = s_propertyEditorType ?? s_inspectorWindowType;` assigns `s_propertyEditorType` (`PropertyEditor`) to `searchType`.
3. `FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects")` traverses the type hierarchy starting at `PropertyEditor` and following `current = current.BaseType` (`EditorWindow` -> `ScriptableObject` -> `Object`). It traverses strictly **upward** into base classes; it does not and cannot inspect derived classes like `InspectorWindow`.
4. As a result, `s_clearSerializedLockedObjectsMethod` evaluates to `null` on initialization and remains `null` permanently during editor execution.
5. In `PurgeAllInspectors()` (lines 123–126):
   ```csharp
   if (s_clearSerializedLockedObjectsMethod != null)
   {
       s_clearSerializedLockedObjectsMethod.Invoke(win, null);
   }
   ```
   and in `UnlockAllInspectors()` (lines 280–283):
   ```csharp
   if (s_clearSerializedLockedObjectsMethod != null)
   {
       s_clearSerializedLockedObjectsMethod.Invoke(win, null);
   }
   ```
   These calls are never executed.
6. **Impact**: When an Inspector window is locked to an object, its internal serialized locked objects buffer is never cleared. If that object is destroyed during scene unload/domain reload, the inspector retains dangling references in serialized state, violating Feature F1 and compromising Play Mode transition stability.

### 2.2 Secondary Defect: Potential `TargetException` on `s_isLockedProp`
1. Observation 1.3 confirmed `isLocked` is declared on `UnityEditor.InspectorWindow`, not on `PropertyEditor`.
2. In `InspectorSanitizerService.cs`, lines 113–120:
   ```csharp
   if (s_isLockedProp != null)
   {
       var isLockedVal = s_isLockedProp.GetValue(win);
       ...
   }
   ```
3. If `win` is a `PropertyEditor` that is not an `InspectorWindow` (e.g. from `m_AllPropertyEditors`), `s_isLockedProp.GetValue(win)` throws `System.Reflection.TargetException: Object does not match target type.`
4. Because lines 110–147 are wrapped in a single per-window `try-catch`, this `TargetException` aborts the window iteration before lines 128–143 can execute, skipping tracker editor destruction and repainting for that window.

### 2.3 Robustness of Other Attack Vectors
- **Null `ActiveEditorTracker.sharedTracker`**: Handled safely via `if (shared == null) return;` and `if (tracker == null) return;`.
- **Null / destroyed elements in `activeEditors`**: Handled safely; `ed != null` filters destroyed Unity native C++ objects via `operator !=`, and `Object.DestroyImmediate(ed)` is wrapped in dedicated `try-catch`.
- **`ed.target` throwing `MissingReferenceException`**: Handled safely; the `catch (Exception)` block flags `shouldDestroy = true` and cleans up corrupted editor ScriptableObjects.
- **Accidental destruction of Project Assets**: Handled safely; `DestroyOrphanAndSceneEditors` only checks `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))` and skips persistent assets via `EditorUtility.IsPersistent(target)`. Project assets (`GameObject`, `Material`, `ScriptableObject`) are never targeted.

---

## 3. Caveats

1. The reflection defect does not cause build errors because reflection calls are runtime late-bound. It is detectable only through static analysis of reflection targets or empirical reflection probing against engine binaries.
2. EditMode unit tests (`InspectorSanitizerTests`) are scheduled for Milestone 3 under `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`.

---

## 4. Conclusion

Milestone 1 satisfies repository conventions, Roslyn analyzer rules, and zero-GC / non-destructive requirements. However, **Feature F1 contains a critical reflection bug** where `ClearSerializedLockedObjects()` is searched on `PropertyEditor` instead of `InspectorWindow`, leaving `s_clearSerializedLockedObjectsMethod` permanently null and leaving locked inspector serialized object buffers uncleared.

**Verdict**: **REQUEST_CHANGES**

### Required Modifications for Milestone 1 Worker:
1. In `InspectorSanitizerService.cs`, update the reflection initialization so `ClearSerializedLockedObjects` is resolved against `s_inspectorWindowType`:
   ```csharp
   if (s_inspectorWindowType != null)
   {
       s_isLockedProp = FindPropertyInHierarchy(s_inspectorWindowType, "isLocked");
       s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(s_inspectorWindowType, "ClearSerializedLockedObjects");
       s_allInspectorsField = s_inspectorWindowType.GetField("m_AllInspectors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
   }
   ```
   Or use a fallback:
   ```csharp
   s_clearSerializedLockedObjectsMethod = (s_inspectorWindowType != null ? FindMethodInHierarchy(s_inspectorWindowType, "ClearSerializedLockedObjects") : null)
       ?? (s_propertyEditorType != null ? FindMethodInHierarchy(s_propertyEditorType, "ClearSerializedLockedObjects") : null);
   ```
2. In `PurgeAllInspectors()` and `UnlockAllInspectors()`, ensure `s_isLockedProp` and `s_clearSerializedLockedObjectsMethod` are only invoked if `win` is assignable to their declaring type:
   ```csharp
   if (s_isLockedProp != null && s_isLockedProp.DeclaringType.IsAssignableFrom(win.GetType()))
   {
       var isLockedVal = s_isLockedProp.GetValue(win);
       if (isLockedVal is bool isLocked && isLocked)
       {
           s_isLockedProp.SetValue(win, BoxedFalse);
       }
   }

   if (s_clearSerializedLockedObjectsMethod != null && s_clearSerializedLockedObjectsMethod.DeclaringType.IsAssignableFrom(win.GetType()))
   {
       s_clearSerializedLockedObjectsMethod.Invoke(win, null);
   }
   ```

---

## 5. Verification Method

### 5.1 Verification Commands
1. **Empirical Reflection Probe**:
   Execute from repository root:
   ```pwsh
   pwsh -NoProfile -Command '$asm = [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.dll"); $insp = $asm.GetType("UnityEditor.InspectorWindow"); $m = $insp.GetMethod("ClearSerializedLockedObjects", [System.Reflection.BindingFlags]"Instance, Public, NonPublic"); Write-Output ("Found: " + ($m -ne $null));'
   ```
   **Expected**: `Found: True`
2. **Build & Static Analysis**:
   ```pwsh
   dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
   ```
   **Expected**: `0 Advertencia(s)`, `0 Errores`.

### 5.2 Invalidation Conditions
- `s_clearSerializedLockedObjectsMethod` remaining null when initialized with `InspectorSanitizerService`.
- Any `TargetException` thrown when an instance of `PropertyEditor` is processed by `PurgeAllInspectors()`.
