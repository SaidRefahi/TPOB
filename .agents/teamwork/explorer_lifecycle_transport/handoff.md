# AUDIT REPORT: Online Subsystem Connection Lifecycle, Transport Architecture & Dependency Injection

**Author**: `explorer_lifecycle_transport`  
**Date**: 2026-11-10  
**Scope**: Transport Switching Architecture, Lobby/Roles, Reconnection Lifecycle, VContainer & LifetimeScopes  
**Workspace**: `c:\Users\SaidR\Documents\GitHub\TPOB`

---

## 1. Observation

### 1.1 Transport Switching Architecture & Rebinding

#### Observation 1.1.1: Asymmetric Transport Selection in ConnectionHUD
In `Assets/_Project/Network/UI/ConnectionHUD.cs` (lines 147–187):
```csharp
147:            GUILayout.BeginHorizontal();
148:            GUILayout.Label("Puerto:", GUILayout.Width(60));
149:            _serverPort = GUILayout.TextField(_serverPort, GUILayout.Width(80));
150:            GUILayout.EndHorizontal();
151:
152:            if (GUILayout.Button("▶ Iniciar como Host", GUILayout.Height(35)))
153:            {
154:                if (ushort.TryParse(_serverPort, out ushort port))
155:                {
156:                    _networkService?.StartHost(port);
157:                }
...
177:            if (GUILayout.Button("⚡ Unirse como Cliente", GUILayout.Height(35)))
178:            {
179:                if (ushort.TryParse(_serverPort, out ushort port))
180:                {
181:                    _networkService?.StartClient(_serverAddress, port);
182:                }
```
In `Assets/_Project/Network/Services/NetworkService.cs`:
- `StartHost(ushort port)` (line 330) sets `ServerPort = port` and calls `StartHost()` (line 247).
- `ExecuteStartHostAsync()` (lines 280–328) forces `PurrTransport`:
  ```csharp
  282:            var purr = GetPurrTransport();
  283:            if (purr != null)
  284:            {
  285:                SetActiveTransport(purr);
  286:                if (string.IsNullOrWhiteSpace(purr.roomName))
  287:                {
  288:                    purr.roomName = !string.IsNullOrWhiteSpace(_fallbackRoomName) ? _fallbackRoomName : "TPOB-ROOM";
  289:                }
  290:                purr.attemptDirectConnection = true;
  291:                purr.masterServer = "https://purrtransport.purrservers.com/";
  ```
- Conversely, `StartClient(string address, ushort port)` (lines 423–435):
  ```csharp
  423:        public void StartClient(string address, ushort port)
  424:        {
  425:            var purr = GetPurrTransport();
  426:            if (purr != null && !IsIpAddress(address))
  427:            {
  428:                RoomName = address;
  429:                StartClient();
  430:            }
  431:            else
  432:            {
  433:                StartLanClient(address, port);
  434:            }
  435:        }
  ```
  Where `IsIpAddress("127.0.0.1")` evaluates to `true` (line 480).
  And `StartLanClient(address, port)` (lines 450–473) explicitly selects `UDPTransport`:
  ```csharp
  459:            var udp = GetUDPTransport();
  460:            if (udp != null)
  461:            {
  462:                SetActiveTransport(udp);
  463:                udp.address = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
  464:                udp.serverPort = port;
  465:            }
  466:            _cachedManager.StartClient();
  ```

#### Observation 1.1.2: Additive Transport Components on Same GameObject & Incomplete Teardown
In `Assets/_Project/Network/Services/NetworkService.cs` (lines 205–231):
```csharp
213:            comp = _cachedManager.gameObject.AddComponent<PurrTransport>();
...
226:            comp = _cachedManager.gameObject.AddComponent<UDPTransport>();
```
In `Assets/PurrNet/Runtime/Transports/UDPTransport.cs` (lines 175–198) and `Assets/PurrNet/Runtime/Transports/PurrTransport.cs` (lines 589–613), both transports instantiate independent `LiteNetLib.NetManager` server and client listeners in their respective `OnEnable()`.
In `Assets/PurrNet/Runtime/Managers/NetworkManager.cs` (lines 330–359):
```csharp
333:            set
334:            {
335:                if (_transport)
336:                {
337:                    if (serverState != ConnectionState.Disconnected ||
338:                        clientState != ConnectionState.Disconnected)
339:                    {
340:                        throw new InvalidOperationException(
341:                            PurrLogger.FormatMessage("Cannot change transport while it is being used."));
342:                    }
343:
344:                    TeardownTransportLayer();
345:                }
346:                _transport = value;
```
Where `TeardownTransportLayer()` (lines 376–389) only unbinds event handlers (`_transportLayer.onConnected -= ...`) and does NOT stop, dispose, or disable the previous transport component.

