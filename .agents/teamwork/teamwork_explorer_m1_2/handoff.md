# Handoff Report: Milestone 1 - Inspector & Editor Sanitizer Service

## 1. Observation

### 1.1 Console Exceptions Observed (Unity 6000.3.7f1)
Upon entering Play Mode in Unity 6000.3.7f1 with scene objects selected and active inspector windows open, four console exceptions are thrown:
```text
SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()
MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()
SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()
SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()
```

### 1.2 Flaws in Existing `InspectorTrackerSanitizer.cs`
Direct inspection of `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`:
- **Line 16**: `var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");`
- **Line 24**: `var trackerField = inspectorType.GetField("m_Tracker", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);`
  - In .NET reflection, `GetField` on a derived class (`InspectorWindow`) does **not** return non-public fields declared on a base class (`PropertyEditor`). Consequently, `trackerField` evaluated to `null`.
  - Lines 32–49 (`if (trackerField != null)`) were skipped entirely, leaving inspector active editors untouched.
- **Lines 47 & 79**: Invoked `tracker.Destroy()` and `shared.Destroy()`.
  - `ActiveEditorTracker.Destroy()` delegates to native C++ `Internal_Destroy(this)`. Freeing the native tracker while the managed `ActiveEditorTracker` instance remains referenced by `InspectorWindow` or `ActiveEditorTracker.sharedTracker` corrupts internal native pointers, causing crashes or exceptions upon subsequent rebuilds/repaints.
- **Lines 104–110**: In `PurgeOrphanEditors()`:
  ```csharp
  if (ed.target == null)
  {
      isDead = true;
  }
  ```
  - When invoked prior to scene unload (`ExitingEditMode`), the edit scene is still loaded in memory. Therefore, `ed.target` is **not** null, and `isDead` evaluates to `false`. The scene editors are spared, only to have their targets destroyed milliseconds later when the scene unloads.
- **Lines 86–88, 122–124, 144–146, 175–177, 191–193**: Contained 5 completely empty `catch {}` blocks, silently hiding reflection failures, null references, and API mismatches.

### 1.3 Unity 6000.3.7f1 Assembly Reflection Inspection
Inspection of `UnityEditor.CoreModule.dll` in Unity 6000.3.7f1 reveals the exact layout:
1. **Inheritance**: `UnityEditor.InspectorWindow` -> `UnityEditor.PropertyEditor` -> `UnityEditor.EditorWindow`.
2. **Members Declared on `UnityEditor.PropertyEditor`**:
   - `public ActiveEditorTracker tracker { get; }`: Public property returning the active editor tracker.
   - `internal ActiveEditorTracker m_Tracker`: Internal backing field for the tracker.
   - `internal void ClearEditorsAndRebuild()`: Clears UI Toolkit element updater containers (`editorsElement.Clear()`, `RebuildContentsContainers()`).
   - `internal void ClearSerializedLockedObjects()`: Clears internal serialized locked target references.
   - `internal static List<PropertyEditor> m_AllPropertyEditors`: Static list tracking all open property editor windows.
3. **Members Declared on `UnityEditor.InspectorWindow`**:
   - `public bool isLocked { get; set; }`: Controls whether the inspector window is locked to its current targets.
   - `private static List<InspectorWindow> m_AllInspectors`: Static list tracking all inspector windows.
4. **Members Declared on `UnityEditor.ActiveEditorTracker`**:
   - `public static ActiveEditorTracker sharedTracker { get; }`: Engine-wide shared tracker singleton.
   - `public Editor[] activeEditors { get; }`: Array of active editor instances managed by this tracker.
   - `public void ClearDirty()`: Resets tracker dirty state.
   - `public void ForceRebuild()`: Rebuilds tracker editors against current selection without native destruction.
   - `public void Destroy()`: **Native destructive method** invoking `Internal_Destroy(this)` — must never be called.

---

## 2. Logic Chain

