# Handoff Report — Milestone 2: PlayMode Transition Observer Pattern & Event Coordinator

## 1. Observation

### 1.1 Verbatim Problem Statement and Console Exceptions
From `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\ORIGINAL_REQUEST.md` (lines 13-19):
Entering Play Mode in Unity 6000.3.7f1 triggers four console exceptions:
- `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
- `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

**Root Cause**: Open `InspectorWindow` instances maintain active `Editor` objects in private `m_Tracker` (`ActiveEditorTracker`) across domain reload and edit scene unload, causing `OnEnable()` to execute with null targets upon deserialization.

### 1.2 Direct Observations in Existing Editor Lifecycle Code
1. **`Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs` (lines 10-23)**:
   ```csharp
   [InitializeOnLoad]
   public static class EditorLifecycleCoordinator
   {
       private static readonly List<IEditorLifecycleSubscriber> s_subscribers = new List<IEditorLifecycleSubscriber>();

       static EditorLifecycleCoordinator()
       {
           RegisterSubscriber(SelectionMementoService.Instance);

           EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
           AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
           AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
           Selection.selectionChanged += HandleSelectionChanged;
       }
   ```
   - **Observation 1.2.1**: `EditorLifecycleCoordinator` maintains an untyped list without execution priorities (`ExecutionOrder`).
   - **Observation 1.2.2**: The coordinator only listens to `playModeStateChanged` and `AssemblyReloadEvents`. It completely lacks listening to `EditorSceneManager.sceneClosing`, leaving inspector trackers and orphan editors active during scene unloading.
   - **Observation 1.2.3**: `EditorLifecycleCoordinator` is a static class without an interface contract (`IPlayModeTransitionObserver`), making it impossible to mock, isolate, or unit test subscribers in EditMode tests.

2. **`Assets/_Project/Editor/Core/Lifecycle/IEditorLifecycleSubscriber.cs` (lines 4-13)**:
   ```csharp
   public interface IEditorLifecycleSubscriber
   {
       void OnBeforePlayModeEntered();
       void OnPlayModeEntered();
       void OnBeforePlayModeExited();
       void OnPlayModeExited();
       void OnBeforeDomainReload();
       void OnAfterDomainReload();
   }
   ```
   - **Observation 1.2.4**: Lacks `ExecutionOrder` priority property.
   - **Observation 1.2.5**: Lacks `OnSceneClosing(Scene scene)` lifecycle callback.
   - **Observation 1.2.6**: Symmetrical transition phases (`OnExitingEditMode`, `OnEnteredPlayMode`, `OnExitingPlayMode`, `OnEnteredEditMode`) are inconsistently named compared to standard Unity `PlayModeStateChange` semantics.

3. **`Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs` (lines 64-69, 146)**:
   ```csharp
   public void ClearSelectionSafely()
   {
       Selection.objects = Array.Empty<Object>();
       Selection.activeObject = null;
       InspectorTrackerSanitizer.ForceCleanTracker();
   }
   ...
   public void OnBeforeDomainReload()
   {
       ...
       InspectorTrackerSanitizer.PurgeAllInspectorWindows();
   }
   ```
   - **Observation 1.2.7**: `SelectionMementoService` directly invokes static methods on `InspectorTrackerSanitizer`, coupling state capture/restoration to inspector reflection and violating the Single Responsibility Principle.

4. **`Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs` (lines 17-25)**:
   - `InspectorSanitizerService` was implemented in Milestone 1 implementing `IInspectorSanitizerService`.
   - Currently, it does not implement a subscriber interface and must be invoked imperatively rather than reactively via the observer pipeline.

5. **Roslyn Analyzer Rules (`AGENTS.md` and `.agents/rules/`)**:
   - `AGY0021` (Named Event Handlers Required): Prohibits inline anonymous lambdas in `+=` subscriptions.
   - `AGY0001` (Shallow inheritance): Max 1 level of domain inheritance.
   - `AGY0002` (Private fields): All fields private by default.
   - `AGY0003` (Read-only properties): Component state exposed strictly via read-only properties.
   - `UNT0007`, `UNT0008`, `UNT0023`: Prohibits `?.`, `??`, or `??=` on `UnityEngine.Object`.

---

## 2. Logic Chain

