#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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

        static EditorLifecycleCoordinator()
        {
            // Register default legacy singleton if not already present
            RegisterSubscriber(SelectionMementoService.Instance);

            // Listen to selection changes to purge destroyed native targets
            Selection.selectionChanged += HandleSelectionChanged;
        }

        /// <summary>
        /// Registers a legacy lifecycle subscriber, bridging it to PlayModeTransitionObserver.
        /// </summary>
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

        private sealed class LegacySubscriberAdapter : IPlayModeTransitionSubscriber
        {
            private readonly IEditorLifecycleSubscriber _target;

            public int ExecutionOrder => 0;

            public LegacySubscriberAdapter(IEditorLifecycleSubscriber target)
            {
                _target = target;
            }

            public void OnExitingEditMode() => _target.OnBeforePlayModeEntered();
            public void OnEnteredPlayMode() => _target.OnPlayModeEntered();
            public void OnExitingPlayMode() => _target.OnBeforePlayModeExited();
            public void OnEnteredEditMode() => _target.OnPlayModeExited();
            public void OnBeforeAssemblyReload() => _target.OnBeforeDomainReload();
            public void OnAfterAssemblyReload() => _target.OnAfterDomainReload();
            public void OnSceneClosing(UnityEngine.SceneManagement.Scene scene) { }
        }
    }
}
#endif
