# BRIEFING — 2026-11-07T01:10:00Z

## Mission
Design the PlayModeTransitionObserver pattern, event coordinator, and execution ordering for SelectionMementoService & InspectorSanitizerService in Milestone 2.

## 🔒 My Identity
- Archetype: Teamwork explorer
- Roles: Explorer, Architectural Designer, Roslyn Compliance Verifier
- Working directory: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m2_3
- Original parent: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Milestone: Milestone 2: Selection Memento Service & PlayMode Observer

## 🔒 Key Constraints
- Read-only investigation — do NOT implement or modify project source code/tests.
- Write only to working directory `.agents/teamwork/teamwork_explorer_m2_3/`.
- Strict Roslyn compliance: AGY0021 (no anonymous lambdas in += subscriptions), AGY0001 (shallow inheritance), AGY0002 (private serialized fields), AGY0003 (read-only properties), UNT0007/8/23 (no ?. or ?? on UnityEngine.Object).
- Backwards compatibility for EditorLifecycleCoordinator.
- Execution ordering: SelectionMementoService (capture & clear selection) BEFORE InspectorSanitizerService (purges open inspectors, shared tracker, scene/orphan editors).

## Current Parent
- Conversation ID: 3370c761-5485-4812-ad19-6f9cfcb9094d
- Updated: not yet

## Investigation State
- **Explored paths**:
  - `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/IEditorLifecycleSubscriber.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs`
  - `Assets/_Project/Editor/Core/Lifecycle/SelectionMemento.cs`
  - `Assets/_Project/Editor/Game.Editor.asmdef`
  - Peer explorer findings in `teamwork_explorer_m2_1` and `teamwork_explorer_m2_2`
- **Key findings**:
  - Event hooks: `EditorApplication.playModeStateChanged`, `AssemblyReloadEvents.beforeAssemblyReload`, `AssemblyReloadEvents.afterAssemblyReload`, and `EditorSceneManager.sceneClosing` (`(Scene, bool)`).
  - Deterministic execution ordering: `SelectionMementoService` must execute at `ExecutionOrder = 100` (capture + clear selection), followed by `InspectorSanitizerService` at `ExecutionOrder = 200` (purge windows, trackers, orphan editors).
  - Backwards compatibility achieved via `LegacySubscriberBridge : IPlayModeTransitionSubscriber` in `EditorLifecycleCoordinator.cs`, preventing duplicate event hooks and preserving legacy callers.
  - Strict compliance with `AGY0021` by binding all static and deferred events with named methods.
- **Unexplored areas**: Test suite implementation and E2E PlayMode verification (assigned to M3 and M4).

## Key Decisions Made
- `PlayModeTransitionObserver` acts as the single source of truth for Unity lifecycle hooks.
- Execution priorities: `SelectionMemento = 100`, `InspectorSanitizer = 200`, `Default = 500`.
- All subscriber notifications wrapped in individual try-catch blocks to prevent cascaded failures.
- Symmetrical unhooking before hooking in static constructors to eliminate duplicate handler registration.

## Artifact Index
- DISPATCH.md — Incoming dispatch message
- BRIEFING.md — Persistent working memory
- progress.md — Liveness heartbeat and task progress
- handoff.md — Final 5-component handoff report