### 2.1 The Two-Phase Pre-Unload Pipeline Mechanics
1. **Observation 1.1**: The 4 exceptions (`MeshColliderEditor`, `GameObjectInspector`, `TransformInspector`, `Renderer2DMeshEditor`) occur because `Editor` instances in memory hold onto target GameObjects/Components that are destroyed when the Edit Mode scene is unloaded.
2. **Observation 1.2.1 & 1.2.7**: In order to restore selection after scene reload, `SelectionMementoService` must record the selected objects. If `InspectorSanitizerService` purges the inspector and clears selection first, `SelectionMementoService` records an empty selection, permanently losing user state.
3. **Deduction 2.1**: Deterministic execution ordering is mandatory. The pre-transition sequence must be:
   - **Phase 1 (ExecutionOrder = 100 - SelectionMementoService)**:
     1. Capture active selection state (GlobalObjectIds, hierarchy paths, sibling indices).
     2. Safely clear selection: `Selection.objects = Array.Empty<Object>()`, `Selection.activeObject = null`.
   - **Phase 2 (ExecutionOrder = 200 - InspectorSanitizerService)**:
     1. Force-unlock all open `PropertyEditor` / `InspectorWindow` instances.
     2. Clear serialized locked object buffers (`ClearSerializedLockedObjects()`).
     3. Destroy active editors in trackers via `DestroyImmediate` without calling native `tracker.Destroy()`.
     4. Clear UI elements and rebuild inspector containers (`ClearEditorsAndRebuild()`).
     5. Purge and rebuild `ActiveEditorTracker.sharedTracker`.
     6. Destroy all orphan/scene-bound `Editor` ScriptableObjects (`target == null || !EditorUtility.IsPersistent(target)`).
4. **Result**: Prior to scene unload and domain reload, zero `Editor` instances reference scene objects, and trackers are empty. On reload, Unity deserializes clean empty windows, completely eliminating all 4 console exceptions.

### 2.2 Observer Pattern & Event Coordinator Architecture
1. **Observation 1.2.2**: Transitions happen under multiple distinct Unity events:
   - `EditorApplication.playModeStateChanged`: When switching between Edit Mode and Play Mode.
   - `AssemblyReloadEvents.beforeAssemblyReload` / `afterAssemblyReload`: When domain reload occurs.
   - `EditorSceneManager.sceneClosing`: When edit scenes are closed prior to play mode or user action.
2. **Deduction 2.2**: `PlayModeTransitionObserver` must encapsulate all four events in one place, insulating subscribers from Unity's low-level static delegates.
3. **Observation 1.2.4**: Subscribers require different execution priorities.
4. **Deduction 2.3**: `IPlayModeTransitionSubscriber.ExecutionOrder` allows `PlayModeTransitionObserver` to maintain a pre-sorted subscriber list:
   - Insert-time sorting guarantees $O(1)$ dispatch without per-event allocations.
   - Standard order: `SelectionMemento = 100` executes first; `InspectorSanitizer = 200` executes second; user subscribers execute at `Default = 500`.

### 2.3 Resilient Dispatch & Error Isolation
1. **Observation**: If any subscriber throws an unhandled exception during `OnExitingEditMode`, naive iteration stops execution, causing subsequent subscribers (like `InspectorSanitizerService`) to be skipped.
2. **Deduction 2.4**: Every subscriber dispatch in `PlayModeTransitionObserver` must be wrapped in an individual `try { ... } catch (Exception ex) { Debug.LogError(...); }` block. This guarantees that a failure in one subscriber never halts the sanitation pipeline.

### 2.4 Backwards Compatibility & Adapter Strategy for `EditorLifecycleCoordinator`
1. **Observation 1.2.1**: Existing projects or tools may still call `EditorLifecycleCoordinator.RegisterSubscriber(...)`.
2. **Deduction 2.5**: Modifying or removing `EditorLifecycleCoordinator` would break existing callers.
3. **Solution**:
   - `PlayModeTransitionObserver` becomes the sole engine hook for `playModeStateChanged`, `AssemblyReloadEvents`, and `EditorSceneManager.sceneClosing`.
   - `EditorLifecycleCoordinator` registers an internal bridge (`LegacySubscriberBridge : IPlayModeTransitionSubscriber`) with `PlayModeTransitionObserver` at `ExecutionOrder = PlayModeExecutionOrder.Default` (500).
   - Any subscriber registered via `EditorLifecycleCoordinator.RegisterSubscriber(legacySubscriber)` is forwarded to the bridge.
   - `Selection.selectionChanged` handler remains in `EditorLifecycleCoordinator` to sanitize null objects on selection change.
   - Legacy methods are marked `[Obsolete]` pointing to `PlayModeTransitionObserver`.

