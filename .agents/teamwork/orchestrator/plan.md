# REMEDIATION PLAN (/plan)
## Online Multiplayer Networking Architecture Refactor
### TwoPilotsOneBody (TPOB)

**Document Version**: 1.0.0  
**Target Engine**: Unity 6000.3.7f1 (URP)  
**Core Frameworks**: PurrNet 1.23.0-beta.51, VContainer, UniTask, Tri-Inspector  
**Performance Budget**: 0 B GC Alloc in hot paths (`AGY0011`–`AGY0016`), <50ms local responsiveness  
**Integrity Gating**: Strictly server-authoritative, zero hardcoded facade logic, zero code modifications in this planning phase

---

## 1. Architectural Strategy & Phasing Roadmap

The remediation is sequenced into **six chronological phases** designed to eliminate defects in dependency order:
```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 0: Architectural Foundation & VContainer Lifecycle Harmonization  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Transport Layer & Connection Lifecycle Unification            │
└───────────────────────────────────┬────────────────────────────────────┘
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 2: Server-Authoritative Identity, Lobby & Reconnection Overhaul  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 3: Interactable Network Architecture & Torso Interaction Fix     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 4: Input Streaming Pacing, Client Prediction & Zero-GC Profiling │
└───────────────────────────────────┬────────────────────────────────────┘
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 5: Automated Testing, Network Stress Verification & Certification│
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Detailed Phase Specifications

### Phase 0: Architectural Foundation & VContainer Lifecycle Harmonization

#### 1. Objectives
- Eliminate static service locator anti-patterns (`GameLifetimeScope.Instance.Container.Resolve`).
- Guarantee automatic VContainer dependency injection for all dynamically spawned PurrNet `NetworkBehaviour` instances.
- Decouple scene-bound UI components from root `DontDestroyOnLoad` LifetimeScopes to prevent dangling references on room transitions.
- Eliminate singleton duplication in `RoomLifetimeScope`.

#### 2. Affected Components & Files
- `Assets/_Project/Network/Scopes/GameLifetimeScope.cs` (lines 54, 108–112)
- `Assets/_Project/Gameplay/Rooms/RoomLifetimeScope.cs` (lines 50–67)
- `Assets/_Project/Network/Services/NetworkService.cs` (lines 550–560)
- `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 489–515)
- `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 127–131, 986–988)
- `Assets/_Project/Network/Controllers/LobbyNetworkController.cs` (lines 132–147)
- `Assets/_Project/Network/Events/NetworkEventRelay.cs` (lines 48–63)

#### 3. Architectural Redesign

##### 3.1 Network Entity Injection Bridge (`INetworkPrefabInjector`)
Create a dedicated service `NetworkPrefabInjector` registered in `GameLifetimeScope`:
```csharp
namespace Game.Network.Injection
{
    public interface INetworkPrefabInjector
    {
        GameObject InstantiateAndInject(GameObject prefab, Vector3 position, Quaternion rotation);
        void InjectExisting(GameObject instance);
    }

    public sealed class NetworkPrefabInjector : INetworkPrefabInjector
    {
        private readonly IObjectResolver _container;

        public NetworkPrefabInjector(IObjectResolver container)
        {
            _container = container;
        }

        public GameObject InstantiateAndInject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            _container.InjectGameObject(instance);
            return instance;
        }

