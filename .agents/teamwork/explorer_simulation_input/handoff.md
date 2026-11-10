# Audit Report: Input Streaming, Dual Simulation, Desynchronization & Zero-GC in TPOB

**Report Type**: Hard Handoff (Investigation & Architectural Audit Complete)  
**Author**: Teamwork Explorer (Physics Simulation, Input Streaming & Networking Specialist)  
**Target Path**: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\explorer_simulation_input\handoff.md`  
**Date**: 2026-11-10T01:40:00Z  

---

## 1. Observation

### Observation 1.1: Input Streaming Pipeline & Dual Dispatch Racing in LegsController
- **File**: `Assets/_Project/Gameplay/Player/Legs/LegsController.cs`
- **Lines 443–495**:
  ```csharp
  bool hasAuthority = !isSpawned ? true : isOwner;
  if (!hasAuthority || _inputReader == null) return;

  UpdateKickCharge();
  bool jumpTriggered = _inputReader.ConsumeJumpTrigger();
  bool fuseTriggered = _inputReader.ConsumeFuseTrigger();

  if (_commandInvoker != null)
  {
      _commandInvoker.Execute(new MoveCommand(_inputReader.MoveInput, _inputReader.IsSprintHeld));
      if (jumpTriggered) _commandInvoker.Execute(new JumpCommand());
      if (fuseTriggered)
      {
          if (_isFused) _commandInvoker.Execute(new SeparateCommand());
          else _commandInvoker.Execute(new FuseCommand());
      }
  }
  else if (fuseTriggered)
  {
      if (_isFused) RequestSeparation();
      else RequestFusion();
  }

  LegsInputData inputData = new LegsInputData(
      _inputReader.MoveInput,
      jumpTriggered,
      false,
      _inputReader.IsSprintHeld
  );

  if (!isSpawned || isServer || isOwner) ProcessInput(inputData);
  if (isSpawned && !isServer) SendInputServerRpc(inputData);
  ```
- **Lines 497–501**:
  ```csharp
  [ServerRpc(Channel.Unreliable)]
  private void SendInputServerRpc(LegsInputData inputData, RPCInfo info = default)
  {
      ProcessInput(inputData);
  }
  ```
- **File**: `Assets/_Project/Gameplay/Player/Commands/CommandInvoker.cs`
- **Lines 59–78**:
  ```csharp
  public void Execute<TCommand>(TCommand command) where TCommand : struct, IPlayerCommand
  {
      if (!isSpawned || isServer)
      {
          command.Execute(_context);
          OnCommandExecuted?.Invoke(command.CommandId);
      }
      else if (isOwner)
      {
          PlayerCommandPacket packet = command.ToPacket();
          if (command.IsReliable) SendReliableCommandServerRpc(packet);
          else SendUnreliableCommandServerRpc(packet);
      }
  }
  ```
- **Lines 80–90**:
  ```csharp
  [ServerRpc(Channel.Unreliable)]
  private void SendUnreliableCommandServerRpc(PlayerCommandPacket packet, RPCInfo info = default) => ExecutePacket(packet);

  [ServerRpc(Channel.ReliableOrdered)]
  private void SendReliableCommandServerRpc(PlayerCommandPacket packet, RPCInfo info = default) => ExecutePacket(packet);
  ```
- **Direct Fact**: Input is sampled inside Unity's variable-rate `Update()` method, not synchronized to fixed network ticks or PhysX ticks. When `_commandInvoker != null`, Pilot A dispatches **two parallel, redundant RPC streams**:
  1. `CommandInvoker.SendUnreliableCommandServerRpc(PlayerCommandPacket)` (for move) and `SendReliableCommandServerRpc(PlayerCommandPacket)` (for jump/fuse).
  2. `LegsController.SendInputServerRpc(LegsInputData)` (unreliable).
  Both arrive asynchronously at the server and overwrite `_pendingInput` independently.

---

### Observation 1.2: Complete Absence of Client-Side Prediction (Asymmetric Passive Puppet Simulation)
- **File**: `Assets/_Project/Gameplay/Player/Legs/LegsController.cs`
- **Lines 534–538**:
  ```csharp
  bool isSimulated = !isSpawned || isServer;
  if (!isSimulated)
  {
      return;
  }

  if (_tumbleController != null && _tumbleController.IsTumbling) return;

  ApplyLocomotion();
  ApplyJumpAndGravity();
  ApplyKick();
  ```
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Lines 137–140**:
  ```csharp
  protected override void OnSpawned()
  {
      base.OnSpawned();
      if (_rigidbody != null)
      {
          _rigidbody.isKinematic = !isServer;
      }
      _syncedFusedRotation.onChanged += HandleSyncedFusedRotationChanged;
  }
  ```
- **Lines 410–414**:
  ```csharp
  bool isSimulated = !isSpawned || isServer;
  if (!isSimulated || _rigidbody == null)
  {
      return;
  }
  ```
- **File**: `Assets/_Project/Editor/PlaygroundBuilder.cs`
- **Lines 469–484**:
  ```csharp
  public static NetworkTransform AddServerAuthoritativeTransform(GameObject root)
  {
      var nt = root.AddComponent<NetworkTransform>();
      var ntSo = new SerializedObject(nt);
      var ownerAuthProp = ntSo.FindProperty("_ownerAuth");
      if (ownerAuthProp != null) ownerAuthProp.boolValue = false;
      var syncPosProp = ntSo.FindProperty("_syncPosition");
      if (syncPosProp != null) syncPosProp.enumValueIndex = (int)SyncMode.World;
      var syncRotProp = ntSo.FindProperty("_syncRotation");
      if (syncRotProp != null) syncRotProp.enumValueIndex = (int)SyncMode.World;
      var syncScaleProp = ntSo.FindProperty("_syncScale");
      if (syncScaleProp != null) syncScaleProp.boolValue = false;
      var syncParentProp = ntSo.FindProperty("_syncParent");
      if (syncParentProp != null) syncParentProp.boolValue = false;
      ntSo.ApplyModifiedPropertiesWithoutUndo();
      return nt;
  }
  ```
- **Direct Fact**: In online multiplayer (`isSpawned && !isServer`), **both clients have zero physics simulation**. Their Rigidbody is marked `isKinematic = true` (or simulation is aborted in `FixedUpdate`). The movement of both players is 100% server-authoritative, streamed back to the clients via PurrNet's `NetworkTransform` with interpolation.

---

### Observation 1.3: Fused Mode Rotation Fighting & Desynchronization
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Lines 340–354 (Client local rotation)**:
  ```csharp
  if (_isFused)
  {
      Vector2 aim = inputData.AimDirection;
      if (aim.sqrMagnitude < 0.04f && inputData.MoveDirection.sqrMagnitude >= 0.04f)
      {
          aim = inputData.MoveDirection;
      }

      if (aim.sqrMagnitude >= 0.04f)
      {
          Vector3 aimDir = new Vector3(aim.x, 0f, aim.y).normalized;
          Quaternion targetRot = Quaternion.LookRotation(aimDir, Vector3.up);
          _fusedWorldRotation = Quaternion.Slerp(_fusedWorldRotation, targetRot, _aimRotationSpeed * Time.deltaTime);
      }
  }
  ```
- **Lines 370–376 (LateUpdate local application)**:
  ```csharp
  private void LateUpdate()
  {
      if (_isFused)
      {
          transform.rotation = _fusedWorldRotation;
      }
  }
  ```
- **Lines 511–525 (Server rotation in FixedUpdate)**:
  ```csharp
  if (_isFused)
  {
      if (aimInput.sqrMagnitude >= 0.04f)
      {
          Vector3 aimDir = new Vector3(aimInput.x, 0f, aimInput.y).normalized;
          Quaternion targetRotation = Quaternion.LookRotation(aimDir, Vector3.up);
          _fusedWorldRotation = Quaternion.Slerp(_fusedWorldRotation, targetRotation, _aimRotationSpeed * Time.fixedDeltaTime);
      }

      transform.rotation = _fusedWorldRotation;
      if (isServer)
      {
          _syncedFusedRotation.value = _fusedWorldRotation;
      }
      return;
  }
  ```
- **Lines 914–920 (SyncVar callback on client)**:
  ```csharp
  private void HandleSyncedFusedRotationChanged(Quaternion newRotation)
  {
      if (!isServer)
      {
          _fusedWorldRotation = newRotation;
      }
  }
  ```
- **Direct Fact**: In fused mode, Pilot B's client computes local rotation smoothly via `Time.deltaTime` in `Update()`/`LateUpdate()`. However, whenever the server writes `_syncedFusedRotation.value` in `FixedUpdate()`, PurrNet triggers `HandleSyncedFusedRotationChanged` on Pilot B's machine. Because it checks `if (!isServer)` instead of `if (!isOwner)`, it unconditionally overwrites Pilot B's active rotation with the server's delayed snapshot. This causes violent camera/torso rubberbanding and jitter for Pilot B.

---

### Observation 1.4: Hierarchy Reparenting and NetworkTransform Lifecycle in Fusion/Separation
- **File**: `Assets/_Project/Gameplay/Player/Robot/FusionStrategy.cs`
- **Lines 37–44**:
  ```csharp
  Transform attachPoint = context.Socket.AttachPoint;
  if (attachPoint != null)
  {
      context.Torso.transform.SetParent(attachPoint, false);
      context.Torso.transform.localPosition = Vector3.zero;
      context.Torso.transform.localRotation = Quaternion.identity;
      context.Torso.transform.localScale = context.Torso.InitialScale;
  }
  ```
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Lines 878–881**:
  ```csharp
  if (_networkTransform != null)
  {
      _networkTransform.enabled = !isFused;
  }
  ```
- **File**: `Assets/_Project/Gameplay/Player/Robot/SeparationStrategy.cs`
- **Lines 26–36**:
  ```csharp
  context.Torso.transform.SetParent(null, true);
  context.Torso.transform.localScale = context.Torso.InitialScale;

  Vector3 offset = context.Socket != null ? context.Socket.SeparationOffset : new Vector3(0f, 0.25f, 1.5f);
  Vector3 targetPosition = context.Legs.transform.position
      + context.Legs.transform.forward * offset.z
      + Vector3.up * offset.y;

  context.Torso.transform.position = targetPosition;
  context.Torso.transform.rotation = context.Legs.transform.rotation;
  ```
- **Direct Fact**: When fusing, Torso is reparented to Legs' socket (`attachPoint`), and `_networkTransform.enabled` is set to `false`. However, `NetworkTransform` on Legs was created with `_syncParent = false`. When separating, Torso is unparented to root (`SetParent(null, true)`) and `_networkTransform.enabled` is set to `true` without resetting PurrNet's internal interpolation buffer or calling `Teleport()`. `NetworkTransform` attempts to interpolate from the stale coordinates recorded before fusion, creating a massive visual snap.

---

### Observation 1.5: Mechanism Interaction 'E' Failure Mechanism
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoInputReader.cs`
- **Lines 76–81**:
  ```csharp
  var keyboard = Keyboard.current;
  if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame))
  {
      _grabTriggered = true;
      _interactTriggered = true;
  }
  ```
