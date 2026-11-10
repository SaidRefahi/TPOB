# PurrNet Specification & Authoritative Networking Architecture Benchmark

## Observation

Based on official PurrNet package inspection (`PurrNet 1.23.0-beta.51` in `Assets/PurrNet/`), official documentation (`purrnet.dev/docs` via Context7 `/purrnet/purrdocs`), and architectural inspection of the TPOB codebase:

1. **PurrNet Architecture & Philosophy:**
   - **Authority Model**: PurrNet is strictly server-authoritative by default. `NetworkIdentity.cs` (lines 338–350) defines `IsController(bool ownerAuth)`: when `ownerAuth == false` (default), only the server is controller (`isServer`); when `ownerAuth == true`, only the owner (`isOwner`) is controller (falling back to server if no owner).
   - **Dual-Pilot / Shared Physics**: Official PurrNet physics movement pattern (`PlayerPhysicsMovement` in `guides/easy-multiplayer-physics-input-sync.md`) dictates:
     - All clients set `rigidbody.isKinematic = !isServer` so that *only the server simulates PhysX*.
     - Clients collect input during `Update()` and stream it to the server in `networkManager.onTick` using `[ServerRpc(Channel.Unreliable)]`.
     - The server applies forces/movement directly in fixed ticks; `NetworkRigidbody` or `NetworkTransform` synchronizes position/rotation back to all clients.
     - For parenting/docking without breaking PhysX and interpolation, `NetworkRigidbody.SetSoftParent(NetworkIdentity parent)` (and `ClearSoftParent()`) establishes sync frame reference without altering the Unity Transform hierarchy.
   - **TPOB Deviation**: In `FusionStrategy.cs` (line 40), `context.Torso.transform.SetParent(attachPoint, false)` mutates the Unity hierarchy directly, while `TorsoController.SetFused` (line 880) sets `_networkTransform.enabled = !isFused`, completely disabling transform replication and interpolation during fusion.

2. **RPCs & SyncVars:**
   - **ServerRpc Ownership Enforcement**: `ServerRpcAttribute.cs` (lines 18–31) sets `bool requireOwnership = true` by default. `NetworkIdentity.Broadcasting.cs` (lines 653–665, 713–717) enforces this: if `signature.requireOwnership && !isOwner`, `ValidateSendingRPC` logs an error and halts transmission client-side; if received, `ValidateIncomingRPC` drops it with `RpcError.RequireOwnership`.
   - **TPOB Torso Interaction Failure**: In `TorsoController.cs` (lines 378–390), `SendTorsoInputServerRpc` and `SendTorsoActionServerRpc` use `[ServerRpc(Channel.Unreliable)]` and `[ServerRpc(Channel.ReliableOrdered)]` without setting `requireOwnership: false`. If the client controlling the Torso is not the owner of the `NetworkIdentity` (e.g. during fusion or when spawned under another client/server ID), all key 'E' / interaction RPCs are silently blocked by PurrNet's validation pipeline.
   - **SyncVar Lifecycles**: `SyncVar<T>.cs` (lines 49–76, 206–289) provides dirty masking, automatic tick batching, and dual callbacks (`onChanged`, `onChangedWithOld`). It enforces controller permissions: writing to `.value` on a non-controlling peer throws a permission error.
   - **Host Mode Callbacks**: `NetworkIdentity.cs` (lines 814–822) documents that in Host mode, `OnSpawned(bool asServer)` and `OnDespawned(bool asServer)` execute **twice** (once for server, once for client).

3. **NetworkManager & Transport Lifecycle:**
   - **Transport Switching**: `NetworkManager.cs` (lines 328–358) enforces that setting `NetworkManager.transport` throws an `InvalidOperationException("Cannot change transport while it is being used.")` if `serverState != ConnectionState.Disconnected || clientState != ConnectionState.Disconnected`.
   - **Asynchronous Disconnection Flaw**: In TPOB's `NetworkService.cs` (lines 239–244, 351–360), `SetActiveTransport()` checks `serverState == Disconnected && clientState == Disconnected` synchronously right after calling `StopServer()`/`StopClient()`. Because transport shutdown is asynchronous (`Disconnecting` state), the active transport is never reassigned to `UDPTransport`, causing local LAN host/client attempts to run against `PurrTransport` or fail silently.
   - **Reconnection**: PurrNet tracks persistent sessions via `CookieScope.LiveWithProcess` (`NetworkManager.cookieScope`). In `NetworkManager.cs` (line 1065), `onPlayerJoined` fires `(PlayerID player, bool isReconnect, bool asServer)`, providing native reconnection awareness.

