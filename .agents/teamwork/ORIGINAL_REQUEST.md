# Original User Request

## Initial Request — 2026-11-07T00:01:06Z

Use the full team to resolve console exceptions occurring in Unity 6000.3.7f1 when entering Play Mode, planning and executing the resolution cleanly within the project repository.

Integrity mode: development
The team must NOT: delete or modify any files outside the TPOB project repository.

## Requirements

### R1. Play Mode Transition Exception Resolution
Resolve the 4 console exceptions logged when entering Play Mode in Unity 6000.3.7f1:
- `SerializedObjectNotCreatableException: Object at index 0 is null at MeshColliderEditor.OnEnable ()`
- `MissingReferenceException: The variable m_Targets of GameObjectInspector doesn't exist anymore at GameObjectInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at TransformInspector.OnEnable ()`
- `SerializedObjectNotCreatableException: Object at index 0 is null at Renderer2DMeshEditor.OnEnable ()`
Root cause: Open `InspectorWindow` instances maintain active `Editor` objects in private `m_Tracker` (`ActiveEditorTracker`) across domain reload and edit scene unload, causing `OnEnable()` to execute with null targets upon deserialization.
Implement purging of all `InspectorWindow` instances (calling `ClearEditorsAndRebuild()`) and destroy orphan `Editor` instances using `DestroyImmediate` prior to scene unload and Domain Reload. Restore selection state across the transition using the Memento pattern (`SelectionMementoService`).

### R2. Architecture and Design Patterns
The solution must follow clean code principles, SOLID (Single Responsibility Principle), and design patterns (Memento and Observer). All implementations must conform to repository guidelines (`AGENTS.md` and `.agents/rules/`).

### R3. Repository Scope and Safety Constraint
Operate strictly within the TPOB repository (`c:\Users\SaidR\Documents\GitHub\TPOB`). Under no circumstances delete or modify any files or directories outside this repository.

### R4. Token & Architecture Exploration Directive
For codebase exploration, architectural discovery, or tracing relationships between C# scripts, do not scan files sequentially with filesystem or shell tools. Verify graph health via `codebase-memory:index_status` and query the semantic graph (`query_graph`, `trace_path`, `search_graph`) and ULSM (`ulsm:find_references`) to minimize token consumption.

## Acceptance Criteria

### Console Stability
- [ ] Entering Play Mode logs zero instances of `SerializedObjectNotCreatableException` or `MissingReferenceException` from `MeshColliderEditor`, `GameObjectInspector`, `TransformInspector`, or `Renderer2DMeshEditor`.
- [ ] No regression exceptions are introduced during Play Mode exit or subsequent domain reloads.

### State Lifecycle & Memento Restoration
- [ ] Active editor tracking in `InspectorWindow` is cleanly purged before scene unload/domain reload.
- [ ] Active selection is cleanly restored after domain reload without leaving orphaned `Editor` ScriptableObjects.

### Safety & Scope
- [ ] All code modifications and additions are contained strictly within the TPOB repository.
- [ ] Zero files outside the repository are touched, modified, or deleted.
