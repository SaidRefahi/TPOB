#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
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

#pragma warning disable CS0618 // Type or member is obsolete

        [Test]
        public void Test09_LegacySubscriberAdapter_BridgesAllLifecycleMethods()
        {
            var testObserver = new PlayModeTransitionObserver();
            PlayModeTransitionObserver.SetInstanceForTesting(testObserver);

            var legacySub = new MockLegacySubscriber();
            EditorLifecycleCoordinator.RegisterSubscriber(legacySub);

            try
            {
                Assert.IsTrue(ContainsLegacySubscriber(EditorLifecycleCoordinator.LegacySubscribers, legacySub));
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
                Assert.IsFalse(ContainsLegacySubscriber(EditorLifecycleCoordinator.LegacySubscribers, legacySub));
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
                Assert.IsTrue(ContainsLegacySubscriber(EditorLifecycleCoordinator.LegacySubscribers, dualSub));
                Assert.AreEqual(1, testObserver.Subscribers.Count);
                Assert.AreSame(dualSub, testObserver.Subscribers[0], "Dual interface subscriber should be registered directly without wrapper adapter.");
            }
            finally
            {
                EditorLifecycleCoordinator.UnregisterSubscriber(dualSub);
                Assert.AreEqual(0, testObserver.Subscribers.Count);
            }
        }

#pragma warning restore CS0618

        private static bool ContainsLegacySubscriber(IReadOnlyList<IEditorLifecycleSubscriber> list, IEditorLifecycleSubscriber item)
        {
            if (list == null || item == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                {
                    return true;
                }
            }

            return false;
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

            public void OnExitingEditMode()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnExitingEditMode");
                }
            }

            public void OnEnteredPlayMode()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnEnteredPlayMode");
                }
            }

            public void OnExitingPlayMode()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnExitingPlayMode");
                }
            }

            public void OnEnteredEditMode()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnEnteredEditMode");
                }
            }

            public void OnBeforeAssemblyReload()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnBeforeAssemblyReload");
                }
            }

            public void OnAfterAssemblyReload()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnAfterAssemblyReload");
                }
            }

            public void OnSceneClosing(Scene scene)
            {
                LastSceneClosed = scene;
                if (_log != null)
                {
                    _log.Add($"{_name}:OnSceneClosing");
                }
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
                if (_log != null)
                {
                    _log.Add($"{_name}:OnExitingEditMode");
                }
                throw new InvalidOperationException("Intentional Test Exception in OnExitingEditMode");
            }

            public void OnEnteredPlayMode()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnEnteredPlayMode");
                }
                throw new InvalidOperationException("Intentional Test Exception in OnEnteredPlayMode");
            }

            public void OnExitingPlayMode()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnExitingPlayMode");
                }
                throw new InvalidOperationException("Intentional Test Exception in OnExitingPlayMode");
            }

            public void OnEnteredEditMode()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnEnteredEditMode");
                }
                throw new InvalidOperationException("Intentional Test Exception in OnEnteredEditMode");
            }

            public void OnBeforeAssemblyReload()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnBeforeAssemblyReload");
                }
                throw new InvalidOperationException("Intentional Test Exception in OnBeforeAssemblyReload");
            }

            public void OnAfterAssemblyReload()
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnAfterAssemblyReload");
                }
                throw new InvalidOperationException("Intentional Test Exception in OnAfterAssemblyReload");
            }

            public void OnSceneClosing(Scene scene)
            {
                if (_log != null)
                {
                    _log.Add($"{_name}:OnSceneClosing");
                }
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

    [InitializeOnLoad]
    internal static class BatchModeTestSceneInitializer
    {
        static BatchModeTestSceneInitializer()
        {
            EditorApplication.delayCall += EnsureCleanSceneForBatchModeTests;
        }

        private static void EnsureCleanSceneForBatchModeTests()
        {
            if (!Application.isBatchMode)
            {
                return;
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isDirty)
            {
                const string fallbackScene = "Assets/_Project/Scenes/Rooms/Prueba.unity";
                if (System.IO.File.Exists(fallbackScene))
                {
                    EditorSceneManager.OpenScene(fallbackScene, OpenSceneMode.Single);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
        }
    }
}
#endif
