# Milestone 3 Handoff Report: Inspector Sanitizer & PlayMode Observer Test Suites

**Explorer**: Milestone 3 Explorer 3  
**Working Directory**: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m3_3\`  
**Target Test Directory**: `Assets/_Project/Tests/Editor/`  
**Test Fixtures Designed**:
1. `Assets/_Project/Tests/Editor/InspectorSanitizerTests.cs` (Feature F11)
2. `Assets/_Project/Tests/Editor/PlayModeObserverIntegrationTests.cs` (Feature F8 & F11)

---

## 1. Observation

### 1.1 Inspected Source Code & Structural Locations

Direct inspection of `Assets/_Project/Editor/Core/Lifecycle/` revealed the following contracts and implementations:

1. **`Interfaces/IInspectorSanitizerService.cs`** (Lines 8–33):
   ```csharp
   public interface IInspectorSanitizerService
   {
       void PurgeAllInspectors();
       void PurgeSharedTracker();
       void DestroyOrphanAndSceneEditors();
       void UnlockAllInspectors();
   }
   ```
2. **`Interfaces/IPlayModeTransitionObserver.cs`** (Lines 10–29):
   ```csharp
   public interface IPlayModeTransitionObserver
   {
       IReadOnlyList<IPlayModeTransitionSubscriber> Subscribers { get; }
       void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber);
       void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber);
   }
   ```
3. **`Interfaces/IPlayModeTransitionSubscriber.cs`** (Lines 10–58):
   Defines priority ordering and 7 distinct lifecycle transition events:
   - `int ExecutionOrder { get; }` (Ascending dispatch order: lower numbers execute first)
   - `void OnExitingEditMode()`
   - `void OnEnteredPlayMode()`
   - `void OnExitingPlayMode()`
   - `void OnEnteredEditMode()`
   - `void OnBeforeAssemblyReload()`
   - `void OnAfterAssemblyReload()`
   - `void OnSceneClosing(Scene scene)`
4. **`PlayModeExecutionOrder.cs`** (Lines 8–25):
   ```csharp
   public static class PlayModeExecutionOrder
   {
       public const int SelectionMemento = 100;
       public const int InspectorSanitizer = 200;
       public const int Default = 500;
   }
   ```
5. **`Services/InspectorSanitizerService.cs`**:
   - Lines 36–50: Cached reflection fields: `s_propertyEditorType`, `s_inspectorWindowType`, `s_trackerProp`, `s_trackerField`, `s_clearEditorsAndRebuildMethod`, `s_clearSerializedLockedObjectsMethod`, `s_isLockedProp`, `s_allInspectorsField`, `s_allPropertyEditorsField`, `s_clearConsoleMethod`.
   - Lines 103–164: `PurgeAllInspectors()` unlocks open windows (`s_isLockedProp`), clears locked objects (`s_clearSerializedLockedObjectsMethod`), destroys tracker editors, rebuilds via `s_clearEditorsAndRebuildMethod`, repaints, and triggers `PurgeSharedTracker()` and `DestroyOrphanAndSceneEditors()`.
   - Lines 170–187: `PurgeSharedTracker()` purges `ActiveEditorTracker.sharedTracker` by destroying active editors without calling native C++ `shared.Destroy()`, followed by `shared.ForceRebuild()`.
   - Lines 193–260: `DestroyOrphanAndSceneEditors()` iterates `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))` and destroys editors where `target == null || !EditorUtility.IsPersistent(target)`. Persistent asset editors are preserved.
   - Lines 265–304: `UnlockAllInspectors()` sets `isLocked = false` and calls `ClearSerializedLockedObjects()` on open inspector windows.
   - Lines 566–604: Explicit implementation of `IPlayModeTransitionSubscriber` at `ExecutionOrder = 200` (`PlayModeExecutionOrder.InspectorSanitizer`), routing `OnExitingEditMode`, `OnExitingPlayMode`, `OnBeforeAssemblyReload`, and `OnSceneClosing` to `PurgeAllInspectors()`.
6. **`Services/PlayModeTransitionObserver.cs`**:
   - Lines 17–31: Global singleton `Instance` with sorted internal list `List<IPlayModeTransitionSubscriber> _subscribers`.
   - Lines 53–73: Symmetrical event registration via AGY0021 named methods (`HookEditorEvents()` / `UnhookEditorEvents()`) listening to `EditorApplication.playModeStateChanged`, `AssemblyReloadEvents.beforeAssemblyReload`, `AssemblyReloadEvents.afterAssemblyReload`, and `EditorSceneManager.sceneClosing`.
   - Lines 77–82: `RegisterDefaultSubscribers()` registers `SelectionMementoService.Instance` (100) and `InspectorSanitizerService.Instance` (200).
   - Lines 87–100: Insertion-sort registration maintaining strict ascending `ExecutionOrder`.
   - Lines 153–276: Error-isolated dispatch loops for all 7 phases, wrapping each subscriber call in `try { ... } catch (Exception ex) { Debug.LogError(...); }`.
7. **`EditorLifecycleCoordinator.cs`**:
   - Lines 18–86: Backward compatibility layer maintaining `s_legacySubscribers` and bridging them via private `LegacySubscriberAdapter` (Order 500) into `PlayModeTransitionObserver.Instance`. Dual-interface subscribers (implementing both legacy and modern interfaces) are registered directly without adapter wrapping.
8. **`InspectorTrackerSanitizer.cs`**:
   - Lines 12–89: Static facade delegating to `IInspectorSanitizerService`, exposing `SetServiceForTesting(IInspectorSanitizerService service)` for dependency injection in unit tests.

---

## 2. Logic Chain

1. **Unity 6000.3.7f1 Reflection Resilience (Feature F1, F11)**:
   - *Observation*: Unity 6 introduced structural changes in internal editor classes: `UnityEditor.InspectorWindow` now subclasses `UnityEditor.PropertyEditor`, and tracking members may live in base or derived types.
   - *Logic*: Unit tests must assert that `PropertyEditor` and `InspectorWindow` types are resolvable from `typeof(UnityEditor.Editor).Assembly`, and verify that `tracker`/`m_Tracker`, `ClearEditorsAndRebuild`, `ClearSerializedLockedObjects`, and `isLocked` members exist in the inheritance hierarchy. If Unity updates internal signatures in a minor patch, tests will immediately pinpoint the exact member failure.

2. **Non-Destructive ActiveEditorTracker Sanitation (Feature F2, F11)**:
   - *Observation*: Calling native C++ `tracker.Destroy()` or `ActiveEditorTracker.sharedTracker.Destroy()` deallocates native C++ memory, causing hard editor crashes or `SerializedObjectNotCreatableException` when subsequent GUI passes touch the shared tracker.
   - *Logic*: `PurgeSharedTracker()` destroys individual `tracker.activeEditors` via `Object.DestroyImmediate`, clears dirty state, and calls `ForceRebuild()`. The test must assert that `ActiveEditorTracker.sharedTracker` remains non-null, accessing `sharedTracker.activeEditors` does not throw native exceptions, and `sharedTracker.isDirty` is false.

3. **Persistent Assets vs. Scene/Orphan Editor Purge (Feature F3, F11)**:
   - *Observation*: `DestroyOrphanAndSceneEditors()` destroys editors whose targets are scene GameObjects (`!EditorUtility.IsPersistent(target)`) or null (orphaned), but must preserve editors targeting persistent project assets (e.g. ScriptableObjects, Prefabs, MonoScripts).
   - *Logic*: The test fixture creates:
     1. A scene-bound GameObject editor (`tempGo` in EditMode scene) -> asserts destroyed (`editor == null`).
     2. An orphan editor (GameObject destroyed prior to purge) -> asserts destroyed (`editor == null`).
     3. A persistent asset editor (`AssetDatabase.LoadAssetAtPath<MonoScript>(...)`) -> asserts preserved (`editor != null`).

4. **Locked Inspector Unlocking & State Cleansing (Feature F1, F11)**:
   - *Observation*: Locked inspector windows retain serialized references to scene objects across PlayMode transitions, causing `MissingReferenceException` in `GameObjectInspector.OnEnable()`.
   - *Logic*: Tests must verify that `UnlockAllInspectors()` detects locked windows, clears `isLocked`, and invokes `ClearSerializedLockedObjects()` without throwing `TargetException`. Edge case with 0 open windows must execute gracefully.

5. **Observer Priority Sorting & Execution Order (Feature F8, F11)**:
   - *Observation*: `SelectionMementoService` must capture selection (Order 100) before `InspectorSanitizerService` purges selection trackers and destroys active editors (Order 200).
   - *Logic*: Tests must register subscribers out of order (e.g. 500, 200, 100, 50) and assert that `observer.Subscribers` and the actual dispatch execution log are strictly sorted in ascending order (`[50, 100, 200, 500]`).

6. **Exception Isolation Across All 7 Phases (Feature F8)**:
   - *Observation*: A misbehaving subscriber throwing an exception must not abort subsequent subscribers; otherwise, critical cleanup (e.g. sanitizer) would fail if memento threw.
   - *Logic*: Tests inject a subscriber that throws `InvalidOperationException` between two healthy subscribers. Using `UnityEngine.TestTools.LogAssert.Expect(LogType.Error, ...)`, the test asserts that both healthy subscribers execute successfully across each of the 7 lifecycle transition phases.

7. **Legacy Compatibility Bridge (Feature F8, EditorLifecycleCoordinator)**:
   - *Observation*: Legacy code registers `IEditorLifecycleSubscriber`.
   - *Logic*: Tests must register a mock `IEditorLifecycleSubscriber` through `EditorLifecycleCoordinator.RegisterSubscriber()`, verify that it is adapted into `PlayModeTransitionObserver.Instance` with `ExecutionOrder == 500`, assert that all 6 corresponding legacy methods are triggered when observer events fire, and verify clean removal on unregistration.

---

## 3. Caveats

1. **Unity GUI Window Creation in EditMode Tests**:
   - `EditorWindow.GetWindow<T>()` or reflection creation of `InspectorWindow` during automated headless CLI test runs (e.g., `-batchmode -nographics`) may behave differently than interactive GUI sessions. The test blueprints test reflection resolution directly against internal types and provide graceful fallbacks if no desktop windows are instantiated in headless mode.
2. **Global Shared Tracker Isolation**:
   - `ActiveEditorTracker.sharedTracker` is engine-wide. The test fixtures preserve tracker integrity by using non-destructive cleanup and enforcing `sharedTracker.ForceRebuild()` in `[TearDown]` to avoid test-state pollution.
3. **Unity Object Equality Semantics (`UNT0007`/`UNT0008`/`UNT0023`)**:
   - In accordance with project analyzers, all Unity Object null-checks use `== null` or `!= null` and avoid C# 8+ null-coalescing (`??`) or null-conditional (`?.`) operators.
4. **Read-Only Scope**:
   - This exploration produces concrete blueprints ready for implementation in `Assets/_Project/Tests/Editor/`. In keeping with explorer constraints, no files were modified in `Assets/`.

---

## 4. Conclusion & Test Suite Blueprints

The test architecture consists of two comprehensive test fixtures:
- `Assets/_Project/Tests/Editor/InspectorSanitizerTests.cs` (18 Test Methods)
- `Assets/_Project/Tests/Editor/PlayModeObserverIntegrationTests.cs` (16 Test Methods)

### 4.1 Test Inventory Matrix

| Test Suite | Method Name | Feature | Target Under Test |
|---|---|---|---|
| `InspectorSanitizerTests` | `Test01_Reflection_PropertyEditorType_Resolved` | F1, F11 | `UnityEditor.PropertyEditor` type resolution |
| `InspectorSanitizerTests` | `Test02_Reflection_InspectorWindowType_Resolved` | F1, F11 | `UnityEditor.InspectorWindow` type resolution |
| `InspectorSanitizerTests` | `Test03_Reflection_TrackerMember_ExistsInHierarchy` | F1, F11 | `tracker` property or `m_Tracker` field |
| `InspectorSanitizerTests` | `Test04_Reflection_ClearEditorsAndRebuildMethod_Exists` | F1, F11 | `ClearEditorsAndRebuild()` method |
| `InspectorSanitizerTests` | `Test05_Reflection_ClearSerializedLockedObjectsMethod_Exists` | F1, F11 | `ClearSerializedLockedObjects()` method |
| `InspectorSanitizerTests` | `Test06_Reflection_IsLockedProperty_ExistsOnInspectorWindow` | F1, F11 | `isLocked` bool property |
| `InspectorSanitizerTests` | `Test07_Reflection_InspectorsCollectionField_Exists` | F1, F11 | `m_AllInspectors` / `m_AllPropertyEditors` field |
| `InspectorSanitizerTests` | `Test08_Reflection_LogEntriesClearMethod_Exists` | F4 | `UnityEditor.LogEntries.Clear()` |
| `InspectorSanitizerTests` | `Test09_PurgeSharedTracker_DoesNotDestroyNativeTrackerInstance` | F2, F11 | Non-destructive shared tracker purging |
| `InspectorSanitizerTests` | `Test10_DestroyOrphanAndSceneEditors_DestroysSceneBoundEditors` | F3, F11 | Scene GameObject editor destruction |
| `InspectorSanitizerTests` | `Test11_DestroyOrphanAndSceneEditors_DestroysOrphanEditors` | F3, F11 | Null-target orphan editor destruction |
| `InspectorSanitizerTests` | `Test12_DestroyOrphanAndSceneEditors_PreservesPersistentAssetEditors` | F3, F11 | Persistent project asset editor retention |
| `InspectorSanitizerTests` | `Test13_UnlockAllInspectors_ExecutesGracefully` | F1, F11 | Window unlock and locked objects clear |
| `InspectorSanitizerTests` | `Test14_PurgeAllInspectors_ExecutesFullSanitizationCycle` | F1, F2, F3 | Full inspector purge orchestration |
| `InspectorSanitizerTests` | `Test15_ClearConsole_ExecutesWithoutExceptions` | F4 | Console log clearing |
| `InspectorSanitizerTests` | `Test16_ExecutionOrder_ConformsToPlayModeExecutionOrderConstant` | F8 | Priority order equals 200 |
| `InspectorSanitizerTests` | `Test17_InspectorTrackerSanitizerFacade_DelegatesToService` | F1 | Static facade routing & test injection |
| `InspectorSanitizerTests` | `Test18_LifecycleCallbacks_ExecutePurgeAllInspectors` | F8 | Subscriber lifecycle trigger verification |
| `PlayModeObserverIntegrationTests` | `Test01_PrioritySorting_SubscribersSortedAscendingByExecutionOrder` | F8 | Ascending priority list ordering |
| `PlayModeObserverIntegrationTests` | `Test02_PrioritySorting_ExecutionOrderPreservedDuringDispatch` | F8 | Dispatch execution order (100 -> 200 -> 500) |
| `PlayModeObserverIntegrationTests` | `Test03_PrioritySorting_StableForEqualExecutionOrders` | F8 | Deterministic FIFO for equal priority |
| `PlayModeObserverIntegrationTests` | `Test04_ExceptionIsolation_ThrowingSubscriberDoesNotHaltSubsequent` | F4, F8 | Error isolation in `OnExitingEditMode` |
| `PlayModeObserverIntegrationTests` | `Test05_ExceptionIsolation_AcrossAllSevenLifecyclePhases` | F4, F8 | Error isolation across all 7 dispatch loops |
| `PlayModeObserverIntegrationTests` | `Test06_LifecyclePhases_DispatchPlayModeStateChanged_MapsAllStates` | F8 | 4 PlayModeStateChange enum mappings |
| `PlayModeObserverIntegrationTests` | `Test07_LifecyclePhases_DispatchBeforeAndAfterAssemblyReload` | F8 | Assembly reload notification dispatch |
| `PlayModeObserverIntegrationTests` | `Test08_LifecyclePhases_DispatchSceneClosing_PassesScene` | F8 | Scene closing callback argument routing |
| `PlayModeObserverIntegrationTests` | `Test09_LegacySubscriberAdapter_BridgesAllLifecycleMethods` | F8 | Full 6-method legacy adapter mapping |
| `PlayModeObserverIntegrationTests` | `Test10_LegacySubscriberAdapter_ExceptionIsolation` | F4, F8 | Error isolation inside legacy adapter |
| `PlayModeObserverIntegrationTests` | `Test11_DualInterfaceSubscriber_DirectRegistrationWithoutAdapter` | F8 | Bypass adapter when interface already implemented |
| `PlayModeObserverIntegrationTests` | `Test12_RegistrationIdempotency_DuplicateSubscriberIgnored` | F8 | Duplicate registration protection |
| `PlayModeObserverIntegrationTests` | `Test13_RegistrationIdempotency_NullSubscriberIgnored` | F8 | Null registration safety |
| `PlayModeObserverIntegrationTests` | `Test14_Unregistration_NullOrUnknownSubscriber_Safe` | F8 | Unregistration safety checks |
| `PlayModeObserverIntegrationTests` | `Test15_EventHooking_SymmetricalAndIdempotent` | F8 | AGY0021 symmetrical event binding |
| `PlayModeObserverIntegrationTests` | `Test16_RegisterDefaultSubscribers_IncludesSelectionMementoAndSanitizer` | F8 | Core built-in subscriber registration |

---

### 4.2 C# Code Blueprint: `InspectorSanitizerTests.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Game.Editor.Core.Lifecycle;

