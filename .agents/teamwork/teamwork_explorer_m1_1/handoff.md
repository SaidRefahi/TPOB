# Handoff Report: Milestone 1 - Inspector & Editor Sanitizer Service

## 1. Observation

### 1.1 Existing Source Code & Call Sites
Direct examination of `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs` (lines 1-198) revealed:
- **`PurgeAllInspectorWindows()`** (lines 11-89):
  - Line 16: Queries `typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow")`.
  - Line 24: Queries `inspectorType.GetField("m_Tracker", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)`.
  - Line 47: Invokes `tracker.Destroy()` on the inspector's `ActiveEditorTracker`.
  - Lines 79-80: Calls `shared.Destroy()` followed by `shared.ForceRebuild()` on `ActiveEditorTracker.sharedTracker`.
  - Lines 86-88: Swallows all exceptions in empty `catch {}` blocks.
- **`PurgeOrphanEditors()`** (lines 91-125):
  - Lines 104-114: Checks only `if (ed.target == null) isDead = true;` using `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))`.
- **`ForceCleanTracker()`** (lines 127-130):
  - Calls `PurgeAllInspectorWindows()`.
- **`SanitizeActiveEditors()`** (lines 132-147):
  - Calls `PurgeOrphanEditors()`, then `ActiveEditorTracker.sharedTracker.ForceRebuild()`.
- **`UnlockInspectors(bool forceUnlock = false)`** (lines 149-178):
  - Lines 167-172: Only sets `isLockedProp.SetValue(win, false)` when `forceUnlock == true`.
- **`ClearConsole()`** (lines 180-194):
  - Reflects `UnityEditor.LogEntries` to invoke `Clear()`.

Call sites of `InspectorTrackerSanitizer` across the repository:
1. `Assets/_Project/Editor/Core/Lifecycle/EditorLifecycleCoordinator.cs:135`:
   `InspectorTrackerSanitizer.SanitizeActiveEditors();`
2. `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs:68`:
   `InspectorTrackerSanitizer.ForceCleanTracker();`
3. `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs:113`:
   `InspectorTrackerSanitizer.SanitizeActiveEditors();`
4. `Assets/_Project/Editor/Core/Lifecycle/SelectionMementoService.cs:146`:
   `InspectorTrackerSanitizer.PurgeAllInspectorWindows();`
5. `Assets/_Project/Editor/LevelEditor/LevelBuilderWindow.cs:115`:
   `Game.Editor.Core.Lifecycle.InspectorTrackerSanitizer.ClearConsole();`
6. `Assets/_Project/Editor/LevelEditor/InspectorSanitizer.cs:10-22`:
   - Line 10: `InspectorTrackerSanitizer.SanitizeActiveEditors();`
   - Line 11: `InspectorTrackerSanitizer.UnlockInspectors(forceUnlock: false);`
   - Line 16: `InspectorTrackerSanitizer.ClearConsole();`
   - Line 22: `InspectorTrackerSanitizer.ClearConsole();`

### 1.2 Unity 6000.3.7f1 Internal Type Hierarchy & Reflection Metadata
From Survey 2 analysis of `UnityEditor.CoreModule.dll`:
1. `UnityEditor.InspectorWindow` inherits from `UnityEditor.PropertyEditor`, which inherits from `UnityEditor.EditorWindow`.
2. Property and field declarations:
   - `PropertyEditor` declares `public ActiveEditorTracker tracker { get; }` and internal backing field `internal ActiveEditorTracker m_Tracker`.
   - `PropertyEditor` declares `internal void ClearEditorsAndRebuild()`.
   - `PropertyEditor` declares `internal void ClearSerializedLockedObjects()`.
   - `PropertyEditor` declares `internal static List<PropertyEditor> m_AllPropertyEditors`.
   - `InspectorWindow` declares `public bool isLocked { get; set; }`.
   - `InspectorWindow` declares `private static List<InspectorWindow> m_AllInspectors`.
3. In `UnityEditor.ActiveEditorTracker`:
   - `ActiveEditorTracker.Destroy()` delegates to native C++ `Internal_Destroy(this)`. Freeing this native object invalidates internal handles, causing subsequent engine calls to crash or access destroyed memory.
   - `ActiveEditorTracker.sharedTracker` is the persistent engine-wide singleton tracker. Calling `Destroy()` on it corrupts engine editor state.

---

## 2. Logic Chain

### Step 1: Interface Contract Definition (`IInspectorSanitizerService`)
1. In `PROJECT.md` (lines 68-74), `IInspectorSanitizerService` requires four core methods:
   - `PurgeAllInspectors()`
   - `PurgeSharedTracker()`
   - `DestroyOrphanAndSceneEditors()`
   - `UnlockAllInspectors()`
