# COMPREHENSIVE ARCHITECTURAL AUDIT & DIAGNOSIS
## Online Networking Subsystem — TwoPilotsOneBody (TPOB)

**Date**: 2026-11-10  
**Evaluator**: Teamwork Orchestrator Hierarchy  
**Integrity Mode**: Development (Read-Only Audit — Zero Code Modifications)  
**Authoritative Standards**: PurrNet 1.23.0-beta.51, VContainer 1.15+, Unity 6000.3.7f1, TPOB AGENTS.md Zero-GC Rules

---

## 1. Executive Summary

A comprehensive architectural audit of the online multiplayer subsystem in TwoPilotsOneBody was conducted across four specialized exploration tracks:
1. **PurrNet Official Documentation & Specification Mining** (`spec_miner_purrnet`)
2. **Connection Lifecycle, Transport Architecture & Dependency Injection** (`explorer_lifecycle_transport`)
3. **Torso Mode Interaction Mechanics & Authority Pipeline** (`explorer_torso_interaction`)
4. **Input Streaming, Dual Simulation, Desynchronization & Zero-GC Profiling** (`explorer_simulation_input`)

The investigation isolated the exact root causes behind:
- **Torso Key 'E' Interaction Failure**: A conjunction of missing `QueryTriggerInteraction.Collide` in physics queries, interactable mechanisms lacking `NetworkIdentity` in procedural builders, input preemption in `ApplyGrabAndRelease`, and PurrNet's default `requireOwnership: true` suppressing client RPCs.
- **Severe Input Lag & Rubberbanding**: A passive puppet simulation model (clients execute zero physics prediction, waiting on RTT + interpolation delay of 150–250ms), aggravated by 240+ RPCs/s dual-stream flooding in variable `Update()` and unconditional SyncVar rotation stomping during robot fusion.
- **Transport Switching Failure (Local UDP vs PurrTransport/Relay)**: Asymmetric transport selection in `ConnectionHUD` (Host binds Cloud Relay while Client binds UDP), combined with additive transport component accumulation without socket disposal, and silent skips in `SetActiveTransport()` during asynchronous disconnection.
- **Reconnection Disembodiment**: `NetworkService` drops `isReconnect`; `PlayerRegistry` purges player data on disconnect; and `TPOBPlayerSpawner` traps reconnected players in the `Observer` role because previous avatars remain in the scene.
- **VContainer Decoupling & Memory Leaks**: PurrNet network spawning completely bypasses VContainer's `LifetimeScope`, forcing static service locators (`GameLifetimeScope.Instance.Container.Resolve`), while `GameLifetimeScope` retains dangling references to scene-destroyed `ConnectionHUD` instances across room loads.

---

## 2. In-Depth Root Cause Diagnosis

### Issue Area 1: Torso Mode Interaction ('E' Key) Failure & Authority Pipeline

#### Finding 1.1: Missing `QueryTriggerInteraction.Collide` in Physics Queries
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Component**: `Game.Gameplay.Player.Torso.TorsoController.ApplyMechanismInteraction()`
- **Lines**: 763–788 (specifically line 773)
- **Root Cause Mechanism**:
  ```csharp
  int count = Physics.OverlapSphereNonAlloc(center, _interactRadius, _mechanismColliders, _mechanismLayer);
  ```
  `Physics.OverlapSphereNonAlloc` omits `QueryTriggerInteraction.Collide`, defaulting to `QueryTriggerInteraction.UseGlobal`. In Unity, if global physics settings ignore triggers, or if layer masks filter them, triggers are invisible. Meanwhile, all interactable mechanism prefabs (`LeverSwitch`, `TargetButton`, `PuzzleSocket`, `HeavyDoor`) configure their interaction volume as `isTrigger = true` (`PlaygroundBuilder.cs:737–740, 1234–1239`). In contrast, lines 575 (`ApplyGrabAndRelease`) and 725 (`ApplyMagnet`) explicitly pass `QueryTriggerInteraction.Collide`.
