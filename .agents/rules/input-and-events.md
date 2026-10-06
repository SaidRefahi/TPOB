# INPUT SYSTEM & EVENT PATTERNS
Rules for using UnityEngine.InputSystem and event handling in Unity C#.

---

## 1. Input Architecture & Separation of Concerns

### Rule 1.1: Input Adapters as Intent Broadcasters [GUIDED] [Input, Architecture]
- Components listening to `UnityEngine.InputSystem` actions should function as adapters or intent broadcasters.
- Never place gameplay decision logic, entity state transitions, or complex branch conditions inside raw input callbacks.
- Input handlers should translate hardware actions (`InputAction.CallbackContext`) into high-level entity intents, C# events, or command method calls.

---

## 2. Subscription Lifecycle

### Rule 2.1: Symmetrical Event Subscription Lifecycle [GUIDED] [Input, Runtime]
- Symmetrical binding is mandatory to prevent dangling callbacks and memory leaks:
  - Components that can be enabled and disabled during gameplay must subscribe in `OnEnable()` and unsubscribe in `OnDisable()`. This ensures deactivated objects do not execute callbacks while inactive.
  - Persistent, scene-lifetime managers that are never deactivated may bind during explicit initialization (`Initialize(...)` or `Start()`), provided they unbind symmetrically in `OnDestroy()`.
  - Never subscribe in `Awake()` or `Start()` without a guaranteed symmetrical unsubscription.

```csharp
private void OnEnable()
{
    _inputActions.Player.Jump.performed += OnJumpPerformed;
    _inputActions.Player.Jump.canceled += OnJumpCanceled;
    _inputActions.Player.Enable();
}

private void OnDisable()
{
    _inputActions.Player.Jump.performed -= OnJumpPerformed;
    _inputActions.Player.Jump.canceled -= OnJumpCanceled;
    _inputActions.Player.Disable();
}
```

---

## 3. Event Handlers & Delegate Allocation

### Rule 3.1: Named Method Handlers for Event Subscriptions [ENFORCEABLE] [CSharp, Input, Performance]
- **Enforcement**: Roslyn analyzer `AGY0021` (`error`).
- Use named instance or static methods when subscribing to events and input actions; do not use anonymous inline lambdas (`action.performed += ctx => ...`).
- **Technical Rationale**:
  1. **Unsubscription Capability**: Anonymous inline lambdas cannot be symmetrically removed via `-=`, creating permanent subscription leaks and dangling callbacks when components are disabled or destroyed.
  2. **Closures**: Lambdas capturing outer scope variables allocate compiler-generated closure display classes on the managed heap each time they are created.
  3. **Delegate Allocation**: All event bindings instantiate a delegate on the heap upon subscription. Named methods allow clean one-time binding in lifecycle methods and deterministic unsubscription, whereas inline lambdas create anonymous delegates that cannot be cleanly unsubscribed.
- Always declare named private methods matching the event signature:
  ```csharp
  private void OnJumpPerformed(InputAction.CallbackContext context)
  {
      // Handle action
  }
  ```