4. **Performance & Zero-GC Hot Paths:**
   - **Zero-Allocation Serialization**: PurrNet provides `IPacked` (`Write`/`Read`) and `IPackedSimple` (`Serialize`) interfaces and static bitpacker extension methods (`public static void Write(this BitPacker, MyStruct)`), combined with IL postprocessing (`PostProcessor.cs`) to eliminate GC allocation and boxing.
   - **Hot Path Packet Processing**: Streaming inputs at Unity render frame rates (144Hz+) floods packet buffers. PurrNet best practices require input collection in `Update()` with network transmission scheduled strictly on `networkManager.onTick` (typically 20–60Hz).

5. **VContainer Integration:**
   - **Dynamic Network Spawning Gap**: PurrNet instantiates network prefabs via `UnityProxy.Instantiate` / standard `Instantiate()`, bypassing VContainer's `LifetimeScope`.
   - **Injection Pattern**: Injected dependencies (`[Inject] Construct(...)`) in `NetworkBehaviour` components remain unpopulated unless explicitly resolved via `Container.Inject(instance)` during `Awake()`. In TPOB, `TorsoController.cs` has `[Inject] Construct(IGameEventBus)` but never invokes container injection, leaving `_eventBus` null.

---

## Logic Chain

1. **Torso Mode 'E' Interaction & Input Failure**:
   - `ServerRpcAttribute.requireOwnership` defaults to `true`.
   - `TorsoController.cs` declares `[ServerRpc(Channel.ReliableOrdered)] private void SendTorsoActionServerRpc(...)` with default parameters (`requireOwnership == true`).
   - If player roles or fusion change component ownership, or if the Torso GameObject is unowned by the client sending the RPC, `NetworkIdentity.Broadcasting.ValidateSendingRPC` returns `false` and drops the packet.
   - In addition, interaction in `ApplyMechanismInteraction()` is guarded by `isSimulated` (`!isSpawned || isServer`). The client depends entirely on the RPC reaching the server. When the RPC is dropped, key 'E' never activates interactables.

2. **Severe Lag & Desynchronization Under Dual Simulation**:
   - In `TorsoController.cs`, `SendTorsoInputServerRpc` is dispatched every frame inside `Update()` instead of syncing on `networkManager.onTick`.
   - In `FusionStrategy.cs`, `context.Torso.transform.SetParent(attachPoint, false)` changes Unity hierarchy without informing PurrNet's `HierarchyFactory`, while disabling `NetworkTransform`.
   - Disabling `NetworkTransform` cuts off snapshot interpolation and baseline state reconciliation. The server and client simulations diverge instantaneously.

3. **Transport Switching Failures (UDP vs PurrTransport/Relay)**:
   - `NetworkManager.transport` setter strictly forbids swapping transport while `serverState` or `clientState` is not `Disconnected`.
   - In `NetworkService.cs`, switching to LAN UDP occurs immediately after calling `Disconnect()`. Because `Disconnect()` triggers an asynchronous teardown (`Disconnecting`), `SetActiveTransport(udp)` aborts silently, leaving the stale transport attached. Subsequent connection calls throw or execute against the wrong backend.

4. **Missing Dependency Injection**:
   - VContainer cannot automatically detect GameObjects spawned at runtime by network events.
   - `LobbyNetworkController` manually patched this with `ResolveDependenciesIfNeeded()`, but `TorsoController` and other gameplay entities omitted this step, resulting in null services (`_eventBus == null`) and failed event dispatching.

---

## Features Discovered

| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Authority | `NetworkIdentity.isController` | Returns true if local peer has control (owner if ownerAuth, or server if no owner). | `bool ownerAuth` | `bool` | None; safe getter | `NetworkIdentity.cs:338` & `purrdocs` |
| 2 | Authority | `NetworkIdentity.GiveOwnership` | Transfers authority over identity to a specific PlayerID. | `PlayerID newOwner` | `void` | Throws/logs if caller has no authority | `NetworkIdentity.cs:1222` |
| 3 | Authority | `NetworkIdentity.RemoveOwnership` | Strips ownership, returning authority to the server. | None | `void` | Ignored if caller lacks authority | `NetworkIdentity.cs:1250` |
| 4 | Physics | `NetworkRigidbody.SetSoftParent` | Establishes a sync frame using another NetworkIdentity without altering Unity transform hierarchy. | `NetworkIdentity parent` | `void` | Reverts to world space if parent is destroyed | `NetworkRigidbody.cs` & `purrdocs` |
| 5 | Physics | `NetworkRigidbody.ClearSoftParent` | Clears soft parent frame and restores original parent frame. | None | `void` | None | `NetworkRigidbody.cs` & `purrdocs` |
| 6 | Physics | `NetworkRigidbody.TeleportTo` | Instantly repositions Rigidbody, clearing interpolation buffers and sending reliable RPC. | `Vector3 pos, Quaternion rot` | `void` | Zeroes velocity; resets buffers | `NetworkRigidbody.cs` & `purrdocs` |
| 7 | RPC | `[ServerRpc]` | Executes method from client to server. Supports channel, ownership, and delta packing. | `Channel, runLocally, requireOwnership, immediate` | RPC packet | Drops call if `requireOwnership && !isOwner` | `ServerRpcAttribute.cs:18` |
| 8 | RPC | `[ObserversRpc]` | Executes method from server to all clients. Supports bufferLast and runLocally. | `Channel, runLocally, bufferLast, requireServer` | RPC packet | Drops call if `requireServer && !isServer` | `ObserversRpcAttribute.cs:12` |
| 9 | RPC | `[TargetRpc]` | Server sends method execution to a specific target PlayerID. | `PlayerID, Channel, runLocally, bufferLast` | RPC packet | Drops call if caller is not server | `TargetRpcAttribute.cs:12` |
| 10 | SyncVar | `SyncVar<T>` | Synchronized variable with dirty tracking, tick batching, and change delegates. | `T initialValue, float sendInterval, bool ownerAuth` | `.value` getter/setter | Throws error if non-controller mutates `.value` | `SyncVar.cs:49` |
| 11 | SyncVar | `SyncVar.onChanged` | Action event firing when synchronized value changes. | Action delegate `Action<T>` | Event invocation | Exception caught and logged | `SyncVar.cs:38` |
| 12 | SyncVar | `SyncVar.onChangedWithOld` | Action event providing both previous and new values. | Delegate `Action<T, T>` | Event invocation | Exception caught and logged | `SyncVar.cs:41` |
| 13 | Lifecycle | `NetworkBehaviour.OnSpawned(bool asServer)` | Lifecycle callback invoked when object is spawned on network. | `bool asServer` | Callback | Invoked twice in Host mode | `NetworkIdentity.cs:820` |
| 14 | Lifecycle | `NetworkBehaviour.OnDespawned(bool asServer)` | Lifecycle callback invoked when object is despawned. | `bool asServer` | Callback | Invoked twice in Host mode | `NetworkIdentity.cs:838` |
| 15 | Transport | `NetworkManager.transport` setter | Swaps active transport backend between PurrTransport, UDPTransport, etc. | `GenericTransport target` | Property assign | Throws `InvalidOperationException` if not Disconnected | `NetworkManager.cs:330` |
| 16 | Transport | `NetworkManager.onPlayerJoined` | Event raised when a player completes connection handshake. | `(PlayerID, bool isReconnect, bool asServer)` | Event invocation | None | `NetworkManager.cs:1061` |
| 17 | Transport | `NetworkManager.onPlayerLeft` | Event raised when a player leaves the session. | `(PlayerID, bool asServer)` | Event invocation | None | `NetworkManager.cs:1085` |
| 18 | Transport | `NetworkManager.cookieScope` | Determines lifetime of reconnection cookies across sessions. | `CookieScope` enum | Property assign | Error logged if modified while active | `NetworkManager.cs:101` |
| 19 | Serialization | `IPacked` | Interface for custom zero-allocation bitpacker serialization. | `BitPacker packer` | `Write` / `Read` | Handled via custom logic | `purrdocs` & `Packer.cs` |
| 20 | Serialization | `IPackedSimple` | Simplified single-method serializer interface. | `BitPacker packer` | `Serialize` | Handled via custom logic | `purrdocs` & `Packer.cs` |
| 21 | Serialization | Static BitPacker Extensions | Zero-GC static Read/Write methods for arbitrary external types. | `BitPacker packer, ref T value` | Method call | Detected by codegen | `purrdocs` & `PostProcessor.cs` |
| 22 | Ticking | `NetworkManager.onTick` | Ticked at configured network tickRate (e.g. 20Hz). Used for input streaming. | `bool asServer` | Event invocation | Runs twice on host | `NetworkManager.cs:23` |

---

## Edge Cases