- **Lines 191–198 & 214–219**:
  ```csharp
  _grabAction = _inputActions.FindAction("Player/Interact", false);
  ...
  _grabAction.started += HandleGrabPerformed;
  _grabAction.performed += HandleGrabPerformed;
  ```
- **Lines 307–311**:
  ```csharp
  private void HandleGrabPerformed(InputAction.CallbackContext ctx)
  {
      _grabTriggered = true;
      _interactTriggered = true;
  }
  ```
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Lines 331–338 & 360–367**:
  ```csharp
  TorsoInputData inputData = new TorsoInputData(
      _inputReader.MoveInput,
      _inputReader.LookInput,
      _inputReader.ConsumeGrabTrigger(),
      _inputReader.ConsumeThrowTrigger(),
      _inputReader.IsMagnetHeld,
      _inputReader.ConsumeInteractTrigger()
  );
  ...
  SendTorsoInputServerRpc(inputData);
  if (inputData.GrabTriggered || inputData.ThrowTriggered || inputData.InteractTriggered)
  {
      SendTorsoActionServerRpc(inputData.GrabTriggered, inputData.ThrowTriggered, inputData.InteractTriggered);
  }
  ```
- **Lines 433–436 (Server FixedUpdate execution order)**:
  ```csharp
  ApplyGrabAndRelease();
  ApplyThrow();
  ApplyMagnet();
  ApplyMechanismInteraction();
  ```