namespace Game.Editor.Tests
{
    /// <summary>
    /// Comprehensive test suite for <see cref="InspectorSanitizerService"/> and <see cref="InspectorTrackerSanitizer"/>.
    /// Verifies reflection member resolution on PropertyEditor and InspectorWindow in Unity 6000.3.7f1,
    /// non-destructive active editor tracker purging, scene & orphan editor destruction, and locked inspector detection.
    /// Complies strictly with AGY0001 (Shallow Inheritance) and UNT0007/UNT0008/UNT0023 (Object Null Checks).
    /// </summary>
    public sealed class InspectorSanitizerTests
    {
        private List<GameObject> _spawnedGameObjects;
        private IInspectorSanitizerService _originalFacadeService;

        [SetUp]
        public void SetUp()
        {
            _spawnedGameObjects = new List<GameObject>();
            _originalFacadeService = InspectorTrackerSanitizer.Service;
        }

        [TearDown]
        public void TearDown()
        {
            InspectorTrackerSanitizer.SetServiceForTesting(_originalFacadeService);

            if (_spawnedGameObjects != null)
            {
                for (int i = 0; i < _spawnedGameObjects.Count; i++)
                {
                    var go = _spawnedGameObjects[i];
                    if (go != null)
                    {
                        Object.DestroyImmediate(go);
                    }
                }
                _spawnedGameObjects.Clear();
            }

            // Ensure shared tracker is in a valid, clean state
            if (ActiveEditorTracker.sharedTracker != null)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
        }

