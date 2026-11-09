# Handoff Report: Milestone 1 Adversarial Challenge & Stress Verification

**Agent**: `teamwork_challenger_m1_2`  
**Milestone**: Milestone 1: Inspector & Editor Sanitizer Service  
**Date**: 2026-11-07  
**Verdict**: **REQUEST_CHANGES**  
**Parent Orchestrator**: `3370c761-5485-4812-ad19-6f9cfcb9094d`

---

## 1. Observation

### 1.1 Empirical Roslyn Analyzer and Build Verification
Executed `dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"`:
```text
  SimpleWebTransport -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\SimpleWebTransport\SimpleWebTransport.dll
  ToolbarExtender.Editor -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\ToolbarExtender.Editor\ToolbarExtender.Editor.dll
  LiteNetLib -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\LiteNetLib\LiteNetLib.dll
  PurrNet.Edgegap.Runtime -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Edgegap.Runtime\PurrNet.Edgegap.Runtime.dll
  PurrNet.Runtime -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Runtime\PurrNet.Runtime.dll
  Game.Core -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Core\Game.Core.dll
  PurrNet.Editor -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\PurrNet.Editor\PurrNet.Editor.dll
  Game.Network -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Network\Game.Network.dll
  Game.Gameplay -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Gameplay\Game.Gameplay.dll
  Game.Editor -> C:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll

Compilación correcta.
    0 Advertencia(s)
    0 Errores
```
Observation: Code builds cleanly with zero Roslyn compiler warnings and zero errors. Analyzers `Antigravity.Unity.Analyzers`, `Microsoft.Unity.Analyzers`, and `PurrNet.Analyzers` report 0 violations.

---

### 1.2 Empirical Reflection Inspection of `UnityEditor.dll`
Inspecting `UnityEditor.PropertyEditor` and `UnityEditor.InspectorWindow` members in Unity 6000.3.7f1 (`C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll`):
- `UnityEditor.InspectorWindow` inherits directly from `UnityEditor.PropertyEditor`.
- `UnityEditor.PropertyEditor` declares:
  - `Property: tracker`
  - `Field: m_Tracker`
  - `Method: ClearEditorsAndRebuild()`
  - `Field: m_AllPropertyEditors`
- `UnityEditor.InspectorWindow` declares:
  - `Method: ClearSerializedLockedObjects()`
  - `Property: isLocked`
  - `Field: m_AllInspectors`
  - `Field: m_ObjectsLockedBeforeSerialization`
  - `Field: m_InstanceIDsLockedBeforeSerialization`

Observation: `ClearSerializedLockedObjects()` is declared on `UnityEditor.InspectorWindow`, and is **NOT** declared on `UnityEditor.PropertyEditor`.

---

### 1.3 Critical Defect: `s_clearSerializedLockedObjectsMethod` Evaluates Unconditionally to `null`
Direct inspection of `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs` lines 57–67:
```csharp
57:                 s_propertyEditorType = editorAssembly.GetType("UnityEditor.PropertyEditor");
58:                 s_inspectorWindowType = editorAssembly.GetType("UnityEditor.InspectorWindow");
59: 
60:                 var searchType = s_propertyEditorType ?? s_inspectorWindowType;
61:                 if (searchType != null)
62:                 {
63:                     s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
64:                     s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
65:                     s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
66:                     s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
67:                 }
```
Empirical evaluation in compiled `Game.Editor.dll`:
```pwsh
pwsh -NoProfile -Command '& { [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine.dll") | Out-Null; [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll") | Out-Null; $gameEditorAsm = [System.Reflection.Assembly]::LoadFrom("c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll"); $type = $gameEditorAsm.GetType("Game.Editor.Core.Lifecycle.InspectorSanitizerService"); [System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($type.TypeHandle); $field = $type.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic"); Write-Host "s_clearSerializedLockedObjectsMethod is:" ($field.GetValue($null) -ne $null); }'
```
Output:
```text
s_clearSerializedLockedObjectsMethod is: False
```
Observation: `s_clearSerializedLockedObjectsMethod` is `null` in the compiled binary.

---

### 1.4 Code Impact: Locked Inspector Buffer Is Never Cleared
Direct inspection of `InspectorSanitizerService.cs`:
- Line 123–126:
  ```csharp
  // 2. Clear locked serialized objects
  if (s_clearSerializedLockedObjectsMethod != null)
  {
      s_clearSerializedLockedObjectsMethod.Invoke(win, null);
  }
  ```
- Line 280–283:
  ```csharp
  if (s_clearSerializedLockedObjectsMethod != null)
  {
      s_clearSerializedLockedObjectsMethod.Invoke(win, null);
  }
  ```
Observation: Because `s_clearSerializedLockedObjectsMethod == null`, both blocks are permanently bypassed. The internal lists `m_ObjectsLockedBeforeSerialization` and `m_InstanceIDsLockedBeforeSerialization` on locked `InspectorWindow` instances are **never** cleared.

---

### 1.5 Sub-Optimal Type Lookup in `GetInspectorWindows`
Direct inspection of `InspectorSanitizerService.cs` line 485:
```csharp
var targetType = s_inspectorWindowType ?? s_propertyEditorType;
if (targetType != null)
{
    var objs = Resources.FindObjectsOfTypeAll(targetType);
```
Observation: In `Resources.FindObjectsOfTypeAll`, querying a derived class (`InspectorWindow`) will not return generic instances of the base class (`PropertyEditor`).

---

## 2. Logic Chain