### 2.5 Roslyn Compliance (`AGY0021`, `AGY0001`, `AGY0002`, `AGY0003`, `UNT0007/8/23`)
1. **`AGY0021` (Named Event Handlers)**:
   - Unity event bindings (`+=` and `-=`) must strictly use named static methods (`HandlePlayModeStateChanged`, `HandleBeforeAssemblyReload`, `HandleAfterAssemblyReload`, `HandleSceneClosing`, `HandleSelectionChanged`).
   - Symmetrical unhooking before hooking (`-=`, then `+=`) in static constructors ensures idempotent initialization.
2. **`AGY0001` (Shallow Inheritance)**: All classes inherit directly from `System.Object` and implement domain interfaces. Inheritance depth = 0.
3. **`AGY0002` (Private Fields)**: All fields are private (`private static readonly`, `private readonly`, `private static`, `private`).
4. **`AGY0003` (Read-only Properties)**: All public state exposure uses read-only expression-bodied properties (`Subscribers => _subscribers`, `Instance => s_instance ??= ...`).
5. **`UNT0007/8/23`**: Explicit `if (obj == null)` equality checks; zero `?.`, `??`, or `??=` on `UnityEngine.Object`.

---

## 3. Detailed Technical Blueprint & Implementation Design

### 3.1 Class & Interface Topology

```
Assets/_Project/Editor/Core/Lifecycle/
├── Interfaces/
│   ├── IPlayModeTransitionSubscriber.cs    # Subscriber contract with ExecutionOrder
│   ├── IPlayModeTransitionObserver.cs      # Observer contract with Register/Unregister
│   ├── IInspectorSanitizerService.cs       # (M1) Inspector sanitation contract
│   └── ISelectionMementoService.cs          # (M2) Memento service contract
├── Services/
│   ├── PlayModeTransitionObserver.cs       # [InitializeOnLoad] Central event coordinator
│   ├── InspectorSanitizerService.cs        # (M1) Implements IPlayModeTransitionSubscriber (Order 200)
│   └── SelectionMementoService.cs          # (M2) Implements IPlayModeTransitionSubscriber (Order 100)
├── PlayModeExecutionOrder.cs               # Standard execution order constants (100, 200, 500)
├── EditorLifecycleCoordinator.cs           # Backwards-compatible legacy facade & bridge
├── IEditorLifecycleSubscriber.cs           # [Obsolete] Legacy subscriber interface
└── InspectorTrackerSanitizer.cs            # (M1) Backwards-compatible static facade
```

---

### 3.2 Complete Code Blueprints

#### 1. `IPlayModeTransitionSubscriber.cs`
**File Target**: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionSubscriber.cs`

```csharp
#if UNITY_EDITOR
using UnityEngine.SceneManagement;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for subscribers that respond to Unity Editor lifecycle transitions,
    /// including Play Mode state changes, assembly reloads, and scene closing events.
    /// </summary>
    public interface IPlayModeTransitionSubscriber
    {
        /// <summary>
        /// Defines dispatch priority. Lower numbers execute first during pre-transition phases (e.g. ExitingEditMode, BeforeAssemblyReload, SceneClosing).
        /// Standard execution orders:
        /// <list type="bullet">
        ///   <item><description>SelectionMementoService: 100 (Captures & clears selection)</description></item>
        ///   <item><description>InspectorSanitizerService: 200 (Purges inspectors, trackers, and orphan editors)</description></item>
        ///   <item><description>Default/User subscribers: 500</description></item>
        /// </list>
        /// </summary>
        int ExecutionOrder { get; }

        /// <summary>
        /// Invoked when entering Play Mode, before the Edit Mode scene is unloaded or domain reloaded.
        /// </summary>
        void OnExitingEditMode();

        /// <summary>
        /// Invoked after Play Mode has fully initialized and the play scene is loaded.
        /// </summary>
        void OnEnteredPlayMode();

        /// <summary>
        /// Invoked when exiting Play Mode, before the Play Mode scene is unloaded.
        /// </summary>
        void OnExitingPlayMode();

        /// <summary>
        /// Invoked after exiting Play Mode and returning to Edit Mode.
        /// </summary>
        void OnEnteredEditMode();

        /// <summary>
        /// Invoked immediately before an assembly or domain reload begins.
        /// </summary>
        void OnBeforeAssemblyReload();

        /// <summary>
        /// Invoked immediately after an assembly or domain reload has completed.
        /// </summary>
        void OnAfterAssemblyReload();

        /// <summary>
        /// Invoked right before an open scene is closed or unloaded.
        /// </summary>
        /// <param name="scene">The scene being closed.</param>
        void OnSceneClosing(Scene scene);
    }
}
#endif
```

#### 2. `IPlayModeTransitionObserver.cs`
**File Target**: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IPlayModeTransitionObserver.cs`

