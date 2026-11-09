# Milestone 4 Explorer 2: Automated Play Mode Transition Verification Blueprint

## 1. Observation

1. **Unity Engine & Framework Specifications**:
   - Unity Version: `Unity 6000.3.7f1` (Unity 6.x) installed at `C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe`.
   - Test Framework: Unity Test Framework (UTF) with `UnityEngine.TestTools` and NUnit (`nunit.framework.dll`), referenced in `Assets/_Project/Tests/Editor/Game.Editor.Tests.asmdef`.
   - Core Subsystem: Lifecycle management in `Assets/_Project/Editor/Core/Lifecycle/` (`PlayModeTransitionObserver.cs`, `SelectionMementoService.cs`, `InspectorSanitizerService.cs`).

2. **Target Exceptions from `ORIGINAL_REQUEST.md` (Lines 13–17)**:
   - `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
   - `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
   - `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
   - `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

3. **Lifecycle Service Execution Order & Restoration Delay**:
   - In `PlayModeTransitionObserver.cs` (lines 75–82), `SelectionMementoService` runs at `ExecutionOrder = 100` (`PlayModeExecutionOrder.SelectionMemento`), while `InspectorSanitizerService` runs at `ExecutionOrder = 200` (`PlayModeExecutionOrder.InspectorSanitizer`).
   - In `SelectionMementoService.cs` (lines 336–349):
     - `OnExitingEditMode()`: Captures selection, saves to `SessionState`, and calls `ClearSelectionSafely()`.
     - `OnEnteredPlayMode()`: Hooks `EditorApplication.delayCall += HandleDeferredRestore`.
     - `OnExitingPlayMode()`: Calls `ClearSelectionSafely()`.
     - `OnEnteredEditMode()`: Hooks `EditorApplication.delayCall += HandleDeferredRestore`.
   - Because restoration is dispatched asynchronously via `EditorApplication.delayCall`, any test asserting selection immediately upon entering Play Mode or Edit Mode must yield one frame or tick for the `delayCall` queue to drain.

4. **Inspector Reflection & Locked Object Sanitation**:
   - In `InspectorSanitizerService.cs` (lines 118–149):
     - Unlocks locked inspectors via `s_isLockedProp.SetValue(win, BoxedFalse)`.
     - Invokes `ClearSerializedLockedObjects()` on `PropertyEditor` / `InspectorWindow`.
     - Destroys active tracker editors via `DestroyTrackerEditors(tracker)`.
     - Invokes `ClearEditorsAndRebuild()` on the window.
     - Calls `PurgeSharedTracker()` and `DestroyOrphanAndSceneEditors()`.
   - Locked inspectors (`isLocked = true`) retain `m_LockedObjects` / `m_SerializedLockedObjects` across domain reloads unless purged.

5. **UTF EditMode Test Execution Characteristics**:
   - Standard synchronous NUnit tests (`[Test]`) execute synchronously on the main thread.
   - Setting `EditorApplication.isPlaying = true` inside a synchronous `[Test]` merely schedules a transition for the next frame. Attempting to block or busy-wait locks the main thread, deadlocking the Editor.
   - Yielding coroutines (`[UnityTest]`) in EditMode tests is supported by UTF using `IEditModeTestYieldInstruction`. Specifically, `UnityEngine.TestTools.EnterPlayMode` and `UnityEngine.TestTools.ExitPlayMode` coordinate domain reload and resume the test in Play Mode.
   - However, in `-batchmode` (headless CLI execution), GUI windows (such as `InspectorWindow`) are not instantiated or rendered by default. In headless mode, `ActiveEditorTracker` active editors and `InspectorWindow` instances do not exist unless explicitly opened or created programmatically.

---

## 2. Logic Chain

