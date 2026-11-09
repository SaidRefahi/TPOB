# BRIEFING — 2026-11-07T00:18:00Z

## Mission
Analyze rules, static analysis constraints, Zero-GC / lifecycle patterns, and formulate verification guidelines & an implementation checklist for InspectorSanitizerService in Milestone 1.

## 🔒 My Identity
- Archetype: explorer
- Roles: explorer, synthesizer
- Working directory: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_3
- Original parent: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Milestone: Milestone 1: Inspector & Editor Sanitizer Service

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Review rules & static analysis constraints for InspectorSanitizerService & IInspectorSanitizerService
- Formulate verification guidelines and implementation checklist for Worker
- Do not modify or create project source code or tests

## Current Parent
- Conversation ID: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Updated: 2026-11-07T00:18:00Z

## Investigation State
- **Explored paths**:
  - `ORIGINAL_REQUEST.md` (console exceptions, requirements R1-R4)
  - `PROJECT.md` (architecture, interface contracts, milestones M1-M4)
  - `AGENTS.md` and `.agents/rules/*` (`core-architecture.md`, `performance-hotpaths.md`, `input-and-events.md`)
  - `.editorconfig` (Roslyn rules AGY0001-0003, AGY0011-0016, AGY0021, AGY0031, UNT0002, UNT0007, UNT0008, UNT0023, UNT0028, UNT0041, UNT0046)
  - Existing implementation: `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`, `EditorLifecycleCoordinator.cs`, `LevelEditor/InspectorSanitizer.cs`
  - Survey reports: `teamwork_explorer_survey_1`, `teamwork_explorer_survey_2`, `teamwork_explorer_survey_3`
- **Key findings**:
  - `UNT0007`/`UNT0008`/`UNT0023`: Strict prohibition of `?.`, `??`, `??=` on `UnityEngine.Object` types (`Editor`, `EditorWindow`, `GameObject`, `Component`).
  - `AGY0001`: Maximum 1 level of class inheritance (plain C# class deriving from `object` and implementing `IInspectorSanitizerService` has 0 class inheritance).
  - `AGY0002`: All fields must be `private`.
  - `AGY0003`: Properties must be read-only (`get;` only, no `set;`).
  - `AGY0021`: Named event handlers required in `+=` subscriptions.
  - Performance: Static caching of reflection metadata (`PropertyEditor`, `InspectorWindow`, methods, fields) to eliminate allocations and CPU overhead on transitions; zero LINQ; zero boxing.
  - Symmetrical lifecycle: Never call `tracker.Destroy()` or `shared.Destroy()`; destroy only managed `Editor` ScriptableObjects (`tracker.activeEditors` and orphan `Editor`s); purge scene editors where `target == null || !EditorUtility.IsPersistent(target)`.
  - Robust diagnostics: Replace silent `catch {}` with structured `Debug.LogWarning` including failure context.
- **Unexplored areas**: Milestone 2 and M3 scope (out of scope for M1).

## Key Decisions Made
- Formulated complete Roslyn and engine rule mapping for M1.
- Formulated 10-point Worker Implementation Checklist.
- Formulated static analysis and functional verification guidelines.

## Artifact Index
- `DISPATCH.md` — Incoming dispatch instructions
- `progress.md` — Liveness heartbeat and progress log
- `handoff.md` — Final handoff report (Observations, Logic Chain, Caveats, Conclusion, Verification Method)
