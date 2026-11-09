#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Legacy static coordinator retained for backwards compatibility.
    /// Bridges legacy <see cref="IEditorLifecycleSubscriber"/> registrations into
    /// the new <see cref="PlayModeTransitionObserver"/> architecture and sanitizes null selections.
    /// Complies strictly with AGY0001, AGY0002, AGY0003, AGY0021, and UNT0007/8/23.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorLifecycleCoordinator
    {
        private static readonly List<IEditorLifecycleSubscriber> s_legacySubscribers = new List<IEditorLifecycleSubscriber>();
        private static readonly Dictionary<IEditorLifecycleSubscriber, IPlayModeTransitionSubscriber> s_adapterMap =
            new Dictionary<IEditorLifecycleSubscriber, IPlayModeTransitionSubscriber>();

        /// <summary>
        /// Read-only collection of registered legacy subscribers.
        /// </summary>
        public static IReadOnlyList<IEditorLifecycleSubscriber> LegacySubscribers => s_legacySubscribers;

        static EditorLifecycleCoordinator()
        {
            // Symmetrically bind selection change listener per AGY0021
            Selection.selectionChanged -= HandleSelectionChanged;
            Selection.selectionChanged += HandleSelectionChanged;
        }

        /// <summary>
        /// Registers a legacy lifecycle subscriber, bridging it to PlayModeTransitionObserver.
        /// </summary>
        [Obsolete("Use PlayModeTransitionObserver.Instance.RegisterSubscriber(IPlayModeTransitionSubscriber) instead.")]
        public static void RegisterSubscriber(IEditorLifecycleSubscriber subscriber)
        {
            if (subscriber == null || s_legacySubscribers.Contains(subscriber))
            {
                return;
            }

            s_legacySubscribers.Add(subscriber);

            // If the subscriber is already an IPlayModeTransitionSubscriber, register it directly
            if (subscriber is IPlayModeTransitionSubscriber transitionSubscriber)
            {
                PlayModeTransitionObserver.Instance.RegisterSubscriber(transitionSubscriber);
            }
            else
            {
                // Otherwise create an adapter bridge
                var adapter = new LegacySubscriberAdapter(subscriber);
                s_adapterMap[subscriber] = adapter;
                PlayModeTransitionObserver.Instance.RegisterSubscriber(adapter);
            }
        }

        /// <summary>
        /// Unregisters a legacy lifecycle subscriber.
        /// </summary>
        [Obsolete("Use PlayModeTransitionObserver.Instance.UnregisterSubscriber(IPlayModeTransitionSubscriber) instead.")]
        public static void UnregisterSubscriber(IEditorLifecycleSubscriber subscriber)
        {
            if (subscriber == null)
            {
                return;
            }

            s_legacySubscribers.Remove(subscriber);

            if (subscriber is IPlayModeTransitionSubscriber transitionSubscriber)
            {
                PlayModeTransitionObserver.Instance.UnregisterSubscriber(transitionSubscriber);
            }
            else if (s_adapterMap.TryGetValue(subscriber, out var adapter))
            {
                PlayModeTransitionObserver.Instance.UnregisterSubscriber(adapter);
                s_adapterMap.Remove(subscriber);
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
        /// Adapter that maps legacy <see cref="IEditorLifecycleSubscriber"/> invocations
        /// into the <see cref="IPlayModeTransitionSubscriber"/> pipeline.
        /// </summary>
        private sealed class LegacySubscriberAdapter : IPlayModeTransitionSubscriber
        {
            private readonly IEditorLifecycleSubscriber _target;

            public int ExecutionOrder => PlayModeExecutionOrder.Default; // 500

            public LegacySubscriberAdapter(IEditorLifecycleSubscriber target)
            {
                _target = target;
            }

            public void OnExitingEditMode()
            {
                try
                {
                    _target.OnBeforePlayModeEntered();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{_target.GetType().Name}' threw exception in OnBeforePlayModeEntered: {ex}");
                }
            }

            public void OnEnteredPlayMode()
            {
                try
                {
                    _target.OnPlayModeEntered();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{_target.GetType().Name}' threw exception in OnPlayModeEntered: {ex}");
                }
            }

            public void OnExitingPlayMode()
            {
                try
                {
                    _target.OnBeforePlayModeExited();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{_target.GetType().Name}' threw exception in OnBeforePlayModeExited: {ex}");
                }
            }

            public void OnEnteredEditMode()
            {
                try
                {
                    _target.OnPlayModeExited();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{_target.GetType().Name}' threw exception in OnPlayModeExited: {ex}");
                }
            }

            public void OnBeforeAssemblyReload()
            {
                try
                {
                    _target.OnBeforeDomainReload();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{_target.GetType().Name}' threw exception in OnBeforeDomainReload: {ex}");
                }
            }

            public void OnAfterAssemblyReload()
            {
                try
                {
                    _target.OnAfterDomainReload();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EditorLifecycleCoordinator] Legacy subscriber '{_target.GetType().Name}' threw exception in OnAfterDomainReload: {ex}");
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