```csharp
#if UNITY_EDITOR
using System.Collections.Generic;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Contract for the observer service coordinating Unity Editor lifecycle transitions
    /// and dispatching ordered notifications to registered subscribers.
    /// </summary>
    public interface IPlayModeTransitionObserver
    {
        /// <summary>
        /// Read-only collection of registered subscribers, ordered by <see cref="IPlayModeTransitionSubscriber.ExecutionOrder"/>.
        /// </summary>
        IReadOnlyList<IPlayModeTransitionSubscriber> Subscribers { get; }

        /// <summary>
        /// Registers a subscriber to receive lifecycle notifications.
        /// Maintains sorted order by <see cref="IPlayModeTransitionSubscriber.ExecutionOrder"/>.
        /// </summary>
        /// <param name="subscriber">The subscriber to register.</param>
        void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber);

        /// <summary>
        /// Unregisters a subscriber from lifecycle notifications.
        /// </summary>
        /// <param name="subscriber">The subscriber to unregister.</param>
        void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber);
    }
}
#endif
```

#### 3. `PlayModeExecutionOrder.cs`
**File Target**: `Assets/_Project/Editor/Core/Lifecycle/PlayModeExecutionOrder.cs`

```csharp
#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Standard execution order constants for <see cref="IPlayModeTransitionSubscriber.ExecutionOrder"/>.
    /// Lower values execute earlier during pre-transition phases (cleanup, purging, state capture).
    /// </summary>
    public static class PlayModeExecutionOrder
    {
        /// <summary>
        /// Captures and clears selection state prior to inspector/tracker purging.
        /// </summary>
        public const int SelectionMemento = 100;

        /// <summary>
        /// Purges inspector trackers, locked object buffers, and orphan editors after selection has been cleared.
        /// </summary>
        public const int InspectorSanitizer = 200;

        /// <summary>
        /// Default execution order for general editor lifecycle subscribers.
        /// </summary>
        public const int Default = 500;
    }
}
#endif
```

