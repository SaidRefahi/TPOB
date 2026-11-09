# BRIEFING — 2026-11-07T00:18:40Z

## Mission
Investigate and design `IInspectorSanitizerService` and `InspectorSanitizerService` in `Game.Editor.Core.Lifecycle` for Milestone 1, ensuring backward compatibility with `InspectorTrackerSanitizer` and callers.

## 🔒 My Identity
- Archetype: teamwork_explorer
- Roles: [explorer, investigator, analyst]
- Working directory: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_1
- Original parent: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Milestone: Milestone 1: Inspector & Editor Sanitizer Service

## 🔒 Key Constraints
- Read-only investigation — do NOT implement or modify project code or tests
- Only write inside working directory .agents/teamwork/teamwork_explorer_m1_1/
- No sequential file scans; use codebase-memory, ULSM, or targeted queries
- Adhere to project guidelines (AGENTS.md, PROJECT.md)

## Current Parent
- Conversation ID: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Updated: not yet

## Investigation State
- **Explored paths**:
  - `ORIGINAL_REQUEST.md`, `PROJECT.md`, `teamwork_explorer_survey_2/handoff.md`
  - `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`
  - `Assets/_Project/Editor/LevelEditor/InspectorSanitizer.cs`, `LevelBuilderWindow.cs`, `SelectionMementoService.cs`, `EditorLifecycleCoordinator.cs`
  - `UnityEditor.CoreModule.dll` type hierarchy: `InspectorWindow` : `PropertyEditor` : `EditorWindow`
- **Key findings**:
  - `tracker`, `ClearEditorsAndRebuild()`, and `ClearSerializedLockedObjects()` are declared on `PropertyEditor` in Unity 6000.3.7f1.
  - Calling `tracker.Destroy()` or `shared.Destroy()` destroys native C++ tracker wrappers, corrupting memory. Non-destructive sanitation must destroy `activeEditors` directly and call `ForceRebuild()`.
  - Checking `ed.target == null || !EditorUtility.IsPersistent(ed.target)` safely eliminates scene editors prior to scene unload.
  - Backwards compatibility requires static facade `InspectorTrackerSanitizer` delegating all 6 legacy methods to `InspectorSanitizerService.Instance`.
- **Unexplored areas**: None for M1 scope.

## Key Decisions Made
- `IInspectorSanitizerService` defines exactly 4 core methods: `PurgeAllInspectors()`, `PurgeSharedTracker()`, `DestroyOrphanAndSceneEditors()`, `UnlockAllInspectors()`.
- `InspectorSanitizerService` implements `IInspectorSanitizerService` plus utility methods `ClearConsole()` and `SanitizeActiveEditors()`.
- Static `ReflectionCache` caches hierarchy methods across `InspectorWindow -> PropertyEditor -> EditorWindow` to eliminate reflection allocations and handle Unity 6 internal polymorphism.
- `InspectorTrackerSanitizer` delegates completely to `InspectorSanitizerService.Instance`.

## Artifact Index
- DISPATCH.md — Initial dispatch instructions
- BRIEFING.md — Persistent context & state
- progress.md — Liveness heartbeat
- proposed_IInspectorSanitizerService.cs — Proposed contract for implementer
- proposed_InspectorSanitizerService.cs — Proposed implementation for implementer
- proposed_InspectorTrackerSanitizer.cs — Proposed backwards-compatible facade for implementer
- handoff.md — Final 5-component handoff report
