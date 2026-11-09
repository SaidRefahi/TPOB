## 2026-11-07T00:42:42Z
You are teamwork_worker for Milestone 1 Iteration 2.
Your working directory is: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_worker_m1_it2\
Your parent orchestrator conversation ID: 3370c761-5485-4812-ad19-6f9cfcb9094d

MANDATORY FIRST STEP: Read the user's original request verbatim at:
c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md

Read the project specification at:
c:\Users\SaidR\Documents\GitHub\TPOB\PROJECT.md

READ EXPLORER HANDOFF REPORTS (which contain exact before/after code blocks):
1. c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_it2_3\handoff.md (Sections 4 & 5 detail the exact code edits and verification commands)
2. c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_it2_1\handoff.md
3. c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_it2_2\handoff.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

WRITE OWNERSHIP (You exclusively own and may modify these files):
1. `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
2. `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`

REQUIRED CODE CHANGES:
1. In `InspectorSanitizerService.cs`:
   - Change line 60: `var searchType = s_inspectorWindowType ?? s_propertyEditorType;` (searching from InspectorWindow ensures ClearSerializedLockedObjects declared on InspectorWindow and tracker/m_Tracker/ClearEditorsAndRebuild declared on base PropertyEditor are all resolved). Add fallback if needed.
   - Guard reflection calls (`s_isLockedProp`, `s_clearSerializedLockedObjectsMethod`, `s_clearEditorsAndRebuildMethod`, `s_trackerProp`, `s_trackerField`) with `member.DeclaringType != null && member.DeclaringType.IsInstanceOfType(win)` (or `IsAssignableFrom`) to prevent `TargetException` when processing non-InspectorWindow `PropertyEditor` instances.
   - In `GetInspectorWindows()` fallback query, use `s_propertyEditorType ?? s_inspectorWindowType` to capture all derived `InspectorWindow` instances.
2. In `InspectorTrackerSanitizer.cs`:
   - In `PurgeAllInspectorWindows()`, keep only `Service.PurgeAllInspectors();` (eliminating redundant sub-calls).

VERIFICATION REQUIREMENTS:
- Run `dotnet build Game.Editor.csproj` to verify build succeeds with 0 errors and 0 warnings.
- Run the PowerShell reflection probe to verify `s_clearSerializedLockedObjectsMethod` resolves to non-null:
  `pwsh -NoProfile -Command '& { [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine.dll") | Out-Null; [System.Reflection.Assembly]::LoadFrom("C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEditor.dll") | Out-Null; $a = [System.Reflection.Assembly]::LoadFrom("c:\Users\SaidR\Documents\GitHub\TPOB\Temp\Bin\Debug\Game.Editor\Game.Editor.dll"); $t = $a.GetType("Game.Editor.Core.Lifecycle.InspectorSanitizerService"); [System.Runtime.CompilerServices.RuntimeHelpers]::RunClassConstructor($t.TypeHandle); $f = $t.GetField("s_clearSerializedLockedObjectsMethod", [System.Reflection.BindingFlags]"Static,NonPublic"); $v = $f.GetValue($null); Write-Host "Non-null:" ($v -ne $null) "DeclaringType:" $v.DeclaringType.Name; }'`
- Document all verification results in `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_worker_m1_it2\handoff.md`.
- Communicate completion back to parent via `send_message` with Recipient="3370c761-5485-4812-ad19-6f9cfcb9094d".
