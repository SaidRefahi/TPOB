## 2026-11-07T00:19:52Z
You are teamwork_worker for Milestone 1: Inspector & Editor Sanitizer Service.
Your working directory is: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_worker_m1\
Your parent orchestrator conversation ID: 3370c761-5485-4812-ad19-6f9cfcb9094d

MANDATORY FIRST STEP: Read the user's original request verbatim at:
c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md

Read the project specification at:
c:\Users\SaidR\Documents\GitHub\TPOB\PROJECT.md

READ THE THREE EXPLORER HANDOFF REPORTS:
1. c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_1\handoff.md (Interface & Delegation design, plus proposed source code files in that directory)
2. c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_2\handoff.md (Detailed reflection mapping, PropertyEditor base class in Unity 6000.3.7f1, non-destructive tracker cleaning, and scene editor destruction)
3. c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_3\handoff.md (10-point Worker Implementation Checklist & Roslyn rules: AGY0001, AGY0002, AGY0003, AGY0021, UNT0007/8/23, Zero-GC, no empty catch)

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

WRITE OWNERSHIP (You exclusively own and may create/modify these files):
1. `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs` (Create)
2. `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs` (Create)
3. `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs` (Update existing)

CHECKLIST MANDATES:
- Ensure AGY0001: InspectorSanitizerService is `public sealed class InspectorSanitizerService : IInspectorSanitizerService`
- Ensure AGY0002: All fields are private
- Ensure AGY0003: Read-only properties. In InspectorTrackerSanitizer, `public static IInspectorSanitizerService Service => s_service ??= InspectorSanitizerService.Instance;` (no setter). Provide `public static void SetServiceForTesting(IInspectorSanitizerService service)` for testing
- Ensure UNT0007/UNT0008/UNT0023: Zero `?.` or `??` on UnityEngine.Object types (Editor, EditorWindow, GameObject, Component). Use explicit `if (obj != null)`
- Ensure AGY0021: Any event subscriptions must use named methods
- Zero LINQ (no `System.Linq`), cache all reflection members in static readonly fields
- Never call `tracker.Destroy()` or `shared.Destroy()` (non-destructive)
- Destroy scene/orphan editors checking `ed.target == null || !EditorUtility.IsPersistent(ed.target)`
- Zero empty `catch {}` blocks. Log warnings with `Debug.LogWarning`

VERIFICATION:
- Verify that the code compiles cleanly in Unity.
- Document compilation / verification results in your handoff report at:
  `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_worker_m1\handoff.md`
- Communicate completion back to parent via `send_message` with Recipient="3370c761-5485-4812-ad19-6f9cfcb9094d".
