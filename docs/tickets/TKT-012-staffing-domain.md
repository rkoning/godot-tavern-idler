# TKT-012: Staffing domain implementation (Roster)

> Status: DONE
> Type: implementation
> Domain: DOM-005 | System: SYS-005
> Traces to: REQ-056, REQ-057, REQ-058, REQ-059, REQ-060, REQ-061, REQ-062, REQ-063, REQ-064, REQ-065, REQ-108, REQ-109
> Blocked by: TKT-002, TKT-004 | Blocks: TKT-019
> Session: /implement TKT-012 (2026-07-15)

## Goal

The `Roster` aggregate implementing CON-009 against CON-010 driven ports (stubbed): hiring (ordinary + named), prep-gated assignment with maxima, the Open/Degraded/Closed evaluation with refusal exclusion, speed factors, orphaning, wage bill.

## Contracts

| Contract | Role |
|---|---|
| CON-009 | implements |
| CON-010 | consumes (stubs in tests) |
| CON-002 | consumes (phase gate) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/staffing/**   (EXCEPT src/domains/staffing/ports/** — read-only, owned by the contract ticket)
tests/domains/staffing/**
```

## Acceptance criteria

- [x] REQ-058 state table green incl. refusal flipping a room to Closed
- [x] Speed factor formula (clamp 0.5–1.5, degraded cap 0.8, closed 0) green
- [x] Orphaning on OnRoomRemoved keeps employees paid (REQ-109); dismissal stops wages, keeps arrears payable (REQ-108)
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Room-state cache recomputed only at EvaluateRoomStates / OnRoom* calls, not per query.

## Session log

| Date | Event |
|---|---|
| 2026-07-15 | `/implement TKT-012` (headless). TDD: wrote CON-009 harness/fakes + `RosterConformanceTests` subclass + focused `RosterTests` first (compile-RED), then implemented the aggregate (GREEN). |
| 2026-07-15 | Implemented `StaffRoster` (`src/domains/staffing/StaffRoster.cs`) + domain catalog types (`RosterCatalog.cs`) + snapshot payload (`StaffingSnapshotJson.cs`). Full suite **268 passed / 0 failed / 0 skipped** (baseline 238 + 30 new: 17 CON-009 conformance via subclass, 13 unit). |

### Decisions & notes

- **Class named `StaffRoster`, not `Roster`.** CON-009 `IStaffingQueries` declares a query property `Roster`; C# forbids a type member sharing the enclosing type's name, so the aggregate class is `StaffRoster` (the "Roster aggregate"). No contract impact.
- **`OnRoomDeactivated` does not orphan staff.** REQ-098 rooms can reactivate, so deactivation only re-computes that single room's staff-state cache (per CON-009); staff assignments are preserved. Orphaning happens only on `OnRoomRemoved` (REQ-109).
- **`SetRefusals` emits no events and does not itself recompute room-state** — CON-009 defines no refusal event and lists state recomputation only at `EvaluateRoomStates`/`OnRoom*`. It is legal in any phase (event reaction).
- **Employee ids never reused** across `ResetAll` (prestige) or within a save — mirrors Structure's room-id discipline.
- **Snapshot** (CON-017 rules: camelCase, strict unknown-field rejection) persists only mutable roster state (id, role/named-hire, assigned room, refusing) + the id counter; display name/wage/traits are re-derived from the catalog on `Restore` (catalog is authoritative). Derived room-state cache is not persisted. `Capture` rejected during Service.
- **Tooling constraint:** the headless sandbox blocked file deletion/rename under `.claude/worktrees`, and `git rm` needs interactive approval. The now-superseded `src/domains/staffing/Roster.cs` was neutralized to a comment-only file; it is safe to `git rm` at commit time.

### Contract compliance

```
CONTRACT COMPLIANCE — TKT-012 — 2026-07-15
[PASS] 1. Ownership — changed paths: src/domains/staffing/{StaffRoster,RosterCatalog,StaffingSnapshotJson,Roster(placeholder)}.cs,
             tests/domains/staffing/{StaffingTestDoubles,RosterConformanceTests,RosterTests}.cs. ports/ untouched; nothing in tests/contracts/.
[PASS] 2. Frozen-doc integrity — no edits to any docs/contracts/CON-*.md or REGISTRY.md.
[PASS] 3. Interface fidelity — StaffRoster implements IStaffingCommands + IStaffingQueries verbatim (compiler-enforced);
             CON-009/CON-010 port files unchanged. RosterCatalog/StaffRoleDef/NamedHireDef are DOM-005 model types, not contract surface.
[PASS] 4. Conformance tests — CON-009 StaffingApiConformanceTests: 17 passed via RosterConformanceTests; full suite 268/0/0;
             tests/contracts/ files byte-identical (unchanged).
[PASS] 5. Consumption fidelity — ICycleQueries (Phase), CON-010 IRoomRequirements (Get→KeyNotFound handled as UnknownRoom,
             RoomsWithRequirements), IHireUnlocks (UnlockedNamedHires). No undefined ops; every StaffingError variant reachable.
[PASS] 6. Domain purity — no engine/adapter usings under src/domains/staffing (grep clean); only Kernel/Cycle/Structure + System.Text.Json.
[N/A ] 7. Registry sync — implementation ticket; no registry/contract changes.
VERDICT: COMPLIANT
```
