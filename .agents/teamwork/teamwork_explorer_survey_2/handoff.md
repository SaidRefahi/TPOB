# Handoff Report: Phase 0 Survey - Unity 6000.3.7f1 Inspector and Orphan Editor Exceptions

## 1. Observation

### 1.1 Verbatim Console Exceptions
The following four exceptions are logged upon entering Play Mode in Unity 6000.3.7f1:
1. `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
2. `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
3. `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
4. `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`

### 1.2 Inspection of Existing Lifecycle Codebase
Investigation of `Assets/_Project/Editor/Core/Lifecycle/`:
- `InspectorTrackerSanitizer.cs`:
  - Lines 16-25: Queried `typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow")`.
  - Line 24: Attempted reflection `inspectorType.GetField("m_Tracker", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)`.
  - Lines 34-48: Called `tracker.Destroy()` on the inspector's `ActiveEditorTracker`.
  - Lines 65-81: Accessed `ActiveEditorTracker.sharedTracker`, destroyed active editors, and called `shared.Destroy()` followed by `shared.ForceRebuild()`.
  - Lines 91-125: `PurgeOrphanEditors()` checked only `if (ed.target == null) isDead = true;` using `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))`.
- `EditorLifecycleCoordinator.cs`:
  - Lines 41-75: Listens to `EditorApplication.playModeStateChanged`. At `PlayModeStateChange.ExitingEditMode`, calls `subscriber.OnBeforePlayModeEntered()`.
  - Lines 82-95: Listens to `AssemblyReloadEvents.beforeAssemblyReload`.
- `SelectionMementoService.cs`:
  - Lines 64-69: `ClearSelectionSafely()` sets `Selection.objects = Array.Empty<Object>()` and `Selection.activeObject = null`, then invokes `InspectorTrackerSanitizer.ForceCleanTracker()`.
  - Lines 116-120: `OnBeforePlayModeEntered()` calls `CaptureSelection()` followed by `ClearSelectionSafely()`.
  - Lines 122-125: `OnPlayModeEntered()` registers `OnDeferredRestore` via `EditorApplication.delayCall`.

### 1.3 Unity 6000.3.7f1 Assembly Reflection Observations
Direct inspection of `UnityEditor.CoreModule.dll` for Unity 6000.3.7f1 (`C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Data\Managed\UnityEngine\UnityEditor.CoreModule.dll`):
1. **Type Hierarchy**:
   - `UnityEditor.InspectorWindow` inherits directly from `UnityEditor.PropertyEditor`, which inherits from `UnityEditor.EditorWindow`.
2. **Member Declaration Scope**:
   - `m_Tracker`: Declared on `UnityEditor.PropertyEditor` (`internal ActiveEditorTracker m_Tracker`).
   - `tracker`: Declared on `UnityEditor.PropertyEditor` as a public property: `public ActiveEditorTracker tracker { get; }`.
   - `ClearEditorsAndRebuild`: Declared on `UnityEditor.PropertyEditor` as `internal void ClearEditorsAndRebuild()`.
   - `ClearSerializedLockedObjects`: Declared on `UnityEditor.PropertyEditor` as `internal void ClearSerializedLockedObjects()`.
   - `isLocked`: Declared on `UnityEditor.InspectorWindow` as `public bool isLocked { get; set; }`.
   - `m_AllInspectors`: Declared on `UnityEditor.InspectorWindow` as `private static List<InspectorWindow> m_AllInspectors`.
   - `m_AllPropertyEditors`: Declared on `UnityEditor.PropertyEditor` as `internal static List<PropertyEditor> m_AllPropertyEditors`.
3. **Internal Behavior of `ClearEditorsAndRebuild`**:
   - IL inspection shows calls to: `PropertyEditor.get_editorsElement` -> `VisualElement.Clear` -> `EditorElementUpdater.Clear` -> `PropertyEditor.RebuildContentsContainers`.
   - It clears UI Toolkit containers and rebuilds them from the tracker; it does not destroy underlying `Editor` ScriptableObjects on its own.