        public void InjectExisting(GameObject instance)
        {
            _container.InjectGameObject(instance);
        }
    }
}
```
- Hook `NetworkPrefabInjector` into `TPOBPlayerSpawner` and `NetworkService.SpawnPrefab()`, ensuring `IObjectResolver.InjectGameObject(instance)` runs *before* `NetworkManager.Spawn()`.
##### 3.2 Root LifetimeScope Decoupling
- In `GameLifetimeScope.cs`, remove `builder.RegisterComponent(hud)` for `ConnectionHUD`.
- `ConnectionHUD` will instead resolve `INetworkService` through standard constructor/method injection or a dedicated UI scope.
- In `RoomLifetimeScope.cs`, remove duplicate registrations for `IGameEventBus`, `IGameManager`, `INetworkService`, and `IPlayerRegistry`. Enforce that `RoomLifetimeScope` strictly resolves these as inherited singletons from parent `GameLifetimeScope`.

##### 3.3 Remote Client Spawn Injection Hook (Adversarial Mitigation C-01)
When the server calls `NetworkManager.Spawn(instance)`, remote clients instantiate the prefab via PurrNet's internal `SpawnManager` rather than local factory methods. To guarantee remote client instances of `TorsoController`, `LegsController`, and mechanisms receive VContainer dependency injection:
- Register a global spawn hook in `NetworkService` during initialization:
  ```csharp
  _cachedManager.onPrefabSpawned += (instance) => _container.InjectGameObject(instance);
  ```
- This guarantees all injected fields (`IGameEventBus`, `INetworkService`, `IPlayerRegistry`) are populated immediately upon remote instantiation before `OnSpawned()` fires, eliminating all `NullReferenceException` crashes without relying on service locators.

#### 4. Risk Mitigation
- **Risk**: Prefabs spawned directly by PurrNet internal callbacks on remote clients bypass `TPOBPlayerSpawner`.
- **Mitigation (C-01)**: The `NetworkManager.onPrefabSpawned` global injection delegate guarantees 100% DI coverage across both server and remote client topologies.

---

### Phase 1: Transport Layer & Connection Lifecycle Unification

#### 1. Objectives
- Unify transport selection in `ConnectionHUD` so Host and Client always agree on the active transport backend (LAN UDP vs Cloud Relay).
- Eliminate additive transport component accumulation on `NetworkManager` and guarantee clean socket disposal.
- Resolve race conditions during transport switching by awaiting asynchronous shutdown completion before assigning `NetworkManager.transport`.

#### 2. Affected Components & Files
- `Assets/_Project/Network/UI/ConnectionHUD.cs` (lines 147–187)
- `Assets/_Project/Network/Services/NetworkService.cs` (lines 205–244, 280–328, 423–473)
- `Assets/PurrNet/Runtime/Managers/NetworkManager.cs` (lines 330–359)

#### 3. Architectural Redesign

##### 3.1 Explicit Transport Mode Protocol Selection
Refactor `ConnectionHUD` to expose an explicit **Transport Mode Selector** (Enum: `LocalUDP`, `PurrRelay`):
```csharp
public enum NetworkTransportMode
{
    LocalUDP,
    PurrRelay
}
```
- When `LocalUDP` is selected:
  - "Start Host" binds `UDPTransport` on `0.0.0.0:Port` (default 5000).
  - "Start Client" binds `UDPTransport` targeting `TargetAddress:Port`.
- When `PurrRelay` is selected:
  - "Start Host" binds `PurrTransport` with the specified `RoomName` on `https://purrtransport.purrservers.com/`.
  - "Start Client" binds `PurrTransport` targeting `RoomName`.

##### 3.2 Asynchronous Transport Lifecycle & Safe Switching
Refactor `NetworkService.SetActiveTransportAsync()` to use UniTask to enforce clean teardown:
```csharp
public async UniTask<bool> SwitchTransportAsync(GenericTransport newTransport, CancellationToken ct = default)
{
    EnsureManager();
    if (_cachedManager == null || newTransport == null) return false;
    if (_cachedManager.transport == newTransport) return true;

    // 1. If currently connected or disconnecting, trigger and await full shutdown
    if (_cachedManager.serverState != ConnectionState.Disconnected ||
        _cachedManager.clientState != ConnectionState.Disconnected)
    {
        await DisconnectAsync();
        await UniTask.WaitUntil(() => 
            _cachedManager.serverState == ConnectionState.Disconnected &&
            _cachedManager.clientState == ConnectionState.Disconnected, 
            cancellationToken: ct);
    }

    // 2. Teardown and disable old transport component
    if (_cachedManager.transport != null)
    {
        _cachedManager.transport.StopTransport();
        _cachedManager.transport.enabled = false;
    }

    // 3. Assign new transport and enable
    newTransport.enabled = true;
    _cachedManager.transport = newTransport;
    return true;
}
```

##### 3.3 Transport Component Isolation
Instead of attaching all transports additively to the `NetworkManager` GameObject, place `UDPTransport` and `PurrTransport` on separate child GameObjects (`NetworkManager/Transports/UDP`, `NetworkManager/Transports/Relay`) under `NetworkManager`. Only enable the GameObject of the active transport, guaranteeing zero background socket collisions.