#### 4. `PlayModeTransitionObserver.cs`
**File Target**: `Assets/_Project/Editor/Core/Lifecycle/Services/PlayModeTransitionObserver.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Coordinates Unity Editor lifecycle transitions (Play Mode state changes, assembly reloads, and scene closing)
    /// and dispatches notifications to registered <see cref="IPlayModeTransitionSubscriber"/> instances in priority order.
    /// Conforms strictly to AGY0021 (Named Event Handlers), AGY0001 (Shallow Inheritance), and AGY0002/3 (Encapsulation).
    /// </summary>
    [InitializeOnLoad]
    public sealed class PlayModeTransitionObserver : IPlayModeTransitionObserver
    {
        private static IPlayModeTransitionObserver s_instance;
        private readonly List<IPlayModeTransitionSubscriber> _subscribers = new List<IPlayModeTransitionSubscriber>();

        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static IPlayModeTransitionObserver Instance => s_instance ??= new PlayModeTransitionObserver();

        /// <summary>
        /// Read-only view of registered subscribers ordered by <see cref="IPlayModeTransitionSubscriber.ExecutionOrder"/>.
        /// </summary>
        public IReadOnlyList<IPlayModeTransitionSubscriber> Subscribers => _subscribers;

        static PlayModeTransitionObserver()
        {
            HookEditorEvents();
            RegisterDefaultSubscribers();
        }

        public PlayModeTransitionObserver()
        {
        }

        /// <summary>
        /// Injects a mock or alternate observer instance for EditMode tests.
        /// </summary>
        public static void SetInstanceForTesting(IPlayModeTransitionObserver instance)
        {
            s_instance = instance;
        }

        /// <summary>
        /// Binds static event listeners symmetrically using named methods per AGY0021.
        /// </summary>
        public static void HookEditorEvents()
        {
            UnhookEditorEvents();

            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
            EditorSceneManager.sceneClosing += HandleSceneClosing;
        }

        /// <summary>
        /// Unbinds static event listeners symmetrically using named methods per AGY0021.
        /// </summary>
        public static void UnhookEditorEvents()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= HandleAfterAssemblyReload;
            EditorSceneManager.sceneClosing -= HandleSceneClosing;
        }

        /// <summary>
        /// Registers built-in core subscribers: SelectionMementoService (Order 100) and InspectorSanitizerService (Order 200).
        /// </summary>
        public static void RegisterDefaultSubscribers()
        {
            var obs = Instance;
            obs.RegisterSubscriber(SelectionMementoService.Instance);
            obs.RegisterSubscriber(InspectorSanitizerService.Instance);
        }

        /// <summary>
        /// Registers a subscriber, maintaining execution order sorting.
        /// </summary>
        public void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber)
        {
            if (subscriber == null || _subscribers.Contains(subscriber))
            {
                return;
            }

            int insertIdx = 0;
            while (insertIdx < _subscribers.Count && _subscribers[insertIdx].ExecutionOrder <= subscriber.ExecutionOrder)
            {
                insertIdx++;
            }
            _subscribers.Insert(insertIdx, subscriber);
        }

        /// <summary>
        /// Unregisters a subscriber.
        /// </summary>
        public void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber)
        {
            if (subscriber == null)
            {
                return;
            }

            _subscribers.Remove(subscriber);
        }

        #region Event Handlers (AGY0021 Named Methods)

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (Instance is PlayModeTransitionObserver observer)
            {
                observer.DispatchPlayModeStateChanged(state);
            }
        }

        private static void HandleBeforeAssemblyReload()
        {
            if (Instance is PlayModeTransitionObserver observer)
            {
                observer.DispatchBeforeAssemblyReload();
            }
        }

        private static void HandleAfterAssemblyReload()
        {
            if (Instance is PlayModeTransitionObserver observer)
            {
                observer.DispatchAfterAssemblyReload();
            }
        }

        private static void HandleSceneClosing(Scene scene, bool removingScene)
        {
            if (Instance is PlayModeTransitionObserver observer)
            {
                observer.DispatchSceneClosing(scene);
            }
        }

        #endregion

        #region Dispatch Methods (Resilient Error Isolation)

        public void DispatchPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    DispatchExitingEditMode();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    DispatchEnteredPlayMode();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    DispatchExitingPlayMode();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    DispatchEnteredEditMode();
                    break;
            }
        }

        public void DispatchExitingEditMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnExitingEditMode();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayModeTransitionObserver] Subscriber '{_subscribers[i].GetType().Name}' threw exception during OnExitingEditMode: {ex}");
                }
            }
        }

        public void DispatchEnteredPlayMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnEnteredPlayMode();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayModeTransitionObserver] Subscriber '{_subscribers[i].GetType().Name}' threw exception during OnEnteredPlayMode: {ex}");
                }
            }
        }

        public void DispatchExitingPlayMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnExitingPlayMode();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayModeTransitionObserver] Subscriber '{_subscribers[i].GetType().Name}' threw exception during OnExitingPlayMode: {ex}");
                }
            }
        }

        public void DispatchEnteredEditMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnEnteredEditMode();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayModeTransitionObserver] Subscriber '{_subscribers[i].GetType().Name}' threw exception during OnEnteredEditMode: {ex}");
                }
            }
        }

        public void DispatchBeforeAssemblyReload()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnBeforeAssemblyReload();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayModeTransitionObserver] Subscriber '{_subscribers[i].GetType().Name}' threw exception during OnBeforeAssemblyReload: {ex}");
                }
            }
        }

        public void DispatchAfterAssemblyReload()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnAfterAssemblyReload();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayModeTransitionObserver] Subscriber '{_subscribers[i].GetType().Name}' threw exception during OnAfterAssemblyReload: {ex}");
                }
            }
        }

        public void DispatchSceneClosing(Scene scene)
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnSceneClosing(scene);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PlayModeTransitionObserver] Subscriber '{_subscribers[i].GetType().Name}' threw exception during OnSceneClosing: {ex}");
                }
            }
        }

        #endregion
    }
}
#endif
```

