#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service implementing reflection-based and non-destructive inspector and editor sanitation.
    /// Handles PropertyEditor base class reflection in Unity 6000.3.7f1, non-destructive ActiveEditorTracker
    /// cleanup, and scene/orphan editor purging prior to scene unload and domain reload.
    /// </summary>
    public sealed class InspectorSanitizerService : IInspectorSanitizerService, IPlayModeTransitionSubscriber
    {
        private static InspectorSanitizerService s_instance;

        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static InspectorSanitizerService Instance => s_instance ??= new InspectorSanitizerService();

        /// <summary>
        /// Allows test suites to inject a mock or alternate service instance.
        /// </summary>
        public static void SetInstanceForTesting(InspectorSanitizerService instance)
        {
            s_instance = instance;
        }

        // Cached Reflection Metadata
        private static readonly Type s_propertyEditorType;
        private static readonly Type s_inspectorWindowType;

        private static readonly PropertyInfo s_trackerProp;
        private static readonly FieldInfo s_trackerField;

        private static readonly MethodInfo s_clearEditorsAndRebuildMethod;
        private static readonly MethodInfo s_clearSerializedLockedObjectsMethod;

        private static readonly PropertyInfo s_isLockedProp;
        private static readonly FieldInfo s_allInspectorsField;
        private static readonly FieldInfo s_allPropertyEditorsField;

        private static readonly MethodInfo s_clearConsoleMethod;
        private static readonly object BoxedFalse = false;

        static InspectorSanitizerService()
        {
            try
            {
                var editorAssembly = typeof(UnityEditor.Editor).Assembly;

                s_propertyEditorType = editorAssembly.GetType("UnityEditor.PropertyEditor");
                s_inspectorWindowType = editorAssembly.GetType("UnityEditor.InspectorWindow");

                var searchType = s_inspectorWindowType ?? s_propertyEditorType;
                if (searchType != null)
                {
                    s_trackerProp = FindPropertyInHierarchy(searchType, "tracker");
                    s_trackerField = FindFieldInHierarchy(searchType, "m_Tracker");
                    s_clearEditorsAndRebuildMethod = FindMethodInHierarchy(searchType, "ClearEditorsAndRebuild");
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(searchType, "ClearSerializedLockedObjects");
                }

                if (s_clearSerializedLockedObjectsMethod == null && s_propertyEditorType != null)
                {
                    s_clearSerializedLockedObjectsMethod = FindMethodInHierarchy(s_propertyEditorType, "ClearSerializedLockedObjects");
                }

                if (s_propertyEditorType != null)
                {
                    s_allPropertyEditorsField = s_propertyEditorType.GetField("m_AllPropertyEditors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (s_inspectorWindowType != null)
                {
                    s_isLockedProp = FindPropertyInHierarchy(s_inspectorWindowType, "isLocked");
                    s_allInspectorsField = s_inspectorWindowType.GetField("m_AllInspectors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }

                var logEntriesType = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntriesType != null)
                {
                    s_clearConsoleMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Exception initializing reflection metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Purges all open Inspector/PropertyEditor windows: unlocks them, clears serialized locked objects,
        /// destroys tracker active editors, clears UI elements, and requests repaint.
        /// Also purges the shared tracker and destroys orphan/scene editors.
        /// </summary>
        public void PurgeAllInspectors()
        {
            try
            {
                var windows = GetInspectorWindows();
                for (int i = 0; i < windows.Count; i++)
                {
                    var win = windows[i];
                    if (win == null)
                    {
                        continue;
                    }

                    try
                    {
                        // 1. Force unlock if locked (safely check declaring type to prevent TargetException)
                        if (s_isLockedProp != null && s_isLockedProp.DeclaringType != null && s_isLockedProp.DeclaringType.IsInstanceOfType(win))
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                            }
                        }

                        // 2. Clear locked serialized objects (safely check declaring type to prevent TargetException)
                        if (s_clearSerializedLockedObjectsMethod != null && s_clearSerializedLockedObjectsMethod.DeclaringType != null && s_clearSerializedLockedObjectsMethod.DeclaringType.IsInstanceOfType(win))
                        {
                            s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                        }

                        // 3. Purge tracker active editors without calling tracker.Destroy()
                        var tracker = GetTracker(win);
                        if (tracker != null)
                        {
                            DestroyTrackerEditors(tracker);
                        }

                        // 4. Clear UI elements & rebuild containers (safely check declaring type to prevent TargetException)
                        if (s_clearEditorsAndRebuildMethod != null && s_clearEditorsAndRebuildMethod.DeclaringType != null && s_clearEditorsAndRebuildMethod.DeclaringType.IsInstanceOfType(win))
                        {
                            s_clearEditorsAndRebuildMethod.Invoke(win, null);
                        }

                        // 5. Repaint window
                        win.Repaint();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[InspectorSanitizerService] Error purging inspector window: {ex.Message}");
                    }
                }

                // Purge shared tracker and all orphan/scene editors
                PurgeSharedTracker();
                DestroyOrphanAndSceneEditors();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error during PurgeAllInspectors: {ex.Message}");
            }
        }

        /// <summary>
        /// Purges the engine-wide ActiveEditorTracker.sharedTracker by destroying its active editors,
        /// clearing dirty state, and forcing rebuild against an empty selection without calling native Destroy().
        /// </summary>
        public void PurgeSharedTracker()
        {
            try
            {
                var shared = ActiveEditorTracker.sharedTracker;
                if (shared == null)
                {
                    return;
                }

                DestroyTrackerEditors(shared);
                shared.ForceRebuild();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error purging shared tracker: {ex.Message}");
            }
        }

        /// <summary>
        /// Identifies and destroys all Editor ScriptableObjects in memory whose target is null or non-persistent (scene objects),
        /// preventing them from surviving across scene unload and domain reload.
        /// </summary>
        public void DestroyOrphanAndSceneEditors()
        {
            try
            {
                var allEditors = Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor));
                if (allEditors == null || allEditors.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < allEditors.Length; i++)
                {
                    var ed = allEditors[i] as UnityEditor.Editor;
                    if (ed == null)
                    {
                        continue;
                    }

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
                            if (targets != null && targets.Length > 0)
                            {
                                for (int j = 0; j < targets.Length; j++)
                                {
                                    var t = targets[j];
                                    if (t == null || !EditorUtility.IsPersistent(t))
                                    {
                                        shouldDestroy = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Any exception accessing target/targets implies corrupted state or dead native reference
                        shouldDestroy = true;
                    }

                    if (shouldDestroy)
                    {
                        try
                        {
                            Object.DestroyImmediate(ed);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[InspectorSanitizerService] Failed destroying editor: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error during DestroyOrphanAndSceneEditors: {ex.Message}");
            }
        }

        /// <summary>
        /// Unlocks all open Inspector windows and clears their serialized locked objects buffer.
        /// </summary>
        public void UnlockAllInspectors()
        {
            try
            {
                var windows = GetInspectorWindows();
                for (int i = 0; i < windows.Count; i++)
                {
                    var win = windows[i];
                    if (win == null)
                    {
                        continue;
                    }

                    try
                    {
                        if (s_isLockedProp != null && s_isLockedProp.DeclaringType != null && s_isLockedProp.DeclaringType.IsInstanceOfType(win))
                        {
                            var isLockedVal = s_isLockedProp.GetValue(win);
                            if (isLockedVal is bool isLocked && isLocked)
                            {
                                s_isLockedProp.SetValue(win, BoxedFalse);
                                if (s_clearSerializedLockedObjectsMethod != null && s_clearSerializedLockedObjectsMethod.DeclaringType != null && s_clearSerializedLockedObjectsMethod.DeclaringType.IsInstanceOfType(win))
                                {
                                    s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                                }
                                win.Repaint();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[InspectorSanitizerService] Error unlocking inspector: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error during UnlockAllInspectors: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the Unity Editor Console.
        /// </summary>
        public void ClearConsole()
        {
            try
            {
                if (s_clearConsoleMethod != null)
                {
                    s_clearConsoleMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] ClearConsole encountered an exception: {ex.Message}");
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

        private static void DestroyTrackerEditors(ActiveEditorTracker tracker)
        {
            if (tracker == null)
            {
                return;
            }

            try
            {
                var editors = tracker.activeEditors;
                if (editors != null && editors.Length > 0)
                {
                    for (int i = 0; i < editors.Length; i++)
                    {
                        var ed = editors[i];
                        if (ed != null)
                        {
                            try
                            {
                                Object.DestroyImmediate(ed);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning($"[InspectorSanitizerService] Failed destroying tracker editor: {ex.Message}");
                            }
                        }
                    }
                }

                tracker.ClearDirty();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error destroying tracker editors: {ex.Message}");
            }
        }

        private static ActiveEditorTracker GetTracker(EditorWindow window)
        {
            if (window == null)
            {
                return null;
            }

            try
            {
                // 1. Try cached PropertyInfo
                if (s_trackerProp != null && s_trackerProp.DeclaringType != null && s_trackerProp.DeclaringType.IsInstanceOfType(window))
                {
                    var tracker = s_trackerProp.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }

                // 2. Try cached FieldInfo
                if (s_trackerField != null && s_trackerField.DeclaringType != null && s_trackerField.DeclaringType.IsInstanceOfType(window))
                {
                    var tracker = s_trackerField.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null)
                    {
                        return tracker;
                    }
                }

                // 3. Fallback: traverse runtime hierarchy
                var currentType = window.GetType();
                while (currentType != null && currentType != typeof(EditorWindow))
                {
                    var prop = currentType.GetProperty("tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        var val = prop.GetValue(window) as ActiveEditorTracker;
                        if (val != null)
                        {
                            return val;
                        }
                    }

                    var field = currentType.GetField("m_Tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var val = field.GetValue(window) as ActiveEditorTracker;
                        if (val != null)
                        {
                            return val;
                        }
                    }

                    currentType = currentType.BaseType;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error resolving tracker on window: {ex.Message}");
            }

            return null;
        }

        private static List<EditorWindow> GetInspectorWindows()
        {
            var result = new List<EditorWindow>();

            // 1. Static list m_AllInspectors
            if (s_allInspectorsField != null)
            {
                try
                {
                    if (s_allInspectorsField.GetValue(null) is IList inspectors && inspectors.Count > 0)
                    {
                        for (int i = 0; i < inspectors.Count; i++)
                        {
                            if (inspectors[i] is EditorWindow ew && ew != null && !result.Contains(ew))
                            {
                                result.Add(ew);
                            }
                        }
                        if (result.Count > 0)
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InspectorSanitizerService] Error accessing m_AllInspectors: {ex.Message}");
                }
            }

            // 2. Static list m_AllPropertyEditors
            if (s_allPropertyEditorsField != null)
            {
                try
                {
                    if (s_allPropertyEditorsField.GetValue(null) is IList propEditors && propEditors.Count > 0)
                    {
                        for (int i = 0; i < propEditors.Count; i++)
                        {
                            if (propEditors[i] is EditorWindow ew && ew != null && !result.Contains(ew))
                            {
                                result.Add(ew);
                            }
                        }
                        if (result.Count > 0)
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InspectorSanitizerService] Error accessing m_AllPropertyEditors: {ex.Message}");
                }
            }

            // 3. Fallback via Resources.FindObjectsOfTypeAll (query base PropertyEditor to capture all derived InspectorWindow instances)
            var targetType = s_propertyEditorType ?? s_inspectorWindowType;
            if (targetType != null)
            {
                try
                {
                    var objs = Resources.FindObjectsOfTypeAll(targetType);
                    if (objs != null && objs.Length > 0)
                    {
                        for (int i = 0; i < objs.Length; i++)
                        {
                            if (objs[i] is EditorWindow ew && ew != null && !result.Contains(ew))
                            {
                                result.Add(ew);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InspectorSanitizerService] Error querying Resources.FindObjectsOfTypeAll: {ex.Message}");
                }
            }

            return result;
        }

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

        #region IPlayModeTransitionSubscriber Implementation

        /// <summary>
        /// Executes at priority 200 (after SelectionMementoService at 100 has captured and cleared selection).
        /// </summary>
        public int ExecutionOrder => PlayModeExecutionOrder.InspectorSanitizer;

        public void OnExitingEditMode()
        {
            PurgeAllInspectors();
        }

        public void OnEnteredPlayMode()
        {
            // No-op: editors rebuild naturally as objects are selected/restored
        }

        public void OnExitingPlayMode()
        {
            PurgeAllInspectors();
        }

        public void OnEnteredEditMode()
        {
            // No-op: editors rebuild naturally as objects are selected/restored
        }

        public void OnBeforeAssemblyReload()
        {
            PurgeAllInspectors();
        }

        public void OnAfterAssemblyReload()
        {
            // No-op
        }

        public void OnSceneClosing(Scene scene)
        {
            PurgeAllInspectors();
        }

        #endregion
    }
}
#endif
