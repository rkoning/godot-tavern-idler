# TKT-003: Structure contracts (CON-003, CON-004)

> Status: DONE
> Type: contract-definition
> Domain: DOM-001 | System: SYS-001
> Traces to: REQ-001, REQ-066–075, REQ-097–100
> Blocked by: TKT-001 | Blocks: TKT-004, TKT-006, TKT-007, TKT-008, TKT-009, TKT-011, TKT-020
> Session: /implement TKT-003 (2026-07-13)

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-003 and CON-004 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-003 | defines |
| CON-004 | defines |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/structure/ports/**
tests/contracts/structure/**
```

## Acceptance criteria

- [x] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [x] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [x] No behavior beyond type definitions; suites compile against interfaces only (only exception: the normative `TraversalGraph.Neighbors` edge rule, which the contract itself defines as a concrete method)
- [x] Room sheet JSON golden file + validation-rule tests included (CON-004)
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

`TraversalGraph.Neighbors` edge rule (vertical = stair↔stair only) is normative — encode it in suite tests. Includes the sample rooms.json golden file as test data (content adapter later loads real content).

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Session started. Status → IN PROGRESS. Read CON-001/003/004, DOM-001, TKT-001 scaffold. Plan: define CON-003 + CON-004 port types verbatim under `src/domains/structure/ports/`; concrete `TraversalGraph.Neighbors` (TDD red-green); abstract conformance suites for CON-003 (structure API) and CON-004 driven ports; sample `rooms.json` golden + schema-validation-rule suite. |
| 2026-07-13 | **Contract clarification (user-decided, not a contract edit).** CON-003 defines both `UnknownRoomType` and `RoomTypeLocked`, but CON-004's only room-catalog channel (`IRoomContent.AvailableRoomTypes()`) is already unlock-filtered, so as literally read DOM-001 could not distinguish a locked-but-known type from an unknown one and could never provoke `RoomTypeLocked`. Raised to user. **Decision:** match the Staffing pattern — DOM-001 is constructed with the *full static room-type catalog* (all known types) as a construction input; `AvailableRoomTypes()` remains the dynamic unlock filter. Domain distinguishes: requested type not in full catalog → `UnknownRoomType`; in full catalog but not currently available → `RoomTypeLocked`. No frozen-contract change. **Note for TKT-011:** the Tavern aggregate must accept the full room-type catalog at construction (separate from the `IRoomContent` driven port); the conformance harness (`IStructureTestHarness`) supplies both a `FullCatalog` and an `Available` subset. |
| 2026-07-13 | Implemented. Port surface written verbatim from CON-003/CON-004 under `src/domains/structure/ports/` (`StructureApi.cs`, `StructureDrivenPorts.cs`). `TraversalGraph.Neighbors` built TDD red→green (9 concrete tests, `TraversalGraphConformanceTests`). Abstract conformance suites: CON-003 API (`StructureApiConformanceTests` + `StructureConformanceSupport` harness) covering all 9 conformance bullets; CON-004 driven suites (`driven/BuildLedgerConformanceTests`, `driven/LotConstraintsConformanceTests`, `driven/RoomContentConformanceTests`) + golden `driven/rooms.sample.json` + 7 schema-validation-rule tests. Abstract suites don't run until subclassed (TKT-011 for CON-003, TKT-019/020 for CON-004 driven), matching the CON-015 Random precedent. Full suite: **46 passed, 0 failed, 0 skipped**. |
| 2026-07-13 | **Contract-compliance report** (skill `.claude/skills/contract-compliance`):<br>`[PASS] 1. Ownership` — all changed code/test files under `src/domains/structure/ports/**` + `tests/contracts/structure/**`; doc edits (PIPELINE/BOARD/ticket) are required bookkeeping.<br>`[PASS] 2. Frozen-doc integrity` — no edits to any `docs/contracts/CON-*.md` or REGISTRY rows.<br>`[PASS] 3. Interface fidelity` — CON-003 + CON-004 types reproduced verbatim (names/namespaces/signatures); no extra public surface in the `TavernIdler.Domains.Structure` namespace.<br>`[PASS] 4. Conformance tests` — runnable suite `TraversalGraphConformanceTests` 9/9 pass; abstract suites compile against interfaces only and are (correctly) not collected until subclassed.<br>`[PASS] 5. Consumption fidelity` — consumes only CON-001 kernel types (Money/GridRect/CellCoord/ids/Outcome/IDomainEvent); no undefined operations.<br>`[PASS] 6. Domain purity` — no `using Godot`/engine imports under `src/domains/`.<br>`[PASS] 7. Registry sync` — CON-003/004 already FROZEN + registered at Gate 4; unchanged and consistent.<br>**VERDICT: COMPLIANT.** |