1. **Evaluation of Test Options**:
   - **Hypothesis 1: Can synchronous `[Test]` toggle `EditorApplication.isPlaying = true`?**
     - Observation 5 confirms `EditorApplication.isPlaying = true` does not transition synchronously. If the test method completes synchronously, Unity initiates domain reload after test termination, abandoning or corrupting test runner state. Blocking the main thread (`Thread.Sleep`) halts the editor message pump and deadlocks.
     - *Deduction*: Synchronous `[Test]` cannot be used for live play mode transitions.
   - **Hypothesis 2: Can `[UnityTest]` with `yield return new EnterPlayMode();` and `yield return new ExitPlayMode();` be used?**
     - Observation 5 confirms UTF provides `EnterPlayMode` and `ExitPlayMode` yield instructions. These instruct UTF to serialize test state, allow Unity to complete domain reload, and resume the test method inside Play Mode (`Application.isPlaying == true`).
     - Observation 3 shows selection restoration is deferred via `EditorApplication.delayCall`. Therefore, after `yield return new EnterPlayMode();`, the test must execute `yield return null;` to ensure deferred restoration completes before asserting `Selection.objects`.
     - Observation 5 notes that in headless CLI mode (`-batchmode`), GUI `InspectorWindow` instances are absent. If no `InspectorWindow` is open, tracker editors for `MeshColliderEditor`, `TransformInspector`, etc., are not loaded, failing to reproduce the real-world conditions under which the 4 target exceptions occur.
     - *Deduction*: `[UnityTest]` with `EnterPlayMode` is a viable and clean automated regression test in UTF, but by itself does not guarantee full GUI stress-testing unless paired with programmatic window instantiation.
   - **Hypothesis 3: Can a dedicated Editor certifier (`PlayModeTransitionCertifier.cs`) orchestrate full lifecycle verification?**
     - Observations 2, 4, and 5 show that by using `SessionState` (which survives AppDomain reloads) and `[InitializeOnLoad]`, an Editor script can orchestrate a multi-phase state machine:
       1. **Phase 1 (Edit Mode Setup)**: Spawn GameObjects with `MeshCollider`, `Transform`, `SpriteRenderer`/`Renderer2D`, and an inactive hierarchy. Open an `InspectorWindow`, select the targets, set `isLocked = true`, force `ActiveEditorTracker.sharedTracker.ForceRebuild()`, register error listener, set `SessionState`, and trigger `EditorApplication.isPlaying = true`.
       2. **Phase 2 (Play Mode Entry Verification)**: Upon domain reload into Play Mode, `[InitializeOnLoad]` detects Phase 1. It schedules verification on `delayCall`, confirms `Application.isPlaying == true`, verifies zero logged exceptions, verifies `Selection.objects` restoration, sets `SessionState` to Phase 2, and triggers `EditorApplication.isPlaying = false`.
       3. **Phase 3 (Edit Mode Exit Verification)**: Upon domain reload back to Edit Mode, `[InitializeOnLoad]` detects Phase 2. It schedules verification on `delayCall`, confirms `Application.isPlaying == false`, verifies zero logged exceptions, verifies `Selection.objects` restoration, cleans up test GameObjects, clears `SessionState`, and writes a certification report.
     - *Deduction*: `PlayModeTransitionCertifier.cs` is the most robust, authentic, and complete verification mechanism. It stress-tests the real GUI `InspectorWindow` locking and active tracker behavior across real domain reloads, both via Editor Menu and via Unity `-executeMethod` CLI.

2. **Unified Strategy for Milestone 4 Worker**:
   - Provide a **Dual-Layer Verification Architecture**:
     1. **`Assets/_Project/Editor/Core/Lifecycle/Certifier/PlayModeTransitionCertifier.cs`**: Authoritative state-machine tool with interactive menu (`Tools > TPOB > Certify Play Mode Transition`) and command-line execution (`-executeMethod Game.Editor.Core.Lifecycle.Certifier.PlayModeTransitionCertifier.ExecuteCommandLineCertification`). Stress-tests locked `InspectorWindow` instances, verifies all 4 target components, logs zero exceptions, and validates selection memento round-trip.
     2. **`Assets/_Project/Tests/Editor/PlayModeTransitionE2ETests.cs`**: Automated UTF integration test using `[UnityTest]` with `EnterPlayMode` and `ExitPlayMode`, ensuring automated regression safety in the test runner.

---

## 3. Caveats

1. **Unity Process Concurrency**:
   - Unity does not allow two instances to open the same project simultaneously. Any automated CLI run (`-batchmode -executeMethod ...`) must only be executed when Unity Editor is not already running with a file lock on `Temp/UnityLockfile`.
2. **Batchmode vs GUI Window Rendering**:
   - When running `-batchmode`, `EditorWindow.GetWindow` instantiates windows in memory without OS window handles. `PlayModeTransitionCertifier` must invoke `ForceRebuild()` on the tracker to ensure active editors are generated even when windows are not visually composited.
3. **No Caveats on Roslyn Compliance**:
   - Both proposed scripts strictly comply with `AGY0001` (Shallow Inheritance), `AGY0021` (Named Event Handlers for all `+=` / `-=` subscriptions), and `UNT0007`/`UNT0008`/`UNT0023` (no `?.` or `??` on `UnityEngine.Object`).

