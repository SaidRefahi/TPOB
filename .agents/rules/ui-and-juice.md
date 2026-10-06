# UI & JUICE (PRAGMATIC LEAF ARCHITECTURE)
Pragmatic rules for user interface, screen transitions, and audiovisual polish in Unity C#.

---

## 1. Pragmatic Architecture for Leaf Components

### Rule 1.1: Leaf Component KISS Heuristic [MANUAL] [UI, Architecture]
- For self-contained, isolated visual and audiovisual polish components (button punch animations, screen fades, floating damage numbers, camera shake impulses):
  - Do not introduce premature abstractions, interfaces (`IJuiceService`), or complex dependency injection wrappers.
  - Consolidate behavior in 1 to 2 focused MonoBehaviours using direct `[SerializeField]` references.
  - **Heuristic**: Component size (~150 lines or less) serves as an informal indicator of a cohesive leaf component, not a rigid gate. The key criteria is complete domain independence from game logic.

---

## 2. Tweening & Coroutine Lifecycle

### Rule 2.1: Tween Lifecycle Management [GUIDED] [UI, Runtime]
- When using tween libraries (e.g., PrimeTween, DOTween), always cancel, kill, or complete running tweens in `OnDisable()`.
- Tweens operating on deactivated GameObjects can throw exceptions or cause visual glitches upon reactivation.

### Rule 2.2: Coroutine Concurrency Control [GUIDED] [UI, Runtime]
- When triggering UI animations via coroutines, verify and stop any existing active routine before launching a replacement:
  ```csharp
  if (_activeRoutine != null)
  {
      StopCoroutine(_activeRoutine);
  }
  _activeRoutine = StartCoroutine(AnimateScaleRoutine());
  ```
- Cache reusable `WaitForSeconds` or yield instructions as member fields when the interval is fixed.

---

## 3. UI Rendering & Canvas Performance

### Rule 3.1: Dynamic Text Update Caching [ENFORCEABLE] [UI, Performance]
- Avoid updating TextMeshPro text strings every frame.
- Only assign `TMP_Text.text` when the underlying value has actually changed to avoid triggering unnecessary text mesh rebuilding.

### Rule 3.2: Canvas Partitioning [GUIDED] [UI, Performance]
- Isolate dynamic, frequently redrawn UI elements (e.g., animated health bars, cooldown indicators, minimaps) onto dedicated sub-canvases separated from static panels (backgrounds, borders, static icons).
- Canvas geometry regeneration is per-canvas; separating dynamic elements avoids re-batching static UI vertices.

### Rule 3.3: Raycast Target Optimization [MANUAL] [UI, Performance]
- Disable `Raycast Target` on all UI Graphic components (Image, RawImage, TextMeshProUGUI) that do not require pointer clicks or hover detection.
- This minimizes the Raycast budget processed by `GraphicRaycaster` on user pointer input.