#### Observation 1.1.3: Silent Failure to Rebind Transport During State Transitions
In `Assets/_Project/Network/Services/NetworkService.cs` (lines 233–244):
```csharp
233:        private void SetActiveTransport(GenericTransport targetTransport)
234:        {
235:            EnsureManager();
236:            if (_cachedManager == null || targetTransport == null) return;
237:            if (_cachedManager.transport == targetTransport) return;
238:
239:            if (_cachedManager.serverState == ConnectionState.Disconnected &&
240:                _cachedManager.clientState == ConnectionState.Disconnected)
241:            {
242:                _cachedManager.transport = targetTransport;
243:            }
244:        }
```
When `serverState` or `clientState` is `ConnectionState.Disconnecting` (or still closing WebRTC sockets), `SetActiveTransport` exits silently without assigning `_cachedManager.transport = targetTransport`. The subsequent `StartServer()` or `StartClient()` runs against the old transport.

---

### 1.2 Lobby, Matchmaking & Roles

#### Observation 1.2.1: Dual Local Authority Race in `PlayerRegistry`
In `Assets/_Project/Network/Services/PlayerRegistry.cs` (lines 35–49):
```csharp
35:         private void HandlePlayerConnected(int playerId, bool isLocal)
36:         {
37:             PlayerRole assignedRole = DetermineNextAvailableRole();
38:             var slot = new PlayerSlot(playerId, assignedRole, isLocal);
39:             _players.Add(slot);
40:
41:             if (isLocal)
42:             {
43:                 LocalRole = assignedRole;
44:             }
...
69:         private PlayerRole DetermineNextAvailableRole()
70:         {
71:             bool hasLegs = false;
72:             bool hasTorso = false;
73:
74:             for (int i = 0; i < _players.Count; i++)
75:             {
76:                 if (_players[i].Role == PlayerRole.Legs) hasLegs = true;
77:                 if (_players[i].Role == PlayerRole.Torso) hasTorso = true;
78:             }
79:
80:             if (!hasLegs) return PlayerRole.Legs;
81:             if (!hasTorso) return PlayerRole.Torso;
82:             return PlayerRole.Observer;
83:         }
```
When a client joins, `_networkService.OnPlayerConnected` triggers locally on the client. At that instant, `_players` on the client has 0 entries. `DetermineNextAvailableRole()` executes on the client and assigns `PlayerRole.Legs` to itself (`LocalRole = PlayerRole.Legs`), while on the server it was assigned `PlayerRole.Torso`.

#### Observation 1.2.2: Fragmented Role & Ownership Authority
- `LobbyNetworkController.cs` (lines 262–308) manages role assignments via server RPC `RequestRoleSelectionServerRpc` and syncs via `SyncLobbyStateObserversRpc`.
- `TPOBPlayerSpawner.cs` (lines 328–360) ignores the lobby if not yet assigned, determining roles by querying scene instances:
  ```csharp
  341:            bool hasLegs = Object.FindFirstObjectByType<LegsController>() != null;
  342:            bool hasTorso = Object.FindFirstObjectByType<TorsoController>() != null;
  ```