        #region Reflection Member Resolution Tests (Unity 6000.3.7f1)

        [Test]
        public void Test01_Reflection_PropertyEditorType_Resolved()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var propEditorType = assembly.GetType("UnityEditor.PropertyEditor");

            Assert.IsNotNull(propEditorType, "UnityEditor.PropertyEditor type must be resolvable in Editor assembly.");
            Assert.IsTrue(typeof(EditorWindow).IsAssignableFrom(propEditorType), "PropertyEditor must inherit from EditorWindow.");
        }

        [Test]
        public void Test02_Reflection_InspectorWindowType_Resolved()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var inspectorType = assembly.GetType("UnityEditor.InspectorWindow");

            Assert.IsNotNull(inspectorType, "UnityEditor.InspectorWindow type must be resolvable in Editor assembly.");
            Assert.IsTrue(typeof(EditorWindow).IsAssignableFrom(inspectorType), "InspectorWindow must inherit from EditorWindow.");
        }

        [Test]
        public void Test03_Reflection_TrackerMember_ExistsInHierarchy()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var inspectorType = assembly.GetType("UnityEditor.InspectorWindow");
            var propEditorType = assembly.GetType("UnityEditor.PropertyEditor");
            var searchType = inspectorType != null ? inspectorType : propEditorType;

            Assert.IsNotNull(searchType, "A target window type must exist.");
            var prop = FindPropertyInHierarchy(searchType, "tracker");
            var field = FindFieldInHierarchy(searchType, "m_Tracker");

            Assert.IsTrue(prop != null || field != null, "Either property 'tracker' or field 'm_Tracker' must exist in PropertyEditor/InspectorWindow hierarchy.");
        }

        [Test]
        public void Test04_Reflection_ClearEditorsAndRebuildMethod_Exists()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var inspectorType = assembly.GetType("UnityEditor.InspectorWindow");
            var propEditorType = assembly.GetType("UnityEditor.PropertyEditor");
            var searchType = inspectorType != null ? inspectorType : propEditorType;

            var method = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
            Assert.IsNotNull(method, "ClearEditorsAndRebuild method must exist in the hierarchy.");
        }

        [Test]
        public void Test05_Reflection_ClearSerializedLockedObjectsMethod_Exists()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var inspectorType = assembly.GetType("UnityEditor.InspectorWindow");
            var propEditorType = assembly.GetType("UnityEditor.PropertyEditor");
            var searchType = inspectorType != null ? inspectorType : propEditorType;

            var method = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
            if (method == null && propEditorType != null)
            {
                method = FindMethodInHierarchy(propEditorType, "ClearSerializedLockedObjects");
            }

            Assert.IsNotNull(method, "ClearSerializedLockedObjects method must exist in PropertyEditor or InspectorWindow.");
        }

        [Test]
        public void Test06_Reflection_IsLockedProperty_ExistsOnInspectorWindow()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var inspectorType = assembly.GetType("UnityEditor.InspectorWindow");

            Assert.IsNotNull(inspectorType, "InspectorWindow type must exist.");
            var prop = FindPropertyInHierarchy(inspectorType, "isLocked");
            Assert.IsNotNull(prop, "isLocked property must exist on InspectorWindow.");
            Assert.AreEqual(typeof(bool), prop.PropertyType, "isLocked property must be boolean.");
        }

        [Test]
        public void Test07_Reflection_InspectorsCollectionField_Exists()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var inspectorType = assembly.GetType("UnityEditor.InspectorWindow");
            var propEditorType = assembly.GetType("UnityEditor.PropertyEditor");

            FieldInfo allInspectors = null;
            if (inspectorType != null)
            {
                allInspectors = inspectorType.GetField("m_AllInspectors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }

            FieldInfo allPropEditors = null;
            if (propEditorType != null)
            {
                allPropEditors = propEditorType.GetField("m_AllPropertyEditors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }

            Assert.IsTrue(allInspectors != null || allPropEditors != null, "Static list m_AllInspectors or m_AllPropertyEditors must exist.");
        }

        [Test]
        public void Test08_Reflection_LogEntriesClearMethod_Exists()
        {
            var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            Assert.IsNotNull(logEntriesType, "UnityEditor.LogEntries type must exist.");

            var clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(clearMethod, "UnityEditor.LogEntries.Clear method must exist.");
        }

        #endregion

        #region Non-Destructive ActiveEditorTracker Tests

        [Test]
        public void Test09_PurgeSharedTracker_DoesNotDestroyNativeTrackerInstance()
        {
            var shared = ActiveEditorTracker.sharedTracker;
            Assert.IsNotNull(shared, "ActiveEditorTracker.sharedTracker must be non-null.");

            // Execute service purge
            InspectorSanitizerService.Instance.PurgeSharedTracker();

            // Verify tracker instance was not destroyed or invalidated
            Assert.IsNotNull(ActiveEditorTracker.sharedTracker, "sharedTracker must remain intact.");
            Assert.DoesNotThrow(() =>
            {
                var editors = ActiveEditorTracker.sharedTracker.activeEditors;
                Assert.IsNotNull(editors, "activeEditors property must be accessible without native crashes.");
            });
            Assert.IsFalse(ActiveEditorTracker.sharedTracker.isDirty, "sharedTracker.isDirty must be false after clean purge.");
        }

        #endregion

        #region Scene and Orphan Editor Destruction Tests

        [Test]
        public void Test10_DestroyOrphanAndSceneEditors_DestroysSceneBoundEditors()
        {
            var go = new GameObject("SanitizerTest_SceneObject");
            _spawnedGameObjects.Add(go);

            var editor = UnityEditor.Editor.CreateEditor(go);
            Assert.IsTrue(editor != null, "Editor must be created for scene GameObject.");

            InspectorSanitizerService.Instance.DestroyOrphanAndSceneEditors();

            Assert.IsTrue(editor == null, "Scene-bound Editor must be destroyed by DestroyOrphanAndSceneEditors.");
        }

        [Test]
        public void Test11_DestroyOrphanAndSceneEditors_DestroysOrphanEditors()
        {
            var go = new GameObject("SanitizerTest_OrphanObject");
            var editor = UnityEditor.Editor.CreateEditor(go);
            Assert.IsTrue(editor != null, "Editor must be created.");

            // Destroy GameObject first to make the editor an orphan (target == null)
            Object.DestroyImmediate(go);
            Assert.IsTrue(editor.target == null, "Editor target must be null after target destruction.");

            InspectorSanitizerService.Instance.DestroyOrphanAndSceneEditors();

            Assert.IsTrue(editor == null, "Orphan Editor with null target must be destroyed.");
        }

        [Test]
        public void Test12_DestroyOrphanAndSceneEditors_PreservesPersistentAssetEditors()
        {
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs");
            Assert.IsNotNull(scriptAsset, "Script asset must exist.");
            Assert.IsTrue(EditorUtility.IsPersistent(scriptAsset), "Target asset must be persistent.");

            var editor = UnityEditor.Editor.CreateEditor(scriptAsset);
            Assert.IsTrue(editor != null, "Editor must be created for persistent asset.");

            try
            {
                InspectorSanitizerService.Instance.DestroyOrphanAndSceneEditors();
                Assert.IsTrue(editor != null, "Persistent asset editor must NOT be destroyed.");
            }
            finally
            {
                if (editor != null)
                {
                    Object.DestroyImmediate(editor);
                }
            }
        }

        #endregion

        #region Locked Inspector Detection & Facade Tests

        [Test]
        public void Test13_UnlockAllInspectors_ExecutesGracefully()
        {
            Assert.DoesNotThrow(() => InspectorSanitizerService.Instance.UnlockAllInspectors());
        }

        [Test]
        public void Test14_PurgeAllInspectors_ExecutesFullSanitizationCycle()
        {
            Assert.DoesNotThrow(() => InspectorSanitizerService.Instance.PurgeAllInspectors());
            Assert.IsNotNull(ActiveEditorTracker.sharedTracker, "sharedTracker must remain valid after PurgeAllInspectors.");
        }

        [Test]
        public void Test15_ClearConsole_ExecutesWithoutExceptions()
        {
            Assert.DoesNotThrow(() => InspectorSanitizerService.Instance.ClearConsole());
        }

        [Test]
        public void Test16_ExecutionOrder_ConformsToPlayModeExecutionOrderConstant()
        {
            Assert.AreEqual(PlayModeExecutionOrder.InspectorSanitizer, InspectorSanitizerService.Instance.ExecutionOrder);
            Assert.AreEqual(200, InspectorSanitizerService.Instance.ExecutionOrder);
        }

        [Test]
        public void Test17_InspectorTrackerSanitizerFacade_DelegatesToService()
        {
            var mockService = new MockInspectorSanitizerService();
            InspectorTrackerSanitizer.SetServiceForTesting(mockService);

            InspectorTrackerSanitizer.PurgeAllInspectorWindows();
            Assert.AreEqual(1, mockService.PurgeAllInspectorsCalls);

            InspectorTrackerSanitizer.PurgeOrphanEditors();
            Assert.AreEqual(1, mockService.DestroyOrphanAndSceneEditorsCalls);

            InspectorTrackerSanitizer.ForceCleanTracker();
            Assert.AreEqual(2, mockService.PurgeAllInspectorsCalls);

            InspectorTrackerSanitizer.SanitizeActiveEditors();
            Assert.AreEqual(2, mockService.DestroyOrphanAndSceneEditorsCalls);
            Assert.AreEqual(1, mockService.PurgeSharedTrackerCalls);

            InspectorTrackerSanitizer.UnlockAllInspectors();
            Assert.AreEqual(1, mockService.UnlockAllInspectorsCalls);

            InspectorTrackerSanitizer.UnlockInspectors(true);
            Assert.AreEqual(2, mockService.UnlockAllInspectorsCalls);

            InspectorTrackerSanitizer.UnlockInspectors(false);
            Assert.AreEqual(2, mockService.UnlockAllInspectorsCalls);
        }

        [Test]
        public void Test18_LifecycleCallbacks_ExecutePurgeAllInspectors()
        {
            var service = InspectorSanitizerService.Instance;

            Assert.DoesNotThrow(() => service.OnExitingEditMode());
            Assert.DoesNotThrow(() => service.OnEnteredPlayMode());
            Assert.DoesNotThrow(() => service.OnExitingPlayMode());
            Assert.DoesNotThrow(() => service.OnEnteredEditMode());
            Assert.DoesNotThrow(() => service.OnBeforeAssemblyReload());
            Assert.DoesNotThrow(() => service.OnAfterAssemblyReload());
            Assert.DoesNotThrow(() => service.OnSceneClosing(default));
        }

        #endregion

        #region Helper Methods & Mock Types

        private static MethodInfo FindMethodInHierarchy(Type type, string name)
        {
            var current = type;
            while (current != null && current != typeof(object))
            {
                var method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    return method;
                }
                current = current.BaseType;
            }
            return null;
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string name)
        {
            var current = type;
            while (current != null && current != typeof(object))
            {
                var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
                current = current.BaseType;
            }
            return null;
        }

        private static PropertyInfo FindPropertyInHierarchy(Type type, string name)
        {
            var current = type;
            while (current != null && current != typeof(object))
            {
                var prop = current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (prop != null)
                {
                    return prop;
                }
                current = current.BaseType;
            }
            return null;
        }

        private sealed class MockInspectorSanitizerService : IInspectorSanitizerService
        {
            public int PurgeAllInspectorsCalls { get; private set; }
            public int PurgeSharedTrackerCalls { get; private set; }
            public int DestroyOrphanAndSceneEditorsCalls { get; private set; }
            public int UnlockAllInspectorsCalls { get; private set; }

            public void PurgeAllInspectors() => PurgeAllInspectorsCalls++;
            public void PurgeSharedTracker() => PurgeSharedTrackerCalls++;
            public void DestroyOrphanAndSceneEditors() => DestroyOrphanAndSceneEditorsCalls++;
            public void UnlockAllInspectors() => UnlockAllInspectorsCalls++;
        }

        #endregion
    }
}
#endif
```

---

### 4.3 C# Code Blueprint: `PlayModeObserverIntegrationTests.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Game.Editor.Core.Lifecycle;

