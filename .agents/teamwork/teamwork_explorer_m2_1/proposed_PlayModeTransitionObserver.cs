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
    /// Coordinates Unity editor lifecycle events and broadcasts them to ordered subscribers.
    /// Initializes on editor load via [InitializeOnLoad] and uses named handlers to satisfy AGY0021.
    /// Implements IPlayModeTransitionObserver and enforces non-allocating, exception-isolated dispatch.
    /// Complies strictly with AGY0001, AGY0002, AGY0003, and UNT0007/8/23.
    /// </summary>
    [InitializeOnLoad]
    public sealed class PlayModeTransitionObserver : IPlayModeTransitionObserver
    {
        private static PlayModeTransitionObserver s_instance;

        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static PlayModeTransitionObserver Instance => s_instance ??= new PlayModeTransitionObserver();

        /// <summary>
        /// Allows test suites to inject an observer instance.
        /// </summary>
        public static void SetInstanceForTesting(PlayModeTransitionObserver instance)
        {
            s_instance = instance;
        }

        private readonly List<IPlayModeTransitionSubscriber> _subscribers = new List<IPlayModeTransitionSubscriber>();

        static PlayModeTransitionObserver()
        {
            // Attach static event handlers to Unity lifecycle using named methods (AGY0021)
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
            EditorSceneManager.sceneClosing += HandleSceneClosing;
        }

        public PlayModeTransitionObserver()
        {
            // Automatically register default service subscribers
            RegisterSubscriber(SelectionMementoService.Instance);
        }

        public void RegisterSubscriber(IPlayModeTransitionSubscriber subscriber)
        {
            if (subscriber == null || _subscribers.Contains(subscriber))
            {
                return;
            }

            _subscribers.Add(subscriber);
            _subscribers.Sort(CompareExecutionOrder);
        }

        public void UnregisterSubscriber(IPlayModeTransitionSubscriber subscriber)
        {
            if (subscriber != null)
            {
                _subscribers.Remove(subscriber);
            }
        }

        private static int CompareExecutionOrder(IPlayModeTransitionSubscriber a, IPlayModeTransitionSubscriber b)
        {
            return a.ExecutionOrder.CompareTo(b.ExecutionOrder);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            try
            {
                var instance = Instance;
                switch (state)
                {
                    case PlayModeStateChange.ExitingEditMode:
                        instance.NotifyExitingEditMode();
                        break;
                    case PlayModeStateChange.EnteredPlayMode:
                        instance.NotifyEnteredPlayMode();
                        break;
                    case PlayModeStateChange.ExitingPlayMode:
                        instance.NotifyExitingPlayMode();
                        break;
                    case PlayModeStateChange.EnteredEditMode:
                        instance.NotifyEnteredEditMode();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void HandleBeforeAssemblyReload()
        {
            try
            {
                Instance.NotifyBeforeAssemblyReload();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void HandleAfterAssemblyReload()
        {
            try
            {
                Instance.NotifyAfterAssemblyReload();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void HandleSceneClosing(Scene scene, bool removingScene)
        {
            try
            {
                Instance.NotifySceneClosing(scene);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void NotifyExitingEditMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnExitingEditMode();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayModeTransitionObserver] Subscriber {_subscribers[i].GetType().Name} failed OnExitingEditMode: {ex.Message}");
                }
            }
        }

        private void NotifyEnteredPlayMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnEnteredPlayMode();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayModeTransitionObserver] Subscriber {_subscribers[i].GetType().Name} failed OnEnteredPlayMode: {ex.Message}");
                }
            }
        }

        private void NotifyExitingPlayMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnExitingPlayMode();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayModeTransitionObserver] Subscriber {_subscribers[i].GetType().Name} failed OnExitingPlayMode: {ex.Message}");
                }
            }
        }

        private void NotifyEnteredEditMode()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnEnteredEditMode();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayModeTransitionObserver] Subscriber {_subscribers[i].GetType().Name} failed OnEnteredEditMode: {ex.Message}");
                }
            }
        }

        private void NotifyBeforeAssemblyReload()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnBeforeAssemblyReload();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayModeTransitionObserver] Subscriber {_subscribers[i].GetType().Name} failed OnBeforeAssemblyReload: {ex.Message}");
                }
            }
        }

        private void NotifyAfterAssemblyReload()
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnAfterAssemblyReload();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayModeTransitionObserver] Subscriber {_subscribers[i].GetType().Name} failed OnAfterAssemblyReload: {ex.Message}");
                }
            }
        }

        private void NotifySceneClosing(Scene scene)
        {
            for (int i = 0; i < _subscribers.Count; i++)
            {
                try
                {
                    _subscribers[i].OnSceneClosing(scene);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PlayModeTransitionObserver] Subscriber {_subscribers[i].GetType().Name} failed OnSceneClosing: {ex.Message}");
                }
            }
        }
    }
}
#endif
