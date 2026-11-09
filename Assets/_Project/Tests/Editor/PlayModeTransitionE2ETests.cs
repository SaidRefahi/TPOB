#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Game.Editor.Core.Lifecycle;
using Object = UnityEngine.Object;

namespace Game.Editor.Tests
{
    /// <summary>
    /// End-to-End integration test suite for Milestone 4 validating live Play Mode transitions.
    /// Exercises all 4 target component scenarios from the original issue:
    ///   1. MeshCollider (MeshColliderEditor.OnEnable)
    ///   2. Multi-selection GameObject (GameObjectInspector.m_Targets)
    ///   3. Inactive GameObject with Transform (TransformInspector.OnEnable)
    ///   4. SpriteRenderer / 2D Mesh (Renderer2DMeshEditor.OnEnable)
    /// Asserts zero console exceptions during PlayMode entry/exit and verifies selection restoration.
    /// Complies strictly with AGY0001, AGY0021 (Named Event Handlers), and UNT0007/8/23.
    /// </summary>
    [TestFixture]
    public sealed class PlayModeTransitionE2ETests
    {
        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<string> _capturedErrors = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _capturedErrors.Clear();
            Application.logMessageReceived -= HandleLogMessage;
            Application.logMessageReceived += HandleLogMessage;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= HandleLogMessage;

            for (int i = 0; i < _spawnedObjects.Count; i++)
            {
                var go = _spawnedObjects[i];
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            _spawnedObjects.Clear();

            var testNames = new HashSet<string>
            {
                "E2E_Test_MeshColliderObj",
                "E2E_Test_SpriteRendererObj",
                "E2E_Test_InactiveParentObj",
                "E2E_Test_InactiveChildObj",
                "E2E_Test_GameObjectInspectorTarget",
                "E2E_Test_LockedInspectorObj"
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

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
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
                    _capturedErrors.Add($"[{type}] {condition}\n{stackTrace}");
                }
            }
        }

        [UnityTest]
        public IEnumerator Test01_PlayModeTransition_AllFourComponentScenarios_ZeroExceptionsAndSelectionPreserved()
        {
            // 1. Arrange: Create GameObjects covering all 4 target exception scenarios
            // Scenario 1: MeshCollider (MeshColliderEditor)
            var goMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goMesh.name = "E2E_Test_MeshColliderObj";
            _spawnedObjects.Add(goMesh);

            // Scenario 4: SpriteRenderer / 2D Mesh (Renderer2DMeshEditor)
            var goSprite = new GameObject("E2E_Test_SpriteRendererObj");
            goSprite.AddComponent<SpriteRenderer>();
            _spawnedObjects.Add(goSprite);

            // Scenario 3: Inactive GameObject with Transform (TransformInspector)
            var goInactiveParent = new GameObject("E2E_Test_InactiveParentObj");
            var goInactiveChild = new GameObject("E2E_Test_InactiveChildObj");
            goInactiveChild.transform.SetParent(goInactiveParent.transform, false);
            goInactiveParent.SetActive(false);
            _spawnedObjects.Add(goInactiveParent);
            _spawnedObjects.Add(goInactiveChild);

            // Scenario 2: Multi-selection GameObject (GameObjectInspector.m_Targets)
            var goInspector = new GameObject("E2E_Test_GameObjectInspectorTarget");
            _spawnedObjects.Add(goInspector);

            var targets = new Object[] { goMesh, goSprite, goInactiveParent, goInspector };
            Selection.objects = targets;
            Selection.activeGameObject = goMesh;

            // Force ActiveEditorTracker to build active editors for these targets
            if (ActiveEditorTracker.sharedTracker != null)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }

            // 2. Act: Enter Play Mode
            yield return new EnterPlayMode();

            // 3. Assert inside Play Mode
            Assert.IsTrue(Application.isPlaying, "Unity must be in Play Mode.");

            // Allow deferred delayCall from SelectionMementoService.OnEnteredPlayMode to execute
            yield return null;

            AssertZeroTargetExceptions("Play Mode Entry");