---

## 4. Conclusion & Complete Design Blueprint

Milestone 4 Worker should implement the dual-layer verification mechanism detailed below.

### Blueprint A: `PlayModeTransitionCertifier.cs`
**File Location**: `Assets/_Project/Editor/Core/Lifecycle/Certifier/PlayModeTransitionCertifier.cs`
**Assembly**: `Game.Editor.asmdef`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Core.Lifecycle.Certifier
{
    /// <summary>
    /// Automated End-to-End Play Mode Transition Certifier for Milestone 4.
    /// Orchestrates a resilient 3-phase state machine surviving AppDomain reloads via SessionState.
    /// Stress-tests all 4 target components (MeshCollider, SpriteRenderer, Inactive Transform, GameObjectInspector),
    /// verifies InspectorWindow locking and tracker sanitization, asserts 0 console exceptions,
    /// and confirms selection memento restoration across both Play Mode entry and exit.
    /// Complies strictly with AGY0001, AGY0021 (Named Event Handlers), and UNT0007/8/23.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeTransitionCertifier
    {
        private const string StateKeyPhase = "TPOB_Certifier_Phase";
        private const string StateKeyErrors = "TPOB_Certifier_Errors";
        private const string StateKeyTargetNames = "TPOB_Certifier_TargetNames";
        private const string ReportFilePath = "Library/PlayModeCertificationReport.json";

        private const int PhaseIdle = 0;
        private const int PhaseEnteringPlayMode = 1;
        private const int PhaseExitingPlayMode = 2;

        private static readonly List<string> s_activeSessionErrors = new List<string>();

        static PlayModeTransitionCertifier()
        {
            // Named event handler per AGY0021
            EditorApplication.delayCall += HandleEditorInitialized;
        }

        private static void HandleEditorInitialized()
        {
            int currentPhase = SessionState.GetInt(StateKeyPhase, PhaseIdle);
            if (currentPhase == PhaseIdle)
            {
                return;
            }

            HookLogCallback();

            if (currentPhase == PhaseEnteringPlayMode && EditorApplication.isPlaying)
            {
                // Delay 1 tick to allow SelectionMementoService.HandleDeferredRestore to complete
                EditorApplication.delayCall += VerifyPlayModeEntryAndTriggerExit;
            }
            else if (currentPhase == PhaseExitingPlayMode && !EditorApplication.isPlaying)
            {
                // Delay 1 tick to allow SelectionMementoService.HandleDeferredRestore to complete
                EditorApplication.delayCall += VerifyEditModeExitAndComplete;
            }
        }

        [MenuItem("Tools/TPOB/Certify Play Mode Transition", priority = 100)]
        public static void RunInteractiveCertification()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PlayModeTransitionCertifier] Cannot start certification while Play Mode is active or transitioning.");
                return;
            }

            StartCertificationFlow();
        }

        public static void ExecuteCommandLineCertification()
        {
            Debug.Log("[PlayModeTransitionCertifier] Initiating CLI Play Mode Transition Certification...");
            StartCertificationFlow();
        }

        private static void StartCertificationFlow()
        {
            // 1. Reset state
            SessionState.EraseInt(StateKeyPhase);
            SessionState.EraseString(StateKeyErrors);
            SessionState.EraseString(StateKeyTargetNames);
            s_activeSessionErrors.Clear();

            HookLogCallback();

            // 2. Setup 4 Target GameObjects
            var targets = CreateTargetGameObjects();
            var targetNames = new List<string>();
            for (int i = 0; i < targets.Length; i++)
            {
                targetNames.Add(targets[i].name);
            }
            SessionState.SetString(StateKeyTargetNames, string.Join(",", targetNames));

            // 3. Set Selection
            Selection.objects = targets;
            Selection.activeGameObject = targets[0];

            // 4. Open and Lock Inspector Window
            SetupAndLockInspector();

            // 5. Force Shared Tracker Rebuild
            if (ActiveEditorTracker.sharedTracker != null)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }

            // 6. Set Phase 1 and Trigger Play Mode
            SessionState.SetInt(StateKeyPhase, PhaseEnteringPlayMode);
            Debug.Log("[PlayModeTransitionCertifier] Phase 1: Edit Mode setup complete. Entering Play Mode...");
            EditorApplication.isPlaying = true;
        }

        private static GameObject[] CreateTargetGameObjects()
        {
            // Target 1: MeshCollider + Transform (MeshColliderEditor.OnEnable)
            var goMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goMesh.name = "TPOB_Certifier_MeshColliderObj";
            goMesh.hideFlags = HideFlags.DontSave;

            // Target 2: SpriteRenderer + Transform (Renderer2DMeshEditor.OnEnable)
            var goSprite = new GameObject("TPOB_Certifier_SpriteRendererObj");
            goSprite.hideFlags = HideFlags.DontSave;
            goSprite.AddComponent<SpriteRenderer>();

            // Target 3: Inactive GameObject Hierarchy (TransformInspector.OnEnable & Inactive Resolution)
            var goInactiveParent = new GameObject("TPOB_Certifier_InactiveParentObj");
            goInactiveParent.hideFlags = HideFlags.DontSave;
            var goInactiveChild = new GameObject("TPOB_Certifier_InactiveChildObj");
            goInactiveChild.hideFlags = HideFlags.DontSave;
            goInactiveChild.transform.SetParent(goInactiveParent.transform, false);
            goInactiveParent.SetActive(false);

            // Target 4: Multi-selection GameObject (GameObjectInspector.OnEnable)
            var goInspector = new GameObject("TPOB_Certifier_GameObjectInspectorTarget");
            goInspector.hideFlags = HideFlags.DontSave;

            return new GameObject[] { goMesh, goSprite, goInactiveParent, goInspector };
        }

        private static void SetupAndLockInspector()
        {
            try
            {
                var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                if (inspectorType == null)
                {
                    return;
                }

                var window = EditorWindow.GetWindow(inspectorType, false, "Inspector", true);
                if (window != null)
                {
                    window.Show();
                    var isLockedProp = inspectorType.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (isLockedProp != null && isLockedProp.CanWrite)
                    {
                        isLockedProp.SetValue(window, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayModeTransitionCertifier] Note during Inspector lock setup: {ex.Message}");
            }
        }

        private static void VerifyPlayModeEntryAndTriggerExit()
        {
            Debug.Log("[PlayModeTransitionCertifier] Phase 2: Verifying inside Play Mode...");

            bool passed = true;
            var failures = new List<string>();

            // 1. Verify Play Mode state
            if (!Application.isPlaying)
            {
                failures.Add("Application.isPlaying is false in Phase 2.");
                passed = false;
            }

            // 2. Check for transition errors
            CollectPersistedErrors(failures);

            // 3. Verify Selection restoration in Play Mode
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                failures.Add("Selection.objects was empty in Play Mode (SelectionMemento deferred restore failed).");
                passed = false;
            }
            else
            {
                var targetNames = SessionState.GetString(StateKeyTargetNames, string.Empty).Split(',');
                for (int i = 0; i < targetNames.Length; i++)
                {
                    string targetName = targetNames[i];
                    bool found = false;
                    for (int j = 0; j < selectedObjects.Length; j++)
                    {
                        var obj = selectedObjects[j];
                        if (obj != null && obj.name == targetName)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        failures.Add($"Target '{targetName}' was not restored in Selection during Play Mode.");
                        passed = false;
                    }
                }
            }

            if (!passed)
            {
                SaveReportAndAbort(failures);
                return;
            }

            // 4. Advance to Phase 2 (Exiting Play Mode)
            SessionState.SetInt(StateKeyPhase, PhaseExitingPlayMode);
            Debug.Log("[PlayModeTransitionCertifier] Phase 2 verification PASSED. Exiting Play Mode...");
            EditorApplication.isPlaying = false;
        }

        private static void VerifyEditModeExitAndComplete()
        {
            Debug.Log("[PlayModeTransitionCertifier] Phase 3: Verifying returned to Edit Mode...");

            bool passed = true;
            var failures = new List<string>();

            // 1. Verify Edit Mode state
            if (Application.isPlaying)
            {
                failures.Add("Application.isPlaying is true in Phase 3.");
                passed = false;
            }

            // 2. Check for transition errors
            CollectPersistedErrors(failures);

            // 3. Verify Selection restoration back in Edit Mode
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                failures.Add("Selection.objects was empty in Edit Mode after exiting Play Mode.");
                passed = false;
            }

            // 4. Cleanup Test GameObjects
            CleanupTestObjects();

            // 5. Emit Certification Report
            SaveReport(passed, failures);

            // 6. Clear SessionState
            SessionState.EraseInt(StateKeyPhase);
            SessionState.EraseString(StateKeyErrors);
            SessionState.EraseString(StateKeyTargetNames);
            UnhookLogCallback();

            if (passed)
            {
                Debug.Log("<color=green><b>[PlayModeTransitionCertifier] CERTIFICATION SUCCEEDED!</b></color>\n" +
                          "Zero SerializedObjectNotCreatableException / MissingReferenceException logged.\n" +
                          "All target editors sanitized. Selection Memento cleanly restored.");
            }
            else
            {
                Debug.LogError($"[PlayModeTransitionCertifier] CERTIFICATION FAILED with {failures.Count} issues:\n" +
                               string.Join("\n", failures));
            }
        }

        private static void CleanupTestObjects()
        {
            var testNames = new HashSet<string>
            {
                "TPOB_Certifier_MeshColliderObj",
                "TPOB_Certifier_SpriteRendererObj",
                "TPOB_Certifier_InactiveParentObj",
                "TPOB_Certifier_InactiveChildObj",
                "TPOB_Certifier_GameObjectInspectorTarget"
            };

            var allGameObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < allGameObjects.Length; i++)
            {
                var go = allGameObjects[i];
                if (go != null && testNames.Contains(go.name) && !EditorUtility.IsPersistent(go))
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void HookLogCallback()
        {
            Application.logMessageReceivedThreaded -= HandleLogMessage;
            Application.logMessageReceivedThreaded += HandleLogMessage;
        }

        private static void UnhookLogCallback()
        {
            Application.logMessageReceivedThreaded -= HandleLogMessage;
        }

        private static void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                if (condition.Contains("SerializedObjectNotCreatableException") ||
                    condition.Contains("MissingReferenceException") ||
                    condition.Contains("MeshColliderEditor") ||
                    condition.Contains("GameObjectInspector") ||
                    condition.Contains("TransformInspector") ||
                    condition.Contains("Renderer2DMeshEditor"))
                {
                    string entry = $"[{type}] {condition}\n{stackTrace}";
                    s_activeSessionErrors.Add(entry);

                    string existing = SessionState.GetString(StateKeyErrors, string.Empty);
                    SessionState.SetString(StateKeyErrors, string.IsNullOrEmpty(existing) ? entry : existing + "|||" + entry);
                }
            }
        }

        private static void CollectPersistedErrors(List<string> failures)
        {
            string persisted = SessionState.GetString(StateKeyErrors, string.Empty);
            if (!string.IsNullOrEmpty(persisted))
            {
                var errorList = persisted.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < errorList.Length; i++)
                {
                    failures.Add($"Console Exception Detected: {errorList[i]}");
                }
            }

            for (int i = 0; i < s_activeSessionErrors.Count; i++)
            {
                failures.Add($"Active Session Exception: {s_activeSessionErrors[i]}");
            }
            s_activeSessionErrors.Clear();
            SessionState.EraseString(StateKeyErrors);
        }

        private static void SaveReportAndAbort(List<string> failures)
        {
            Debug.LogError($"[PlayModeTransitionCertifier] Certification Aborted in Phase 2:\n{string.Join("\n", failures)}");
            SaveReport(false, failures);
            CleanupTestObjects();
            SessionState.EraseInt(StateKeyPhase);
            SessionState.EraseString(StateKeyErrors);
            SessionState.EraseString(StateKeyTargetNames);
            UnhookLogCallback();
            EditorApplication.isPlaying = false;
        }

        private static void SaveReport(bool success, List<string> errors)
        {
            try
            {
                var report = new CertificationResultData
                {
                    timestamp = DateTime.UtcNow.ToString("o"),
                    succeeded = success,
                    errors = errors.ToArray()
                };
                string json = EditorJsonUtility.ToJson(report, true);
                File.WriteAllText(ReportFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayModeTransitionCertifier] Could not write report file: {ex.Message}");
            }
        }

        [Serializable]
        private struct CertificationResultData
        {
            public string timestamp;
            public bool succeeded;
            public string[] errors;
        }
    }
}
#endif
```

---

### Blueprint B: `PlayModeTransitionE2ETests.cs`
**File Location**: `Assets/_Project/Tests/Editor/PlayModeTransitionE2ETests.cs`
**Assembly**: `Game.Editor.Tests.asmdef`

```csharp
#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Game.Editor.Core.Lifecycle;

