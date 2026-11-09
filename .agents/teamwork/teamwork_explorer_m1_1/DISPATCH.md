## 2026-11-07T00:13:39Z
You are teamwork_explorer instance 1 for Milestone 1: Inspector & Editor Sanitizer Service.
Your working directory is: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_1\
Your parent orchestrator conversation ID: 3370c761-5485-4812-ad19-6f9cfcb9094d

MANDATORY FIRST STEP: Read the user's original request verbatim at:
c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md

Read the project specification at:
c:\Users\SaidR\Documents\GitHub\TPOB\PROJECT.md
Also review Survey 2 handoff at:
c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_survey_2\handoff.md

TOKEN & ARCHITECTURE DIRECTIVE:
For codebase exploration, architectural discovery, or tracing relationships between C# scripts, do not scan files sequentially with filesystem or shell tools. Verify graph health via codebase-memory:index_status and query the semantic graph (query_graph, trace_path, search_graph) and ULSM (ulsm:find_references) to minimize token consumption.

YOUR SPECIFIC MISSION FOR MILESTONE 1:
1. Examine `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs` and design the interface `IInspectorSanitizerService` and implementation `InspectorSanitizerService` in `Game.Editor.Core.Lifecycle`.
2. Ensure `InspectorSanitizerService` implements:
   - `PurgeAllInspectors()`
   - `PurgeSharedTracker()`
   - `DestroyOrphanAndSceneEditors()`
   - `UnlockAllInspectors()`
3. Ensure backwards compatibility: `InspectorTrackerSanitizer` must delegate its public static methods to `InspectorSanitizerService.Instance` or a default instance without breaking callers (e.g., `LevelEditor/InspectorSanitizer.cs`).
4. Detail the exact public and internal method signatures, file locations, and namespace contracts.

SCOPE BOUNDARIES:
- Read-only investigation. You MUST NOT modify or create any project source code or tests.
- Update `progress.md` in your working directory as you make progress.
- Write your final report to: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_1\handoff.md`.
- Communicate completion back to parent via `send_message` with Recipient="3370c761-5485-4812-ad19-6f9cfcb9094d".
