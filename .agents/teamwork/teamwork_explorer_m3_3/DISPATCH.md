## 2026-11-07T01:29:29Z
You are Milestone 3 Explorer 3 for the TPOB Unity project.
Your working directory: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m3_3\
Original request path: c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md
Scope document: c:\Users\SaidR\Documents\GitHub\TPOB\PROJECT.md

Token & Exploration Directive:
For architectural exploration, use codebase-memory and ULSM rather than sequential shell/filesystem scans.

Your Task:
Design the test suites for Inspector Sanitizer & PlayMode Observer in Milestone 3:
1. Inspect `Assets/_Project/Editor/Core/Lifecycle/`:
   - `Interfaces/IInspectorSanitizerService.cs`
   - `Interfaces/IPlayModeTransitionObserver.cs`
   - `Interfaces/IPlayModeTransitionSubscriber.cs`
   - `PlayModeExecutionOrder.cs`
   - `Services/InspectorSanitizerService.cs`
   - `Services/PlayModeTransitionObserver.cs`
   - `EditorLifecycleCoordinator.cs`
2. Design comprehensive NUnit test fixtures for:
   - `InspectorSanitizerTests.cs`:
     - Test reflection member resolution on `PropertyEditor` and `InspectorWindow` (seed type, methods, properties).
     - Test non-destructive active editor tracker purging (verify zero calls to native Destroy()).
     - Test scene & orphan editor destruction (`!EditorUtility.IsPersistent(target)` vs persistent assets).
     - Test locked inspector detection and unlocking.
   - `PlayModeObserverIntegrationTests.cs`:
     - Test priority sorting: subscribers execute in ascending order of `ExecutionOrder` (`SelectionMemento` 100 before `InspectorSanitizer` 200).
     - Test exception isolation: a throwing subscriber in phase X does not halt subsequent subscribers.
     - Test all 7 lifecycle phases (`OnExitingEditMode`, `OnEnteredPlayMode`, `OnExitingPlayMode`, `OnEnteredEditMode`, `OnBeforeAssemblyReload`, `OnAfterAssemblyReload`, `OnSceneClosing`).
     - Test `LegacySubscriberAdapter` bridging in `EditorLifecycleCoordinator`.
     - Test event registration idempotency and cleanup.
3. Provide concrete C# code blueprints for both test fixtures adhering to `AGENTS.md` and `.agents/rules/`.

Write your structured report to `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m3_3\handoff.md`.
Notify orchestrator when done via send_message.
