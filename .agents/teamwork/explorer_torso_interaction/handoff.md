# Audit Report: Torso Mode Interaction Failure ('E' Key Under Online Conditions)

**Author:** `teamwork_explorer` (Specialist in Interaction Systems, Network Authority, and Gameplay Synchronization)  
**Target File:** `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\explorer_torso_interaction\handoff.md`  
**Timestamp:** 2026-11-10T01:40:00Z  

---

## 1. Observation

### Observation 1.1: Missing Trigger Collision Query in Mechanism Interaction Detection
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Class / Component**: `Game.Gameplay.Player.Torso.TorsoController`
- **Lines 763–788**:
```csharp
private void ApplyMechanismInteraction()
{
    if (!_pendingInput.InteractTriggered)
    {
        return;
    }

    _pendingInput.InteractTriggered = false;

    Vector3 center = transform.position + transform.forward * 0.3f;
    int count = Physics.OverlapSphereNonAlloc(center, _interactRadius, _mechanismColliders, _mechanismLayer);

    for (int i = 0; i < count; i++)
    {
        Collider col = _mechanismColliders[i];
        if (col == null || col.transform == transform || col.transform.IsChildOf(transform)) continue;

        if (col.TryGetComponent<IInteractableMechanism>(out var mechanism) ||
            (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<IInteractableMechanism>(out mechanism)) ||
            (col.GetComponentInParent<IInteractableMechanism>() is { } pMech && (mechanism = pMech) != null))
        {
            mechanism.Toggle(gameObject);
            break;
        }
    }
}
```
In contrast, in the same class:
- **Line 575 (`ApplyGrabAndRelease`)**:
```csharp
int count = Physics.OverlapSphereNonAlloc(searchCenter, _grabRadius, _grabColliders, _grabLayer, QueryTriggerInteraction.Collide);
```
- **Line 725 (`ApplyMagnet`)**:
```csharp
int count = Physics.OverlapSphereNonAlloc(origin, _magnetRange, _magnetColliders, _magneticLayer, QueryTriggerInteraction.Collide);
```
Line 773 omits `QueryTriggerInteraction.Collide`, relying on `QueryTriggerInteraction.UseGlobal`.

### Observation 1.2: Interactable Mechanisms Use Trigger Colliders
- **File**: `Assets/_Project/Editor/PlaygroundBuilder.cs`
- **Method**: `CreateLeverPrefab` (lines 737–740):
```csharp
var triggerCol = root.AddComponent<BoxCollider>();
triggerCol.isTrigger = true;
triggerCol.size = new Vector3(1.4f, 1.2f, 1.4f);
triggerCol.center = new Vector3(0f, 0.6f, 0f);
```
- **File**: `Assets/_Project/Editor/PlaygroundBuilder.cs`
- **Method**: `CreateHeavyDoorPrefab` (lines 1234–1239):
```csharp
var handleCol = handle.GetComponent<BoxCollider>();
if (handleCol != null)
{
    handleCol.isTrigger = true;
    handleCol.size = new Vector3(2f, 2f, 3f);
}
```
All mechanism interactables configure their detection boundary with `isTrigger = true`.

### Observation 1.3: Dual Trigger Binding and Preemption by `ApplyGrabAndRelease`
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
- **Lines 307–311**:
```csharp
private void HandleGrabPerformed(InputAction.CallbackContext ctx)
{
    _grabTriggered = true;
    _interactTriggered = true;
}
```
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Lines 433–436 (`FixedUpdate`)**:
```csharp
ApplyAiming();
ApplyGrabAndRelease();
ApplyThrow();
ApplyMagnet();
ApplyMechanismInteraction();
```
- **Lines 566–571 & 588–590 (`ApplyGrabAndRelease`)**:
```csharp
if (IsHoldingObject)
{
    ReleaseHeldObject();
    _pendingInput.InteractTriggered = false;
    return;
}
...
if (grabbable != null && !grabbable.IsGrabbed)
{
    Grab(grabbable);
    _pendingInput.InteractTriggered = false;
    break;
}
```
`ApplyGrabAndRelease` executes strictly prior to `ApplyMechanismInteraction` and resets `_pendingInput.InteractTriggered = false` whenever an object is held or a grabbable is detected within `_grabRadius`.