4. **Behavior of `ActiveEditorTracker`**:
   - `ActiveEditorTracker.Destroy()` delegates to native `Internal_Destroy(this)`. Freeing the native tracker object makes subsequent calls to `ForceRebuild()` or inspector painting invoke invalid C++ native pointers.
   - `ActiveEditorTracker.sharedTracker` is the persistent engine-wide shared tracker singleton. Invoking `Destroy()` on `sharedTracker` corrupts editor-wide tracking state.

---

## 2. Logic Chain

### Step 1: Why the 4 Specific Exceptions Occur (Root Cause)
1. `UnityEditor.Editor` inherits from `UnityEngine.ScriptableObject`.
2. When a GameObject is selected in the Editor, `ActiveEditorTracker` instantiates `Editor` instances for the GameObject and each attached component:
   - GameObject -> `GameObjectInspector` (holds `m_Targets` array)
   - Transform -> `TransformInspector`
   - MeshCollider -> `MeshColliderEditor`
   - 2D Mesh/Sprite/Renderer -> `Renderer2DMeshEditor`
3. In Unity, `ScriptableObject` instances in Editor memory are tracked by the native engine object manager. Unless explicitly destroyed via `UnityEngine.Object.DestroyImmediate`, they survive across scene unload and are serialized across Domain Reload.
4. When entering Play Mode:
   - `PlayModeStateChange.ExitingEditMode` triggers.
   - The Edit Mode scene unloads. All Edit Mode scene GameObjects and Components are destroyed in native memory.
   - If Domain Reload is executed, or during Play Mode serialization, Unity reloads assemblies and deserializes all surviving `ScriptableObject`s.
   - Unity's native lifecycle calls `OnEnable()` on every deserialized `Editor`.
   - In `OnEnable()`, each editor attempts to initialize its `SerializedObject` from `target` / `targets`, or accesses `m_Targets`.
   - Because the target scene objects were destroyed when the scene unloaded, the `target` references are null or destroyed native pointers (`m_CachedPtr == 0`).
   - Consequently:
     - `MeshColliderEditor.OnEnable()` invokes `new SerializedObject(targets)` -> throws `SerializedObjectNotCreatableException: Object at index 0 is null`.
     - `GameObjectInspector.OnEnable()` accesses `m_Targets` -> throws `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore`.
     - `TransformInspector.OnEnable()` invokes `new SerializedObject(targets)` -> throws `SerializedObjectNotCreatableException: Object at index 0 is null`.
     - `Renderer2DMeshEditor.OnEnable()` invokes `new SerializedObject(targets)` -> throws `SerializedObjectNotCreatableException: Object at index 0 is null`.

### Step 2: Why the Existing `InspectorTrackerSanitizer` Failed
1. **Target Null Check Timing Flaw**:
   `PurgeOrphanEditors()` evaluated `if (ed.target == null) isDead = true;`. When called in `OnBeforePlayModeEntered()` (during `ExitingEditMode`), the Edit Scene is STILL LOADED. Therefore, `ed.target` is NOT null. The check evaluated to `false`, and none of the scene editors were destroyed. Then the scene unloaded, making the targets null *after* the purge had already completed.
2. **Destructive Tracker Invalidation**:
   Calling `tracker.Destroy()` and `shared.Destroy()` freed the underlying native C++ tracker wrappers. Subsequent calls to `shared.ForceRebuild()` or `ClearEditorsAndRebuild()` operated on destroyed native handles.
3. **Reflection Hierarchy Miss**:
   Querying `typeof(InspectorWindow).GetField("m_Tracker", BindingFlags.Instance | BindingFlags.NonPublic)` overlooks that `m_Tracker` is declared on `PropertyEditor` (the base class). In .NET Standard reflection, `GetField` without hierarchy iteration does not locate non-public fields declared on base classes. Furthermore, `tracker` is already exposed as a public property (`public ActiveEditorTracker tracker { get; }`).