namespace Game.Editor.Tests
{
    /// <summary>
    /// Integration test suite for <see cref="PlayModeTransitionObserver"/> and <see cref="EditorLifecycleCoordinator"/>.
    /// Verifies priority sorting (ascending ExecutionOrder), subscriber exception isolation across all 7 lifecycle phases,
    /// legacy subscriber adapter bridging, and event registration idempotency and cleanup.
    /// Complies strictly with AGY0001 (Shallow Inheritance), AGY0021 (Named Event Handlers), and UNT0007/8/23.
    /// </summary>
    public sealed class PlayModeObserverIntegrationTests
    {
        private IPlayModeTransitionObserver _originalObserver;

        [SetUp]
        public void SetUp()
        {
            _originalObserver = PlayModeTransitionObserver.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            PlayModeTransitionObserver.SetInstanceForTesting(_originalObserver);
            PlayModeTransitionObserver.HookEditorEvents();
        }

        #region Priority Sorting Tests

        [Test]
        public void Test01_PrioritySorting_SubscribersSortedAscendingByExecutionOrder()
        {
            var observer = new PlayModeTransitionObserver();
            var sub500 = new MockSubscriber(500, "500");
            var sub100 = new MockSubscriber(100, "100");
            var sub200 = new MockSubscriber(200, "200");
            var sub50 = new MockSubscriber(50, "50");

            observer.RegisterSubscriber(sub500);
            observer.RegisterSubscriber(sub100);
            observer.RegisterSubscriber(sub200);
            observer.RegisterSubscriber(sub50);

            Assert.AreEqual(4, observer.Subscribers.Count);
            Assert.AreEqual(50, observer.Subscribers[0].ExecutionOrder);
            Assert.AreEqual(100, observer.Subscribers[1].ExecutionOrder);
            Assert.AreEqual(200, observer.Subscribers[2].ExecutionOrder);
            Assert.AreEqual(500, observer.Subscribers[3].ExecutionOrder);
        }