| # | Feature | Input | Observed Behavior |
|---|---------|-------|-------------------|
| 1 | `[ServerRpc]` without Ownership | Non-owner client calls `[ServerRpc(requireOwnership: true)]` | Client logs error in `ValidateSendingRPC` and never sends packet. Server rejects with `RpcError.RequireOwnership` if packet bypasses client check. |
| 2 | `SyncVar<T>.value` Mutation | Client mutates `.value` on a server-authoritative SyncVar (`ownerAuth: false`) | PurrNet logs `"Invalid permissions when setting SyncVar..."` and discards the new value. |
| 3 | Host Mode Lifecycle | Host starts game; `OnSpawned(bool asServer)` fires | Method runs twice: first with `asServer == true`, then with `asServer == false`. Without `asServer` guard, inputs and events duplicate. |
| 4 | Transport Hot Swap | Setting `networkManager.transport` while `serverState == ConnectionState.Disconnecting` | Throws `InvalidOperationException("Cannot change transport while it is being used.")` or is silently skipped if guarded by `== Disconnected`. |
| 5 | Physical Hierarchy Parenting | Calling `transform.SetParent` on a networked entity with disabled `NetworkTransform` | Hierarchy changes fail to replicate to observers; interpolation history corrupts; client and server positions desynchronize permanently. |
| 6 | Unreliable Input Flooding | Calling `[ServerRpc(Channel.Unreliable)]` inside `Update()` at 144Hz | Transport outbound ring buffer saturates, triggering packet drops, latency spikes, and input starvation. |
| 7 | VContainer Dynamic Injection | Spawning a `NetworkBehaviour` via `UnityProxy.Instantiate` without container injection | `[Inject]` dependencies remain `null`, causing immediate or deferred `NullReferenceException` during gameplay interaction. |
| 8 | Player Reconnection | Player disconnects and reconnects within cookie lifetime | `onPlayerJoined` fires with `isReconnect: true`, preserving PlayerID without re-executing full initialization. |

---

## Caveats

1. **Unity Physics Determinism**: As noted in PurrNet's `PredictedRigidbody` documentation, Unity PhysX does not produce bit-identical results across platforms. Dual simulation must rely on server authority for rigidbodies rather than parallel unguided client simulations.
2. **Third-Party Transport Dependencies**: `PurrTransport` relies on PurrNet relay servers (`purrtransport.purrservers.com`), requiring NAT traversal and ping negotiation. UDPTransport operates strictly over LAN / local loopback.
3. **Read-Only Scope**: In strict compliance with directives, no codebase files were modified during this investigation.

---

## Conclusion

The online networking subsystem in TPOB exhibits four fundamental architectural breakdowns rooted in divergence from PurrNet specifications:

1. **Torso Mode Key 'E' / Interaction Failure**:
   Caused by `[ServerRpc]` attributes on `TorsoController.cs` (`SendTorsoInputServerRpc`, `SendTorsoActionServerRpc`) lacking `requireOwnership: false`. When Torso is not explicitly owned by the sending client, PurrNet's `ValidateSendingRPC` suppresses the RPC entirely.
2. **Severe Lag & Desynchronization**:
   Caused by unthrottled per-frame RPC streaming in `Update()` instead of syncing on `networkManager.onTick`, coupled with `FusionStrategy.cs` breaking the network transform hierarchy by using Unity `SetParent` while disabling `NetworkTransform`, instead of utilizing PurrNet's recommended `NetworkRigidbody.SetSoftParent`.
3. **Transport Switching Failures**:
   Caused by synchronous checks against `ConnectionState.Disconnected` in `NetworkService.cs` during asynchronous transport shutdown, preventing `UDPTransport` from being assigned to `NetworkManager.transport`.
4. **VContainer Lifecycle Disconnect**:
   Network prefabs instantiated by PurrNet do not pass through VContainer's container pipeline, leaving injected fields (e.g. `IGameEventBus`) uninitialized.

Remediating these issues requires aligning with PurrNet's server-authoritative physics guidelines, configuring RPC ownership overrides, implementing tick-based input buffering, awaiting asynchronous transport states, and hooking VContainer injection into `NetworkBehaviour.Awake()`.

---

## Verification Method

1. **Inspect RPC Ownership Validation**:
   Inspect `Assets/PurrNet/Runtime/Components/NetworkIdentity/NetworkIdentity.Broadcasting.cs` lines 653–665 to verify client-side RPC drop behavior when `signature.requireOwnership && !isOwner`.
2. **Inspect Transport Setter Contract**:
   Inspect `Assets/PurrNet/Runtime/Managers/NetworkManager.cs` lines 328–358 to verify that transport cannot be reassigned until states reach `ConnectionState.Disconnected`.
3. **Inspect Soft Parenting API**:
   Inspect `Assets/PurrNet/Runtime/Components/NetworkRigidbody/NetworkRigidbody.cs` to verify `SetSoftParent(NetworkIdentity)` and `ClearSoftParent()` implementations.
4. **Inspect SyncVar Permission Gating**:
   Inspect `Assets/PurrNet/Runtime/Components/NetworkModule/SyncVar.cs` lines 49–67 to verify `InvalidateIsController()` and permission logging.
5. **Inspect TPOB TorsoController & FusionStrategy**:
   Inspect `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 378–390) and `Assets/_Project/Gameplay/Player/Robot/FusionStrategy.cs` (lines 37–48) to confirm the exact presence of the observed defects.
