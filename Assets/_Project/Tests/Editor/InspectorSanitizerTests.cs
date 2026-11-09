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
            Assert.IsTrue(ActiveEditorTracker.sharedTracker.isDirty, "sharedTracker.isDirty must be true after ForceRebuild to schedule rebuild.");
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
