# PHYSICS & MATH OPTIMIZATIONS
Guidelines for spatial queries, math operations, and Rigidbody interactions in Unity PhysX.

---

## 1. Spatial Queries & Allocations

### Rule 1.1: NonAlloc Physics Queries [ENFORCEABLE] [Physics, Performance]
- **Enforcement**: Roslyn analyzer `UNT0028` (`error`).
- Avoid allocating physics queries (`Physics.OverlapSphere`, `Physics.RaycastAll`) inside hot paths or gameplay loops.
- Preallocate a fixed buffer and use the `NonAlloc` variants (`OverlapSphereNonAlloc`, `RaycastNonAlloc`, etc.).
- Always iterate strictly up to the returned hit count; buffer elements beyond `hitCount` retain stale references from previous queries:
  ```csharp
  private readonly Collider[] _hitBuffer = new Collider[16];

  private void CheckSurroundings()
  {
      int hitCount = Physics.OverlapSphereNonAlloc(transform.position, _radius, _hitBuffer, _layerMask);
      for (int i = 0; i < hitCount; i++)
      {
          Collider hit = _hitBuffer[i];
          // Process hit...
      }
  }
  ```

---

## 2. Spatial Math & Distance Comparisons

### Rule 2.1: Distance Comparisons via .sqrMagnitude [ENFORCEABLE] [Physics, CSharp]
- **Enforcement**: Roslyn analyzer `AGY0031` (`error`).
- Never use `Vector3.Distance(a, b)` or `vector.magnitude` when comparing distances or ranges.
- Always compare squared magnitudes using `.sqrMagnitude` against a precalculated squared threshold to eliminate costly square root calculations (`Mathf.Sqrt`):
  ```csharp
  // Correct
  float rangeSqr = _attackRange * _attackRange;
  if ((targetPos - transform.position).sqrMagnitude <= rangeSqr)
  {
      // Target in range
  }
  ```

---

## 3. Fast Motion & Tunneling Prevention

### Rule 3.1: Fast Motion & Collision Detection Strategy [GUIDED] [Physics, Runtime]
- Configure `Rigidbody.collisionDetectionMode` according to physics behavior and velocity:
  - `CollisionDetectionMode.ContinuousDynamic`: Reserve for fast-moving dynamic Rigidbody objects colliding against other dynamic or static colliders where discrete tunneling occurs. Note that this adds noticeable PhysX solver CPU cost; do not enable indiscriminately.
  - `CollisionDetectionMode.ContinuousSpeculative`: Cheaper predictive sweep collision detection suitable for kinematic bodies and fast rotational movements.
  - Predictive Ray/Sphere Sweeps: For high-velocity projectiles (bullets, lasers, spells), per-frame predictive sweeps (`Physics.Raycast` or `Physics.SphereCast`) are generally more controllable, performant, and reliable than relying solely on PhysX collider movement.

---

## 4. Unity Object Lifetime & Null Semantics

### Rule 4.1: UnityEngine.Object Null Checks [ENFORCEABLE] [CSharp, Runtime]
- **Enforcement**: Roslyn analyzers `UNT0007`, `UNT0008`, `UNT0023` (`error`).
- Never use C# null-conditional (`?.`) or null-coalescing (`??`) operators on types deriving from `UnityEngine.Object` (MonoBehaviour, Component, ScriptableObject, GameObject).
- **Technical Rationale**: Unity overloads `operator ==` and `operator !=` to check whether the underlying native C++ engine object has been destroyed (the "fake null" state where the managed wrapper object still exists on the CLR heap). C# language operators `?.` and `??` bypass user-defined overloaded operators and evaluate only CLR managed heap reference nullity. Consequently, calling `destroyedObject?.DoSomething()` treats destroyed objects as alive, resulting in `MissingReferenceException` or silent logic failures.
- Always check explicit equality:
  ```csharp
  // Correct
  if (target != null)
  {
      target.TakeDamage();
  }

  // Forbidden: bypasses Unity C++ destruction check
  target?.TakeDamage();
  ```