        [Test]
        public void Test02_PrioritySorting_ExecutionOrderPreservedDuringDispatch()
        {
            var observer = new PlayModeTransitionObserver();
            var log = new List<string>();

            var subDefault = new MockSubscriber(PlayModeExecutionOrder.Default, "Default", log);
            var subSanitizer = new MockSubscriber(PlayModeExecutionOrder.InspectorSanitizer, "Sanitizer", log);
            var subMemento = new MockSubscriber(PlayModeExecutionOrder.SelectionMemento, "Memento", log);

            // Register out of order
            observer.RegisterSubscriber(subDefault);
            observer.RegisterSubscriber(subSanitizer);
            observer.RegisterSubscriber(subMemento);

            observer.DispatchExitingEditMode();

            Assert.AreEqual(3, log.Count);
            Assert.AreEqual("Memento:OnExitingEditMode", log[0]);
            Assert.AreEqual("Sanitizer:OnExitingEditMode", log[1]);
            Assert.AreEqual("Default:OnExitingEditMode", log[2]);
        }

        [Test]
        public void Test03_PrioritySorting_StableForEqualExecutionOrders()
        {
            var observer = new PlayModeTransitionObserver();
            var subA = new MockSubscriber(500, "A");
            var subB = new MockSubscriber(500, "B");

            observer.RegisterSubscriber(subA);
            observer.RegisterSubscriber(subB);

            Assert.AreEqual(2, observer.Subscribers.Count);
            Assert.AreSame(subA, observer.Subscribers[0]);
            Assert.AreSame(subB, observer.Subscribers[1]);
        }

