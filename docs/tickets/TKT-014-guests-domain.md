# TKT-014: Guests domain implementation (agent simulation)

> Status: DONE
> Type: implementation
> Domain: DOM-003 | System: SYS-003
> Traces to: REQ-002, REQ-008, REQ-009, REQ-010, REQ-018, REQ-023, REQ-024, REQ-048, REQ-049, REQ-050, REQ-051, REQ-052, REQ-053, REQ-054, REQ-055, REQ-092, REQ-093, REQ-102, REQ-103, REQ-104, REQ-107
> Blocked by: TKT-002, TKT-006 | Blocks: TKT-019
> Session: —

## Goal

The `GuestPopulation` aggregate implementing CON-005 against CON-006 driven ports (stubbed): attraction-weighted trickle arrivals, capacity admission + FIFO queue with patience, BFS pathing over the traversal graph, agenda execution with blocked-wait/skip, per-room crowding, satisfaction→payment modifier, effect application, VIP visit logic, lodger persistence, night stats. The largest domain ticket — budget a full session.

## Contracts

| Contract | Role |
|---|---|
| CON-005 | implements |
| CON-006 | consumes (stubs in tests) |
| CON-011 | consumes (EmittedEffect payloads) |
| CON-015 | consumes |
| CON-002 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/guests/**   (EXCEPT src/domains/guests/ports/** — read-only, owned by the contract ticket)
tests/domains/guests/**
```

## Acceptance criteria

- [x] Determinism test green (same seed + scripted stubs ⇒ identical event streams)
- [x] Queue/admission/patience/drain flows green (REQ-008/010/018); AllGuestsGone exactly once + NightStatsFinal
- [x] Agenda semantics green: nearest-room tie-break by lowest RoomId, blocked-wait −0.2 penalty, wallet-empty exit (REQ-048/053/054)
- [x] Crowding table, payment-modifier clamp, every EmittedEffect kind applied (REQ-009/023/042)
- [x] VIP frequency/revisit statistics green (REQ-050/055); lodger snapshot round-trip (REQ-107)
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [x] contract-compliance skill check passes (see session log — COMPLIANT)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Movement: `GuestTicksPerCell` from config; interpolation data (`NextCell`, `MoveProgress`) is view state, not physics. Use the `"guests"` RNG stream exclusively. Re-read CON-005 semantics before starting — most normative constants live there.

## Session log

| Date | Event |
|---|---|
| 2026-07-16 | `/implement TKT-014` — status → IN PROGRESS. Read CON-005/006/001/002/015 + consumed port types (Structure, Staffing, Traits) + all four guest conformance suites (behavioral, catalog, driven, support). Implementing `GuestPopulation` aggregate + `GuestCatalog`/`GuestCatalogJson` (G1) per resolved-context G1–G12. |
| 2026-07-16 | Implemented (TDD): `GuestCatalog`/`GuestCatalogJson` (data model + JSON loader/validator, G1 — validation only on the load path so out-of-band behavioral scenarios still build), `GuestPathfinding` (BFS nearest-provider, ties→lowest RoomId), `GuestAgent`, `GuestPopulation` (arrivals G2 floor+Bernoulli discretization, FIFO patience queue, agenda walk + all `BlockReason`s, crowding G6 self-inclusion, satisfaction→payment modifier, `ApplyEffects` G12, VIP roll/arrival/satisfied/revisit, lodger snapshot/checkout, night stats G7, phase model G10). Test fixtures: `GuestCatalogConformance` + `GuestSimConformance` subclass the frozen abstract suites; `GuestCatalogMapping` bridges the test-support ↔ domain catalog models; unit tests for pathfinding (4) and presence (3, G11). Movement constant `guestTicksPerCell=1` injected by the harness (G3); RNG via the real `DeterministicRandomSource` seeded from `GuestWorld.Seed`, never reseeded across nights (G4). `GuestsDrivenPortsConformanceTests` (CON-006) left abstract — implemented by TKT-019. Full suite **328 passed / 0 failed / 0 skipped** (baseline 268 + catalog 10 + pathfinding 4 + behavioral 43 + presence 3). |
| 2026-07-16 | **contract-compliance** — CONTRACT COMPLIANCE — TKT-014: [PASS] 1 Ownership (only `src/domains/guests/**` non-ports + `tests/domains/guests/**` + ticket/BOARD). [PASS] 2 Frozen-doc integrity (no CON-*/REGISTRY/design/PIPELINE edits). [PASS] 3 Interface fidelity (CON-005 `IGuestSimCommands`/`IGuestView`/`IGuestPresence` implemented verbatim from the frozen `ports/`; no extra contract surface). [PASS] 4 Conformance tests (CON-005 behavioral 43 + catalog 10 all pass, `tests/contracts/**` byte-identical, 0 skipped; CON-006 driven suite left to TKT-019). [PASS] 5 Consumption fidelity (CON-006: all four `TransactionResult` variants handled, `State`/`SpeedFactor` only for active rooms; CON-011 all 5 `EmittedEffect` + 3 `BehaviorOutcome` handled; CON-015 draws only from `"guests"`; no swallowed error modes). [PASS] 6 Domain purity (only `System.*`, `TavernIdler.Kernel`, and the contract-sanctioned `Domains.{Structure,Staffing,Traits}` port types; no engine/UI). [PASS] 7 Registry sync N/A (not a contract ticket). **VERDICT: COMPLIANT.** Status → DONE. |
