# Project Configuration & Guidelines: TwoPilotsOneBody (TPOB)

## 1. Project Specifications
- Unity Version: Unity 6000.3.7f1 (Unity 6.x)
- Render Pipeline: Universal Render Pipeline (URP)
- Language & Runtime: C# 9.0+ / .NET Standard 2.1
- Language Server / Analysis: ULSM (Unity Language Server MCP) enabled
- Core Stack: PurrNet (Server-Authoritative Network), VContainer (Dependency Injection), UniTask (Zero-GC Async), Cinemachine 3.x, DOTween, Tri-Inspector

## 2. Solution & Assembly Structure
- Assemblies are structured via `.asmdef` files to minimize recompilation times.
- Root Namespace: `Game`
- Assembly & Namespace Layers:
  - `Game.Core`: Interfaces, data definitions, ScriptableObjects, Enums, Structs, Commands, Events, Audio services.
  - `Game.Gameplay`: Concrete mechanics, controllers, player movement/torso coordination, rooms, hazards, and state machines.
  - `Game.Network`: PurrNet network entities, sync variables, RPC bindings, lifetime scopes, and network services.
  - `Game.Editor`: Zero-GC auditors, procedural level/room builders, standalone build tools, and custom inspectors.

## 3. Active Rule Modules (Knowledge Base)
The project adheres to modular rules defined in `.agents/rules/`:
- **Core Architecture**: `.agents/rules/core-architecture.md`
- **Performance & Zero-GC**: `.agents/rules/performance-hotpaths.md`
- **Physics & Math**: `.agents/rules/physics-and-math.md`
- **Input System**: `.agents/rules/input-and-events.md`
- **UI & Polish**: `.agents/rules/ui-and-juice.md`

### Rule Classification Model
Every rule in the knowledge base is classified by its enforcement level:
- **`[GUIDED]`**: Architectural and design conventions that the agent must proactively follow during implementation.
- **`[ENFORCEABLE]`**: Deterministic patterns and syntactic constraints that can be mechanically validated via static analysis, Roslyn analyzers, or linter rules. (Note: Runtime metrics like `0 B GC Alloc` require profiler measurement rather than static assumptions).
- **`[MANUAL]`**: Engineering heuristics requiring contextual judgment or situational evaluation without rigid automated gating.

### Rule Scopes
Rules are categorized with relevant technical scopes:
- **`Architecture`**: Component hierarchy, ownership, decoupling, and subsystem boundaries.
- **`CSharp`**: Language idioms, encapsulation, memory structures, and operator semantics.
- **`Performance`**: Memory allocation elimination, caching, and CPU efficiency.
- **`Runtime`**: Execution lifecycle, state persistence, and runtime behavior.
- **`Physics`**: Spatial queries, PhysX interactions, and continuous collision detection.
- **`Input`**: Hardware event adapters and action map binding lifecycles.
- **`UI`**: User interface rendering, canvas layouts, and audiovisual polish.
- **`Assets`**: Data serialization, ScriptableObject state, and asset workflows.

## 4. MCP Tools & Verification Workflow
- Before major refactors or type extractions, leverage `ulsm:find_references` and `ulsm:extract_interface`.
- When adding third-party packages or unfamiliar APIs, query `context7:query-docs`.
- For architectural discovery, structural dependency analysis, and blast radius calculation, query `codebase-memory` (`query_graph`, `trace_path`, `detect_changes`) after validating index availability with `graph-health`.
- Roslyn Analyzers are deployed in `Assets/Analyzers/` and enforced via `.editorconfig` with `error` severity for active `[ENFORCEABLE]` rules:
  - **Native (`Microsoft.Unity.Analyzers`)**:
    - `UNT0002`: `CompareTag` enforcement.
    - `UNT0007`, `UNT0008`, `UNT0023`: `UnityEngine.Object` null checks (`??`, `?.`, `??=`).
    - `UNT0028`: Non-allocating physics APIs (`Physics.*NonAlloc`).
    - `UNT0041`, `UNT0046`: Precalculation of Animator and Shader property hashes.
  - **Custom (`Antigravity.Unity.Analyzers`)**:
    - `AGY0001`: Shallow inheritance depth (max 1 level from base domain classes).
    - `AGY0002`: Encapsulate serialized fields (`[SerializeField] private`).
    - `AGY0003`: Read-only property external access (no non-private setters on component state).
    - `AGY0011`: Prohibit LINQ methods and queries in hot paths.
    - `AGY0012`: Prohibit `GetComponent*` calls in hot paths (cache in `Awake`).
    - `AGY0013`: Prohibit reference type instantiation (`new`) in hot paths.
    - `AGY0014`: Prohibit string manipulation/interpolation/`ToString` and logging in hot paths.
    - `AGY0015`: Prohibit value type boxing in hot paths.
    - `AGY0016`: Prohibit closures and lambdas in hot paths.
    - `AGY0021`: Named event handlers required in `+=` subscriptions (no inline lambdas).
    - `AGY0031`: Distance comparisons via `.sqrMagnitude` (avoid magnitude and `Vector3.Distance`).
- Informational/guided rules (`UNT0038` for yield caching, `UNT0044` for TMP text) are configured with `warning` severity.
- Check all hot path loops for zero heap allocations before finalizing code.