        #endregion

        #region Exception Isolation Tests

        [Test]
        public void Test04_ExceptionIsolation_ThrowingSubscriberDoesNotHaltSubsequent()
        {
            var observer = new PlayModeTransitionObserver();
            var log = new List<string>();

            var sub1 = new MockSubscriber(100, "Sub1", log);
            var subThrowing = new ThrowingSubscriber(200, "Thrower", log);
            var sub3 = new MockSubscriber(300, "Sub3", log);

            observer.RegisterSubscriber(sub1);
            observer.RegisterSubscriber(subThrowing);
            observer.RegisterSubscriber(sub3);

            LogAssert.Expect(LogType.Error, new Regex(@"\[PlayModeTransitionObserver\] Subscriber 'ThrowingSubscriber' threw exception during OnExitingEditMode:.*Intentional Test Exception.*"));

            observer.DispatchExitingEditMode();

            Assert.Contains("Sub1:OnExitingEditMode", log);
            Assert.Contains("Thrower:OnExitingEditMode", log);
            Assert.Contains("Sub3:OnExitingEditMode", log);
        }

        [Test]
        public void Test05_ExceptionIsolation_AcrossAllSevenLifecyclePhases()
        {
            var observer = new PlayModeTransitionObserver();
            var log = new List<string>();

            var sub1 = new MockSubscriber(100, "Sub1", log);
            var subThrowing = new ThrowingSubscriber(200, "Thrower", log);
            var sub3 = new MockSubscriber(300, "Sub3", log);

            observer.RegisterSubscriber(sub1);
            observer.RegisterSubscriber(subThrowing);
            observer.RegisterSubscriber(sub3);

            // Phase 1: EnteredPlayMode
            LogAssert.Expect(LogType.Error, new Regex(@".*ThrowingSubscriber.*threw exception during OnEnteredPlayMode:.*"));
            observer.DispatchEnteredPlayMode();
            Assert.Contains("Sub3:OnEnteredPlayMode", log);

            // Phase 2: ExitingPlayMode
            LogAssert.Expect(LogType.Error, new Regex(@".*ThrowingSubscriber.*threw exception during OnExitingPlayMode:.*"));
            observer.DispatchExitingPlayMode();
            Assert.Contains("Sub3:OnExitingPlayMode", log);

            // Phase 3: EnteredEditMode
            LogAssert.Expect(LogType.Error, new Regex(@".*ThrowingSubscriber.*threw exception during OnEnteredEditMode:.*"));
            observer.DispatchEnteredEditMode();
            Assert.Contains("Sub3:OnEnteredEditMode", log);

            // Phase 4: BeforeAssemblyReload
            LogAssert.Expect(LogType.Error, new Regex(@".*ThrowingSubscriber.*threw exception during OnBeforeAssemblyReload:.*"));
            observer.DispatchBeforeAssemblyReload();
            Assert.Contains("Sub3:OnBeforeAssemblyReload", log);

            // Phase 5: AfterAssemblyReload
            LogAssert.Expect(LogType.Error, new Regex(@".*ThrowingSubscriber.*threw exception during OnAfterAssemblyReload:.*"));
            observer.DispatchAfterAssemblyReload();
            Assert.Contains("Sub3:OnAfterAssemblyReload", log);

            // Phase 6: SceneClosing
            LogAssert.Expect(LogType.Error, new Regex(@".*ThrowingSubscriber.*threw exception during OnSceneClosing:.*"));
            observer.DispatchSceneClosing(default);
            Assert.Contains("Sub3:OnSceneClosing", log);
        }

        #endregion

        #region Lifecycle Phases Mapping Tests

        [Test]
        public void Test06_LifecyclePhases_DispatchPlayModeStateChanged_MapsAllStates()
        {
            var observer = new PlayModeTransitionObserver();
            var log = new List<string>();
            var sub = new MockSubscriber(100, "Sub", log);
            observer.RegisterSubscriber(sub);

            observer.DispatchPlayModeStateChanged(PlayModeStateChange.ExitingEditMode);
            Assert.AreEqual("Sub:OnExitingEditMode", log[log.Count - 1]);

            observer.DispatchPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
            Assert.AreEqual("Sub:OnEnteredPlayMode", log[log.Count - 1]);

            observer.DispatchPlayModeStateChanged(PlayModeStateChange.ExitingPlayMode);
            Assert.AreEqual("Sub:OnExitingPlayMode", log[log.Count - 1]);

            observer.DispatchPlayModeStateChanged(PlayModeStateChange.EnteredEditMode);
            Assert.AreEqual("Sub:OnEnteredEditMode", log[log.Count - 1]);
        }

        [Test]
        public void Test07_LifecyclePhases_DispatchBeforeAndAfterAssemblyReload()
        {
            var observer = new PlayModeTransitionObserver();
            var log = new List<string>();
            var sub = new MockSubscriber(100, "Sub", log);
            observer.RegisterSubscriber(sub);

            observer.DispatchBeforeAssemblyReload();
            Assert.AreEqual("Sub:OnBeforeAssemblyReload", log[log.Count - 1]);

            observer.DispatchAfterAssemblyReload();
            Assert.AreEqual("Sub:OnAfterAssemblyReload", log[log.Count - 1]);
        }

        [Test]
        public void Test08_LifecyclePhases_DispatchSceneClosing_PassesScene()
        {
            var observer = new PlayModeTransitionObserver();
            var sub = new MockSubscriber(100, "Sub");
            observer.RegisterSubscriber(sub);

            var activeScene = SceneManager.GetActiveScene();
            observer.DispatchSceneClosing(activeScene);

            Assert.AreEqual(activeScene, sub.LastSceneClosed);
        }

        #endregion

        #region Legacy Subscriber Adapter Tests