- `ConnectionHUD.cs` (lines 301–320) implements "Intercambiar Roles (Swap)":
  ```csharp
  303:                var relay = Object.FindFirstObjectByType<NetworkEventRelay>();
  304:                if (relay != null)
  305:                {
  306:                    if (relay.isServer)
  307:                    {
  308:                        _playerRegistry?.SwapRoles();
  309:                    }
  310:                    else
  311:                    {
  312:                        relay.RequestSwapRolesServerRpc();
  313:                    }
  314:                }
  ```
  In `NetworkEventRelay.cs` (lines 67–81):
  ```csharp
  80:             _playerRegistry?.SwapRoles();
  ```
  `TPOBPlayerSpawner.SwapRolesOnServer()` (lines 296–326) is never called. PurrNet `NetworkIdentity.GiveOwnership()` is never executed on the actual player game objects (`Legs` / `Torso`).

#### Observation 1.2.3: Editor Multi-Play Ownership Hijack
In `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 206–218, 243–252):
```csharp
206: #if UNITY_EDITOR
207:             if (_spawnedPlayers.Count <= 1)
208:             {
209:                 var torso = Object.FindFirstObjectByType<TorsoController>();
210:                 if (torso != null && torso.TryGetComponent<NetworkIdentity>(out var torsoIdentity))
211:                 {
212:                     if (!torsoIdentity.hasOwner || torsoIdentity.owner == PlayerID.Server)
213:                     {
214:                         torsoIdentity.GiveOwnership(localPlayer);
215:                     }
216:                 }
217:             }
218: #endif
```
When running in Unity Editor (including Multiplayer Play Mode virtual players), the host assigns itself ownership of both Legs and Torso before Client 2 can be bound.

---

### 1.3 Reconnection Lifecycle & State Restoration

#### Observation 1.3.1: Ignored Reconnection Flag & Disconnect Eviction
In `Assets/_Project/Network/Services/NetworkService.cs` (line 580):
```csharp
580:         private void HandlePlayerJoined(PlayerID player, bool isReconnect, bool asServer)
```
The parameter `bool isReconnect` is completely dropped; neither `OnPlayerConnected` nor `PlayerConnectedEvent` exposes `isReconnect`.
In `Assets/_Project/Network/Services/PlayerRegistry.cs` (lines 51–67):
```csharp
51:         private void HandlePlayerDisconnected(int playerId)
52:         {
...
62:                     _players.RemoveAt(i);
63:                     OnPlayerUnregistered?.Invoke(playerId);
```
All player registration, slot reservation, and role mappings are purged on disconnect.

#### Observation 1.3.2: Reconnecting Player Trapped in Observer Role
In `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 328–360, 362–384):
```csharp
368:             if (_spawnedPlayers.Contains(playerIdInt)) return;
374:             _spawnedPlayers.Add(playerIdInt);
376:             PlayerRole role = DetermineRoleForPlayer(player);
378:             SpawnPlayerForPlayer(player, role);
```
When a player reconnects, their ID was evicted from `_spawnedPlayers` and `PlayerRegistry`. `DetermineRoleForPlayer` runs:
```csharp
341:             bool hasLegs = Object.FindFirstObjectByType<LegsController>() != null;
342:             bool hasTorso = Object.FindFirstObjectByType<TorsoController>() != null;
...
354:                 role = PlayerRole.Observer;
```
Because `LegsController` and `TorsoController` remain in the scene, both `hasLegs` and `hasTorso` evaluate to `true`. The reconnecting player is assigned `PlayerRole.Observer`. In `SpawnPlayerForPlayer` (line 427), no object is assigned to an `Observer`, leaving the reconnected player permanently disembodied.

#### Observation 1.3.3: Absence of State Restoration Architecture
Across the entire repository (`Assets/_Project`), no memento, snapshot, or resync mechanism exists to restore:
- Checkpoints (`CheckpointSystem.cs`)
- Room progress / mechanisms (`RoomController.cs`, `CatapultMechanism.cs`, `BreakableWall.cs`)
- Robot fusion state (`RobotCoordinator.cs`). If a client reconnects while the host is fused, the client receives no re-parenting command because `PerformFusionLocally()` is only triggered by an unbuffered `OnFusedObserversRpc`.

---

### 1.4 VContainer & Network LifetimeScopes