            // Verify selection restoration inside Play Mode
            Assert.IsNotNull(Selection.activeGameObject, "Active GameObject must be restored in Play Mode.");
            Assert.AreEqual("E2E_Test_MeshColliderObj", Selection.activeGameObject.name, "Active GameObject name must match.");
            Assert.IsNotNull(Selection.objects, "Selection.objects must not be null in Play Mode.");
            Assert.AreEqual(4, Selection.objects.Length, "All 4 target objects must be restored in Play Mode.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_MeshColliderObj"), "MeshCollider object must be in selection.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_SpriteRendererObj"), "SpriteRenderer object must be in selection.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_InactiveParentObj"), "Inactive parent object must be in selection.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_GameObjectInspectorTarget"), "Inspector target object must be in selection.");
            var restoredInactive = FindObjectByName(Selection.objects, "E2E_Test_InactiveParentObj");
            Assert.IsNotNull(restoredInactive, "Inactive parent must be found in restored selection in Play Mode.");
            Assert.IsFalse(restoredInactive.activeSelf, "Inactive parent must remain inactive in Play Mode.");

            // 4. Act: Exit Play Mode
            // Persist current selection so SelectionMementoService can restore when returning to Edit Mode
            var memento = SelectionMementoService.Instance.CaptureCurrentSelection();
            var persistence = new SessionStateSelectionPersistence();
            persistence.SaveMemento(memento);

            yield return new ExitPlayMode();

            // 5. Assert back in Edit Mode
            Assert.IsFalse(Application.isPlaying, "Unity must be in Edit Mode.");

            // Allow deferred delayCall from SelectionMementoService.OnEnteredEditMode to execute
            yield return null;

            AssertZeroTargetExceptions("Play Mode Exit");

            // Verify selection restoration back in Edit Mode
            Assert.IsNotNull(Selection.activeGameObject, "Active GameObject must be restored in Edit Mode.");
            Assert.AreEqual("E2E_Test_MeshColliderObj", Selection.activeGameObject.name, "Active GameObject name must match.");
            Assert.IsNotNull(Selection.objects, "Selection.objects must not be null in Edit Mode.");
            Assert.AreEqual(4, Selection.objects.Length, "All 4 target objects must be restored in Edit Mode.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_MeshColliderObj"), "MeshCollider object must be in selection in Edit Mode.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_SpriteRendererObj"), "SpriteRenderer object must be in selection in Edit Mode.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_InactiveParentObj"), "Inactive parent object must be in selection in Edit Mode.");
            Assert.IsTrue(ContainsObjectName(Selection.objects, "E2E_Test_GameObjectInspectorTarget"), "Inspector target object must be in selection in Edit Mode.");
            var restoredInactiveEdit = FindObjectByName(Selection.objects, "E2E_Test_InactiveParentObj");
            Assert.IsNotNull(restoredInactiveEdit, "Inactive parent must be found in restored selection in Edit Mode.");
            Assert.IsFalse(restoredInactiveEdit.activeSelf, "Inactive parent must remain inactive in Edit Mode.");
        }

        [UnityTest]
        public IEnumerator Test02_PlayModeTransition_WithLockedInspectorWindow_ZeroExceptions()
        {
            // 1. Arrange: Create MeshCollider GameObject and lock an InspectorWindow on it
            var goMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goMesh.name = "E2E_Test_LockedInspectorObj";
            _spawnedObjects.Add(goMesh);

            Selection.objects = new Object[] { goMesh };
            Selection.activeGameObject = goMesh;

            LockInspectorWindow();

            if (ActiveEditorTracker.sharedTracker != null)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }

            // 2. Act: Enter Play Mode
            yield return new EnterPlayMode();

            // 3. Assert inside Play Mode
            Assert.IsTrue(Application.isPlaying, "Unity must be in Play Mode.");
            yield return null;

            AssertZeroTargetExceptions("Locked Inspector Play Mode Entry");

            // 4. Act: Exit Play Mode
            var memento = SelectionMementoService.Instance.CaptureCurrentSelection();
            var persistence = new SessionStateSelectionPersistence();
            persistence.SaveMemento(memento);

            yield return new ExitPlayMode();

            // 5. Assert back in Edit Mode
            Assert.IsFalse(Application.isPlaying, "Unity must be in Edit Mode.");
            yield return null;

            AssertZeroTargetExceptions("Locked Inspector Play Mode Exit");
        }

        private void AssertZeroTargetExceptions(string phase)
        {
            if (_capturedErrors.Count > 0)
            {
                Assert.Fail($"Target exceptions logged during {phase}:\n{string.Join("\n", _capturedErrors)}");
            }
        }

        private static bool ContainsObjectName(Object[] objects, string name)
        {
            if (objects == null)
            {
                return false;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj != null && obj.name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static GameObject FindObjectByName(Object[] objects, string name)
        {
            if (objects == null)
            {
                return null;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                if (obj != null && obj.name == name && obj is GameObject go)
                {
                    return go;
                }
            }
            return null;
        }

        private static void LockInspectorWindow()
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
                Debug.LogWarning($"[PlayModeTransitionE2ETests] Note during Inspector lock: {ex.Message}");
            }
        }
    }
}
#endif
