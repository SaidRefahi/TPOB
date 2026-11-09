#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service implementing reflection-based and non-destructive inspector and editor sanitation.
    /// Addresses Play Mode transition console exceptions in Unity 6000.3.7f1.
    /// </summary>
    public sealed class InspectorSanitizerService : IInspectorSanitizerService
    {
        private static InspectorSanitizerService _instance;

        /// <summary>
        /// Global singleton instance. Can be substituted for test doubles.
        /// </summary>
        public static InspectorSanitizerService Instance
        {
            get => _instance ??= new InspectorSanitizerService();
            set => _instance = value;
        }

        /// <summary>
        /// Purges all open InspectorWindow / PropertyEditor instances.
        /// </summary>
        public void PurgeAllInspectors()
        {
            try
            {
                var windows = GetOpenInspectors();
                for (int i = 0; i < windows.Count; i++)
                {
                    var win = windows[i];
                    if (win == null) continue;

                    try
                    {
                        // 1. Unlock if locked
                        if (ReflectionCache.IsLockedProperty != null)
                        {
                            bool isLocked = (bool)ReflectionCache.IsLockedProperty.GetValue(win);
                            if (isLocked)
                            {
                                ReflectionCache.IsLockedProperty.SetValue(win, false);
                            }
                        }

                        // 2. Clear serialized locked objects buffer
                        ReflectionCache.ClearSerializedLockedObjectsMethod?.Invoke(win, null);

                        // 3. Destroy active editors in window's tracker
                        ActiveEditorTracker tracker = null;
                        if (ReflectionCache.TrackerProperty != null)
                        {
                            tracker = ReflectionCache.TrackerProperty.GetValue(win) as ActiveEditorTracker;
                        }
                        if (tracker == null && ReflectionCache.TrackerField != null)
                        {
                            tracker = ReflectionCache.TrackerField.GetValue(win) as ActiveEditorTracker;
                        }

                        if (tracker != null)
                        {
                            var editors = tracker.activeEditors;
                            if (editors != null)
                            {
                                for (int j = 0; j < editors.Length; j++)
                                {
                                    var ed = editors[j];
                                    if (ed != null)
                                    {
                                        UnityEngine.Object.DestroyImmediate(ed);
                                    }
                                }
                            }
                            tracker.ClearDirty();
                        }

                        // 4. Clear UI Toolkit containers and rebuild
                        ReflectionCache.ClearEditorsAndRebuildMethod?.Invoke(win, null);

                        // 5. Repaint window
                        win.Repaint();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TPOB] Error purging inspector window '{win.name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TPOB] PurgeAllInspectors encountered an exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely purges ActiveEditorTracker.sharedTracker without destroying native C++ wrapper.
        /// </summary>
        public void PurgeSharedTracker()
        {
            try
            {
                var shared = ActiveEditorTracker.sharedTracker;
                if (shared != null)
                {
                    var editors = shared.activeEditors;
                    if (editors != null)
                    {
                        for (int j = 0; j < editors.Length; j++)
                        {
                            var ed = editors[j];
                            if (ed != null)
                            {
                                UnityEngine.Object.DestroyImmediate(ed);
                            }
                        }
                    }
                    shared.ClearDirty();
                    shared.ForceRebuild();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TPOB] PurgeSharedTracker encountered an exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Destroys all Editor ScriptableObjects referencing null or non-persistent scene objects.
        /// </summary>
        public void DestroyOrphanAndSceneEditors()
        {
            try
            {
                var allEditors = Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor));
                if (allEditors == null || allEditors.Length == 0) return;

                for (int i = 0; i < allEditors.Length; i++)
                {
                    var ed = allEditors[i] as UnityEditor.Editor;
                    if (ed == null) continue;

                    bool shouldDestroy = false;
                    try
                    {
                        var target = ed.target;
                        if (target == null || !EditorUtility.IsPersistent(target))
                        {
                            shouldDestroy = true;
                        }
                        else
                        {
                            var targets = ed.targets;
                            if (targets != null)
                            {
                                for (int t = 0; t < targets.Length; t++)
                                {
                                    if (targets[t] == null || !EditorUtility.IsPersistent(targets[t]))
                                    {
                                        shouldDestroy = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Target reference is corrupted or threw MissingReferenceException
                        shouldDestroy = true;
                    }

                    if (shouldDestroy)
                    {
                        try
                        {
                            UnityEngine.Object.DestroyImmediate(ed);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[TPOB] Failed to destroy editor instance '{ed.name}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TPOB] DestroyOrphanAndSceneEditors encountered an exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Unlocks all open inspector windows.
        /// </summary>
        public void UnlockAllInspectors()
        {
            try
            {
                var windows = GetOpenInspectors();
                for (int i = 0; i < windows.Count; i++)
                {
                    var win = windows[i];
                    if (win == null) continue;

                    try
                    {
                        if (ReflectionCache.IsLockedProperty != null)
                        {
                            bool isLocked = (bool)ReflectionCache.IsLockedProperty.GetValue(win);
                            if (isLocked)
                            {
                                ReflectionCache.IsLockedProperty.SetValue(win, false);
                                ReflectionCache.ClearSerializedLockedObjectsMethod?.Invoke(win, null);
                                win.Repaint();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TPOB] Error unlocking inspector window: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TPOB] UnlockAllInspectors encountered an exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the Unity Editor Console.
        /// </summary>
        public void ClearConsole()
        {
            try
            {
                if (ReflectionCache.ClearConsoleMethod != null)
                {
                    ReflectionCache.ClearConsoleMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TPOB] ClearConsole encountered an exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Convenience method combining orphan/scene editor purge and shared tracker rebuild.
        /// </summary>
        public void SanitizeActiveEditors()
        {
            DestroyOrphanAndSceneEditors();
            PurgeSharedTracker();
        }

        private static List<EditorWindow> GetOpenInspectors()
        {
            var result = new List<EditorWindow>();

            // 1. Try InspectorWindow.m_AllInspectors static list
            if (ReflectionCache.AllInspectorsField != null)
            {
                if (ReflectionCache.AllInspectorsField.GetValue(null) is IList list && list.Count > 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] is EditorWindow win && win != null)
                        {
                            result.Add(win);
                        }
                    }
                    if (result.Count > 0) return result;
                }
            }

            // 2. Try PropertyEditor.m_AllPropertyEditors static list
            if (ReflectionCache.AllPropertyEditorsField != null)
            {
                if (ReflectionCache.AllPropertyEditorsField.GetValue(null) is IList propList && propList.Count > 0)
                {
                    for (int i = 0; i < propList.Count; i++)
                    {
                        if (propList[i] is EditorWindow win && win != null)
                        {
                            result.Add(win);
                        }
                    }
                    if (result.Count > 0) return result;
                }
            }

            // 3. Fallback to Resources.FindObjectsOfTypeAll
            var targetType = ReflectionCache.InspectorWindowType ?? ReflectionCache.PropertyEditorType;
            if (targetType != null)
            {
                var objs = Resources.FindObjectsOfTypeAll(targetType);
                if (objs != null && objs.Length > 0)
                {
                    for (int i = 0; i < objs.Length; i++)
                    {
                        if (objs[i] is EditorWindow win && win != null)
                        {
                            result.Add(win);
                        }
                    }
                }
            }

            return result;
        }

        private static class ReflectionCache
        {
            public static readonly Type PropertyEditorType;
            public static readonly Type InspectorWindowType;
            public static readonly PropertyInfo TrackerProperty;
            public static readonly FieldInfo TrackerField;
            public static readonly MethodInfo ClearEditorsAndRebuildMethod;
            public static readonly MethodInfo ClearSerializedLockedObjectsMethod;
            public static readonly PropertyInfo IsLockedProperty;
            public static readonly FieldInfo AllInspectorsField;
            public static readonly FieldInfo AllPropertyEditorsField;
            public static readonly MethodInfo ClearConsoleMethod;

            static ReflectionCache()
            {
                var editorAssembly = typeof(UnityEditor.Editor).Assembly;
                PropertyEditorType = editorAssembly.GetType("UnityEditor.PropertyEditor");
                InspectorWindowType = editorAssembly.GetType("UnityEditor.InspectorWindow");

                var searchType = PropertyEditorType ?? InspectorWindowType;
                if (searchType != null)
                {
                    TrackerProperty = FindPropertyInHierarchy(searchType, "tracker");
                    TrackerField = FindFieldInHierarchy(searchType, "m_Tracker");
                    ClearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
                    ClearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
                }

                if (PropertyEditorType != null)
                {
                    AllPropertyEditorsField = PropertyEditorType.GetField("m_AllPropertyEditors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (InspectorWindowType != null)
                {
                    IsLockedProperty = FindPropertyInHierarchy(InspectorWindowType, "isLocked");
                    AllInspectorsField = InspectorWindowType.GetField("m_AllInspectors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }

                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType != null)
                {
                    ClearConsoleMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }

            private static MethodInfo FindMethodInHierarchy(Type type, string name)
            {
                var current = type;
                while (current != null && current != typeof(object))
                {
                    var method = current.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    if (method != null) return method;
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
                    if (field != null) return field;
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
                    if (prop != null) return prop;
                    current = current.BaseType;
                }
                return null;
            }
        }
    }
}
#endif