- **Citations / Best Practice**: Unity PhysX Manual & PurrNet Interaction Guidelines: Spatial trigger queries must explicitly pass `QueryTriggerInteraction.Collide` when querying trigger volumes.

#### Finding 1.2: Mechanism Prefabs Inherit `NetworkBehaviour` but Lack `NetworkIdentity`
- **Files**: `Assets/_Project/Editor/PlaygroundBuilder.cs` (lines 732–779, 794–834, 839–873, 950–980) and `Assets/_Project/Editor/ModularWallBuilder.cs` (lines 165–275)
- **Components**: `LeverMechanism`, `TargetButton`, `PuzzleSocket`, `WeightPlatform`, `SlidingDoor`
- **Root Cause Mechanism**:
  The procedural builders add `BoxCollider`, `MeshRenderer`, and mechanism scripts (`LeverMechanism`, `SlidingDoor`), but **never add a `NetworkIdentity` component**. In PurrNet, a `NetworkBehaviour` without a `NetworkIdentity` on its GameObject or parent has `isSpawned == false` permanently. In `LeverMechanism.Toggle(GameObject user)` (lines 76–94):
  ```csharp
  if (isSpawned && !isServer) { ToggleServerRpc(); return; }
  _isActivated = !_isActivated;
  if (!isSpawned) { UpdateVisuals(_isActivated); }
  else { UpdateVisualsObserversRpc(_isActivated); }
  ```
  Because `isSpawned` is permanently `false`, `UpdateVisualsObserversRpc` is never called! The lever toggles only on the host machine; remote clients receive zero network updates. Remote clients calling `Toggle()` also fail to invoke `ToggleServerRpc()` because `isSpawned` is false.
- **Citations / Best Practice**: PurrNet Core Manual: Every `NetworkBehaviour` requiring RPCs or observer synchronization MUST have an attached `NetworkIdentity` and be spawned via `networkManager.Spawn()`.

#### Finding 1.3: Key 'E' Intent Cannibalization by `ApplyGrabAndRelease`
- **Files**: `Assets/_Project/Gameplay/Player/Torso/TorsoInputReader.cs` (lines 76–81, 307–311) and `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 433–436, 566–571, 588–590)
- **Components**: `TorsoInputReader`, `TorsoController`
- **Root Cause Mechanism**:
  Pressing 'E' or 'Q' sets both `_grabTriggered = true` and `_interactTriggered = true` simultaneously. In `TorsoController.FixedUpdate()`:
  ```csharp
  ApplyGrabAndRelease(); // Runs first
  ApplyThrow();
  ApplyMagnet();
  ApplyMechanismInteraction(); // Runs last
  ```
  Inside `ApplyGrabAndRelease()`:
  ```csharp
  if (IsHoldingObject) { ReleaseHeldObject(); _pendingInput.InteractTriggered = false; return; }
  ...
  if (grabbable != null && !grabbable.IsGrabbed) { Grab(grabbable); _pendingInput.InteractTriggered = false; break; }
  ```
  If the Torso is holding any object or is within 1.4m of an `IGrabbable`, lines 569 and 588 zero out `InteractTriggered`. `ApplyMechanismInteraction` is skipped entirely.
- **Citations / Best Practice**: Game Input Disambiguation: Dual-action keys must evaluate contextual priority (mechanism interaction vs object grab) without destructively canceling unexamined intent flags.

#### Finding 1.4: PurrNet Default `requireOwnership: true` Suppressing Client RPCs
- **Files**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 378–390), `Assets/PurrNet/Runtime/Components/NetworkIdentity/NetworkIdentity.Broadcasting.cs` (lines 653–665), and `Assets/PurrNet/Runtime/Attributes/ServerRpcAttribute.cs` (lines 18–31)
- **Components**: `TorsoController.SendTorsoInputServerRpc`, `SendTorsoActionServerRpc`
- **Root Cause Mechanism**:
  PurrNet's `[ServerRpc]` attribute defaults to `requireOwnership = true`. If the client controlling the Torso is not the registered owner of the Torso's `NetworkIdentity` (e.g. during role handoffs, editor multiplay, or host-spawned scene objects), `NetworkIdentity.Broadcasting.ValidateSendingRPC` logs an error and halts transmission client-side. The server never receives the interaction RPC.
- **Citations / Best Practice**: PurrNet API Specification: If an entity accepts inputs from non-owners or proxies, `[ServerRpc(requireOwnership = false)]` must be explicitly declared, coupled with manual server-side sender validation.

---

### Issue Area 2: Input Streaming, Dual Simulation, Severe Lag & Desynchronization

#### Finding 2.1: Passive Puppet Simulation & Severe Perceived Lag
- **Files**: `Assets/_Project/Gameplay/Player/Legs/LegsController.cs` (lines 534–538) and `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 137–140, 410–414)
- **Components**: `LegsController`, `TorsoController`
- **Root Cause Mechanism**:
  In multiplayer (`isSpawned && !isServer`), clients completely abort physics simulation (`_rigidbody.isKinematic = true; isSimulated = !isSpawned || isServer; if (!isSimulated) return;`). Clients execute zero local prediction. When a player presses movement keys, the character does not move until:
  `Input Upload (RTT/2) + Server FixedUpdate Step (0–20ms) + Snapshot Download (RTT/2) + NetworkTransform Interpolation Delay (50–100ms) = 150–250ms total delay`.
  Even on a 20ms LAN ping, the controls feel heavily delayed, sluggish, and unresponsive.
