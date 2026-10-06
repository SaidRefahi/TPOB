# CORE ARCHITECTURE & DEPENDENCY MANAGEMENT
Guidelines for structural design, lifetime management, and component interaction in Unity C#.

---

## 1. Component Ownership & Lifecycle

### Rule 1.1: Internal Initialization [ENFORCEABLE] [Architecture, Runtime]
- `Awake()` is strictly reserved for internal component state initialization and caching local dependencies on the same GameObject (e.g. `GetComponent<T>()`, collection instantiation).
- Do not query or assume the readiness of other GameObjects or external components during `Awake()`.

### Rule 1.2: External Linking & Deterministic Orchestration [GUIDED] [Architecture, Runtime]
- `Start()` or explicit orchestration methods (`Initialize(...)`) must be used for linking external dependencies, services, or cross-entity references.
- Unity lifecycle callback order across different GameObjects is non-deterministic by default. When strict operational order is required between interdependent systems, use explicit bootstrapper orchestration or dependency injection rather than relying solely on `Start()` timing.

### Rule 1.3: Symmetrical Lifecycle Cleanup [ENFORCEABLE] [CSharp, Runtime]
- Always clean up event subscriptions, stop running coroutines, and cancel `CancellationTokenSource` instances symmetrically.
- Components that can be enabled and disabled at runtime must perform teardown in `OnDisable()`. Persistent scene-lifetime managers that are never deactivated may perform cleanup in `OnDestroy()`.

---

## 2. Decoupling & Modularity

### Rule 2.1: Narrow Capability Interfaces at Subsystem Boundaries [GUIDED] [Architecture]
- Inter-system communication and cross-entity interactions should depend on focused capability interfaces (e.g., `IDamageable`, `IMovable`, `IInteractable`).
- Avoid over-interfacing purely internal leaf components where direct component composition is simpler and provides high cohesion without architectural penalty.

### Rule 2.2: Separation of Decisions and Execution (Context vs Behavior) [MANUAL] [Architecture]
- In complex systems (AI, State Machines, Gameplay Controllers), separate decision-making and transition logic from low-level engine execution:
  - State / Brain: Encapsulates pure state transitions, timing, and intent evaluation.
  - Context / MonoBehaviour: Acts as the engine executor for physical movement, animation triggers, and audio playback.

### Rule 2.3: Avoid Circular Dependencies [GUIDED] [Architecture]
- Dependencies between components and subsystems must form a directed acyclic graph.
- Never introduce bidirectional direct references between concrete controller classes; invert dependencies using events, callbacks, or interfaces.

---

## 3. Dependency Management & Shared Services

### Rule 3.1: Explicit Injection Over Global Ambient State [GUIDED] [Architecture]
- Prefer explicit dependency injection (via `Initialize(...)`, constructor parameters for plain C# classes, or serialized inspector references) or scoped Service Locators over static Singletons with global mutable state.
- Keep shared managers (Audio, GameState, Pools) behind clear service boundaries to preserve testability and prevent hidden coupling.

### Rule 3.2: Domain Service vs. Network Entity Lifecycle Decoupling [GUIDED] [Architecture, Network]
- Distinguish strictly between application domain services (plain C# classes managed by IoC container / `LifetimeScope`) and volatile network session entities (`NetworkBehaviour` managed by PurrNet).
- Never register a volatile scene `NetworkBehaviour` as a permanent service in the root `LifetimeScope` (`builder.RegisterComponent(...)`) if the networking engine destroys its hierarchy on disconnect/teardown (`HierarchyPool.Dispose`). Doing so causes dangling references to destroyed Unity objects.
- Instead, implement pure C# domain services (`Lifetime.Singleton` in IoC container) that expose dynamic session binding (`BindNetworkController` / `UnbindNetworkController`). The volatile network entity binds on `OnSpawned()` and unbinds symmetrically on `OnDespawned()`, ensuring UI presenters and the IoC container remain fully decoupled from network session lifecycles.

---

## 4. Hierarchy & Composition

### Rule 4.1: Shallow Inheritance Depth [ENFORCEABLE] [Architecture, CSharp]
- **Enforcement**: Roslyn analyzer `AGY0001` (`error`).
- Keep inheritance trees shallow: maximum 1 level of domain inheritance from abstract base classes (e.g., `WeaponBase : MonoBehaviour`).
- Deep class hierarchies introduce fragile base class problems in Unity; prefer composition.

### Rule 4.2: Composition of Focused Components [GUIDED] [Architecture]
- Favor assembling behaviors from small, single-responsibility components over monolithic parent classes.

---

## 5. Encapsulation & Serialization

### Rule 5.1: Private Fields with Explicit Serialization [ENFORCEABLE] [CSharp, Assets]
- **Enforcement**: Roslyn analyzer `AGY0002` (`error`).
- All component fields must be `private` by default.
- Expose variables to the Unity Inspector explicitly using `[SerializeField] private Type _fieldName;`.
- Never declare fields as `public` simply to make them visible in the Inspector.

### Rule 5.2: Read-Only External Access [ENFORCEABLE] [CSharp]
- **Enforcement**: Roslyn analyzer `AGY0003` (`error`).
- Expose component state to external callers strictly through public read-only properties or expression-bodied getters:
  ```csharp
  [SerializeField] private float _moveSpeed = 5f;
  public float MoveSpeed => _moveSpeed;
  ```

### Rule 5.3: ScriptableObject Role & State Boundaries [GUIDED] [Architecture, Assets]
- `ScriptableObject` serves primarily for shared configuration data, game tables, ability definitions, or decoupled architecture channels.
- Distinguish clearly between data roles and lifetimes:
  - **Persistent Asset Data**: Base definitions, design configurations, and static parameters saved and versioned directly as project `.asset` files on disk.
  - **Temporary Runtime / Play Mode Changes**: In-memory modifications during Play Mode or standalone execution are strictly transient; they exist only in memory during the active session and do not automatically persist to asset files on disk.
  - **Explicit Editor Tool Persistence**: Authoring or custom editor tools that intentionally modify asset data must explicitly mark assets dirty (`EditorUtility.SetDirty`) and serialize changes (`AssetDatabase.SaveAssets` / `AssetDatabase.SaveAssetIfDirty`) outside of normal gameplay runtime.
- Do not use ScriptableObjects as scene-bound state containers; scene-specific references and state belong in scene components.