4. **Locked Inspector Target Retention**:
   Locked Inspector windows retain their inspected targets in `m_Tracker` and `m_ObjectsLockedBeforeSerialization`. When entering Play Mode, locked editors survive selection clearing and deserialization unless `isLocked` is reset and `ClearSerializedLockedObjects()` is invoked.

---

## 3. Caveats

1. **Unity Version Specificity**:
   The `PropertyEditor` base class hierarchy (`InspectorWindow : PropertyEditor : EditorWindow`) is present in Unity 6000.x (Unity 6). Earlier Unity versions (e.g., Unity 2021/2022) declared `m_Tracker` directly on `InspectorWindow`. Reflection code should search up the type hierarchy (`BaseType`) or target `PropertyEditor` directly when operating on Unity 6.
2. **Project Asset Editors vs Scene Editors**:
   Editors targeting persistent project assets (e.g., ScriptableObject assets, Materials, Prefabs on disk where `EditorUtility.IsPersistent(target) == true`) do not have their targets destroyed when an edit scene unloads. However, destroying all editors or destroying all non-persistent editors prior to scene unload is safe because Unity re-creates asset editors automatically when the asset is re-selected.
3. **Custom Window Editors**:
   Third-party packages (e.g., PurrNet, Odin, Tri-Inspector) might maintain their own custom `Editor` instances outside `ActiveEditorTracker`. Purging via `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))` catches these, but only if they are not re-instantiated in custom window `OnGUI()` before Play Mode transition finishes.

---

## 4. Conclusion & Technical Recommendations

### 4.1 Exact Reflection and Public API Signatures (Unity 6.x)

| Target API | Member Type | Declaring Type | Accessibility & Signature | Purpose |
|---|---|---|---|---|
| `tracker` | Public Property | `UnityEditor.PropertyEditor` | `public ActiveEditorTracker tracker { get; }` | Safe, non-null retrieval of the window's `ActiveEditorTracker` |
| `m_Tracker` | Internal Field | `UnityEditor.PropertyEditor` | `internal ActiveEditorTracker m_Tracker` | Direct access to backing field if bypassing auto-creation |
| `ClearEditorsAndRebuild` | Internal Method | `UnityEditor.PropertyEditor` | `internal void ClearEditorsAndRebuild()` | Clears UI Toolkit elements and rebuilds empty content containers |
| `ClearSerializedLockedObjects` | Internal Method | `UnityEditor.PropertyEditor` | `internal void ClearSerializedLockedObjects()` | Clears locked target references preventing resurrection across reload |
| `isLocked` | Public Property | `UnityEditor.InspectorWindow` | `public bool isLocked { get; set; }` | Unlocks locked inspector instances |
| `m_AllInspectors` | Private Static Field | `UnityEditor.InspectorWindow` | `private static List<InspectorWindow> m_AllInspectors` | Fast O(1) enumeration of all active inspector windows |
| `m_AllPropertyEditors` | Internal Static Field | `UnityEditor.PropertyEditor` | `internal static List<PropertyEditor> m_AllPropertyEditors` | Comprehensive enumeration of all property editors |
| `sharedTracker` | Public Static Property | `UnityEditor.ActiveEditorTracker` | `public static ActiveEditorTracker sharedTracker { get; }` | Engine shared tracker singleton |
| `ForceRebuild` | Public Method | `UnityEditor.ActiveEditorTracker` | `public void ForceRebuild()` | Rebuilds tracker editor list against current (empty) selection |
| `ClearDirty` | Public Method | `UnityEditor.ActiveEditorTracker` | `public void ClearDirty()` | Resets dirty flag before rebuilding |

### 4.2 Safe Lifecycle Execution Order

Prior to Play Mode scene unload (`PlayModeStateChange.ExitingEditMode`) and before domain reload (`AssemblyReloadEvents.beforeAssemblyReload`):