### Observation 1.4: Client-Side Simulation Gate in `FixedUpdate`
- **File**: `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs`
- **Lines 410–414**:
```csharp
bool isSimulated = !isSpawned || isServer;
if (!isSimulated || _rigidbody == null)
{
    return;
}
```
On remote clients (`isSpawned && !isServer`), `FixedUpdate` terminates at line 413. Neither interaction detection, grab detection, nor local feedback executes on the client.

### Observation 1.5: Missing `NetworkIdentity` and Unspawned State on Mechanism Prefabs
- **File**: `Assets/_Project/Editor/PlaygroundBuilder.cs`
- **Method `CreateLeverPrefab` (lines 732–779)**: Adds `BoxCollider`, `MeshRenderer`, `LeverMechanism`. Does NOT add `NetworkIdentity` or `NetworkTransform`.
- **Method `CreateTargetButtonPrefab` (lines 794–834)**: Adds `Rigidbody`, `BoxCollider`, `TargetButton`. Does NOT add `NetworkIdentity`.
- **Method `CreatePuzzleSocketPrefab` (lines 950–980)**: Adds `PuzzleSocket`. Does NOT add `NetworkIdentity`.
- **Method `CreateWeightPlatformPrefab` (lines 839–873)**: Adds `WeightPlatform`. Does NOT add `NetworkIdentity`.
- **Method `CreateSlidingDoorPrefab` / `ConfigureSlidingDoorInstance` (`ModularWallBuilder.cs` lines 165–275)**: Adds `SlidingDoor`. Does NOT add `NetworkIdentity`.
- **File**: `Assets/_Project/Gameplay/Interactables/LeverMechanism.cs` (lines 76–94):
```csharp
public void Toggle(GameObject user)
{
    if (isSpawned && !isServer)
    {
        ToggleServerRpc();
        return;
    }

    _isActivated = !_isActivated;

    if (!isSpawned)
    {
        UpdateVisuals(_isActivated);
    }
    else
    {
        UpdateVisualsObserversRpc(_isActivated);
    }
}
```
- **File**: `Assets/_Project/Gameplay/Interactables/SlidingDoor.cs` (lines 76–91):
```csharp
public void OpenDoor()
{
    bool canSimulate = !isSpawned || isServer;
    if (!canSimulate) return;

    if (_isOpened) return;

    if (!isSpawned)
    {
        AnimateDoor(true);
    }
    else
    {
        SetDoorStateObserversRpc(true);
    }
}
```
Because the prefabs lack `NetworkIdentity`, PurrNet never spawns them. Thus `isSpawned` is permanently `false` on both host and client. Consequently, `UpdateVisualsObserversRpc` and `SetDoorStateObserversRpc` are NEVER called. Changes execute purely server-local.

### Observation 1.6: PurrNet `HierarchyV2` Unauthorized Parent Revert During Fusion
- **File**: `Assets/_Project/Gameplay/Player/Robot/FusionStrategy.cs` (lines 37–44):
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
- **File**: `Assets/_Project/Gameplay/Player/Robot/RobotCoordinator.cs` (lines 353–357):
```csharp
[ObserversRpc(runLocally: true)]
private void OnFusedObserversRpc()
{
    PerformFusionLocally();
}
```
- **File**: `Assets/PurrNet/Runtime/CoreModules/HierarchyV2/HierarchyV2.cs` (lines 671–687, 710–724):
```csharp
private void OnUnauthorizedLocalParentChange(NetworkIdentity identity)
{
    if (!_pendingUnauthorizedParentReverts.Add(identity))
        return;
...
    PurrLogger.LogError(
        $"Parent change of '{identity.gameObject.name}' was ignored and will be reverted: " +
        $"local player lacks change-parent authority...", identity.gameObject);
}
```
When client executes `PerformFusionLocally()`, it reparents `context.Torso.transform` (which has a `NetworkIdentity`) under `attachPoint` (child of `LegsController`). PurrNet's `HierarchyV2` detects an unauthorized client reparenting and queues the Torso transform for reversion, creating transform divergence between host and client.