#### 4. Risk Mitigation
- **Risk**: NAT traversal failure on `UDPTransport` over WAN.
- **Mitigation**: Clearly label `LocalUDP` for LAN/development only, and default to `PurrRelay` for Internet games, with automatic ping fallback.

---

### Phase 2: Server-Authoritative Identity, Lobby & Reconnection Overhaul

#### 1. Objectives
- Establish a single, server-authoritative source of truth for player roles (`Legs`, `Torso`, `Observer`).
- Synchronize role changes and ensure `Role Swap` calls PurrNet's `NetworkIdentity.GiveOwnership()`.
- Eliminate the Editor debug ownership hijack in `TPOBPlayerSpawner`.
- Overhaul reconnection lifecycle using PurrNet's `cookieScope` to preserve player identity and re-bind reconnected clients to their existing avatars instead of trapping them as `Observer`.
- Implement world state resync (checkpoints, mechanisms, fusion).

#### 2. Affected Components & Files
- `Assets/_Project/Network/Services/PlayerRegistry.cs` (lines 35–83)
- `Assets/_Project/Network/Controllers/LobbyNetworkController.cs` (lines 262–308)
- `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 206–218, 296–360, 362–384)
- `Assets/_Project/Network/Events/NetworkEventRelay.cs` (lines 67–81)
- `Assets/_Project/Network/Services/NetworkService.cs` (line 580)

#### 3. Architectural Redesign

##### 3.1 Server-Authoritative Role Assignment & Ownership Handshake
- Remove local role guessing from `PlayerRegistry.HandlePlayerConnected()`.
- When a client connects:
  1. PurrNet triggers `onPlayerJoined(PlayerID player, bool isReconnect, bool asServer)`.
  2. If `asServer == true`:
     - Server determines role (from lobby configuration or available avatar slots).
     - Server registers the player in `PlayerRegistry` and broadcasts an authoritative `SyncPlayerSlotObserversRpc(PlayerSlot slot)`.
     - Server assigns ownership via `torsoIdentity.GiveOwnership(player)` or `legsIdentity.GiveOwnership(player)`.
  3. Client receives `SyncPlayerSlotObserversRpc` and updates its local `PlayerRegistry.LocalRole`.

##### 3.2 Role Swap Ownership Synchronization
In `NetworkEventRelay.RequestSwapRolesServerRpc()`:
```csharp
[ServerRpc(requireOwnership = false)]
public void RequestSwapRolesServerRpc(RPCInfo info = default)
{
    if (!isServer) return;
    
    // 1. Swap roles in authoritative registry
    _playerRegistry.SwapRoles();
    
    // 2. Authoritatively transfer PurrNet NetworkIdentity ownership
    var playerSpawner = Object.FindFirstObjectByType<TPOBPlayerSpawner>();
    if (playerSpawner != null)
    {
        playerSpawner.SwapRolesOnServer();
    }
    
    // 3. Broadcast updated registry to observers
    SyncRegistryObserversRpc(_playerRegistry.GetAllSlots());
}
```
- Remove `#if UNITY_EDITOR` blocks in `TPOBPlayerSpawner.cs` (lines 206–218, 243–252) that steal client ownership in Multiplayer Play Mode.

##### 3.3 Reconnection Avatar Recovery Pattern
- Configure `NetworkManager.cookieScope = CookieScope.LiveWithProcess`.
- Pass `isReconnect` through `NetworkService.OnPlayerConnected(PlayerID player, bool isReconnect)`.
- When a client disconnects:
  - Do NOT immediately purge their slot in `PlayerRegistry`. Mark the slot as `Disconnected (Reconnection Grace Period: 30s)`.
  - Leave their avatar (`Torso` or `Legs`) in the scene with ownership temporarily returned to Server (`RemoveOwnership()`).
- When the client reconnects within the grace period (`isReconnect == true`):
  - Find the existing reserved slot for their persistent `PlayerID`.
  - Call `avatarIdentity.GiveOwnership(player)`.
  - Broadcast full `GameStateSync` packet (current room index, checkpoint coordinates, mechanism states, fusion status).
  - Client immediately resumes control of the avatar without spawning a duplicate or being demoted to `Observer`.