- **Citations / Best Practice**: PurrNet Multiplayer Physics Guide (`guides/easy-multiplayer-physics-input-sync.md`): Client-authoritative prediction with server reconciliation or fixed-tick input streaming with local kinematic extrapolation is required for responsive co-op controls.

#### Finding 2.2: Dual-Dispatch Unthrottled RPC Flooding in `Update()`
- **Files**: `Assets/_Project/Gameplay/Player/Legs/LegsController.cs` (lines 443–495, 497–501) and `Assets/_Project/Gameplay/Player/Commands/CommandInvoker.cs` (lines 59–90)
- **Components**: `LegsController`, `CommandInvoker`
- **Root Cause Mechanism**:
  Input is sampled and transmitted in Unity's variable render loop (`Update()`). Furthermore, `LegsController` and `CommandInvoker` both transmit the same inputs across two separate RPC streams:
  1. `CommandInvoker.SendUnreliableCommandServerRpc` (Move) + `SendReliableCommandServerRpc` (Jump/Fuse)
  2. `LegsController.SendInputServerRpc(LegsInputData)`
  At 120 FPS, Pilot A transmits **240+ RPCs per second**. This floods PurrNet's batching buffer, exhausts transport bandwidth, causes bufferbloat, and increases packet drop rates for reliable ordered commands.
- **Citations / Best Practice**: PurrNet Networking Standards: Client inputs must be sampled in `Update()` but batched and dispatched strictly on `networkManager.onTick` (typically 20–60Hz). Redundant parallel RPC streams must be consolidated.

#### Finding 2.3: Fused Mode Rotation SyncVar Stomping & Aim Jitter
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 340–376, 511–525, 914–920)
- **Components**: `TorsoController._syncedFusedRotation`, `HandleSyncedFusedRotationChanged`
- **Root Cause Mechanism**:
  In fused mode, Pilot B aims locally in `Update()`/`LateUpdate()` using `_fusedWorldRotation`. Concurrently, the server executes `FixedUpdate()` and replicates `_syncedFusedRotation.value`. On Pilot B's client, PurrNet fires:
  ```csharp
  private void HandleSyncedFusedRotationChanged(Quaternion newRotation)
  {
      if (!isServer) _fusedWorldRotation = newRotation;
  }
  ```
  Because the check is `!isServer` instead of `!isOwner`, Pilot B's local aim is overwritten by the delayed server snapshot every frame! Pilot B's local input and incoming server SyncVar engage in continuous conflict, resulting in severe camera rubberbanding and jitter.
