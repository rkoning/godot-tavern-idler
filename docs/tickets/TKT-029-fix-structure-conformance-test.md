# TKT-029: Fix contradictory CON-003 conformance test (bug)

> Status: IN PROGRESS
> Type: bug
> Domain: DOM-001 | System: SYS-001
> Traces to: CON-003 (placement validation order); REQ-068 (connectivity)
> Blocked by: — | Blocks: TKT-011 (unblocks it)
> Session: /bug (2026-07-13)

## Goal

Correct a self-contradictory conformance test in the CON-003 suite so a deterministic Tavern aggregate can pass it. **No contract change:** CON-003's interface and semantics are unchanged and correct; only a defective test is fixed. Ownership of the one test file is granted here for this fix (TKT-003 stays DONE; user-approved 2026-07-13).

## Contracts

| Contract | Role |
|---|---|
| CON-003 | conformance-test correction only — no interface/semantic change, no version bump |

## File ownership (exclusive)

```
tests/contracts/structure/StructureApiConformanceTests.cs
```

## Diagnosis

`Graph_vertical_edges_only_between_stairs` and `PlaceRoom_supported_but_unreachable_is_Disconnected` required **opposite** outcomes for the identical placement (taproom at `GridRect(0,1,2,1)` above a ground taproom, no stair). CON-003 line 129 (validation order `… → Unsupported → Disconnected → InsufficientGold`, first failure wins) plus line 25 (`Disconnected` is placement-time) make the reject-Disconnected model normative — so `PlaceRoom_supported_but_unreachable_is_Disconnected` is correct and `Graph_vertical_edges_only_between_stairs` is the defect (it `PlaceOk`'d an unreachable placement, mis-applying the REQ-098 deactivation model which is for rooms that lose connectivity via a later mutation, not initial placement). Latent because the abstract suite never ran until TKT-011 became its first subclass.

## Fix

In `Graph_vertical_edges_only_between_stairs`, build the stair column (2,0)/(2,1) **before** placing the upper room, so it is reachable at placement time. Both graph assertions (no room↔room vertical edge; stair↔stair vertical edge) are unchanged and still hold.

## Acceptance criteria

- [ ] The two tests no longer require contradictory outcomes for the same input
- [ ] `Graph_vertical_edges_only_between_stairs` assertions unchanged; only statement order fixed
- [ ] Suite compiles; full repo suite green (structure conformance stays abstract until TKT-011 subclasses it)
- [ ] Runtime-verified by re-dispatching TKT-011 (recorded here)
- [ ] contract-compliance passes; BOARD + status updated

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Bug diagnosed (contradiction verified against CON-003 line 129); user approved the frozen-conformance edit. Fix applied to `Graph_vertical_edges_only_between_stairs`. |