#### 4. Risk Mitigation
- **Risk**: Player disconnects permanently and the grace period leaves the avatar unmovable.
- **Mitigation**: If grace period expires (30s), trigger `OnPlayerAbandonedSession`, allowing an existing or new player to take over the role or transitioning the robot to single-player / AI control.

---

### Phase 3: Interactable Network Architecture & Torso Interaction Fix

#### 1. Objectives
- Fix physics trigger collision query in `TorsoController.ApplyMechanismInteraction()`.
- Add `NetworkIdentity` and network registration to all interactable mechanism prefabs.
- Disambiguate key 'E' / 'Q' input processing to prevent `ApplyGrabAndRelease` from preempting mechanism interaction.
- Set `requireOwnership: false` on `TorsoController` action RPCs with server-side validation.
- Implement client prediction and highlight feedback for mechanisms.

#### 2. Affected Components & Files
- `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 378–390, 433–436, 566–571, 588–590, 763–788)
- `Assets/_Project/Gameplay/Player/Torso/TorsoInputReader.cs` (lines 76–81, 307–311)
- `Assets/_Project/Editor/PlaygroundBuilder.cs` (lines 732–980)
- `Assets/_Project/Editor/ModularWallBuilder.cs` (lines 165–275)
- `Assets/_Project/Gameplay/Interactables/LeverMechanism.cs` (lines 76–94)
- `Assets/_Project/Gameplay/Interactables/TargetButton.cs`
- `Assets/_Project/Gameplay/Interactables/SlidingDoor.cs`

#### 3. Architectural Redesign

##### 3.1 Physics Query Correction
In `TorsoController.cs` (line 773), pass `QueryTriggerInteraction.Collide`:
```csharp
Vector3 center = transform.position + transform.forward * 0.3f;
int count = Physics.OverlapSphereNonAlloc(center, _interactRadius, _mechanismColliders, _mechanismLayer, QueryTriggerInteraction.Collide);
```

##### 3.2 Prefab Procedural Builder Network Registration
In `PlaygroundBuilder.cs` and `ModularWallBuilder.cs`:
- For every interactable mechanism (`CreateLeverPrefab`, `CreateTargetButtonPrefab`, `CreatePuzzleSocketPrefab`, `CreateWeightPlatformPrefab`, `CreateSlidingDoorPrefab`):
  ```csharp
  var identity = root.AddComponent<NetworkIdentity>();
  // Ensure prefab is registered in NetworkManager prefab list
  ```
- This ensures `isSpawned == true` when spawned in network scenes, allowing `ToggleServerRpc()` and `UpdateVisualsObserversRpc()` to execute properly across all clients.

##### 3.3 Non-Destructive Input Intent Resolution
In `TorsoController.FixedUpdate()`:
- Do not let `ApplyGrabAndRelease` clear `_pendingInput.InteractTriggered` if a mechanism is in range.
- Separate input evaluation:
  1. **Primary Intent Detection**: Perform a preliminary non-alloc sphere check for mechanisms.
  2. If an `IInteractableMechanism` is detected in front of the Torso (`center + forward * 0.3f`), prioritize `ApplyMechanismInteraction()`.
  3. Otherwise, execute `ApplyGrabAndRelease()`.
  4. Ensure both actions can be bound to distinct keys or follow clean contextual hierarchy.

##### 3.4 RPC Ownership Decoupling with Server Validation
In `TorsoController.cs`:
```csharp
[ServerRpc(requireOwnership: false)]
private void SendTorsoActionServerRpc(bool grab, bool @throw, bool interact, RPCInfo info = default)
{
    // Validate that sender matches the assigned Torso player ID
    if (!ValidateTorsoSender(info.sender)) return;
    
    if (grab) _pendingInput.GrabTriggered = true;
    if (@throw) _pendingInput.ThrowTriggered = true;
    if (interact) _pendingInput.InteractTriggered = true;
}
```
- Setting `requireOwnership: false` guarantees PurrNet's client-side `ValidateSendingRPC` does NOT suppress the packet, even if client ownership is desynchronized during fusion or role transitions.

#### 4. Risk Mitigation
- **Risk**: Mechanism toggling twice due to client-server double triggering.
- **Mitigation**: Mechanism activation logic must remain 100% server-authoritative. Clients only send the intent RPC and display local visual feedback (e.g. prompt animation); the server validates and invokes `UpdateVisualsObserversRpc(bool newState)`.

---

### Phase 4: Input Streaming Pacing, Client Prediction & Zero-GC Profiling

#### 1. Objectives
- Implement PurrNet-recommended client prediction or fixed-tick input streaming with local kinematic extrapolation for responsive controls.
- Synchronize input transmission strictly to `NetworkManager.onTick` (20–60Hz), eliminating render-loop flooding.
- Consolidate redundant `CommandInvoker` and `LegsController` RPC streams into a single structured input packet.
- Fix fused mode rotation jitter by preventing server SyncVars from overwriting local aim on the owning client.
- Replace destructive `SetParent` hierarchy changes with PurrNet's `NetworkRigidbody.SetSoftParent`.
- Eliminate `GetComponentInParent` calls in `FixedUpdate` hot paths (`AGY0012`) and string allocations (`AGY0014`).

#### 2. Affected Components & Files
- `Assets/_Project/Gameplay/Player/Legs/LegsController.cs` (lines 443–501, 534–538)
- `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 340–376, 511–525, 584, 749, 782, 878–881, 914–920)
- `Assets/_Project/Gameplay/Player/Robot/FusionStrategy.cs` (lines 37–44)
- `Assets/_Project/Gameplay/Player/Robot/SeparationStrategy.cs` (lines 26–36)
- `Assets/_Project/Gameplay/Player/Commands/CommandInvoker.cs` (lines 59–90)
- `Assets/_Project/Core/Structs/LegsInputData.cs`
- `Assets/_Project/Core/Structs/TorsoInputData.cs`
- `Assets/_Project/Network/UI/PerformanceMonitor.cs` (lines 86–121)