- **Citations / Best Practice**: Server-Authoritative SyncVar Standards: SyncVar callbacks must ignore updates on the owning client (`if (isOwner) return;`) to preserve local client-side aim smoothing.

#### Finding 2.4: Direct Hierarchy Mutation & NetworkTransform Disablement in Fusion
- **Files**: `Assets/_Project/Gameplay/Player/Robot/FusionStrategy.cs` (lines 37–44), `SeparationStrategy.cs` (lines 26–36), and `TorsoController.cs` (lines 878–881)
- **Components**: `FusionStrategy`, `SeparationStrategy`, `TorsoController`
- **Root Cause Mechanism**:
  In `FusionStrategy.cs`, `context.Torso.transform.SetParent(attachPoint, false)` mutates the Unity hierarchy directly, while `TorsoController` sets `_networkTransform.enabled = !isFused;`.
  1. PurrNet's `HierarchyV2.cs` (lines 680–686) rejects local parent changes without server authorization, logging `OnUnauthorizedLocalParentChange` and snapping the Torso back.
  2. Disabling `NetworkTransform` halts interpolation and state replication.
  3. On separation, `_networkTransform.enabled = true` is set without calling `NetworkRigidbody.TeleportTo()` or resetting interpolation buffers, resulting in visual teleportation.
- **Citations / Best Practice**: PurrNet API Reference: Docking/mounting networked rigidbodies must utilize `NetworkRigidbody.SetSoftParent(NetworkIdentity)` and `ClearSoftParent()`, which maintain synchronization frames without mutating Unity's transform hierarchy.

#### Finding 2.5: Decoupled Time Model & Missing Tick Sequence Numbers
- **Files**: `Assets/_Project/Core/Structs/LegsInputData.cs`, `TorsoInputData.cs`, and `Assets/_Project/Gameplay/Player/Robot/RobotCoordinator.cs` (lines 195–209)
- **Root Cause Mechanism**:
  Neither `LegsInputData` nor `TorsoInputData` contains a tick sequence number or timestamp. The project completely ignores PurrNet's `TickModule`. Inputs arrive at variable rates and are consumed on arrival by the server. Frame drops on clients lead to missed actions, while frame bursts overwrite unconsumed inputs.
- **Citations / Best Practice**: Multiplayer Time Synchronization: Input packets must carry a monotonically increasing tick index matching the network simulation tick.

---

### Issue Area 3: Transport Switching & Connection Lifecycle (Local UDP vs PurrTransport/Relay)

#### Finding 3.1: Asymmetric Transport Selection in ConnectionHUD
- **Files**: `Assets/_Project/Network/UI/ConnectionHUD.cs` (lines 147–187) and `Assets/_Project/Network/Services/NetworkService.cs` (lines 280–328, 423–473)
- **Components**: `ConnectionHUD`, `NetworkService`
- **Root Cause Mechanism**:
  In `ConnectionHUD.cs`:
  - "▶ Iniciar como Host" calls `_networkService.StartHost(port)`. `NetworkService.ExecuteStartHostAsync()` forces `PurrTransport` (cloud relay).
  - "⚡ Unirse como Cliente" with `127.0.0.1` calls `_networkService.StartClient("127.0.0.1", port)`. `NetworkService` detects an IP address and calls `StartLanClient()`, which forces `UDPTransport`!
  - **Outcome**: The Host listens on the PurrNet Cloud Relay, while the Client sends UDP packets to `127.0.0.1:5000`. The two instances run incompatible transports; connection fails 100% of the time.
- **Citations / Best Practice**: Network Transport Architecture: Host and Client must negotiate and bind the exact same transport protocol (LAN UDP vs Cloud Relay).

