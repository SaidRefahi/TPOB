# ZERO-ALLOCATION PERFORMANCE (HOT PATHS)
Strict guidelines to eliminate garbage collection allocations in per-frame loops and continuous routines in Unity C#.

---

## 1. Hot Path Definition & Scope

### Rule 1.1: Zero Heap Allocation Goal [GUIDED] [Performance, Runtime]
- The following execution paths are strictly designated as **Hot Paths** and must strive for zero heap allocations (`0 B GC Alloc` per frame):
  - Frame callbacks: `Update()`, `FixedUpdate()`, `LateUpdate()`.
  - Physics lifecycle callbacks: `OnTriggerEnter/Stay/Exit`, `OnCollisionEnter/Stay/Exit` (2D and 3D).
  - Continuous loops inside coroutines or async routines (`while (true) { ... yield return ...; }`).
- **Static vs. Runtime Distinction**: Static analysis and Roslyn analyzers can identify syntactic allocation patterns (e.g., `new`, LINQ, string operations, boxing). However, static analysis is not a complete proof of zero runtime GC allocations; native engine calls, third-party libraries, or dynamic array resizes can still allocate. True zero-allocation compliance must be verified via the Unity Profiler (`GC.Alloc` samples).
- **Scope Clarification**: One-time setup routines (`Awake()`, `Start()`, scene loaders, asset setup) and Editor tools are explicitly excluded from the zero-allocation restriction when readability benefits from standard C# constructs.

---

## 2. Allocation Anti-Patterns in Hot Paths

### Rule 2.1: No Reference Type Instantiation [ENFORCEABLE] [Performance, CSharp]
- **Enforcement**: Roslyn analyzer `AGY0013` (`error`).
- Never use the `new` operator for classes, arrays, or delegate instances inside hot paths.
- All reference objects must be preallocated in setup routines (`Awake()`, `Start()`) or managed via object pools.

### Rule 2.2: No LINQ in Hot Paths [ENFORCEABLE] [Performance, CSharp]
- **Enforcement**: Roslyn analyzer `AGY0011` (`error`).
- Prohibit LINQ methods (`.Where()`, `.Select()`, `.Any()`, `.First()`, `.ToList()`, etc.) inside hot paths.
- LINQ queries allocate iterator objects, state machines, and delegate closures on the managed heap. Use standard indexed `for` loops or preallocated buffers instead.

### Rule 2.3: No String Manipulation or Hot-Path Logging [ENFORCEABLE] [Performance, CSharp]
- **Enforcement**: Roslyn analyzer `AGY0014` (`error`).
- Prohibit string concatenation (`+`), string interpolation (`$"{val}"`), `.ToString()`, and recurring `Debug.Log` calls in hot paths.
- Strings in .NET are immutable reference types; formatting and concatenation allocate new string objects on the managed heap.

### Rule 2.4: No Boxing of Value Types [ENFORCEABLE] [Performance, CSharp]
- **Enforcement**: Roslyn analyzer `AGY0015` (`error`).
- Avoid casting structs or enums to `object`, `System.ValueType`, or non-generic interfaces in hot paths.
- Avoid passing value types to methods with `params object[]` arguments inside hot paths.

### Rule 2.5: No Closure Allocations in Hot Paths [ENFORCEABLE] [Performance, CSharp]
- **Enforcement**: Roslyn analyzer `AGY0016` (`error`).
- Prohibit lambdas or local functions that capture outer scope variables inside hot paths.
- Closures generate a compiler-synthesized display class allocated on the managed heap per invocation.

---

## 3. Caching Best Practices

### Rule 3.1: Reusable Yield Instruction Caching [GUIDED] [Performance, Runtime]
- **Enforcement**: Roslyn analyzer `UNT0038` (`warning`).
- Cache coroutine yield instructions (e.g., `new WaitForSeconds(interval)`) in member fields **only when the interval is fixed, recurring, and reusable**.
- Do not force caching when durations are dynamic or calculated per invocation. When intervals vary dynamically, consider tracking elapsed time with `Time.time` in `Update()` or using custom zero-alloc yielders.

### Rule 3.2: Component Lookup Caching [ENFORCEABLE] [Performance, Runtime]
- **Enforcement**: Roslyn analyzer `AGY0012` (`error`).
- Cache all `GetComponent<T>()` references in `Awake()`.
- If dynamic runtime component retrieval is unavoidable, use `TryGetComponent<T>(out var comp)`. Never call `GetComponent<T>()` repeatedly inside `Update()`.

### Rule 3.3: Tag Comparisons via CompareTag [ENFORCEABLE] [Performance, Runtime]
- **Enforcement**: Roslyn analyzer `UNT0002` (`error`).
- Always prefer `gameObject.CompareTag("Tag")` over `gameObject.tag == "Tag"`.
- `CompareTag` is the idiomatic, engine-optimized method for tag comparisons on GameObjects and Colliders.
- **Unity 6+ Optimization**: For high-frequency, repetitive tag comparisons in hot paths, consider caching and using `TagHandle` (`TagHandle.GetExistingTag("Tag")` and `gameObject.CompareTag(tagHandle)`) as an optional optimization to avoid string lookups altogether.

### Rule 3.4: Camera.main Caching [GUIDED] [Performance, Runtime]
- Cache the main camera reference in an instance or subsystem field during `Awake()` or initialization instead of evaluating `Camera.main` repeatedly in hot paths.

### Rule 3.5: Precalculation of Animator and Shader Property Hashes [ENFORCEABLE] [Performance, Runtime]
- **Enforcement**: Roslyn analyzers `UNT0041` and `UNT0046` (`error`).
- Convert string property names to integer IDs at static/initialization time:
  ```csharp
  private static readonly int SpeedHash = Animator.StringToHash("Speed");
  private static readonly int ColorPropertyId = Shader.PropertyToID("_BaseColor");
  ```

---

## 4. Object Pooling

### Rule 4.1: High-Frequency Entity Pooling [GUIDED] [Performance, Runtime]
- Any entity spawned and destroyed repeatedly (projectiles, impact particles, damage numbers, audio one-shots) must utilize object pooling (e.g. `UnityEngine.Pool.ObjectPool<T>`).
- Never call `Instantiate()` or `Destroy()` repeatedly during gameplay loops.

### Rule 4.2: Pool Prewarming Strategy [MANUAL] [Performance, Runtime]
- Prewarm pool capacity during scene loading or boot sequencing based on expected peak concurrency to eliminate mid-game frame spikes.