#### Observation 1.4.1: Network Spawning Completely Bypasses VContainer Injection
In `Assets/_Project/Network/Services/NetworkService.cs` (lines 555–556) and `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 489–515):
```csharp
555:                 var instance = UnityEngine.Object.Instantiate(prefabToSpawn);
556:                 _cachedManager.Spawn(instance);
```
GameObjects and `NetworkBehaviour` components are instantiated via `UnityEngine.Object.Instantiate` or `UnityProxy.Instantiate`. VContainer's `IObjectResolver` is not invoked.
As a consequence, all `[Inject]` methods on spawned network behaviors fail to receive injections:
- `TorsoController.Construct(IGameEventBus)` (lines 127–131)
- `LobbyNetworkController.Construct(...)` (lines 53–67)
- `NetworkEventRelay.Construct(...)` (lines 22–35)
- `TPOBPlayerSpawner.Construct(...)` (lines 43–48)

#### Observation 1.4.2: Anti-Pattern Service Locator Fallback
To circumvent injection failure, multiple scripts perform manual static lookups against `GameLifetimeScope.Instance`:
- `TorsoController.cs` (lines 986–988):
  ```csharp
  if (_eventBus == null && GameLifetimeScope.Instance != null && GameLifetimeScope.Instance.Container != null)
      try { _eventBus = GameLifetimeScope.Instance.Container.Resolve<IGameEventBus>(); } catch { }
  ```
- `LobbyNetworkController.cs` (lines 132–147):
  ```csharp
  var container = GameLifetimeScope.Instance != null ? GameLifetimeScope.Instance.Container : null;
  if (container != null) { ... container.Resolve<IPlayerRegistry>(); ... }
  ```
- `NetworkEventRelay.cs` (lines 48–63):
  ```csharp
  _eventBus = GameLifetimeScope.Instance.Container.Resolve<IGameEventBus>();
  ```
- `ConnectionHUD.cs` (lines 53–61):
  ```csharp
  var scopes = Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
  for (int i = 0; i < scopes.Length; i++)
      scopes[i].Container.Inject(this);
  ```

#### Observation 1.4.3: Root LifetimeScope Holding Destroyed Scene References
In `Assets/_Project/Network/Scopes/GameLifetimeScope.cs` (lines 54, 108–112):
```csharp
54:             DontDestroyOnLoad(gameObject);
...
108:            var hud = FindFirstObjectByType<Game.Network.UI.ConnectionHUD>();
109:            if (hud != null)
110:            {
111:                builder.RegisterComponent(hud);
112:            }
```
`ConnectionHUD` is located in the initial scene and does NOT call `DontDestroyOnLoad`. When `LevelManager` executes `LoadRoomAsync(0)` with `LoadSceneMode.Single`, the `ConnectionHUD` GameObject is destroyed by the engine, leaving `GameLifetimeScope.Container` with a dangling reference to a destroyed Unity component.

#### Observation 1.4.4: Subsystem Duplication on Scope Decoupling
In `Assets/_Project/Gameplay/Rooms/RoomLifetimeScope.cs` (lines 50–67):
```csharp
50:             if (FindParent() == null)
51:             {
52:                 builder.Register<GameEventBus>(Lifetime.Singleton).As<IGameEventBus>();
53:                 builder.Register<GameManager>(Lifetime.Singleton).As<IGameManager>();
54:                 builder.Register<NetworkService>(Lifetime.Singleton).As<INetworkService>();
55:                 builder.Register<PlayerRegistry>(Lifetime.Singleton).As<IPlayerRegistry>();
...
```
If a room is loaded directly in development or if parent lookup fails during scene transition, `RoomLifetimeScope` instantiates a duplicate set of core singletons, isolating the room's event bus and registries from the rest of the application.

---

### 1.5 Torso Interaction ('E') & Fusion Desynchronization

#### Observation 1.5.1: Key 'E' Intent Cannibalization in `TorsoController`
In `Assets/_Project/Gameplay/Player/Torso/TorsoInputReader.cs` (lines 76–81):
```csharp
76:             var keyboard = Keyboard.current;
77:             if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame))
78:             {
79:                 _grabTriggered = true;
80:                 _interactTriggered = true;
81:             }
```
In `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` (lines 433–436):
```csharp
433:            ApplyGrabAndRelease();
434:            ApplyThrow();
435:            ApplyMagnet();
436:            ApplyMechanismInteraction();
```
Where `ApplyGrabAndRelease()` runs first:
```csharp
566:            if (IsHoldingObject)
567:            {
568:                ReleaseHeldObject();
569:                _pendingInput.InteractTriggered = false; // Cancels interaction
570:                return;
571:            }
...
587:                    Grab(grabbable);
588:                    _pendingInput.InteractTriggered = false; // Cancels interaction
```
If the Torso is holding any object or in proximity of a grabbable object, `ApplyGrabAndRelease` zeroes out `_pendingInput.InteractTriggered`, preventing `ApplyMechanismInteraction()` from ever executing.

#### Observation 1.5.2: Unauthorized Local Parenting in Fusion
In `Assets/_Project/Gameplay/Player/Robot/FusionStrategy.cs` (line 40):
```csharp
40:                 context.Torso.transform.SetParent(attachPoint, false);
```
In `Assets/PurrNet/Runtime/CoreModules/HierarchyV2/HierarchyV2.cs` (lines 680–686):
PurrNet's `HierarchyV2` enforces authority over transform hierarchy changes on `NetworkIdentity` components. Calling Unity's `SetParent` locally on non-server peers without PurrNet parent-change authority triggers `OnUnauthorizedLocalParentChange`:
> *"Parent change of 'Torso' was ignored and will be reverted: local player lacks change-parent authority..."*

The client forcibly snaps the Torso back, desynchronizing the Torso's world position from the server, causing mechanism interaction queries (`center = transform.position + transform.forward * 0.3f`) to miss completely.

---

## 2. Logic Chain

```
[Observation 1.1.1] ConnectionHUD "StartHost" -> NetworkService.ExecuteStartHostAsync -> Forces PurrTransport (Relay).
[Observation 1.1.1] ConnectionHUD "StartClient" (127.0.0.1) -> NetworkService.StartLanClient -> Forces UDPTransport.
       │
       ▼