#### 3. Architectural Redesign

##### 3.1 Network-Tick Paced Input Streaming (`NetworkManager.onTick`)
- Add a monotonic `uint Tick` sequence number to `LegsInputData` and `TorsoInputData`.
- In `LegsController` and `TorsoController`:
  - In `Update()`: Read hardware inputs from `IInputReader` and buffer into `_currentTickInput`.
  - In `networkManager.onTick(bool asServer)`: If `asServer == false && isOwner == true`, transmit the buffered input via `SendInputServerRpc(_currentTickInput)`.
  - Reset tick input buffer.
- Eliminate the redundant `CommandInvoker` RPC stream (`SendUnreliableCommandServerRpc`). `CommandInvoker` on clients will execute local predictive effects only, while input streaming carries the authoritative payload.

##### 3.2 Client-Side Locomotion Prediction
- Allow `LegsController` to run local character movement prediction for the owning pilot (`isOwner == true`).
- Record historical inputs and predicted positions in a circular buffer.
- When `NetworkTransform` updates or server reconciliation packets arrive, blend errors using kinematic smoothing instead of hard snaps.

##### 3.3 Fused Mode Rotation Ownership Guard
In `TorsoController.cs`:
```csharp
private void HandleSyncedFusedRotationChanged(Quaternion newRotation)
{
    // Ignore server snapshot if we are the owner currently aiming
    if (isOwner) return;
    
    _fusedWorldRotation = newRotation;
}
```
This single guard eliminates aim fighting and rubberbanding for Pilot B.

##### 3.4 Soft Parenting via `NetworkRigidbody.SetSoftParent`
Refactor `FusionStrategy.cs`:
- Remove `context.Torso.transform.SetParent(attachPoint, false)`.
- Remove `_networkTransform.enabled = !isFused;`.
- Use PurrNet's official API:
  ```csharp
  var torsoNetRb = context.Torso.GetComponent<NetworkRigidbody>();
  var legsNetIdentity = context.Legs.GetComponent<NetworkIdentity>();
  torsoNetRb.SetSoftParent(legsNetIdentity);
  ```
- On separation (`SeparationStrategy.cs`):
  ```csharp
  torsoNetRb.ClearSoftParent();
  torsoNetRb.TeleportTo(targetPosition, targetRotation);
  ```
This maintains snapshot interpolation, eliminates hierarchy mutation errors in PurrNet's `HierarchyV2`, and prevents positional teleportation snaps.

##### 3.5 Zero-GC Hot Path Optimization
- In `TorsoController.Awake()`, register all known colliders to their interface components:
  ```csharp
  private readonly Dictionary<Collider, IGrabbable> _grabbableCache = new(32);
  private readonly Dictionary<Collider, IMagnetic> _magneticCache = new(32);
  private readonly Dictionary<Collider, IInteractableMechanism> _mechanismCache = new(32);
  ```