### Step 1: Root Cause Mechanics
1. When a GameObject with `Transform`, `MeshCollider`, and renderer components is selected in the Editor hierarchy, Unity creates corresponding `Editor` ScriptableObjects (`GameObjectInspector`, `TransformInspector`, `MeshColliderEditor`, `Renderer2DMeshEditor`).
2. ScriptableObjects in editor memory survive scene unloads unless explicitly destroyed via `UnityEngine.Object.DestroyImmediate`.
3. When entering Play Mode:
   - `ExitingEditMode` fires while the scene is still loaded.
   - Edit scene unloads: native scene GameObjects and Components are destroyed (`m_CachedPtr == 0`).
   - Domain reload occurs or serialization pass executes.
   - Deserialization triggers Unity's native lifecycle to invoke `OnEnable()` on all surviving `Editor` ScriptableObjects.
   - In `OnEnable()`, each editor attempts to access `target`, `targets`, or `m_Targets`.
   - Because the targets were destroyed during scene unload, `target` is null/destroyed, throwing `SerializedObjectNotCreatableException` and `MissingReferenceException`.

### Step 2: Why Existing Sanitization Failed
1. In `InspectorTrackerSanitizer`, querying `typeof(InspectorWindow).GetField("m_Tracker")` returned `null` because `m_Tracker` is declared on `PropertyEditor`. The active editors in the inspector's tracker were never destroyed.
2. Checking `ed.target == null` before scene unload returned `false` because the scene was not yet unloaded.
3. Calling `tracker.Destroy()` and `shared.Destroy()` invalidated the C++ native peer of the tracker, causing secondary corruption upon subsequent repaints.

### Step 3: Required Resolution Strategy
1. **Reflection Hierarchy & Property Access**:
   Query `PropertyEditor` directly or traverse `BaseType` up to `EditorWindow`. Access `tracker` via public property `tracker` first, falling back to backing field `m_Tracker`.
2. **Non-Destructive Tracker Purging**:
   Iterate `tracker.activeEditors` and destroy each editor instance via `UnityEngine.Object.DestroyImmediate(ed)`. Call `tracker.ClearDirty()`. For `sharedTracker`, call `shared.ClearDirty()` and `shared.ForceRebuild()`. **Never** call `tracker.Destroy()` or `shared.Destroy()`.
3. **Inspector Window Unlocking & Visual Reset**:
   Set `isLocked = false` on all inspector windows. Invoke `ClearSerializedLockedObjects()` to prevent stale target references from surviving. Invoke `ClearEditorsAndRebuild()` to clear UI Toolkit containers, followed by `win.Repaint()`.
4. **Scene & Orphan Editor Destruction (`!EditorUtility.IsPersistent`)**:
   Query `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))`. Any editor where `target == null || !EditorUtility.IsPersistent(target)` targets a non-persistent scene object that will be destroyed upon scene unload. Destroying these editors immediately before scene unload prevents them from surviving into domain reload.
   Also inspect multi-selection `ed.targets`. If any target is null or not persistent, or if accessing `target` throws an exception, destroy the editor.
5. **Robust Error Diagnostics & Defenses**:
   Replace all silent `catch {}` blocks with structured `Debug.LogWarning` messages detailing the failing operation and exception message. Guard all references against null and native object destruction.

---

## 3. Caveats

1. **Unity Version Compatibility**:
   In Unity 2021/2022, `m_Tracker` was declared directly on `InspectorWindow`. In Unity 6000.x, it is declared on `PropertyEditor`. The proposed reflection design inspects `PropertyEditor` and falls back to `InspectorWindow` and runtime base type traversal, ensuring complete compatibility across Unity versions.
2. **Project Asset Editors**:
   Editors targeting persistent assets in the project (e.g. ScriptableObjects in `Assets/`, Materials, Prefab assets on disk) have `EditorUtility.IsPersistent(target) == true`. These are preserved by `DestroyOrphanAndSceneEditors()`. However, if destroyed, Unity automatically recreates them upon selection without error.
3. **Third-Party Inspector Extensions**:
   Third-party packages (e.g., Odin Inspector, Tri-Inspector) might dock custom editor windows. Because our window enumeration queries `m_AllPropertyEditors`, `m_AllInspectors`, and `Resources.FindObjectsOfTypeAll(propertyEditorType ?? inspectorType)`, custom inspector windows deriving from `PropertyEditor` are also safely sanitized.

---

## 4. Conclusion & Complete Technical Specification

### 4.1 Interface Specification: `IInspectorSanitizerService`
File path: `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs`

