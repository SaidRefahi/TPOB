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