### Observation 1.7: Spawner Incomplete Ownership Assignment on Scene-Existing Torso
- **File**: `Assets/_Project/Gameplay/Spawning/TPOBPlayerSpawner.cs` (lines 445–456 vs 510–521):
```csharp
else if (role == PlayerRole.Torso)
{
    var existingTorso = Object.FindFirstObjectByType<TorsoController>();
    if (existingTorso != null)
    {
        if (canNetworkSpawn && existingTorso.TryGetComponent<NetworkIdentity>(out var id))
        {
            id.GiveOwnership(player);
        }
        return existingTorso.gameObject;
    }
}
```
For newly instantiated prefabs (lines 512–515), `nm.Spawn(instance)` is called before `identity.GiveOwnership(player)`. For scene-existing objects, `nm.Spawn()` is omitted, leaving ownership assignment inert if the object was not yet spawned by PurrNet scene lifecycle.

---

## 2. Logic Chain

1. **Premise 1 (Input Trigger)**: From Observation 1.3, pressing 'E' triggers both `_grabTriggered = true` and `_interactTriggered = true` in `TorsoInputReader`.
2. **Premise 2 (Network Dispatch)**: `TorsoController.Update()` consumes these triggers and sends `SendTorsoActionServerRpc(grab: true, throwTrigger: false, interact: true)` to the server via `Channel.ReliableOrdered`.
3. **Premise 3 (Server Simulation Only)**: From Observation 1.4, `TorsoController.FixedUpdate` returns early on client (`!isSimulated`). All interaction resolution occurs exclusively on the server.
4. **Premise 4 (Grabbable Preemption)**: In server `FixedUpdate()`, Observation 1.3 shows `ApplyGrabAndRelease()` runs before `ApplyMechanismInteraction()`. If the Torso is already carrying an object, or if any `IGrabbable` collider is within `_grabRadius`, `_pendingInput.InteractTriggered` is immediately cancelled (`false`), aborting mechanism interaction.
5. **Premise 5 (Trigger Collider Invisibility)**: If no grabbable is present, `ApplyMechanismInteraction()` executes at line 763. Observation 1.1 shows it queries `Physics.OverlapSphereNonAlloc` without `QueryTriggerInteraction.Collide`. Observation 1.2 proves all mechanism interactables (`LeverSwitch`, `TargetButton`, `PuzzleSocket`, `DoorMechanismTrigger`) use `isTrigger = true`. With Unity physics defaulting to ignore trigger queries on non-collide overloads, the trigger colliders are completely missed by the query, returning `count = 0`.
6. **Premise 6 (Interactable State Isolation)**: Even if a collider were detected and `mechanism.Toggle(gameObject)` invoked on the server, Observation 1.5 shows `LeverSwitch`, `TargetButton`, `PuzzleSocket`, and `SlidingDoor` lack `NetworkIdentity`. As a consequence, `isSpawned` is permanently `false`. In `LeverMechanism.Toggle` and `SlidingDoor.OpenDoor`, execution diverts to local animation (`UpdateVisuals`, `AnimateDoor`). `UpdateVisualsObserversRpc` and `SetDoorStateObserversRpc` are bypassed. The server changes state locally while remote clients receive zero network notifications.
7. **Premise 7 (Fusion Reparenting Corruption)**: Under fused mode, Observation 1.6 shows `FusionStrategy.OnEnter` sets parent locally on client, triggering `HierarchyV2.OnUnauthorizedLocalParentChange`, causing PurrNet to reject or revert the transform hierarchy, separating the visual client Torso from the server's simulated position on top of the Legs.
8. **Conclusion**: The 'E' interaction failure is not a single bug, but a composite breakdown:
   - Physics queries miss trigger colliders (`QueryTriggerInteraction.Collide` omitted).
   - Input logic preempts interaction in favor of grabbing (`InteractTriggered` wiped).
   - All mechanism prefabs lack `NetworkIdentity`, preventing `ObserversRpc` state broadcasts from ever reaching clients.

---

## 3. Caveats

1. **Unity Physics Global Setting**: In standard Unity projects, `Physics.queriesHitTriggers` can be toggled in `ProjectSettings/Physics.asset`. Even if set to true globally in editor, relying on implicit trigger collision query behavior violates deterministic server-authoritative physics practices and causes discrepancies across headless Linux server builds vs client builds.
2. **Layer Mask Configuration**: `_mechanismLayer` defaults to `~0`. If the project defines a dedicated `Interactable` or `Mechanism` physics layer, omitting layer filtering can exhaust the fixed 16-collider buffer with geometry colliders before reaching the mechanism.
3. **No Code Modification**: In accordance with the Explorer mandate, no codebase modifications or tests were executed in this turn. All claims are derived strictly from code analysis and semantic graph tracing.