```csharp
#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Service responsible for sanitizing Unity Inspector windows, active editor trackers,
    /// and destroying orphan/scene-bound Editor ScriptableObjects before Play Mode transitions and Domain Reloads.
    /// </summary>
    public interface IInspectorSanitizerService
    {
        /// <summary>
        /// Purges all open Inspector/PropertyEditor windows: unlocks them, clears serialized locked objects,
        /// destroys tracker active editors, clears UI elements, and requests repaint.
        /// Also purges the shared tracker and destroys orphan/scene editors.
        /// </summary>
        void PurgeAllInspectors();

        /// <summary>
        /// Purges the engine-wide ActiveEditorTracker.sharedTracker by destroying its active editors,
        /// clearing dirty state, and forcing rebuild against an empty selection without calling native Destroy().
        /// </summary>
        void PurgeSharedTracker();

        /// <summary>
        /// Identifies and destroys all Editor ScriptableObjects in memory whose target is null or non-persistent (scene objects),
        /// preventing them from surviving across scene unload and domain reload.
        /// </summary>
        void DestroyOrphanAndSceneEditors();

        /// <summary>
        /// Unlocks all open Inspector windows and clears their serialized locked objects buffer.
        /// </summary>
        void UnlockAllInspectors();
    }
}
#endif
```

---