- **Lines 557–571 & 584–590 (`ApplyGrabAndRelease`)**:
  ```csharp
  private void ApplyGrabAndRelease()
  {
      if (!_pendingInput.GrabTriggered) return;
      _pendingInput.GrabTriggered = false;

      if (IsHoldingObject)
      {
          ReleaseHeldObject();
          _pendingInput.InteractTriggered = false; // <-- RESETS INTERACT
          return;
      }

      Vector3 searchCenter = _holdSocket.position;
      int count = Physics.OverlapSphereNonAlloc(searchCenter, _grabRadius, _grabColliders, _grabLayer, QueryTriggerInteraction.Collide);

      for (int i = 0; i < count; i++)
      {
          Collider col = _grabColliders[i];
          if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;

          var grabbable = col.GetComponentInParent<IGrabbable>();
          if (grabbable != null && !grabbable.IsGrabbed)
          {
              Grab(grabbable);
              _pendingInput.InteractTriggered = false; // <-- RESETS INTERACT
              break;
          }
      }
  }
  ```
- **Lines 763–771 (`ApplyMechanismInteraction`)**:
  ```csharp
  private void ApplyMechanismInteraction()
  {
      if (!_pendingInput.InteractTriggered)
      {
          return;
      }

      _pendingInput.InteractTriggered = false;
      ...
  ```
