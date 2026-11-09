# Project: Unity 6000.3.7f1 Play Mode Transition Exception Resolution & Selection Memento

## Architecture
- **Root Namespace**: `Game.Editor`
- **Assembly Definition**: `Assets/_Project/Editor/Game.Editor.asmdef`
- **Subsystem Path**: `Assets/_Project/Editor/Core/Lifecycle/`
- **Test Assembly Definition**: `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`
- **Design Patterns**:
  - **Observer Pattern**: `IPlayModeTransitionObserver` and `PlayModeTransitionObserver` coordinate Unity editor lifecycle events (`EditorApplication.playModeStateChanged`, `AssemblyReloadEvents`, `EditorSceneManager.sceneClosing`) and dispatch to ordered subscribers (`IPlayModeTransitionSubscriber`).
  - **Memento Pattern**: `SelectionMementoService` captures selection state into an immutable `SelectionMemento`, persists it across Domain Reloads using `SessionState` (`SessionStateSelectionPersistence`), and restores active and inactive GameObjects via hierarchy traversal and `GlobalObjectId`.
  - **Service & Single Responsibility Pattern**: `InspectorSanitizerService` encapsulates reflection-based purging of `PropertyEditor` / `InspectorWindow` trackers, locked object buffers, and orphan `Editor` ScriptableObjects targeting non-persistent scene objects.

```
                    ┌─────────────────────────────────────────┐
                    │       PlayModeTransitionObserver        │
                    │   (EditorApplication & AssemblyReload)  │
                    └────────────────────┬────────────────────┘
                                         │ notifies in order
                                         ▼
                    ┌─────────────────────────────────────────┐
                    │      IPlayModeTransitionSubscriber      │
                    └────────────┬───────────────────┬────────┘
                                 │                   │
                                 ▼                   ▼
       ┌──────────────────────────────┐     ┌────────────────────────────────┐
       │   SelectionMementoService    │     │   InspectorSanitizerService    │
       │ (Captures & Restores State)  │     │ (Purges Trackers & Destroys Ed)│
       └──────────────┬───────────────┘     └────────────────────────────────┘
                      │ persists via
                      ▼
       ┌──────────────────────────────┐
       │ SessionStateSelectionPersist │
       │   (Survives Domain Reload)   │
       └──────────────────────────────┘
```

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| F1 | PropertyEditor Reflection Purge | Access `tracker`, `ClearEditorsAndRebuild()`, and `ClearSerializedLockedObjects()` on `UnityEditor.PropertyEditor` in Unity 6000.3.7f1 | M1 | Survey 2 |
| F2 | Non-Destructive Tracker Sanitation | Destroy `tracker.activeEditors` without invoking `tracker.Destroy()` or `shared.Destroy()`, avoiding native C++ pointer invalidation | M1 | Survey 2, 3 |
| F3 | Scene & Orphan Editor Purge | Purge all `Editor` ScriptableObjects where `target == null || !EditorUtility.IsPersistent(target)` prior to scene unload | M1 | Survey 1, 2 |
| F4 | Robust Error Diagnostics | Replace empty `catch {}` blocks with structured warning logs | M1 | Survey 1, 3 |
| F5 | SessionState Memento Persistence | Store selection memento JSON in `UnityEditor.SessionState` so it survives managed domain reloads | M2 | Survey 1, 3 |
| F6 | Inactive Hierarchy Object Resolution | Traverse `Scene.GetRootGameObjects()` to resolve inactive scene GameObjects and handle duplicate names | M2 | Survey 1, 3 |
| F7 | GlobalObjectId Fallback | Use `GlobalObjectId` parsing and resolution as primary/fallback mechanism for scene objects and project assets | M2 | Survey 3 |
| F8 | Symmetrical Observer Lifecycle | Decouple lifecycle observation via `PlayModeTransitionObserver` using named event handlers (`AGY0021`) | M2 | Survey 1, 3 |
| F9 | EditMode Test Assembly Setup | Create `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef` for isolated unit and integration testing | M3 | Survey 3 |
| F10 | Memento Unit Tests | Test capture, JSON serialization, SessionState persistence, and inactive GameObject restoration | M3 | Survey 3 |
| F11 | Inspector Sanitizer Tests | Test reflection sanitation, locked inspector unlocking, and orphan editor destruction | M3 | Survey 3 |
| F12 | PlayMode E2E Verification | Enter/exit Play Mode in Unity 6000.3.7f1 with zero `SerializedObjectNotCreatableException` and zero `MissingReferenceException` | M4 | ORIGINAL_REQUEST |
| F13 | Roslyn & Rule Compliance | Verify zero violations of `AGY0001`, `AGY0002`, `AGY0003`, `AGY0021`, and zero GC allocations on critical paths | M4 | AGENTS.md |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Inspector & Editor Sanitizer Service | Refactor `InspectorTrackerSanitizer` into `IInspectorSanitizerService` / `InspectorSanitizerService` with Unity 6 `PropertyEditor` reflection, non-destructive tracker sanitation, and scene editor purging (`!EditorUtility.IsPersistent`) | none | DONE |
| M2 | Selection Memento Service & PlayMode Observer | Refactor `SelectionMementoService` and `EditorLifecycleCoordinator` into `ISelectionMementoService`, `SessionStateSelectionPersistence`, `PlayModeTransitionObserver`, supporting inactive hierarchy resolution and SessionState persistence | M1 | DONE |
| M3 | Test Infrastructure & Unit/Integration Tests | Create `Game.Editor.Tests.asmdef` and implement test suites: `SelectionMementoUnitTests`, `HierarchyPathResolverTests`, `InspectorSanitizerTests`, `PlayModeObserverIntegrationTests` | M1, M2 | DONE |
| M4 | E2E PlayMode Verification & Stability Certification | Execute Play Mode transition verification in Unity 6000.3.7f1, inspect `Editor.log`, verify zero console exceptions, verify selection restoration, and validate Roslyn analyzer rules | M1, M2, M3 | DONE |