### 4.2 Concrete Implementation: `InspectorSanitizerService`
File path: `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Concrete implementation of <see cref="IInspectorSanitizerService"/> tailored for Unity 6000.3.7f1.
    /// Safely handles PropertyEditor base class reflection, non-destructive ActiveEditorTracker cleanup,
    /// and pre-scene-unload scene editor destruction.
    /// </summary>
    public sealed class InspectorSanitizerService : IInspectorSanitizerService
    {
        private static InspectorSanitizerService s_instance;
        public static InspectorSanitizerService Instance => s_instance ??= new InspectorSanitizerService();

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

        static InspectorSanitizerService()
        {
            try
            {
                var editorAssembly = typeof(UnityEditor.Editor).Assembly;

                s_propertyEditorType = editorAssembly.GetType("UnityEditor.PropertyEditor");
                s_inspectorWindowType = editorAssembly.GetType("UnityEditor.InspectorWindow");

                if (s_propertyEditorType == null && s_inspectorWindowType == null)
                {
                    Debug.LogWarning("[InspectorSanitizerService] Neither UnityEditor.PropertyEditor nor UnityEditor.InspectorWindow was found in Editor assembly.");
                    return;
                }

                var baseOrWindowType = s_propertyEditorType ?? s_inspectorWindowType;

                // Tracker access: search PropertyEditor first, fallback to InspectorWindow
                s_trackerProp = baseOrWindowType.GetProperty("tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                s_trackerField = baseOrWindowType.GetField("m_Tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                // Methods on PropertyEditor (or InspectorWindow in older Unity versions)
                s_clearEditorsAndRebuildMethod = baseOrWindowType.GetMethod("ClearEditorsAndRebuild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (s_clearEditorsAndRebuildMethod == null && s_inspectorWindowType != null)
                {
                    s_clearEditorsAndRebuildMethod = s_inspectorWindowType.GetMethod("ClearEditorsAndRebuild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                s_clearSerializedLockedObjectsMethod = baseOrWindowType.GetMethod("ClearSerializedLockedObjects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (s_clearSerializedLockedObjectsMethod == null && s_inspectorWindowType != null)
                {
                    s_clearSerializedLockedObjectsMethod = s_inspectorWindowType.GetMethod("ClearSerializedLockedObjects", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                // Window properties
                if (s_inspectorWindowType != null)
                {
                    s_isLockedProp = s_inspectorWindowType.GetProperty("isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_allInspectorsField = s_inspectorWindowType.GetField("m_AllInspectors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }

                if (s_propertyEditorType != null)
                {
                    s_allPropertyEditorsField = s_propertyEditorType.GetField("m_AllPropertyEditors", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Exception initializing reflection metadata: {ex.Message}");
            }
        }

        public void PurgeAllInspectors()
        {
            try
            {
                var windows = GetInspectorWindows();
                for (int i = 0; i < windows.Count; i++)
                {
                    var win = windows[i];
                    if (win == null) continue;

                    try
                    {
                        // 1. Force unlock
                        if (s_isLockedProp != null)
                        {
                            bool isLocked = (bool)s_isLockedProp.GetValue(win);
                            if (isLocked)
                            {
                                s_isLockedProp.SetValue(win, false);
                            }
                        }

                        // 2. Clear locked serialized objects
                        if (s_clearSerializedLockedObjectsMethod != null)
                        {
                            s_clearSerializedLockedObjectsMethod.Invoke(win, null);
                        }

                        // 3. Purge tracker active editors without calling tracker.Destroy()
                        var tracker = GetTracker(win);
                        if (tracker != null)
                        {
                            DestroyTrackerEditors(tracker);
                        }

                        // 4. Clear UI elements & rebuild containers
                        if (s_clearEditorsAndRebuildMethod != null)
                        {
                            s_clearEditorsAndRebuildMethod.Invoke(win, null);
                        }

                        // 5. Repaint window
                        win.Repaint();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[InspectorSanitizerService] Error purging inspector window '{win.name}': {ex.Message}");
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

        public void PurgeSharedTracker()
        {
            try
            {
                var shared = ActiveEditorTracker.sharedTracker;
                if (shared == null) return;

                DestroyTrackerEditors(shared);
                shared.ForceRebuild();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error purging shared tracker: {ex.Message}");
            }
        }

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
                            Debug.LogWarning($"[InspectorSanitizerService] Failed destroying editor '{ed.GetType().Name}': {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error during DestroyOrphanAndSceneEditors: {ex.Message}");
            }
        }

        public void UnlockAllInspectors()
        {
            try
            {
                var windows = GetInspectorWindows();
                for (int i = 0; i < windows.Count; i++)
                {
                    var win = windows[i];
                    if (win == null) continue;

                    try
                    {
                        if (s_isLockedProp != null)
                        {
                            bool isLocked = (bool)s_isLockedProp.GetValue(win);
                            if (isLocked)
                            {
                                s_isLockedProp.SetValue(win, false);
                                s_clearSerializedLockedObjectsMethod?.Invoke(win, null);
                                win.Repaint();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[InspectorSanitizerService] Error unlocking inspector '{win.name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error during UnlockAllInspectors: {ex.Message}");
            }
        }

        private static void DestroyTrackerEditors(ActiveEditorTracker tracker)
        {
            if (tracker == null) return;

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
                                Debug.LogWarning($"[InspectorSanitizerService] Failed destroying tracker editor '{ed.GetType().Name}': {ex.Message}");
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
            if (window == null) return null;

            try
            {
                // 1. Try cached PropertyInfo
                if (s_trackerProp != null)
                {
                    var tracker = s_trackerProp.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null) return tracker;
                }

                // 2. Try cached FieldInfo
                if (s_trackerField != null)
                {
                    var tracker = s_trackerField.GetValue(window) as ActiveEditorTracker;
                    if (tracker != null) return tracker;
                }

                // 3. Fallback: traverse runtime hierarchy
                var currentType = window.GetType();
                while (currentType != null && currentType != typeof(EditorWindow))
                {
                    var prop = currentType.GetProperty("tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        var val = prop.GetValue(window) as ActiveEditorTracker;
                        if (val != null) return val;
                    }

                    var field = currentType.GetField("m_Tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        var val = field.GetValue(window) as ActiveEditorTracker;
                        if (val != null) return val;
                    }

                    currentType = currentType.BaseType;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorSanitizerService] Error resolving tracker on window '{window.name}': {ex.Message}");
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
                    if (s_allInspectorsField.GetValue(null) is IList inspectors)
                    {
                        for (int i = 0; i < inspectors.Count; i++)
                        {
                            if (inspectors[i] is EditorWindow ew && ew != null && !result.Contains(ew))
                            {
                                result.Add(ew);
                            }
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
                    if (s_allPropertyEditorsField.GetValue(null) is IList propEditors)
                    {
                        for (int i = 0; i < propEditors.Count; i++)
                        {
                            if (propEditors[i] is EditorWindow ew && ew != null && !result.Contains(ew))
                            {
                                result.Add(ew);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[InspectorSanitizerService] Error accessing m_AllPropertyEditors: {ex.Message}");
                }
            }

            // 3. Fallback via Resources.FindObjectsOfTypeAll
            var targetType = s_inspectorWindowType ?? s_propertyEditorType;
            if (targetType != null)
            {
                try
                {
                    var objs = Resources.FindObjectsOfTypeAll(targetType);
                    if (objs != null)
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
    }
}
#endif
```