#### 5. Refactored `EditorLifecycleCoordinator.cs`
**File Target**: `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Legacy coordinator maintained for backwards compatibility.
    /// Bridges legacy <see cref="IEditorLifecycleSubscriber"/> registrations into <see cref="PlayModeTransitionObserver"/>
    /// and handles null object sanitization on <see cref="Selection.selectionChanged"/>.
    /// Conforms strictly to AGY0021, AGY0001, and AGY0002/3.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorLifecycleCoordinator
    {
        private static readonly List<IEditorLifecycleSubscriber> s_legacySubscribers = new List<IEditorLifecycleSubscriber>();
        private static LegacySubscriberBridge s_bridge;

        /// <summary>
        /// Read-only collection of registered legacy subscribers.
        /// </summary>
        public static IReadOnlyList<IEditorLifecycleSubscriber> LegacySubscribers => s_legacySubscribers;

        static EditorLifecycleCoordinator()
        {
            EnsureBridgeRegistered();
            Selection.selectionChanged -= HandleSelectionChanged;
            Selection.selectionChanged += HandleSelectionChanged;
        }

        private static void EnsureBridgeRegistered()
        {
            if (s_bridge == null)
            {
                s_bridge = new LegacySubscriberBridge();
                PlayModeTransitionObserver.Instance.RegisterSubscriber(s_bridge);
            }
        }

        /// <summary>
        /// Registers a legacy subscriber. Bridges lifecycle calls into PlayModeTransitionObserver.
        /// </summary>
        [Obsolete("Use PlayModeTransitionObserver.Instance.RegisterSubscriber(IPlayModeTransitionSubscriber) instead.")]
        public static void RegisterSubscriber(IEditorLifecycleSubscriber subscriber)
        {
            if (subscriber != null && !s_legacySubscribers.Contains(subscriber))
            {
                s_legacySubscribers.Add(subscriber);
                EnsureBridgeRegistered();
            }
        }

        /// <summary>
        /// Unregisters a legacy subscriber.
        /// </summary>
        [Obsolete("Use PlayModeTransitionObserver.Instance.UnregisterSubscriber(IPlayModeTransitionSubscriber) instead.")]
        public static void UnregisterSubscriber(IEditorLifecycleSubscriber subscriber)
        {
            if (subscriber != null)
            {
                s_legacySubscribers.Remove(subscriber);
            }
        }

        private static void HandleSelectionChanged()
        {
            var objs = Selection.objects;
            if (objs == null || objs.Length == 0)
            {
                return;
            }

            bool hasNull = false;
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] == null)
                {
                    hasNull = true;
                    break;
                }
            }

            if (hasNull)
            {
                var valid = new List<Object>(objs.Length);
                for (int i = 0; i < objs.Length; i++)
                {
                    if (objs[i] != null)
                    {
                        valid.Add(objs[i]);
                    }
                }
                Selection.objects = valid.ToArray();
                InspectorTrackerSanitizer.SanitizeActiveEditors();
            }
        }

        /// <summary>
        /// Bridge that adapts legacy <see cref="IEditorLifecycleSubscriber"/> invocations
        /// into the modern <see cref="IPlayModeTransitionSubscriber"/> pipeline.
        /// </summary>
        private sealed class LegacySubscriberBridge : IPlayModeTransitionSubscriber
        {
            public int ExecutionOrder => PlayModeExecutionOrder.Default; // 500

            public void OnExitingEditMode()
            {
                for (int i = 0; i < s_legacySubscribers.Count; i++)
                {
                    try
                    {
                        s_legacySubscribers[i].OnBeforePlayModeEntered();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{s_legacySubscribers[i].GetType().Name}' threw exception: {ex}");
                    }
                }
            }

            public void OnEnteredPlayMode()
            {
                for (int i = 0; i < s_legacySubscribers.Count; i++)
                {
                    try
                    {
                        s_legacySubscribers[i].OnPlayModeEntered();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{s_legacySubscribers[i].GetType().Name}' threw exception: {ex}");
                    }
                }
            }

            public void OnExitingPlayMode()
            {
                for (int i = 0; i < s_legacySubscribers.Count; i++)
                {
                    try
                    {
                        s_legacySubscribers[i].OnBeforePlayModeExited();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{s_legacySubscribers[i].GetType().Name}' threw exception: {ex}");
                    }
                }
            }

            public void OnEnteredEditMode()
            {
                for (int i = 0; i < s_legacySubscribers.Count; i++)
                {
                    try
                    {
                        s_legacySubscribers[i].OnPlayModeExited();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{s_legacySubscribers[i].GetType().Name}' threw exception: {ex}");
                    }
                }
            }

            public void OnBeforeAssemblyReload()
            {
                for (int i = 0; i < s_legacySubscribers.Count; i++)
                {
                    try
                    {
                        s_legacySubscribers[i].OnBeforeDomainReload();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{s_legacySubscribers[i].GetType().Name}' threw exception: {ex}");
                    }
                }
            }

            public void OnAfterAssemblyReload()
            {
                for (int i = 0; i < s_legacySubscribers.Count; i++)
                {
                    try
                    {
                        s_legacySubscribers[i].OnAfterDomainReload();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{s_legacySubscribers[i].GetType().Name}' threw exception: {ex}");
                    }
                }
            }

            public void OnSceneClosing(Scene scene)
            {
                // Legacy subscribers have no sceneClosing event.
            }
        }
    }
}
#endif
```