1. **Capture Selection**:
   - `SelectionMementoService.Instance.CaptureSelection()` records hierarchy paths for scene objects and GUIDs for persistent assets.
2. **Clear Selection**:
   - `Selection.objects = Array.Empty<UnityEngine.Object>();`
   - `Selection.activeObject = null;`
3. **Purge All Inspector Windows**:
   - Locate all inspector windows via `m_AllInspectors` (falling back to `Resources.FindObjectsOfTypeAll(inspectorType)`).
   - For each window:
     - Unlock if locked: `win.isLocked = false`.
     - Clear locked object lists: invoke `ClearSerializedLockedObjects()`.
     - Get tracker: `var tracker = win.tracker;`.
     - If `tracker != null`: iterate `tracker.activeEditors` and destroy each non-null editor via `UnityEngine.Object.DestroyImmediate(editor)`.
     - Call `tracker.ClearDirty()`.
     - Call `win.ClearEditorsAndRebuild()`.
     - Call `win.Repaint()`.
4. **Purge Shared Tracker**:
   - `var shared = ActiveEditorTracker.sharedTracker;`
   - If `shared != null`: iterate `shared.activeEditors` and destroy each via `UnityEngine.Object.DestroyImmediate(editor)`.
   - Call `shared.ClearDirty()`.
   - Call `shared.ForceRebuild()`. (DO NOT call `shared.Destroy()`).
5. **Purge Orphan and Scene Editors**:
   - Find all editor ScriptableObjects: `Resources.FindObjectsOfTypeAll(typeof(UnityEditor.Editor))`.
   - For each editor `ed`:
     - If `ed == null`: continue.
     - Check target status:
       ```csharp
       bool shouldDestroy = false;
       try
       {
           if (ed.target == null || !EditorUtility.IsPersistent(ed.target))
           {
               shouldDestroy = true;
           }
       }
       catch
       {
           shouldDestroy = true;
       }
       if (shouldDestroy)
       {
           UnityEngine.Object.DestroyImmediate(ed);
       }
       ```
     - Destroying any editor targeting a non-persistent scene object (`!EditorUtility.IsPersistent(ed.target)`) ensures that when the scene unloads moments later, no orphan editor is left holding a destroyed scene target.
6. **Deferred Selection Restoration**:
   - On `PlayModeStateChange.EnteredPlayMode` (and `afterAssemblyReload`), restore selection using `EditorApplication.delayCall += OnDeferredRestore`.
   - When restored, the Play Mode scene is fully loaded and initialized; Unity instantiates fresh `Editor` instances whose `OnEnable()` executes safely against valid Play Mode objects.

---

## 5. Verification Method

### 5.1 Independent Verification Steps
1. **Compilation & Syntax**:
   Verify that all reflection lookups target `typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.PropertyEditor")` or navigate `BaseType` properly.
2. **Play Mode Transition Test**:
   - Select a GameObject in the scene hierarchy that possesses a `Transform`, `MeshCollider`, and `Renderer` component.
   - Lock an Inspector window on this GameObject.
   - Open a second unlocked Inspector window.
   - Enter Play Mode in Unity 6000.3.7f1.
   - **Verification**: Zero instances of `SerializedObjectNotCreatableException` or `MissingReferenceException` in the Unity Console.
3. **Play Mode Exit Test**:
   - While in Play Mode, select a spawned network or runtime GameObject.
   - Exit Play Mode back to Edit Mode.
   - **Verification**: Console remains clean with zero deserialization errors; Edit Mode selection is cleanly restored to original scene targets.
4. **Domain Reload Test**:
   - Select a scene object.
   - Trigger a domain reload (e.g., `Ctrl+R` or script recompile).
   - **Verification**: No exceptions from `MeshColliderEditor`, `GameObjectInspector`, `TransformInspector`, or `Renderer2DMeshEditor`.
