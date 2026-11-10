# FINAL HANDOFF REPORT: Online Networking Subsystem Architectural Audit & Remediation Plan (/plan)

**Author**: Teamwork Orchestrator  
**Parent Conversation ID**: `a6e63d9f-f7e8-48c2-bcc6-ef1a8c538484`  
**Working Directory**: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\orchestrator\`  
**Target Codebase**: `c:\Users\SaidR\Documents\GitHub\TPOB` (Unity 6000.3.7f1, PurrNet 1.23.0-beta.51)  
**Date**: 2026-11-10T01:52:00Z  

---

## 1. Observation

A full multi-agent exploration and specification mining sweep was executed across the online networking subsystem in TPOB without modifying any codebase files. The team examined:
1. PurrNet 1.23.0-beta.51 official documentation, package code, and server-authoritative physics guidelines.
2. Connection lifecycle, transport selection, socket teardown, and VContainer lifetime scopes.
3. Torso mode interaction failure ('E' key), authority validation, and mechanism network prefabs.
4. Input streaming, dual simulation mechanics, fixed-tick synchronization, and Zero-GC compliance.

### Summary of Discovered Defects (14 Distinct Diagnostic Items)
- **Torso 'E' Key Interaction**:
  - `TorsoController.ApplyMechanismInteraction()` (line 773) omits `QueryTriggerInteraction.Collide` in `Physics.OverlapSphereNonAlloc`, rendering all trigger-based interactables invisible.
  - Procedural mechanism builders (`PlaygroundBuilder.cs:732–980`, `ModularWallBuilder.cs:165–275`) omit `NetworkIdentity` on prefabs, causing `isSpawned == false` permanently and bypassing all client/server synchronization RPCs.
  - `ApplyGrabAndRelease()` zeroes out `InteractTriggered` if holding an object or near an `IGrabbable`, preempting mechanism interaction.
  - Default `requireOwnership: true` on `SendTorsoActionServerRpc` causes PurrNet's client-side `ValidateSendingRPC` to suppress packets whenever the client is not the identity owner.
- **Input Streaming & Dual Simulation**:
  - Naive passive puppet simulation (`isSimulated = isServer`) forces clients to wait for full RTT + interpolation delay (~150–250ms), creating severe perceived lag.
  - Dual unthrottled RPC streams (`LegsController` + `CommandInvoker`) inside `Update()` at render frame rates produce 240+ RPCs/s, saturating transport buffers.
  - In fused mode, `HandleSyncedFusedRotationChanged` checks `!isServer` instead of `!isOwner`, overwriting Pilot B's active local aim with delayed server SyncVars and causing severe camera jitter.
  - `FusionStrategy.cs` calls `SetParent` directly and disables `NetworkTransform`, violating PurrNet's `HierarchyV2` rules instead of using `NetworkRigidbody.SetSoftParent`.
  - Input structs lack network tick sequence numbers.
- **Transport Switching & Lifecycle**:
  - Asymmetric transport selection in `ConnectionHUD.cs`: "Start Host" binds PurrTransport (Cloud Relay), while "Start Client" (127.0.0.1) binds UDPTransport, making connections impossible.
  - Additive transport components on `NetworkManager` leak active sockets and background threads upon switching.
  - `SetActiveTransport` checks `ConnectionState.Disconnected` synchronously during asynchronous teardown, silently skipping transport rebinding.
- **Roles, Reconnection & VContainer**:
  - Local role guessing on clients in `PlayerRegistry` clashes with server assignments.
  - Reconnection drops `isReconnect` and classifies returning players as `Observer` because existing avatars remain in the scene.
  - Network prefabs instantiated by PurrNet bypass VContainer injection, forcing static service locators (`GameLifetimeScope.Instance.Container.Resolve`).
  - Root `GameLifetimeScope` retains dangling references to scene-destroyed `ConnectionHUD`.
  - `GetComponentInParent` calls inside `FixedUpdate()` violate Zero-GC rule `AGY0012`.

---

## 2. Logic Chain

1. **Root Cause Synthesis**:
   - The reported issues are not random network glitches; they stem from deterministic architectural divergences from PurrNet's core framework and project DI constraints.
   - The interaction failure is a quadruple failure cascade (trigger query mask + missing NetworkIdentity + grab preemption + RPC ownership gating).
   - The transport switching failure is an explicit protocol mismatch (Host on WebRTC Relay vs Client on UDP 127.0.0.1).
   - The input lag and rubberbanding are caused by the total omission of client prediction combined with 240+ RPCs/s render-loop flooding and SyncVar aim stomping.
2. **Remediation Strategy**:
   - Sequencing the fix into 6 chronological phases (Phases 0–5) guarantees that dependency injection and transport layers are stabilized before modifying role authority, interactable prefabs, and physics prediction.
   - Independent adversarial review by `reviewer_plan_audit` confirmed 100% of findings, approved the remediation plan, and contributed 5 concrete edge-case mitigations (C-01 to C-05).
3. **Safety & Zero-Code Modification**:
   - In accordance with instructions, 0 codebase files were modified. All diagnostics and plans are delivered as metadata artifacts.

---

## 3. Caveats

1. **Relay Server Ping**: PurrTransport relies on external relay infrastructure (`https://purrtransport.purrservers.com/`). When testing locally, `LocalUDP` should be used for deterministic zero-latency validation.
2. **Unity PhysX Determinism**: Dual-pilot physics co-op cannot rely on unguided client simulation. Server-authoritative physics with client prediction on input streams is mandatory.
3. **OS Socket Cooldown**: Windows OS `TIME_WAIT` delays (150–250ms) must be accommodated during rapid transport reconnect loops.

---

## 4. Conclusion

The online networking subsystem in TPOB has been thoroughly diagnosed and an approved 6-phase remediation plan (`/plan`) has been established:
- **Phase 0**: Architectural Foundation & VContainer Lifecycle Harmonization
- **Phase 1**: Transport Layer & Connection Lifecycle Unification
- **Phase 2**: Server-Authoritative Identity, Lobby & Reconnection Overhaul
- **Phase 3**: Interactable Network Architecture & Torso Interaction Fix
- **Phase 4**: Input Streaming Pacing, Client Prediction & Zero-GC Profiling
- **Phase 5**: Automated Testing, Network Stress Verification & Certification

All acceptance criteria are satisfied. The diagnostic analysis is documented in `DIAGNOSIS_AND_AUDIT.md`, and the master plan is delivered in `plan.md` (and `.agents/teamwork/plan.md`).

---

## 5. Verification Method

1. **Verify No Code Modifications**:
   ```powershell
   git status --porcelain
   ```
   Confirms zero modified or created C# files.
2. **Inspect Plan & Audit Artifacts**:
   - Plan: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\orchestrator\plan.md`
   - Master Plan Link: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\plan.md`
   - Detailed Audit: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\orchestrator\DIAGNOSIS_AND_AUDIT.md`
   - Reviewer Approval: `c:\Users\SaidR\Documents\GitHub\TPOB\.agents\teamwork\reviewer_plan_audit\handoff.md`