## Interface Contracts

### `IInspectorSanitizerService`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public interface IInspectorSanitizerService
    {
        void PurgeAllInspectors();
        void PurgeSharedTracker();
        void DestroyOrphanAndSceneEditors();
        void UnlockAllInspectors();
    }
}
```

### `ISelectionMementoService`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public interface ISelectionMementoService : IPlayModeTransitionSubscriber
    {
        SelectionMemento CaptureCurrentSelection();
        void ClearSelectionSafely();
        void RestoreSelection(SelectionMemento memento);
        bool HasPersistedMemento { get; }
        void RestoreFromPersistence();
        void ClearPersistence();
    }
}
```

### `ISelectionStatePersistence`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public interface ISelectionStatePersistence
    {
        void SaveMemento(SelectionMemento memento);
        SelectionMemento LoadMemento();
        void ClearMemento();
        bool HasMemento { get; }
    }
}
```

### `IPlayModeTransitionObserver` and `IPlayModeTransitionSubscriber`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public interface IPlayModeTransitionSubscriber
    {
        int ExecutionOrder { get; }
        void OnExitingEditMode();
        void OnEnteredPlayMode();
        void OnExitingPlayMode();
        void OnEnteredEditMode();
        void OnBeforeAssemblyReload();
        void OnAfterAssemblyReload();
        void OnSceneClosing(UnityEngine.SceneManagement.Scene scene);
    }

    public interface IPlayModeTransitionObserver
    {
        void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber);
        void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber);
    }
}
```

## Code Layout
- `Assets/_Project/Editor/Game.Editor.asmdef`
- `Assets/_Project/Editor/Core/Lifecycle/Interfaces/`
  - `IInspectorSanitizerService.cs`
  - `ISelectionMementoService.cs`
  - `ISelectionStatePersistence.cs`
  - `IPlayModeTransitionObserver.cs`
  - `IPlayModeTransitionSubscriber.cs`
- `Assets/_Project/Editor/Core/Lifecycle/Models/`
  - `SelectionMemento.cs`
  - `SceneObjectIdentifier.cs`
- `Assets/_Project/Editor/Core/Lifecycle/Services/`
  - `InspectorSanitizerService.cs`
  - `SelectionMementoService.cs`
  - `SessionStateSelectionPersistence.cs`
  - `PlayModeTransitionObserver.cs`
  - `HierarchyPathResolver.cs`
- `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs` (Backwards-compatibility entry point / coordinator)
- `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs` (Delegates to `InspectorSanitizerService` to prevent breaking existing callers)
- `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`
- `Assets/_Project/Tests/Editor/SelectionMementoUnitTests.cs`
- `Assets/_Project/Tests/Editor/HierarchyPathResolverTests.cs`
- `Assets/_Project/Tests/Editor/InspectorSanitizerTests.cs`
- `Assets/_Project/Tests/Editor/PlayModeObserverIntegrationTests.cs`