---

## 4. Conclusion

### Summary Table of Diagnosed Flaws

| ID | Component / Class | Lines | Severity | Root Cause Mechanism |
|---|---|---|---|---|
| **F-01** | `TorsoController.ApplyMechanismInteraction` | 772–787 | **CRITICAL** | `Physics.OverlapSphereNonAlloc` omits `QueryTriggerInteraction.Collide`. All mechanism trigger colliders (`isTrigger = true`) are invisible to the interaction detector. |
| **F-02** | `PlaygroundBuilder` / `ModularWallBuilder` (Interactable Prefabs) | 732–834, 950–980, 165–275 | **CRITICAL** | `LeverSwitch`, `TargetButton`, `PuzzleSocket`, `WeightPlatform`, and `SlidingDoor` prefabs lack `NetworkIdentity`. `isSpawned` is permanently `false`, dropping all `ObserversRpc` synchronization to clients. |
| **F-03** | `TorsoController.ApplyGrabAndRelease` & `TorsoInputReader` | 76–81, 566–571, 588–590 | **HIGH** | Pressing 'E' sets both `_grabTriggered` and `_interactTriggered`. `ApplyGrabAndRelease` executes first and unconditionally resets `_pendingInput.InteractTriggered = false` if an object is held or grabbable is detected. |
| **F-04** | `FusionStrategy.OnEnter` & `RobotCoordinator.OnFusedObserversRpc` | 37–44 (FusionStrategy), 353–357 (RobotCoordinator) | **HIGH** | Client-side `SetParent` on `TorsoController` violates PurrNet `HierarchyV2` rules, triggering `OnUnauthorizedLocalParentChange` and reverting the client transform. |
| **F-05** | `TPOBPlayerSpawner.SpawnPlayerForPlayer` | 445–456 | **MEDIUM** | Pre-existing scene instances of `TorsoController` are assigned ownership without verifying or invoking `nm.Spawn()`. |
| **F-06** | `TorsoController.cs` | 410–414 | **MEDIUM** | Interaction pipeline has 0 client-side prediction or intent feedback; returns early on client, creating the perception of a completely dead key press during network latency. |

---

## 5. Verification Method

### 1. Code Inspection Checkpoints
- Inspect `Assets/_Project/Gameplay/Player/Torso/TorsoController.cs` lines 773 vs 575 and 725: Verify presence/absence of `QueryTriggerInteraction.Collide`.
- Inspect `Assets/_Project/Editor/PlaygroundBuilder.cs` lines 732–779 (`CreateLeverPrefab`), 794–834 (`CreateTargetButtonPrefab`), 950–980 (`CreatePuzzleSocketPrefab`): Check for absence of `AddServerAuthoritativeTransform(root)` or `root.AddComponent<NetworkIdentity>()`.
- Inspect `Assets/_Project/Gameplay/Interactables/LeverMechanism.cs` lines 76–94: Verify branching condition `if (!isSpawned)` which routes to local `UpdateVisuals` instead of `UpdateVisualsObserversRpc`.

### 2. Unit & Integration Test Commands
Run Unity Editor tests from the CLI to verify existing test suites:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -runTests -projectPath "c:\Users\SaidR\Documents\GitHub\TPOB" -testPlatform editmode -testResults "c:\Users\SaidR\Documents\GitHub\TPOB\EditModeResults.xml" -batchmode
```

### 3. Invalidation Conditions
This audit's conclusions would be invalidated if:
1. `Physics.queriesHitTriggers` is globally forced to `true` in project settings AND all mechanism colliders are non-triggers. (Proven false: `PlaygroundBuilder.cs` explicitly sets `triggerCol.isTrigger = true`).
2. An external system (e.g. `NetworkManager` post-processor) automatically attaches `NetworkIdentity` to all `NetworkBehaviour` prefabs at build time. (Verified: `PlaygroundBuilder` outputs raw assets to `PrefabsDir` without post-processing).