[Deduction 1] Host is listening on PurrTransport cloud relay; Client is broadcasting UDP packets to 127.0.0.1:5000.
       │
       ▼
[Conclusion 1] The two instances run completely incompatible transport protocols. Client experiences immediate timeout or unreachable host error.

[Observation 1.1.2] NetworkService attaches both PurrTransport and UDPTransport as components to NetworkManager.
[Observation 1.1.2] NetworkManager.transport setter calls TeardownTransportLayer() without stopping/disposing old transport.
       │
       ▼
[Deduction 2] Deactivated transport retains bound sockets, background pump threads, and event subscriptions.
       │
       ▼
[Conclusion 2] Transport switching causes socket collisions, leaked background tasks, and uncollected network buffers.

[Observation 1.2.1] Client evaluates PlayerRegistry.DetermineNextAvailableRole() with empty local _players list.
[Observation 1.2.3] Host in Editor grants itself ownership of both Legs and Torso in TPOBPlayerSpawner.
       │
       ▼
[Deduction 3] Both Host and Client locally claim PlayerRole.Legs. Server never grants Torso NetworkIdentity ownership to Client.
       │
       ▼
[Conclusion 3] Client has isOwner == false on Torso, causing TorsoController.Update (line 313) to drop all inputs.

[Observation 1.3.1] NetworkService and PlayerRegistry wipe player slots on disconnect.
[Observation 1.3.2] TPOBPlayerSpawner.DetermineRoleForPlayer finds existing scene Legs and Torso on reconnect -> assigns Observer.
       │
       ▼
[Deduction 4] Reconnecting player is classified as Observer; Spawner refuses to bind them to the existing vacant Torso.
       │
       ▼
[Conclusion 4] Reconnection leaves players permanently disembodied without control of their avatar.

[Observation 1.4.1] NetworkManager.Spawn and UnityProxy.Instantiate bypass VContainer.
[Observation 1.4.2] NetworkBehaviours rely on GameLifetimeScope.Instance.Container.Resolve in Start/Update.
[Observation 1.4.3] GameLifetimeScope holds reference to scene-destroyed ConnectionHUD.
       │
       ▼