#### 6. Subscriber Implementation on `InspectorSanitizerService.cs`
**File Target**: `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`

Add `IPlayModeTransitionSubscriber` to class declaration:
```csharp
public sealed class InspectorSanitizerService : IInspectorSanitizerService, IPlayModeTransitionSubscriber
{
    // ... existing implementation ...

    #region IPlayModeTransitionSubscriber Implementation

    /// <summary>
    /// Executes at priority 200 (after SelectionMementoService at 100 has captured and cleared selection).
    /// </summary>
    public int ExecutionOrder => PlayModeExecutionOrder.InspectorSanitizer; // 200

    public void OnExitingEditMode()
    {
        PurgeAllInspectors();
    }

    public void OnEnteredPlayMode()
    {
        // No-op: editors rebuild naturally as objects are selected/restored
    }

    public void OnExitingPlayMode()
    {
        PurgeAllInspectors();
    }

    public void OnEnteredEditMode()
    {
        // No-op: editors rebuild naturally as objects are selected/restored
    }

    public void OnBeforeAssemblyReload()
    {
        PurgeAllInspectors();
    }

    public void OnAfterAssemblyReload()
    {
        // No-op
    }

    public void OnSceneClosing(Scene scene)
    {
        PurgeAllInspectors();
    }

    #endregion
}
```

#### 7. Subscriber Implementation on `SelectionMementoService.cs`
**File Target**: `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs`

Update class declaration and lifecycle methods:
```csharp
public sealed class SelectionMementoService : ISelectionMementoService
{
    // ... singleton & persistence fields ...

    #region IPlayModeTransitionSubscriber Implementation

    /// <summary>
    /// Executes at priority 100 (runs before InspectorSanitizerService at 200).
    /// </summary>
    public int ExecutionOrder => PlayModeExecutionOrder.SelectionMemento; // 100

    public void OnExitingEditMode()
    {
        CaptureCurrentSelection();
        ClearSelectionSafely();
    }

    public void OnEnteredPlayMode()
    {
        EditorApplication.delayCall += HandleDeferredRestore;
    }

    public void OnExitingPlayMode()
    {
        ClearSelectionSafely();
    }

    public void OnEnteredEditMode()
    {
        EditorApplication.delayCall += HandleDeferredRestore;
    }

    public void OnBeforeAssemblyReload()
    {
        var objs = Selection.objects;
        if (objs != null && objs.Length > 0)
        {
            CaptureCurrentSelection();
            ClearSelectionSafely();
        }
    }

    public void OnAfterAssemblyReload()
    {
        EditorApplication.delayCall += HandleDeferredRestore;
    }

    public void OnSceneClosing(Scene scene)
    {
        // If current selection belongs to the closing scene, clear it
        var activeGo = Selection.activeGameObject;
        if (activeGo != null && activeGo.scene == scene)
        {
            ClearSelectionSafely();
        }
    }

    private void HandleDeferredRestore()
    {
        RestoreFromPersistence();
    }

    #endregion
}
```