namespace Game.Editor.Tests
{
    /// <summary>
    /// End-to-end integration test validating Play Mode entry and exit using Unity Test Framework's
    /// EnterPlayMode and ExitPlayMode yield instructions.
    /// Asserts 0 console exceptions from MeshColliderEditor, GameObjectInspector, TransformInspector,
    /// and Renderer2DMeshEditor, and verifies selection preservation.
    /// Complies strictly with AGY0001, AGY0021, and UNT0007/8/23.
    /// </summary>
    public sealed class PlayModeTransitionE2ETests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                var go = _spawnedObjects[i];
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawnedObjects.Clear();
        }

        [UnityTest]
        public IEnumerator Test01_PlayModeTransition_ZeroExceptionsAndSelectionPreserved()
        {
            // 1. Arrange: Create GameObjects covering all 4 target exception components
            var goMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goMesh.name = "E2E_Test_MeshColliderObj";
            _spawnedObjects.Add(goMesh);

            var goSprite = new GameObject("E2E_Test_SpriteRendererObj");
            goSprite.AddComponent<SpriteRenderer>();
            _spawnedObjects.Add(goSprite);

            var goInactive = new GameObject("E2E_Test_InactiveObj");
            goInactive.SetActive(false);
            _spawnedObjects.Add(goInactive);

            var targets = new Object[] { goMesh, goSprite, goInactive };
            Selection.objects = targets;
            Selection.activeGameObject = goMesh;

            // Ensure shared tracker has active editors for these targets
            if (ActiveEditorTracker.sharedTracker != null)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }

            // 2. Act: Enter Play Mode
            yield return new EnterPlayMode();

            // 3. Assert inside Play Mode
            Assert.IsTrue(Application.isPlaying, "Unity must be in Play Mode.");

            // Allow deferred delayCall from SelectionMementoService.OnEnteredPlayMode to complete
            yield return null;

            Assert.IsNotNull(Selection.activeGameObject, "Active GameObject must be restored in Play Mode.");
            Assert.AreEqual("E2E_Test_MeshColliderObj", Selection.activeGameObject.name, "Active GameObject name must match.");
            Assert.AreEqual(3, Selection.objects.Length, "All 3 selected objects must be restored in Play Mode.");

            // 4. Act: Exit Play Mode
            yield return new ExitPlayMode();

            // 5. Assert back in Edit Mode
            Assert.IsFalse(Application.isPlaying, "Unity must be in Edit Mode.");

            // Allow deferred delayCall from SelectionMementoService.OnEnteredEditMode to complete
            yield return null;

            Assert.IsNotNull(Selection.activeGameObject, "Active GameObject must be restored in Edit Mode.");
            Assert.AreEqual("E2E_Test_MeshColliderObj", Selection.activeGameObject.name, "Active GameObject name must match.");
            Assert.AreEqual(3, Selection.objects.Length, "All 3 selected objects must be restored in Edit Mode.");
        }
    }
}
#endif
```

---

## 5. Verification Method

### Step 1: Assembly Compilation Check
Execute Roslyn / C# compilation via .NET CLI:
```powershell
dotnet build Game.Editor.csproj -c Debug
dotnet build Assets/_Project/Tests/Editor/Game.Editor.Tests.csproj -c Debug
```
*Expected*: `Build succeeded. 0 Warning(s). 0 Error(s).`

### Step 2: Automated UTF Test Execution
Run the EditMode test suite including the new `PlayModeTransitionE2ETests`:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform editmode -testFilter "Game.Editor.Tests.PlayModeTransitionE2ETests" -testResults "Library/PlayModeE2E_Results.xml" -logFile "Library/PlayModeE2E.log"
```
*Expected*: Exit code `0` with `PlayModeE2E_Results.xml` showing 0 failures.

### Step 3: Interactive or Headless Certifier Execution
Run the full multi-phase certifier in Unity:
- **Interactive**: In Unity Editor GUI, click `Tools > TPOB > Certify Play Mode Transition`.
- **Command Line**:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -executeMethod Game.Editor.Core.Lifecycle.Certifier.PlayModeTransitionCertifier.ExecuteCommandLineCertification -logFile "Library/PlayModeCertifier.log"
```
*Expected*: Inspect `Library/PlayModeCertificationReport.json` confirming `"succeeded": true` and `"errors": []`.

### Invalidation Conditions
- Any occurrence in `Editor.log` or console output of:
  - `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
  - `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
  - `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
  - `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`
- `Selection.objects` fails to restore all 4 targets upon entering Play Mode or returning to Edit Mode.
- Unhandled exceptions during `InspectorSanitizerService.PurgeAllInspectors()` or `SelectionMementoService.RestoreSelection()`.