- **Direct Fact**: Whenever key 'E' is pressed, both `_grabTriggered` and `_interactTriggered` become `true`. Because `ApplyGrabAndRelease()` runs BEFORE `ApplyMechanismInteraction()`, if Torso is holding an object OR if any collider with an `IGrabbable` component (including catapult ropes, pushable blocks, magnetic keys) is within `_grabRadius` (1.4m), lines 569 and 588 set `_pendingInput.InteractTriggered = false`. Consequently, `ApplyMechanismInteraction()` is completely skipped. Furthermore, `SendTorsoActionServerRpc` does not specify `requireOwnership: false`; if ownership is desynced during role handoff, the RPC is dropped by PurrNet.

---

### Observation 1.6: Tick Synchronization & Time Model Bypassing
- **File**: `Assets/_Project/Gameplay/Player/Robot/RobotCoordinator.cs`
- **Lines 195–209**:
  ```csharp
  private void Update()
  {
      if (_currentStrategy != null && _context != null)
      {
          _currentStrategy.OnTick(_context, Time.deltaTime);
      }
  }

  private void FixedUpdate()
  {
      if (_currentStrategy != null && _context != null)
      {
          _currentStrategy.OnFixedTick(_context, Time.fixedDeltaTime);
      }
  }
  ```
- **File**: `Assets/_Project/Core/Structs/LegsInputData.cs` & `TorsoInputData.cs`:
  - `LegsInputData` contains only: `Vector2 MoveDirection`, `bool JumpTriggered`, `bool KickTriggered`, `bool SprintHeld`.
  - `TorsoInputData` contains only: `Vector2 MoveDirection`, `Vector2 AimDirection`, `bool GrabTriggered`, `bool ThrowTriggered`, `bool MagnetHeld`, `bool InteractTriggered`.