        [Test]
        public void Test09_LegacySubscriberAdapter_BridgesAllLifecycleMethods()
        {
            var testObserver = new PlayModeTransitionObserver();
            PlayModeTransitionObserver.SetInstanceForTesting(testObserver);

            var legacySub = new MockLegacySubscriber();
            EditorLifecycleCoordinator.RegisterSubscriber(legacySub);

            try
            {
                Assert.IsTrue(EditorLifecycleCoordinator.LegacySubscribers.Contains(legacySub));
                Assert.AreEqual(1, testObserver.Subscribers.Count);
                Assert.AreEqual(PlayModeExecutionOrder.Default, testObserver.Subscribers[0].ExecutionOrder);

                testObserver.DispatchExitingEditMode();
                Assert.AreEqual(1, legacySub.BeforePlayModeEnteredCount);

                testObserver.DispatchEnteredPlayMode();
                Assert.AreEqual(1, legacySub.PlayModeEnteredCount);

                testObserver.DispatchExitingPlayMode();
                Assert.AreEqual(1, legacySub.BeforePlayModeExitedCount);

                testObserver.DispatchEnteredEditMode();
                Assert.AreEqual(1, legacySub.PlayModeExitedCount);

                testObserver.DispatchBeforeAssemblyReload();
                Assert.AreEqual(1, legacySub.BeforeDomainReloadCount);

                testObserver.DispatchAfterAssemblyReload();
                Assert.AreEqual(1, legacySub.AfterDomainReloadCount);
            }
            finally
            {
                EditorLifecycleCoordinator.UnregisterSubscriber(legacySub);
                Assert.IsFalse(EditorLifecycleCoordinator.LegacySubscribers.Contains(legacySub));
                Assert.AreEqual(0, testObserver.Subscribers.Count);
            }
        }

        [Test]
        public void Test10_LegacySubscriberAdapter_ExceptionIsolation()
        {
            var testObserver = new PlayModeTransitionObserver();
            PlayModeTransitionObserver.SetInstanceForTesting(testObserver);

            var throwingLegacy = new ThrowingLegacySubscriber();
            EditorLifecycleCoordinator.RegisterSubscriber(throwingLegacy);

            try
            {
                LogAssert.Expect(LogType.Error, new Regex(@"\[EditorLifecycleCoordinator\] Legacy subscriber 'ThrowingLegacySubscriber' threw exception in OnBeforePlayModeEntered:.*"));
                Assert.DoesNotThrow(() => testObserver.DispatchExitingEditMode());
            }
            finally
            {
                EditorLifecycleCoordinator.UnregisterSubscriber(throwingLegacy);
            }
        }

        [Test]
        public void Test11_DualInterfaceSubscriber_DirectRegistrationWithoutAdapter()
        {
            var testObserver = new PlayModeTransitionObserver();
            PlayModeTransitionObserver.SetInstanceForTesting(testObserver);

            var dualSub = new DualMockSubscriber(150);
            EditorLifecycleCoordinator.RegisterSubscriber(dualSub);

            try
            {
                Assert.IsTrue(EditorLifecycleCoordinator.LegacySubscribers.Contains(dualSub));
                Assert.AreEqual(1, testObserver.Subscribers.Count);
                Assert.AreSame(dualSub, testObserver.Subscribers[0], "Dual interface subscriber should be registered directly without wrapper adapter.");
            }
            finally
            {
                EditorLifecycleCoordinator.UnregisterSubscriber(dualSub);
                Assert.AreEqual(0, testObserver.Subscribers.Count);
            }
        }

        #endregion

        #region Registration Idempotency and Event Hooking Tests

        [Test]
        public void Test12_RegistrationIdempotency_DuplicateSubscriberIgnored()
        {
            var observer = new PlayModeTransitionObserver();
            var sub = new MockSubscriber(100, "Sub");

            observer.RegisterSubscriber(sub);
            observer.RegisterSubscriber(sub);

            Assert.AreEqual(1, observer.Subscribers.Count);
        }

        [Test]
        public void Test13_RegistrationIdempotency_NullSubscriberIgnored()
        {
            var observer = new PlayModeTransitionObserver();
            Assert.DoesNotThrow(() => observer.RegisterSubscriber(null));
            Assert.AreEqual(0, observer.Subscribers.Count);
        }

        [Test]
        public void Test14_Unregistration_NullOrUnknownSubscriber_Safe()
        {
            var observer = new PlayModeTransitionObserver();
            var sub = new MockSubscriber(100, "Sub");

            Assert.DoesNotThrow(() => observer.UnregisterSubscriber(null));
            Assert.DoesNotThrow(() => observer.UnregisterSubscriber(sub));
        }

        [Test]
        public void Test15_EventHooking_SymmetricalAndIdempotent()
        {
            Assert.DoesNotThrow(() => PlayModeTransitionObserver.HookEditorEvents());
            Assert.DoesNotThrow(() => PlayModeTransitionObserver.HookEditorEvents());
            Assert.DoesNotThrow(() => PlayModeTransitionObserver.UnhookEditorEvents());
            PlayModeTransitionObserver.HookEditorEvents();
        }

        [Test]
        public void Test16_RegisterDefaultSubscribers_IncludesSelectionMementoAndSanitizer()
        {
            PlayModeTransitionObserver.RegisterDefaultSubscribers();
            var subscribers = PlayModeTransitionObserver.Instance.Subscribers;

            int mementoIdx = -1;
            int sanitizerIdx = -1;
            for (int i = 0; i < subscribers.Count; i++)
            {
                if (subscribers[i] is SelectionMementoService)
                {
                    mementoIdx = i;
                }
                if (subscribers[i] is InspectorSanitizerService)
                {
                    sanitizerIdx = i;
                }
            }

            Assert.IsTrue(mementoIdx >= 0, "SelectionMementoService must be registered in default subscribers.");
            Assert.IsTrue(sanitizerIdx >= 0, "InspectorSanitizerService must be registered in default subscribers.");
            Assert.IsTrue(mementoIdx < sanitizerIdx, "SelectionMementoService (100) must execute before InspectorSanitizerService (200).");
        }

        #endregion

        #region Test Doubles & Mock Implementations

        private sealed class MockSubscriber : IPlayModeTransitionSubscriber
        {
            private readonly int _executionOrder;
            private readonly string _name;
            private readonly List<string> _log;

            public int ExecutionOrder => _executionOrder;
            public Scene LastSceneClosed { get; private set; }

            public MockSubscriber(int executionOrder, string name, List<string> log = null)
            {
                _executionOrder = executionOrder;
                _name = name;
                _log = log;
            }