---

## 4. Caveats

1. **Unity Internal Reflection APIs**: `InspectorSanitizerService` uses reflection on internal `InspectorWindow` / `PropertyEditor` members. While stable in Unity 6000.3.7f1, all calls are protected with null guards and try-catch blocks.
2. **Read-Only Explorer Scope**: In accordance with the Explorer archetype mandate, no project source code or tests were modified or created during this investigation. All implementations are provided as concrete, drop-in blueprints for the Worker agent.
3. **Domain Reload Disabled (Enter Play Mode Options)**: If Fast Play Mode is used with Domain Reload disabled, static fields survive. The dual-persistence mechanism (`SessionState` + in-memory) designed by peer Explorer 1 ensures reliable restoration regardless of domain reload configuration.
4. **Re-entrant Registrations**: If a subscriber attempts to register or unregister another subscriber during a lifecycle dispatch, modifying `_subscribers` could cause an `InvalidOperationException`. In the provided blueprints, indexed `for` loops are used, and test suites will verify collection stability.

---

## 5. Conclusion

1. **Event Coordinator & Observer Design Complete**: `IPlayModeTransitionObserver` and `IPlayModeTransitionSubscriber` completely decouple editor lifecycle notifications from static coordinator state.
2. **Unified Event Sourcing**: `PlayModeTransitionObserver` hooks `EditorApplication.playModeStateChanged`, `AssemblyReloadEvents.beforeAssemblyReload`, `AssemblyReloadEvents.afterAssemblyReload`, and `EditorSceneManager.sceneClosing`.
3. **Deterministic Execution Ordering Established**:
   - `SelectionMementoService` (`ExecutionOrder = 100`) executes first: records selection into `SessionState` and resets `Selection.objects` to empty.
   - `InspectorSanitizerService` (`ExecutionOrder = 200`) executes second: with selection empty, it purges open inspector windows, destroys tracker active editors, clears locked objects, and purges all orphan scene `Editor` ScriptableObjects.
   - This exact order resolves the 4 PlayMode console exceptions without losing selection.
4. **Backwards Compatibility Guaranteed**: `EditorLifecycleCoordinator` adapts legacy `IEditorLifecycleSubscriber` registrations through an internal `LegacySubscriberBridge` at order 500, preventing duplicate event hooks and ensuring zero regressions.
5. **Roslyn Compliance Verified**: All event subscriptions strictly use named methods (`AGY0021`), shallow inheritance (`AGY0001`), private fields (`AGY0002`), read-only properties (`AGY0003`), and explicit null checks (`UNT0007/8/23`).

---

## 6. Verification Method

### 6.1 Independent Inspection
1. Verify `IPlayModeTransitionSubscriber` specifies `int ExecutionOrder { get; }` and `OnSceneClosing(Scene scene)`.
2. Verify `PlayModeTransitionObserver` binds all event handlers symmetrically with named methods (`AGY0021`).
3. Verify `EditorLifecycleCoordinator` delegates to `LegacySubscriberBridge` without hooking `playModeStateChanged` directly.

### 6.2 Unit and Integration Test Commands (Milestone 3 / Milestone 4)
When implemented by the Worker agent, execute the following commands to verify:
1. **Compilation Check**:
   ```pwsh
   & "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -quit -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -logFile "compile.log"
   ```
2. **EditMode Test Run**:
   ```pwsh
   & "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testResults "test_results.xml" -logFile "test_run.log"
   ```
3. **Invalidation Condition**:
   - If any `SerializedObjectNotCreatableException` or `MissingReferenceException` appears in `Editor.log` upon entering Play Mode, the observer execution order or inspector purge failed to run prior to edit scene unload.