- **Direct Fact**: Neither input struct contains a **network tick number**, **sequence number**, or **sampling timestamp**. No gameplay script registers with PurrNet's `TickModule` or implements network tick callbacks. Inputs are sent inside Unity's variable render loop (`Update()`) and consumed by the server on arrival. If client framerate drops or spikes (e.g. 30 FPS vs 144 FPS), the server receives either multiple inputs per fixed tick (overwriting them and discarding intermediate actions) or zero inputs (repeating stale state).

---

### Observation 1.7: Zero-GC & Hot Path Performance Violations
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`:
  - **Line 584**: `var grabbable = col.GetComponentInParent<IGrabbable>();` in `FixedUpdate()`.
  - **Line 749**: `var magnetic = col.GetComponentInParent<IMagnetic>();` in `FixedUpdate()` (executed up to 16 times per fixed tick while holding magnet).
  - **Line 780–782**: `col.GetComponentInParent<IInteractableMechanism>()` in `FixedUpdate()`.
  - *Violation*: Direct violation of project rule `AGY0012` ("Prohibit `GetComponent*` calls in hot paths (cache in `Awake`)").
- **File**: `Assets/_Project/Network/UI/PerformanceMonitor.cs`:
  - **Lines 86–97**: String interpolations (`$"FPS: {_currentFps:0.0}"`, `$"Mono: {_monoUsedMemoryMb} MB (Total: {_gcTotalMemoryMb} MB)"`, `$"GC Gen0: {gen0} | Gen1: {gen1} | Gen2: {gen2}"`, and rich text formatting) allocated on the heap inside `UpdateMetricsStrings()` every 0.25 seconds.
  - **Lines 100–121**: Immediate-mode GUI calls (`OnGUI()`, `GUILayout.Label`) triggering heap allocations every frame.
  - *Violation*: Direct violation of project rule `AGY0014` ("Prohibit string manipulation/interpolation/`ToString` and logging in hot paths").
- **File**: `Assets/_Project/Gameplay/Player/Legs/LegsController.cs`:
  - **Line 456**: `_commandInvoker.Execute(new MoveCommand(_inputReader.MoveInput, _inputReader.IsSprintHeld));` invoked inside `Update()` every frame.
  - Generates redundant delegate and event dispatching (`OnCommandExecuted?.Invoke(command.CommandId)`) every frame.
- **RPC Flooding**:
  - At 120 FPS, Pilot A sends 120 `SendUnreliableCommandServerRpc` + 120 `SendInputServerRpc` = **240 RPCs per second**.
  - PurrNet's batching buffer (`serverRpcModule.BatchToServer`) receives 240+ packet envelopes/sec, causing GC pressure in transport buffer allocation and network packet queues.

---

## 2. Logic Chain

```
[Observation 1.1] Client runs SendInputServerRpc + CommandInvoker.Execute every Update() frame on variable deltaTime
       │
       ▼
[Step 1: Network Inundation & Bufferbloat]
High frame rates (60–144Hz) flood the transport with 240+ RPCs/s without tick pacing.
Packet queues saturate, RTT inflates, and out-of-order unreliable packets cause dropped jump/kick triggers.
       │
       ▼
[Observation 1.2] LegsController & TorsoController abort FixedUpdate simulation when !isServer (isSimulated = false)
       │
       ▼
[Step 2: Perception of Heavy Lag & Sluggish Controls]
Because client prediction is 100% disabled, client position does not react when input is pressed.
Movement occurs ONLY after:
  Input Travel (RTT/2) + Server FixedUpdate Step (0–20ms) + Snapshot Travel (RTT/2) + Interpolation Delay (50–100ms)