1. **Hierarchy Directionality (Obs 1.2)**: `InspectorWindow` is a subclass of `PropertyEditor`. `FindMethodInHierarchy(Type type, string name)` starts at `type` and climbs up `.BaseType` until `typeof(object)`. It never searches derived types down the inheritance tree.
2. **Inverted Search Seed (Obs 1.3)**: Line 60 defines `var searchType = s_propertyEditorType ?? s_inspectorWindowType;`. Since `s_propertyEditorType` is not null, `searchType` is assigned `PropertyEditor`.
3. **Method Lookup Failure (Obs 1.3)**: `FindMethodInHierarchy(PropertyEditor, "ClearSerializedLockedObjects")` searches `PropertyEditor` -> `EditorWindow` -> `ScriptableObject` -> `Object`. Because `ClearSerializedLockedObjects` is declared on `InspectorWindow`, the method is not found, returning `null`.
4. **Failure of Attack Vector 1 (Obs 1.4)**:
   - When an inspector window is locked on a scene GameObject, references are stored in `m_ObjectsLockedBeforeSerialization` and `m_InstanceIDsLockedBeforeSerialization`.
   - `PurgeAllInspectors()` and `UnlockAllInspectors()` check `if (s_clearSerializedLockedObjectsMethod != null)` before invoking. Since the field is null, invocation is silently skipped.
   - During scene unload and domain reload for Play Mode, Unity deserializes `InspectorWindow` using this un-cleared buffer. The targets in native memory are destroyed, causing the exact 4 exceptions reported in `ORIGINAL_REQUEST.md`:
     - `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
     - `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
     - `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
     - `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`
5. **Worker Handoff Discrepancy**: The Worker handoff claimed:
   > *"Hierarchy traversal helpers (...) crawl up through PropertyEditor to EditorWindow, ensuring full compatibility with Unity 6000.3.7f1 where tracker, ClearEditorsAndRebuild, and ClearSerializedLockedObjects are declared on PropertyEditor."*
   Empirical testing shows that `ClearSerializedLockedObjects` is declared on `InspectorWindow`, not `PropertyEditor`. The worker's claim is demonstrably false.

---

## 3. Caveats

1. **Main Thread Concurrency**: Direct testing of multi-threaded concurrent calls is constrained because Unity's engine prohibits native API calls outside the main thread. However, single-threaded rapid successive calls were tested and verified to be safe from null reference crashes due to Unity's `operator ==` handling.
2. **Batchmode Execution**: Batchmode was verified statically and via headless simulation: `GetInspectorWindows()` safely returns an empty list, and `sharedTracker` / `allEditors` null checks prevent crashes.

---

## 4. Conclusion

**Verdict: REQUEST_CHANGES**

Milestone 1 satisfies architectural layering, Roslyn analyzer compliance, non-destructive tracker sanitation, and scene editor purging (`!EditorUtility.IsPersistent`). However, it contains a **critical reflection bug** where `s_clearSerializedLockedObjectsMethod` is unconditionally `null`, completely disabling locked serialized object cleanup.

### Required Changes for Worker:

1. **Fix `ClearSerializedLockedObjects` Reflection Resolution**:
   In `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`:
   Change the reflection search so that `ClearSerializedLockedObjects` is looked up starting from `s_inspectorWindowType` (the derived class declaring the method):
   ```csharp
   // Either start the hierarchy search from s_inspectorWindowType:
   var searchType = s_inspectorWindowType ?? s_propertyEditorType;
   if (searchType != null)
   {
       s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
       s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
       s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
       s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
   }
   ```
   *(Note: Searching up from `s_inspectorWindowType` will find `ClearSerializedLockedObjects` directly on `InspectorWindow`, while `tracker`, `m_Tracker`, and `ClearEditorsAndRebuild` will be found on its base class `PropertyEditor` via `FindMethodInHierarchy` / `FindFieldInHierarchy`.)*

2. **Invert Fallback Type for `Resources.FindObjectsOfTypeAll`**:
   In line 485 of `InspectorSanitizerService.cs`:
   ```csharp
   var targetType = s_propertyEditorType ?? s_inspectorWindowType;
   ```
   Ensures polymorphic retrieval of both `PropertyEditor` and `InspectorWindow` instances when the static lists are unavailable.

3. **Eliminate Redundant Calls in Facade**:
   In `InspectorTrackerSanitizer.PurgeAllInspectorWindows()`:
   Remove redundant calls to `Service.PurgeSharedTracker()` and `Service.DestroyOrphanAndSceneEditors()`, as `Service.PurgeAllInspectors()` already invokes them internally.

---

## 5. Verification Method

### 5.1 Reflection Field Non-Null Verification
Run the following PowerShell command after the worker applies the fix:
```pwsh
pwsh -NoProfile -Command '& { [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine.dll") | Out-Null; [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll") | Out-Null; $gameEditorAsm = [System.Reflection.Assembly]::LoadFrom("c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll"); $type = $gameEditorAsm.GetType("Game.Editor.Core.Lifecycle.InspectorSanitizerService"); [System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($type.TypeHandle); $field = $type.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic"); $val = $field.GetValue($null); Write-Host "s_clearSerializedLockedObjectsMethod:" ($val -ne $null) ($val); if ($val -eq $null) { exit 1 } }'
```
**Expected Result**:
```text
s_clearSerializedLockedObjectsMethod: True Void ClearSerializedLockedObjects()
```

### 5.2 Build & Analyzer Verification
```pwsh
dotnet build "c:\Users\SaidR\Documents\GitHub\TPOB\Game.Editor.csproj"
```
**Expected Result**:
- `Compilación correcta.`
- `0 Advertencia(s)`
- `0 Errores`

### 5.3 Invalidation Conditions
- `s_clearSerializedLockedObjectsMethod` remains `null`.
- Any compiler or Roslyn analyzer warnings or errors in `Game.Editor.csproj`.
