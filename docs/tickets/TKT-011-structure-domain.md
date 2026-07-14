# TKT-011: Structure domain implementation (Tavern aggregate)

> Status: DONE
> Type: implementation
> Domain: DOM-001 | System: SYS-001
> Traces to: REQ-001, REQ-066, REQ-067, REQ-068, REQ-069, REQ-070, REQ-071, REQ-072, REQ-073, REQ-074, REQ-075, REQ-097, REQ-098, REQ-099, REQ-100
> Blocked by: TKT-002, TKT-003 | Blocks: TKT-019
> Session: /implement TKT-011 (2026-07-13)

## Goal

The `Tavern` aggregate implementing CON-003 (commands, queries, snapshot, events) against the driven ports of CON-004 (stubbed here; real bridges in TKT-019): grid placement with the normative validation order, support/connectivity, the versioned `TraversalGraph`, inactive-room lifecycle (REQ-098), full-refund accounting, and the efficiency curve.

## Contracts

| Contract | Role |
|---|---|
| CON-003 | implements |
| CON-004 | consumes (calls driven ports; stubs in tests) |
| CON-002 | consumes (ICycleQueries phase gate) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/structure/**   (EXCEPT src/domains/structure/ports/** — read-only, owned by the contract ticket)
tests/domains/structure/**
```

## Acceptance criteria

- [x] Placement validation order exactly as CON-003 (first failure wins) — full error matrix green
- [x] Graph edge rules (horizontal walkable, vertical stair↔stair, REQ-097 exterior ground) green
- [x] REQ-098 deactivate/reactivate flows; refund equality incl. upgrades (REQ-073/100)
- [x] Every successful mutation's event list ends with StructureChanged; failures emit nothing
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Keep the graph immutable + versioned; recompute on mutation only. `PaidTotal` accumulates post-multiplier charged amounts returned by `IBuildLedger.TryCharge`.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Session started (worktree `tkt-011`). Status → IN PROGRESS; BOARD row updated. TDD: unit tests + CON-003 conformance subclass first, then the `Tavern` aggregate. |
| 2026-07-13 | `Tavern` implemented (`src/domains/structure/Tavern.cs`, `StructureSnapshotJson.cs`): normative validation order, transitive support (REQ-067/099), entrance-reachability (REQ-068/097), immutable versioned graph, REQ-098 (de)activation, full-refund accounting, efficiency curve, snapshot payload. Tests: CON-003 conformance subclass + CON-004 `IBuildLedger` stub subclass + 24 unit tests in `tests/domains/structure/`. Suite: 238 passed / 0 failed / 0 skipped (was 172). Mutation-checked the support rule (disabling it fails 9 tests). |
| 2026-07-13 | **Decisions taken inside ticket scope** (no contract change; recorded for TKT-019/020): (a) **Circulation prices** — no contract supplies them, so `Tavern` takes a `CirculationCosts(Corridor, Stair)` constructor argument; content/config provides the values (TKT-020). (b) **`MoveRoom` semantics (REQ-072/098)** — target must be in-lot, must not cross circulation or partially overlap another room, and the mover must come to rest on the structure in the post-move layout (else `NotOnExistingStructure`); landing exactly on another room of the same dimensions swaps the two (two `RoomMoved` events). Support/connectivity broken for *other* rooms is permitted and flips them inactive per REQ-098 — the reading that keeps REQ-098's "or moving" clause meaningful. (c) **Terrain (REQ-083a)** — placement is gated on the footprint covering a terrain cell whose `EnablesRoomType` names either the room type or the sheet's `RequiresTerrainFeature` key (the contract types allow both readings). Terrain *stat* modifiers (REQ-083b, `ModifiesRoom`) are **not** applied here: REQ-083 belongs to SYS-008/DOM-007 and no contract formula defines their application to `RoomInfo`. (d) **Inactive rooms stay physically present** — their cells remain in `WalkableCells`/support; `Active` is derived (grounded ∧ reachable), so it is recomputed on restore rather than persisted. |
| 2026-07-13 | contract-compliance: **COMPLIANT** — see report below. Status → DONE; BOARD updated. Unblocks TKT-019. |

## Contract compliance report

```
CONTRACT COMPLIANCE — TKT-011 — 2026-07-13
[PASS] 1. Ownership — changed: src/domains/structure/{Tavern,StructureSnapshotJson}.cs,
          tests/domains/structure/** (new), plus this ticket file + its BOARD row.
          src/domains/structure/ports/** untouched; no tests/contracts/** edits.
[PASS] 2. Frozen-doc integrity — no docs/contracts/** or REGISTRY.md changes in the diff.
[PASS] 3. Interface fidelity — CON-003: Tavern implements IStructureCommands, IStructureQueries,
          IStructureSnapshot with the frozen signatures verbatim (no renames, no added members on
          contract types). Added public surface is limited to the aggregate's own construction:
          `Tavern(...)` ctor and `CirculationCosts` (per-cell circulation prices, REQ-099 — no
          contract defines them); neither overlaps a contract type.
[PASS] 4. Conformance tests — CON-003 StructureApiConformanceTests + TraversalGraphConformanceTests
          and CON-004 BuildLedgerConformanceTests (reference stub) run green via the concrete
          subclasses; structure suites 75 passed. Full suite 238 passed / 0 failed / 0 skipped.
          tests/contracts/** byte-identical to session start (git status: unmodified).
[PASS] 5. Consumption fidelity — CON-004: TryCharge (both ChargeResult variants handled; failure →
          PlacementError.InsufficientGold, grid untouched), Refund (only non-negative amounts),
          ILotConstraints (Lot/Entrance/Terrain), IRoomContent.AvailableRoomTypes re-read per
          command, never cached. CON-002: ICycleQueries.Phase only (prep gate; ResetAll ungated per
          CON-003). CON-001: Money.MultiplyRounded/Outcome/GridRect used as specified.
[PASS] 6. Domain purity — no engine/Godot usings anywhere under src/domains/.
[N/A ] 7. Registry sync — implementation ticket; no contract definitions or version changes.
VERDICT: COMPLIANT
```