Total round-trip delay is ~150–250ms even on fast networks, perceived by players as severe lag.
       │
       ▼
[Observation 1.3] HandleSyncedFusedRotationChanged unconditionally overwrites _fusedWorldRotation on all non-servers
       │
       ▼
[Step 3: Fused Aim Stutter & Fighting]
Pilot B aims locally in Update/LateUpdate. The server steps FixedUpdate and replicates _syncedFusedRotation.
When the SyncVar arrives on Pilot B's client, it overwrites the local aim with the delayed server rotation.
Local aim and server replication fight every frame, causing violent camera rubberbanding and jitter.
       │
       ▼
[Observation 1.5] TorsoInputReader binds 'E' to both Grab and Interact; ApplyGrabAndRelease runs before ApplyMechanismInteraction
       │
       ▼
[Step 4: Interaction Failure ('E' key)]
When 'E' is pressed, both GrabTriggered and InteractTriggered are true.
If an object is held or any IGrabbable is nearby, ApplyGrabAndRelease resets InteractTriggered to false.
ApplyMechanismInteraction is skipped, and mechanisms (levers, doors, consoles) fail to toggle.
       │
       ▼
[Observation 1.6 & 1.7] No tick IDs in input structs; GetComponentInParent executed in FixedUpdate
       │
       ▼
[Step 5: Desynchronization & Hot Path Allocations]
Without tick sequence numbers, variable frame rates cause simulation drift and jerky motion.
Hot path GetComponentInParent and string interpolations in UI cause periodic GC pauses.
```

---

## 3. Caveats

1. **PurrTransport Relay vs Local UDP Transport**: The tests and observations analyzed were evaluated based on C# code structure and PurrNet 1.x / 2.x architectural specifications. Actual ping variance in production depends on the physical distance to the PurrRelay server (e.g. Master Server at `https://purrtransport.purrservers.com/`).
2. **Tri-Inspector / Editor Scope**: Profiling Editor scripts and Inspector custom builders (`PlaygroundBuilder`, `LevelBuilderWindow`) was scoped out; only runtime scripts affecting gameplay hot paths were audited.
3. **No Code Modification**: In accordance with the critical safety constraint, zero codebase files were altered during this audit.

---

## 4. Conclusion

The online networking subsystem in TwoPilotsOneBody suffers from **five foundational architectural flaws**:

1. **Naive Asymmetric Authoritative Model without Client Prediction**: The game disables physics simulation on clients, turning them into passive puppets waiting for `NetworkTransform` snapshots. This creates perceived input lag equal to full RTT plus interpolation buffering (~150–250ms).
2. **Dual-Dispatch RPC Flooding**: `LegsController` and `CommandInvoker` both stream move/jump/fuse RPCs every rendering frame in `Update()`, creating up to 240+ RPCs/s per player, causing transport bufferbloat, packet drops, and race conditions on the server.
3. **Fused Mode Rotation Stomping**: `_syncedFusedRotation` unconditionally overwrites Pilot B's local aim rotation on the owner's client, creating a feedback loop of jitter and rubberbanding.
4. **Input Collision in Torso Interaction**: Key 'E' triggers both Grab and Interact simultaneously. `ApplyGrabAndRelease` runs before `ApplyMechanismInteraction` and resets `_pendingInput.InteractTriggered = false`, rendering mechanism interaction inoperable whenever objects or mechanisms with `IGrabbable` are nearby.
5. **Decoupled Time Model & Hot Path GC Violations**: Input streaming is bound to variable `Time.deltaTime` (`Update()`) without tick sequence numbers, bypassing PurrNet's `TickModule`. In hot paths, `GetComponentInParent` is invoked inside `FixedUpdate()`, violating project rule `AGY0012`.

---

## 5. Verification Method

To independently verify these findings, perform the following code inspections and runtime checks:

### File Inspections
1. **Verify Client Simulation Cutoff**:
   - Inspect `Assets/_Project/Gameplay/Player/Legs/LegsController.cs:534–538`. Confirm `isSimulated = !isSpawned || isServer; if (!isSimulated) return;` disables locomotion on clients.
   - Inspect `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs:137–140` and `410–414`. Confirm Rigidbody is kinematic and `FixedUpdate` returns early on clients.
2. **Verify Dual Input Dispatch**:
   - Inspect `Assets/_Project/Gameplay/Player/Legs/LegsController.cs:454–495`. Confirm `_commandInvoker.Execute(...)` and `SendInputServerRpc(...)` are both called in `Update()`.
3. **Verify Fused Rotation Fighting**:
   - Inspect `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs:914–920`. Confirm `HandleSyncedFusedRotationChanged` executes `if (!isServer) _fusedWorldRotation = newRotation;` without checking `!isOwner`.
4. **Verify 'E' Interaction Cancellation**:
   - Inspect `Assets/_Project/Gameplay/Player/Torso/TorsoInputReader.cs:76–81`. Confirm both `_grabTriggered = true` and `_interactTriggered = true` are set on 'E'.
   - Inspect `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs:569` and `588`. Confirm `_pendingInput.InteractTriggered = false` is assigned inside `ApplyGrabAndRelease()`.
5. **Verify Hot Path GetComponent Violations**:
   - Inspect `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs:584, 749, 782`. Confirm `GetComponentInParent` is called inside `FixedUpdate()`.

### Runtime Profiling & Stress Verification
1. **Network Simulation**:
   - Open `ConnectionHUD` (F10) in PlayMode with 2 connected instances. Set `NetworkSimulationProfile.QAStress` (100–150ms latency, 2% packet loss).
   - Observe that jumping as Legs experiences dropped inputs (due to unreliable RPC without client prediction).
   - Fuse the robot and aim with Torso; observe the torso jittering back and forth as the SyncVar fights local rotation.
   - Approach a lever and press 'E' while holding an object or near a grabbable; observe the lever failing to toggle.

---

## 6. Architectural Remediation Recommendations (For /plan)

1. **Client-Side Prediction & Server Reconciliation (CSP/SR)**:
   - Implement fixed network tick input sampling via PurrNet's `TickModule` (e.g. at 50Hz / 60Hz).
   - Add a `uint Tick` field and rolling input buffer to `LegsInputData` and `TorsoInputData`.
   - Enable local physics simulation for the owner on client, maintaining a circular state buffer for reconciliation when server corrections arrive.
2. **Consolidate Input Streaming**:
   - Eliminate redundant `CommandInvoker` network RPCs in `Update()`. Stream a single tick-stamped struct via an unreliable tick stream.
   - Separate discrete action triggers (Jump, Kick, Grab, Interact, Fuse) onto reliable RPCs or tick action bitmasks.
3. **Resolve Fused Rotation Authority**:
   - In `HandleSyncedFusedRotationChanged`, guard with `if (!isServer && !isOwner)`. Allow the local owner to drive aim smoothly without SyncVar feedback.
4. **Decouple Grab and Interact Actions**:
   - Remove hardcoded dual-triggering in `TorsoInputReader.cs`. Assign separate actions: 'E' for Interact, Left Mouse or dedicated key for Grab/Release.
   - Ensure `ApplyGrabAndRelease` never clears `_pendingInput.InteractTriggered` if the intended target is an `IInteractableMechanism`.
   - Mark `SendTorsoActionServerRpc(requireOwnership: false)` or explicitly validate ownership before sending.
5. **Cache Hot Path Queries (Zero-GC)**:
   - Replace `GetComponentInParent<IMagnetic>` and `GetComponentInParent<IGrabbable>` in `TorsoController.FixedUpdate()` with pre-cached component lookup dictionaries or direct collider-to-component mappings in `Awake()`.