[Deduction 5] NetworkBehaviours have null dependencies if spawned before or outside GameLifetimeScope resolution.
       │
       ▼
[Conclusion 5] Architecture violates DI inversion, creating runtime NullReferenceExceptions and memory leaks across scene reloads.
```

---

## 3. Caveats

1. **PurrTransport Cloud Infrastructure Availability**: Relay auto-ping in `NetworkService.cs` (lines 297–312) depends on `https://purrtransport.purrservers.com/`. If offline or behind a restrictive firewall, the 1500ms delay elapses and falls back to default region settings without notifying the user.
2. **Multiplayer Play Mode vs Standalone Builds**: The `#if UNITY_EDITOR` blocks in `TPOBPlayerSpawner.cs` (lines 206–218 and 243–252) specifically distort ownership behaviors in the Unity Editor and Multiplayer Play Mode. Standalone builds exhibit different timing, though the underlying race conditions remain.
3. **LAN Broadcast Filtering**: `LanDiscoveryService.cs` binds to `47777` with `IPAddress.Any` and `ReuseAddress`. On multi-NIC systems or VPN adapters (e.g. Tailscale, Hamachi), broadcast packets may bind to incorrect interfaces.

---

## 4. Conclusion

The online subsystem in TPOB suffers from fundamental architectural flaws across four interconnected layers:

| Layer | Primary Flaw | Severity | Impact |
|---|---|---|---|
| **Transport Architecture** | Asymmetric transport instantiation (`ConnectionHUD` starts Host on PurrTransport and Client on UDPTransport); additive component accumulation without socket disposal. | **CRITICAL** | Clients cannot connect to local hosts; switching transports leaks sockets and locks up `NetworkManager`. |
| **Role & Ownership** | Split authority across 3 systems (`PlayerRegistry`, `LobbyNetworkController`, `TPOBPlayerSpawner`); Editor ownership hijack; Role Swap modifies registry but not PurrNet ownership. | **HIGH** | Client cannot control Torso; controls are dropped; desync between visual roles and actual input authority. |
| **Reconnection Lifecycle** | No reconnection state restoration; disconnecting purges player identity; reconnecting player is classified as `Observer` because scene avatars still exist. | **HIGH** | Reconnection results in permanent loss of avatar control and game desync. |
| **Dependency Injection** | Network-spawned entities bypass VContainer; static service locator anti-patterns; root `GameLifetimeScope` retains destroyed scene references. | **HIGH** | `NullReferenceException` risks on scene transitions; untestable components; memory leaks. |
| **Interaction & Fusion** | Key 'E' triggers both Grab and Interact; `ApplyGrabAndRelease` cancels `InteractTriggered`; local `SetParent` during fusion violates PurrNet hierarchy rules. | **HIGH** | Key 'E' fails in Torso mode; Torso snaps back on clients during fusion. |

---

## 5. Verification Method

### 5.1 Independent Codebase Verification
Inspect the following files and line ranges using `view_file`:
1. **Transport Mismatch**: Check `Assets/_Project/Network/UI/ConnectionHUD.cs:152-187` and trace into `Assets/_Project/Network/Services/NetworkService.cs:282-291` vs `459-466`.
2. **Observer Reconnection Bug**: Check `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs:341-356`.
3. **Role Swap Disconnect**: Check `Assets/_Project/Network/Events/NetworkEventRelay.cs:67-81` and verify absence of `GiveOwnership` calls.
4. **Key 'E' Bug**: Check `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs:566-570` and `587-589` where `_pendingInput.InteractTriggered = false` precedes `ApplyMechanismInteraction()`.
5. **VContainer Dangling Reference**: Check `Assets/_Project/Network/Scopes/GameLifetimeScope.cs:108-112`.

### 5.2 Test & Runtime Verification
- Execute Unity PlayMode tests via command line:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -runTests -testPlatform EditMode -testResults "test_results.xml" -batchmode -nographics
  ```
- Invalidation Condition: If `ConnectionHUD` is modified to allow selecting LAN Host explicitly, and `TPOBPlayerSpawner` is refactored to look up vacant avatars by role on reconnection, test with 2 players will verify role retention and immediate reconnect ability.