#### Finding 3.2: Additive Transport Components & Incomplete Socket Teardown
- **Files**: `Assets/_Project/Network/Services/NetworkService.cs` (lines 205–231), `Assets/PurrNet/Runtime/Managers/NetworkManager.cs` (lines 330–359, 376–389), `Assets/PurrNet/Runtime/Transports/UDPTransport.cs` (lines 175–198), and `Assets/PurrNet/Runtime/Transports/PurrTransport.cs` (lines 589–613)
- **Root Cause Mechanism**:
  `NetworkService` attaches both `PurrTransport` and `UDPTransport` to `NetworkManager` via `AddComponent`. When `NetworkManager.transport` is reassigned, `TeardownTransportLayer()` only unbinds event listeners; it does NOT close sockets, stop listeners, or disable the old transport component. Bound sockets remain active, causing port collisions (`SocketException: Address already in use`) and background thread leaks.
- **Citations / Best Practice**: PurrNet Transport Lifecycle: Inactive transports must be explicitly stopped (`StopTransport()`) and disposed before assigning a new transport to `NetworkManager.transport`.

#### Finding 3.3: Silent Transport Rebinding Skips During Asynchronous Disconnection
- **Files**: `Assets/_Project/Network/Services/NetworkService.cs` (lines 233–244) and `Assets/PurrNet/Runtime/Managers/NetworkManager.cs` (lines 330–345)
- **Root Cause Mechanism**:
  `NetworkManager.transport` setter throws `InvalidOperationException` if called while `serverState != Disconnected || clientState != Disconnected`. `NetworkService.SetActiveTransport()` attempts to guard against this by checking:
  ```csharp
  if (_cachedManager.serverState == ConnectionState.Disconnected &&
      _cachedManager.clientState == ConnectionState.Disconnected)
  {
      _cachedManager.transport = targetTransport;
  }
  ```
  However, calling `Disconnect()` initiates an asynchronous shutdown. At the moment `SetActiveTransport()` executes, the state is `ConnectionState.Disconnecting`. The condition evaluates to `false`, and `SetActiveTransport()` exits silently without swapping the transport! Subsequent `StartServer()` or `StartClient()` runs against the old transport.
- **Citations / Best Practice**: Async Transport Lifecycle: Transport reassignment must await full asynchronous disconnection (`await UniTask.WaitUntil(...)`) before assigning `NetworkManager.transport`.

---

### Issue Area 4: Lobby, Roles, Reconnection & VContainer Architecture

#### Finding 4.1: Split Role Authority & Race Condition on Connect
- **Files**: `Assets/_Project/Network/Services/PlayerRegistry.cs` (lines 35–49, 69–83), `Assets/_Project/Network/Controllers/LobbyNetworkController.cs` (lines 262–308), and `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 206–218, 328–360)
- **Root Cause Mechanism**:
  1. On client connection, `PlayerRegistry.HandlePlayerConnected()` runs locally on the client where `_players` is empty, so `DetermineNextAvailableRole()` assigns `PlayerRole.Legs` to itself. Meanwhile, the server assigned it `PlayerRole.Torso`.
  2. Role Swap in `ConnectionHUD.cs` (line 309) and `NetworkEventRelay.cs` (line 80) modifies `PlayerRegistry`, but never invokes `TPOBPlayerSpawner.SwapRolesOnServer()` or `NetworkIdentity.GiveOwnership()`. The UI changes, but PurrNet ownership is never transferred!
  3. `#if UNITY_EDITOR` blocks in `TPOBPlayerSpawner.cs` (lines 206–218) grant the Host ownership of both Legs and Torso before Client 2 connects.
- **Citations / Best Practice**: Server-Authoritative Identity Management: Roles and ownership must be managed exclusively by the server via `NetworkIdentity.GiveOwnership()`, replicated to clients via reliable state RPCs or SyncVars.