2. Separating these into distinct interface methods satisfies the Single Responsibility Principle:
   - Window lifecycle & UI Toolkit rebuild (`PurgeAllInspectors`)
   - Shared singleton tracker sanitation (`PurgeSharedTracker`)
   - ScriptableObject editor memory purging (`DestroyOrphanAndSceneEditors`)
   - Window lock clearing (`UnlockAllInspectors`)

### Step 2: Implementation Mechanics (`InspectorSanitizerService`)
1. **Window Reflection Purge (`PurgeAllInspectors`)**:
   - Accesses open inspector windows through `m_AllInspectors` (fast static list) or `m_AllPropertyEditors`, falling back to `Resources.FindObjectsOfTypeAll(InspectorWindowType)`.
   - For each window:
     - Unlocks `isLocked` (if locked).
     - Invokes `ClearSerializedLockedObjects()` to wipe serialized locked targets.
     - Obtains `tracker` via public property `tracker` (or backing field `m_Tracker` on `PropertyEditor`).
     - Destroys each active editor ScriptableObject via `UnityEngine.Object.DestroyImmediate(editor)`.
     - Calls `tracker.ClearDirty()`. (Never calls `tracker.Destroy()`).
     - Invokes `ClearEditorsAndRebuild()` on the `PropertyEditor` to clear UI Toolkit element containers (`VisualElement.Clear`) and rebuild empty containers.
     - Calls `win.Repaint()`.
2. **Non-Destructive Shared Tracker Sanitation (`PurgeSharedTracker`)**:
   - Obtains `ActiveEditorTracker.sharedTracker`.
   - Destroys active editors via `UnityEngine.Object.DestroyImmediate(editor)`.
   - Resets dirty flag via `shared.ClearDirty()`.
   - Rebuilds tracker via `shared.ForceRebuild()`.
   - Avoids native memory destruction (`shared.Destroy()`).
3. **Scene and Orphan Editor Destruction (`DestroyOrphanAndSceneEditors`)**:
   - In `PurgeOrphanEditors()`, checking `ed.target == null` failed prior to scene unload because the edit scene was still loaded, leaving targets non-null.
   - By evaluating `ed.target == null || !EditorUtility.IsPersistent(ed.target)` (and checking `ed.targets` for multi-target editors), all editors referencing scene GameObjects and Components are identified and destroyed via `UnityEngine.Object.DestroyImmediate(ed)` before the edit scene unloads.
   - Any access throwing `MissingReferenceException` is caught and treated as `shouldDestroy = true`.
4. **Inspector Unlocking (`UnlockAllInspectors`)**:
   - Iterates open inspector windows, resets `isLocked = false`, clears serialized locked objects, and calls `win.Repaint()`.
5. **Reflection Caching**:
   - Reflection lookups for `PropertyEditor`, `InspectorWindow`, and their methods/fields are cached in a static nested class `ReflectionCache` with hierarchy climbing (`FindMethodInHierarchy`, `FindFieldInHierarchy`, `FindPropertyInHierarchy`) across `InspectorWindow -> PropertyEditor -> EditorWindow`.
   - This eliminates GC allocations during runtime invocation and shields the service against minor Unity patch layout changes.

### Step 3: Backwards Compatibility Delegation (`InspectorTrackerSanitizer`)
1. Existing callers rely on the static methods of `InspectorTrackerSanitizer`.
2. `InspectorTrackerSanitizer` is converted into a thin static facade delegating to `InspectorSanitizerService.Instance`:
   - `PurgeAllInspectorWindows()` delegates to:
     - `InspectorSanitizerService.Instance.PurgeAllInspectors();`
     - `InspectorSanitizerService.Instance.PurgeSharedTracker();`
     - `InspectorSanitizerService.Instance.DestroyOrphanAndSceneEditors();`
   - `PurgeOrphanEditors()` delegates to `InspectorSanitizerService.Instance.DestroyOrphanAndSceneEditors();`
   - `ForceCleanTracker()` delegates to `PurgeAllInspectorWindows();`
   - `SanitizeActiveEditors()` delegates to `InspectorSanitizerService.Instance.SanitizeActiveEditors();`
   - `UnlockInspectors(bool forceUnlock = false)` delegates to `if (forceUnlock) InspectorSanitizerService.Instance.UnlockAllInspectors();`
   - `UnlockAllInspectors()` delegates to `InspectorSanitizerService.Instance.UnlockAllInspectors();`
   - `ClearConsole()` delegates to `InspectorSanitizerService.Instance.ClearConsole();`