- Provide an `IColliderRegistry` service to look up interfaces in $O(1)$ without heap allocations, eliminating all `GetComponentInParent` calls in `FixedUpdate()` (`AGY0012`).
- In `PerformanceMonitor.cs`, replace string interpolations and `OnGUI()` with `TextMeshProUGUI` using pre-allocated `char[]` buffers via `SetCharArray` or `ZString` (`AGY0014`).

#### 4. Risk Mitigation
- **Risk**: Packet loss causing tick gaps on the server.
- **Mitigation**: Server input buffer stores the last 3 inputs. If a tick is missed, duplicate the previous directional input while zeroing one-shot triggers (`JumpTriggered`, `KickTriggered`).

---

### Phase 5: Automated Testing, Network Stress Verification & Certification

#### 1. Objectives
- Validate all bug fixes via automated EditMode and PlayMode integration tests.
- Stress-test input streaming and transport switching under simulated network conditions (latency, jitter, packet loss).
- Verify Zero-GC compliance using Roslyn analyzers and Unity Profiler headless execution.

#### 2. Verification Harness & Test Cases

##### 2.1 EditMode Unit & Architectural Tests
- `TransportSwitchingTests`: Test `SwitchTransportAsync` across all state transitions (`Disconnected`, `Disconnecting`, `Connected`). Verify old transport is stopped and disabled.
- `RoleAuthorityTests`: Verify `PlayerRegistry.DetermineNextAvailableRole` executes only on server, and `SwapRolesOnServer` invokes `NetworkIdentity.GiveOwnership()`.
- `TriggerQueryIntegrityTest`: Static analysis / reflection test verifying that all `Physics.OverlapSphereNonAlloc` calls in player controllers explicitly pass `QueryTriggerInteraction.Collide`.
- `RoslynZeroGCTest`: Verify zero `AGY0011`–`AGY0016` warnings across `Game.Gameplay` and `Game.Network`.

##### 2.2 PlayMode Network Integration Tests
- `TorsoInteractionMultiplayerTest`: Spawn Host and Client in headless batchmode. Client sends 'E' key RPC to toggle `LeverMechanism`. Assert that `LeverMechanism.IsActivated` becomes `true` on both Host and Client within 100ms.
- `DualSimulationStressTest`: Run 2 players for 60 seconds with 100ms simulated RTT and 2% packet loss. Assert that Torso world position remains synchronized with Legs during fusion and separation without `HierarchyV2` exceptions.
- `ReconnectionTest`: Client disconnects during Room 1, reconnects after 5 seconds. Assert that client re-acquires ownership of Torso, role is `PlayerRole.Torso` (not `Observer`), and position matches server.

##### 2.3 Headless CLI Verification Commands
```powershell
# Run EditMode unit tests
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testResults "test_editmode.xml" -batchmode -nographics

# Run PlayMode multiplayer integration tests
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform PlayMode -testResults "test_playmode.xml" -batchmode -nographics
```

---

## 3. Summary of Architectural Adherence

| Constraint | Compliance Mechanism |
|---|---|
| **Server-Authoritative Authority** | All movement, mechanism interactions, and role distributions are validated and simulated on the server. Clients stream inputs and predict locally. |
| **PurrNet Best Practices** | Native `onTick` input streaming, `NetworkRigidbody.SetSoftParent`, `CookieScope.LiveWithProcess`, `requireOwnership: false` overrides. |
| **VContainer Lifecycle** | `NetworkPrefabInjector` bridges dynamic PurrNet instantiations with `IObjectResolver.InjectGameObject()`. Zero service locators. |
| **UniTask Zero-GC Async** | All transport switching, scene transitions, and disconnection handshakes use structured `UniTask` and `CancellationToken`. |
| **Zero-GC Hot Paths** | Collider-to-interface lookup dictionaries eliminate `GetComponentInParent` in `FixedUpdate` (`AGY0012`). String formatting replaced with zero-alloc buffers (`AGY0014`). |
| **Repository Safety** | Zero files outside `c:\Users\SaidR\Documents\GitHub\TPOB` are modified. |