#### Finding 4.2: Reconnection Trapping Players in Observer Role & Zero State Restoration
- **Files**: `Assets/_Project/Network/Services/NetworkService.cs` (line 580), `Assets/_Project/Network/Services/PlayerRegistry.cs` (lines 51–67), and `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 341–356, 368–378)
- **Root Cause Mechanism**:
  1. `NetworkService.HandlePlayerJoined` drops the `bool isReconnect` parameter.
  2. `PlayerRegistry` immediately purges disconnected player slots.
  3. When the player reconnects, `TPOBPlayerSpawner.DetermineRoleForPlayer()` checks if avatars exist in the scene (`bool hasLegs = FindFirstObjectByType<LegsController>() != null; bool hasTorso = ...`). Because both avatars remain in the scene, it classifies the reconnected player as `PlayerRole.Observer`!
  4. For `Observer`, no avatar is assigned, leaving the player permanently disembodied.
  5. No state restoration system (checkpoints, mechanisms, room progression, fusion) exists in the project.
- **Citations / Best Practice**: PurrNet Reconnection Pattern: Native `cookieScope` and `onPlayerJoined(..., isReconnect: true)` must be utilized to preserve player slot reservations and restore avatar ownership upon reconnection.

#### Finding 4.3: Network Spawning Bypassing VContainer LifetimeScope
- **Files**: `Assets/_Project/Network/Services/NetworkService.cs` (lines 555–556), `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 489–515), `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 127–131, 986–988), and `Assets/_Project/Network/Scopes/GameLifetimeScope.cs` (lines 54, 108–112)
- **Root Cause Mechanism**:
  1. PurrNet spawns network entities via `Object.Instantiate` / `UnityProxy.Instantiate`, completely bypassing VContainer's `IObjectResolver`.
  2. `[Inject]` methods on spawned components (such as `TorsoController.Construct(IGameEventBus)`) are never called.
  3. To avoid crashes, scripts resort to static service locators: `GameLifetimeScope.Instance.Container.Resolve<IGameEventBus>()` in `TorsoController.Update()` (lines 986–988), violating DI principles.
  4. `GameLifetimeScope` calls `DontDestroyOnLoad`, but registers a scene component `ConnectionHUD` (`builder.RegisterComponent(hud)`). When `LevelManager` loads a room using `LoadSceneMode.Single`, `ConnectionHUD` is destroyed, leaving `GameLifetimeScope.Container` holding a dangling reference to a destroyed object.
  5. `RoomLifetimeScope` duplicates core singletons (`GameEventBus`, `GameManager`, `NetworkService`, `PlayerRegistry`) if parent lookup fails during scene transition.
- **Citations / Best Practice**: VContainer Unity Integration: Dynamically instantiated GameObjects must be injected via `IObjectResolver.InjectGameObject()` in a custom factory or spawn hook. Root lifetime scopes must never register scene-bound components that are destroyed on scene unload.

#### Finding 4.4: Zero-GC Hot Path Violations
- **Files**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 584, 749, 782) and `Assets/_Project/Network/UI/PerformanceMonitor.cs` (lines 86–121)
- **Root Cause Mechanism**:
  - `TorsoController` invokes `col.GetComponentInParent<IGrabbable>()`, `IMagnetic`, and `IInteractableMechanism` inside `FixedUpdate()` on every detected collider (up to 16 times per tick), violating project rule `AGY0012` ("Prohibit `GetComponent*` calls in hot paths").
  - `PerformanceMonitor` allocates string interpolations and invokes immediate-mode `OnGUI()` every frame, violating `AGY0014`.
- **Citations / Best Practice**: TPOB AGENTS.md & Roslyn Analyzers: Zero heap allocations in `Update`/`FixedUpdate`/network tick hot paths. Pre-cache component interfaces in `Awake` or maintain a static collider-to-interface lookup table.

---

## 3. Diagnostic Matrix

| ID | Component | File Path | Lines | Severity | Root Cause Mechanism | PurrNet / Architectural Violation |
|---|---|---|---|---|---|---|
| **D-01** | `TorsoController` | `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` | 763–788 | **CRITICAL** | `Physics.OverlapSphereNonAlloc` omits `QueryTriggerInteraction.Collide`. Interactables use triggers. | Spatial trigger query contract broken. |
| **D-02** | Mechanism Prefabs | `Assets/_Project/Editor/PlaygroundBuilder.cs` | 732–980 | **CRITICAL** | Mechanism prefabs lack `NetworkIdentity`. `isSpawned` is permanently `false`; RPCs/sync bypassed. | PurrNet entity spawning contract broken. |
| **D-03** | `TorsoInputReader` / `TorsoController` | `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` | 566–590 | **HIGH** | 'E' key triggers Grab & Interact. `ApplyGrabAndRelease` runs first and zeroes `InteractTriggered`. | Input intent preemption / conflict. |
| **D-04** | `TorsoController` | `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` | 378–390 | **HIGH** | `[ServerRpc]` defaults to `requireOwnership: true`. Non-owners have RPCs suppressed by PurrNet. | PurrNet RPC ownership validation suppression. |
| **D-05** | `LegsController` / `TorsoController` | `Assets/_Project/Gameplay/Player/Legs/LegsController.cs` | 534–538 | **CRITICAL** | Passive puppet simulation (`isSimulated = isServer`). Zero client prediction; 150–250ms perceived lag. | Server-authoritative client prediction omitted. |
| **D-06** | `LegsController` / `CommandInvoker` | `Assets/_Project/Gameplay/Player/Legs/LegsController.cs` | 443–501 | **HIGH** | Unthrottled dual-stream RPC dispatch in `Update()` (240+ RPCs/s at 120 FPS), causing bufferbloat. | PurrNet `onTick` batching omitted; RPC flooding. |
| **D-07** | `TorsoController` | `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` | 914–920 | **HIGH** | `HandleSyncedFusedRotationChanged` checks `!isServer` instead of `!isOwner`, stomping Pilot B aim. | Server-authoritative SyncVar ownership violation. |
| **D-08** | `FusionStrategy` / `SeparationStrategy` | `Assets/_Project/Gameplay/Player/Robot/FusionStrategy.cs` | 37–44 | **HIGH** | Direct `SetParent` triggers PurrNet `HierarchyV2` revert; disabling `NetworkTransform` causes snapping. | PurrNet `NetworkRigidbody.SetSoftParent` omitted. |
| **D-09** | `ConnectionHUD` / `NetworkService` | `Assets/_Project/Network/UI/ConnectionHUD.cs` | 147–187 | **CRITICAL** | Asymmetric transport binding: Host binds PurrTransport (Relay), Client binds UDPTransport (127.0.0.1). | Incompatible transport protocol mismatch. |
| **D-10** | `NetworkService` | `Assets/_Project/Network/Services/NetworkService.cs` | 205–244 | **HIGH** | Additive transport components without socket disposal; `SetActiveTransport` skips during `Disconnecting`. | PurrNet transport lifecycle & disposal contract broken. |
| **D-11** | `PlayerRegistry` / `TPOBPlayerSpawner` | `Assets/_Project/Network/Services/PlayerRegistry.cs` | 35–83 | **HIGH** | Split role authority across 3 systems; Editor ownership hijack; Role swap updates UI but not PurrNet ownership. | Server-authoritative identity & role governance. |
| **D-12** | `NetworkService` / `TPOBPlayerSpawner` | `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` | 341–378 | **HIGH** | Disconnect purges player data; `isReconnect` dropped; reconnecting player classified as `Observer`. | PurrNet session cookie & reconnection lifecycle broken. |
| **D-13** | Network Prefabs / `GameLifetimeScope` | `Assets/_Project/Network/Scopes/GameLifetimeScope.cs` | 108–112 | **HIGH** | Network spawns bypass VContainer; static service locator fallbacks; root scope holds destroyed scene references. | VContainer dependency inversion & scope lifetime broken. |
| **D-14** | `TorsoController` | `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` | 584, 749 | **MEDIUM** | `GetComponentInParent` inside `FixedUpdate()` hot path; heap-allocated string formatting in UI. | `AGY0012` & `AGY0014` Zero-GC rule violations. |
