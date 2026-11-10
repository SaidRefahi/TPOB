# Original User Request

## Approved Task Prompt — 2026-11-10T01:21:25Z

Perform an in-depth architectural analysis of the online networking subsystem in TwoPilotsOneBody (TPOB), reviewing PurrNet best practices and documentation to produce a diagnosis and remediation plan (/plan) without modifying code.

Integrity mode: development

## Requirements

### R1. Online Architecture Audit
Perform a comprehensive audit of the online subsystem (PurrNet lifecycle, RPCs, SyncVars, NetworkIdentity, reconnection, lobby, roles, input streaming, and dual simulation) to identify all architectural flaws and root causes behind severe lag, desynchronization, interaction failures (such as key 'E' in Torso mode), and transport switching issues (local UDP vs PurrTransport/Relay). Do not modify codebase files in this run.

### R2. PurrNet Best Practices & Documentation Alignment
Research official PurrNet documentation and authoritative multiplayer patterns to evaluate the current architecture against recommended server-authoritative practices, Zero-GC hot paths, and VContainer dependency injection patterns.

### R3. Remediation Plan (/plan)
Produce a structured, actionable remediation plan (/plan) detailing sequenced steps to fix all identified architectural flaws, adhering strictly to server-authoritative authority, VContainer lifecycles, and Zero-GC constraints.

### R4. Exploration Tooling & Token Constraints
For codebase exploration, architectural discovery, or tracing relationships between C# scripts, do not scan files sequentially with filesystem or shell tools. Verify semantic index health (`codebase-memory:index_status`) and query the semantic graph (`query_graph`, `trace_path`, `search_graph`) and ULSM (`ulsm:find_references`) to minimize token consumption.

## Verification Resources
Prioritize investigation and validation against the reported failure scenarios:
- Torso mode interaction ('E') failure and state synchronization.
- Input streaming under dual simulation.
- Transport switching and connection lifecycle between local UDP and PurrTransport/Relay.
- Official PurrNet documentation and API references for server-authoritative architecture.

## Acceptance Criteria

### Audit & Diagnostic Completeness
- [ ] Every identified issue contains exact file paths, class/component names, and line ranges.
- [ ] Every finding details the specific root cause mechanism (e.g. lifecycle timing, unbuffered RPC, missing authority check, transport state mismatch).
- [ ] Each diagnostic finding is validated with citations or references to PurrNet documentation or server-authoritative best practices.

### Remediation Plan (/plan) Quality
- [ ] The remediation plan is delivered as a structured markdown plan (/plan) with clear chronological phases.
- [ ] Each phase specifies affected components, architectural redesigns, and risk mitigations.
- [ ] All proposed solutions adhere to project architectural constraints (VContainer injection, UniTask async, Zero-GC allocations in hot paths).
- [ ] No codebase files are modified during this audit and planning run.