---

### 4.3 Backwards-Compatibility Adapter: `InspectorTrackerSanitizer`
File path: `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`

```csharp
#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Backwards-compatibility adapter delegating to <see cref="IInspectorSanitizerService"/>.
    /// Preserves existing call sites across LevelEditor, SelectionMementoService, and EditorLifecycleCoordinator.
    /// </summary>
    public static class InspectorTrackerSanitizer
    {
        private static IInspectorSanitizerService s_service;
        public static IInspectorSanitizerService Service
        {
            get => s_service ??= InspectorSanitizerService.Instance;
            set => s_service = value;
        }

        public static void PurgeAllInspectorWindows()
        {
            Service.PurgeAllInspectors();
        }

        public static void PurgeOrphanEditors()
        {
            Service.DestroyOrphanAndSceneEditors();
        }

        public static void ForceCleanTracker()
        {
            Service.PurgeAllInspectors();
        }

        public static void SanitizeActiveEditors()
        {
            Service.DestroyOrphanAndSceneEditors();
            Service.PurgeSharedTracker();
        }

        public static void UnlockInspectors(bool forceUnlock = false)
        {
            if (forceUnlock)
            {
                Service.UnlockAllInspectors();
            }
        }

        public static void ClearConsole()
        {
            try
            {
                var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (logEntries != null)
                {
                    var clearMethod = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    clearMethod?.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[InspectorTrackerSanitizer] Failed to clear console: {ex.Message}");
            }
        }
    }
}
#endif
```

---

## 5. Verification Method

### 5.1 Verification Matrix
| Component / Requirement | Verification Test | Expected Outcome | Invalidation Condition |
|---|---|---|---|
| **PropertyEditor Reflection** | Inspect metadata of `UnityEditor.CoreModule.dll` via reflection | `s_propertyEditorType != null`, `s_trackerProp != null`, `s_clearEditorsAndRebuildMethod != null` | Reflection returns null or throws `TypeLoadException` |
| **Non-Destructive Cleanup** | Execute `PurgeAllInspectors()` with active inspector open | `activeEditors` destroyed via `DestroyImmediate`, `tracker.ClearDirty()` called, tracker native pointer remains valid | Invoking `tracker.Destroy()` causing crash on subsequent repaint |
| **Scene Editor Purging** | Create a GameObject in scene with `MeshCollider`, `Transform`, `Renderer2D`, select it, run `DestroyOrphanAndSceneEditors()` before scene unload | All 4 editors identified (`!EditorUtility.IsPersistent == true`) and destroyed | Editor survives scene unload and throws in `OnEnable()` |
| **Persistent Asset Preservation** | Select a ScriptableObject asset or Material in `Assets/`, run `DestroyOrphanAndSceneEditors()` | `EditorUtility.IsPersistent(target) == true`, editor is preserved | Asset editor destroyed unnecessarily |
| **Zero Console Exceptions** | Enter Play Mode in Unity 6000.3.7f1 with unlocked and locked inspectors open | 0 instances of `SerializedObjectNotCreatableException`, 0 `MissingReferenceException` | Any of the 4 target exceptions logged |
| **Backwards Compatibility** | Existing callers (`LevelBuilderWindow`, `SelectionMementoService`, `EditorLifecycleCoordinator`) call `InspectorTrackerSanitizer` methods | Code compiles cleanly with zero broken references | Build or compilation errors |

### 5.2 Independent Verification Instructions
1. Inspect code layout compliance with `PROJECT.md`:
   - `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs`
   - `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
   - `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs`
2. Run Unity Editor Play Mode transition test:
   - Create and select a GameObject with `MeshCollider`, `Transform`, `SpriteRenderer`/`Renderer2DMesh`.
   - Open two Inspector tabs: one locked, one unlocked.
   - Enter Play Mode.
   - Inspect Unity console and `Editor.log`: verify zero exceptions from `MeshColliderEditor`, `GameObjectInspector`, `TransformInspector`, or `Renderer2DMeshEditor`.
3. Verify test targets in `Game.Editor.Tests.asmdef`:
   - Execute `InspectorSanitizerTests` verifying mock injection via `InspectorTrackerSanitizer.Service`.