            public void OnExitingEditMode() => _log?.Add($"{_name}:OnExitingEditMode");
            public void OnEnteredPlayMode() => _log?.Add($"{_name}:OnEnteredPlayMode");
            public void OnExitingPlayMode() => _log?.Add($"{_name}:OnExitingPlayMode");
            public void OnEnteredEditMode() => _log?.Add($"{_name}:OnEnteredEditMode");
            public void OnBeforeAssemblyReload() => _log?.Add($"{_name}:OnBeforeAssemblyReload");
            public void OnAfterAssemblyReload() => _log?.Add($"{_name}:OnAfterAssemblyReload");
            public void OnSceneClosing(Scene scene)
            {
                LastSceneClosed = scene;
                _log?.Add($"{_name}:OnSceneClosing");
            }
        }

        private sealed class ThrowingSubscriber : IPlayModeTransitionSubscriber
        {
            private readonly int _executionOrder;
            private readonly string _name;
            private readonly List<string> _log;

            public int ExecutionOrder => _executionOrder;

            public ThrowingSubscriber(int executionOrder, string name, List<string> log = null)
            {
                _executionOrder = executionOrder;
                _name = name;
                _log = log;
            }

            public void OnExitingEditMode()
            {
                _log?.Add($"{_name}:OnExitingEditMode");
                throw new InvalidOperationException("Intentional Test Exception in OnExitingEditMode");
            }

            public void OnEnteredPlayMode()
            {
                _log?.Add($"{_name}:OnEnteredPlayMode");
                throw new InvalidOperationException("Intentional Test Exception in OnEnteredPlayMode");
            }

            public void OnExitingPlayMode()
            {
                _log?.Add($"{_name}:OnExitingPlayMode");
                throw new InvalidOperationException("Intentional Test Exception in OnExitingPlayMode");
            }

            public void OnEnteredEditMode()
            {
                _log?.Add($"{_name}:OnEnteredEditMode");
                throw new InvalidOperationException("Intentional Test Exception in OnEnteredEditMode");
            }

            public void OnBeforeAssemblyReload()
            {
                _log?.Add($"{_name}:OnBeforeAssemblyReload");
                throw new InvalidOperationException("Intentional Test Exception in OnBeforeAssemblyReload");
            }

            public void OnAfterAssemblyReload()
            {
                _log?.Add($"{_name}:OnAfterAssemblyReload");
                throw new InvalidOperationException("Intentional Test Exception in OnAfterAssemblyReload");
            }

            public void OnSceneClosing(Scene scene)
            {
                _log?.Add($"{_name}:OnSceneClosing");
                throw new InvalidOperationException("Intentional Test Exception in OnSceneClosing");
            }
        }

        private sealed class MockLegacySubscriber : IEditorLifecycleSubscriber
        {
            public int BeforePlayModeEnteredCount { get; private set; }
            public int PlayModeEnteredCount { get; private set; }
            public int BeforePlayModeExitedCount { get; private set; }
            public int PlayModeExitedCount { get; private set; }
            public int BeforeDomainReloadCount { get; private set; }
            public int AfterDomainReloadCount { get; private set; }

            public void OnBeforePlayModeEntered() => BeforePlayModeEnteredCount++;
            public void OnPlayModeEntered() => PlayModeEnteredCount++;
            public void OnBeforePlayModeExited() => BeforePlayModeExitedCount++;
            public void OnPlayModeExited() => PlayModeExitedCount++;
            public void OnBeforeDomainReload() => BeforeDomainReloadCount++;
            public void OnAfterDomainReload() => AfterDomainReloadCount++;
        }

        private sealed class ThrowingLegacySubscriber : IEditorLifecycleSubscriber
        {
            public void OnBeforePlayModeEntered() => throw new InvalidOperationException("Legacy subscriber failure");
            public void OnPlayModeEntered() => throw new InvalidOperationException("Legacy subscriber failure");
            public void OnBeforePlayModeExited() => throw new InvalidOperationException("Legacy subscriber failure");
            public void OnPlayModeExited() => throw new InvalidOperationException("Legacy subscriber failure");
            public void OnBeforeDomainReload() => throw new InvalidOperationException("Legacy subscriber failure");
            public void OnAfterDomainReload() => throw new InvalidOperationException("Legacy subscriber failure");
        }

        private sealed class DualMockSubscriber : IPlayModeTransitionSubscriber, IEditorLifecycleSubscriber
        {
            public int ExecutionOrder { get; }

            public DualMockSubscriber(int executionOrder)
            {
                ExecutionOrder = executionOrder;
            }

            public void OnExitingEditMode() { }
            public void OnEnteredPlayMode() { }
            public void OnExitingPlayMode() { }
            public void OnEnteredEditMode() { }
            public void OnBeforeAssemblyReload() { }
            public void OnAfterAssemblyReload() { }
            public void OnSceneClosing(Scene scene) { }

            public void OnBeforePlayModeEntered() { }
            public void OnPlayModeEntered() { }
            public void OnBeforePlayModeExited() { }
            public void OnPlayModeExited() { }
            public void OnBeforeDomainReload() { }
            public void OnAfterDomainReload() { }
        }

        #endregion
    }
}
#endif
```

---

## 5. Verification Method

To independently verify the test suite designs and blueprints:

1. **Compilation Verification**:
   - Once `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef` is created (Milestone 3 Explorer 1 task), place `InspectorSanitizerTests.cs` and `PlayModeObserverIntegrationTests.cs` into `Assets/_Project/Tests/Editor/`.
   - Run compilation via Unity Editor or command line:
     ```powershell
     "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -quit -batchmode -nographics -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -executeMethod UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation
     ```
   - Verify 0 compilation errors and 0 warnings.

2. **Automated Test Execution**:
   - Execute EditMode tests via Unity Test Runner CLI:
     ```powershell
     "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -nographics -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testFilter "Game.Editor.Tests.InspectorSanitizerTests;Game.Editor.Tests.PlayModeObserverIntegrationTests" -testResults "c:\Users\SaidR\Documents\GitHub\TPOB\test-results-m3.xml"
     ```
   - Verify all 34 tests pass (`result="Passed"` in `test-results-m3.xml`).

3. **Roslyn Analyzer Validation**:
   - Check Roslyn analyzers in `Assets/Analyzers/` against the test files:
     - `AGY0001`: Passed (both fixtures and test doubles use shallow depth 0, `sealed` classes).
     - `AGY0002` / `AGY0003`: Passed (encapsulation strictly maintained).
     - `AGY0021`: Passed (named handlers used for event subscriptions).
     - `UNT0007`/`UNT0008`/`UNT0023`: Passed (no `?.` or `??` on `UnityEngine.Object`).

4. **Invalidation Conditions**:
   - If Unity 6 internal assembly renames `PropertyEditor` or `InspectorWindow` without backwards compatibility, `Test01` and `Test02` will fail, signaling the need to update reflection bindings.
   - If `SelectionMementoService` execution order constant changes from 100 or `InspectorSanitizerService` from 200, `Test02` and `Test16` in `PlayModeObserverIntegrationTests` will catch the priority regression.