3. All existing call sites in `EditorLifecycleCoordinator.cs`, `SelectionMementoService.cs`, `LevelBuilderWindow.cs`, and `LevelEditor/InspectorSanitizer.cs` remain completely valid without requiring any code changes.

---

## 3. Caveats

1. **Unity Version Compatibility**:
   `UnityEditor.PropertyEditor` is present in Unity 6000.x. In older Unity versions (2021/2022), `m_Tracker` was directly in `InspectorWindow`. The hierarchy climbing mechanism (`FindFieldInHierarchy`, `FindMethodInHierarchy`) handles both configurations automatically.
2. **Third-Party Inspector Extensions**:
   Tools like Odin Inspector or Tri-Inspector may maintain internal custom editor lists. Destroying editors via `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))` catches all `Editor` ScriptableObjects in editor memory regardless of whether they were created by Unity or third-party extensions.
3. **Read-Only Explorer Constraint**:
   In accordance with the subagent directive, no changes have been committed to project source files. Proposed source files have been placed in `.agents/teamwork/teamwork_explorer_m1_1/` for implementation by the Milestone 1 implementer.

---

## 4. Conclusion & Design Specification

### 4.1 Target File Locations and Assembly Structure
- **Assembly Definition**: `Assets/_Project/Editor/Game.Editor.asmdef`
- **Root Namespace**: `Game.Editor`
- **Subsystem Namespace**: `Game.Editor.Core.Lifecycle`
- **Files**:
  1. `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs`
  2. `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`
  3. `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs` (Updated facade)

### 4.2 Exact Type & Method Signatures

#### `IInspectorSanitizerService`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public interface IInspectorSanitizerService
    {
        void PurgeAllInspectors();
        void PurgeSharedTracker();
        void DestroyOrphanAndSceneEditors();
        void UnlockAllInspectors();
    }
}
```

#### `InspectorSanitizerService`
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public sealed class InspectorSanitizerService : IInspectorSanitizerService
    {
        public static InspectorSanitizerService Instance { get; set; }

        public void PurgeAllInspectors();
        public void PurgeSharedTracker();
        public void DestroyOrphanAndSceneEditors();
        public void UnlockAllInspectors();

        // Convenience & utility methods for backwards-compatibility
        public void SanitizeActiveEditors();
        public void ClearConsole();
    }
}
```

#### `InspectorTrackerSanitizer` (Static Facade)
```csharp
namespace Game.Editor.Core.Lifecycle
{
    public static class InspectorTrackerSanitizer
    {
        public static void PurgeAllInspectorWindows();
        public static void PurgeOrphanEditors();
        public static void ForceCleanTracker();
        public static void SanitizeActiveEditors();
        public static void UnlockInspectors(bool forceUnlock = false);
        public static void UnlockAllInspectors();
        public static void ClearConsole();
    }
}
```

### 4.3 Proposed Implementation Artifacts
The complete, ready-to-use C# source files have been authored and placed in:
- `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_1\proposed_IInspectorSanitizerService.cs`
- `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_1\proposed_InspectorSanitizerService.cs`
- `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\teamwork_explorer_m1_1\proposed_InspectorTrackerSanitizer.cs`

---

## 5. Verification Method

### 5.1 Verification Commands and Steps for Implementer
1. **Source File Placement**:
   - Create directories:
     - `Assets/_Project/Editor/Core/Lifecycle/Interfaces/`
     - `Assets/_Project/Editor/Core/Lifecycle/Services/`
   - Copy `proposed_IInspectorSanitizerService.cs` to `Assets/_Project/Editor/Core/Lifecycle/Interfaces/IInspectorSanitizerService.cs`.
   - Copy `proposed_InspectorSanitizerService.cs` to `Assets/_Project/Editor/Core/Lifecycle/Services/InspectorSanitizerService.cs`.
   - Replace `Assets/_Project/Editor/Core/Lifecycle/InspectorTrackerSanitizer.cs` with `proposed_InspectorTrackerSanitizer.cs`.
2. **Compilation Verification**:
   - Unity recompile / IDE build must produce zero compilation errors.
   - Verify that all callers (`LevelEditor/InspectorSanitizer.cs`, `SelectionMementoService.cs`, `EditorLifecycleCoordinator.cs`, `LevelBuilderWindow.cs`) compile without errors or warnings.
3. **Reflection Sanity Check**:
   - Verify `ReflectionCache` locates `PropertyEditor`, `ClearEditorsAndRebuild`, and `ClearSerializedLockedObjects` non-null.
4. **Behavioral Invalidation Conditions**:
   - Any invocation of `tracker.Destroy()` or `shared.Destroy()` is an invalidation condition.
   - Any check of `ed.target == null` without `!EditorUtility.IsPersistent(ed.target)` is an invalidation condition.
