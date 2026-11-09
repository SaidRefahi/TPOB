# BRIEFING — 2026-11-07T00:17:35Z

## Mission
Deeply design the reflection and cleanup mechanics in InspectorSanitizerService for Unity 6000.3.7f1.

## 🔒 My Identity
- Archetype: explorer
- Roles: investigator, synthesizer
- Working directory: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_2
- Original parent: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Milestone: Milestone 1: Inspector & Editor Sanitizer Service

## 🔒 Key Constraints
- Read-only investigation — do NOT implement or modify project code/tests
- Do NOT scan files sequentially with filesystem or shell tools
- Formulate exact reflection/cleanup mechanics for Unity 6000.3.7f1 PropertyEditor / ActiveEditorTracker / orphan editors
- Replace silent catch {} blocks with informative Debug.LogWarning
- Write report to handoff.md and send message back to parent orchestrator

## Current Parent
- Conversation ID: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Updated: 2026-11-07T00:17:35Z

## Investigation State
- **Explored paths**:
  - `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs`
  - `UnityEditor.CoreModule.dll` metadata for `UnityEditor.PropertyEditor`, `UnityEditor.InspectorWindow`, `ActiveEditorTracker`
- **Key findings**:
  - `PropertyEditor` in Unity 6000.3.7f1 is the base class of `InspectorWindow`. Non-public field `m_Tracker` is declared on `PropertyEditor`; calling `inspectorType.GetField("m_Tracker")` without hierarchy traversal returns `null`, causing tracker cleanup to silently fail.
  - `PropertyEditor` declares a public property `tracker` (`public ActiveEditorTracker tracker { get; }`) and internal methods `ClearEditorsAndRebuild()` and `ClearSerializedLockedObjects()`.
  - Calling `tracker.Destroy()` or `shared.Destroy()` destroys native C++ tracker wrappers, causing native crash/corruption on subsequent repaints or rebuilds. Must destroy individual editors via `DestroyImmediate` without destroying trackers.
  - Checking `ed.target == null` fails before scene unload because the scene is still loaded. The condition must be `ed.target == null || !EditorUtility.IsPersistent(ed.target)` to destroy scene editors before scene unloads.
  - Multi-target editors (`ed.targets`) must also be checked. If any target is not persistent or accessing `targets` throws, mark for destruction.
  - Replace 5 silent `catch {}` blocks with structured `Debug.LogWarning`.
- **Unexplored areas**: None for Milestone 1 scope; design is complete and ready for handoff.

## Key Decisions Made
- Architecture decouples interface `IInspectorSanitizerService` and service `InspectorSanitizerService` while providing a backwards-compatible facade `InspectorTrackerSanitizer` delegating to `InspectorSanitizerService.Instance`.
- Static reflection cache initialized once in `InspectorSanitizerService` static constructor with hierarchy traversal fallback.
- Defensive null guards applied to windows, trackers, editors, targets, and reflection handles.

## Artifact Index
- DISPATCH.md — Incoming dispatch instructions
- BRIEFING.md — Persistent working memory
- progress.md — Heartbeat and progress tracking
- handoff.md — Final 5-component handoff report
