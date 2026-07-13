# TKT-014: Guests domain implementation (agent simulation)

> Status: TODO
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

- [ ] Determinism test green (same seed + scripted stubs ⇒ identical event streams)
- [ ] Queue/admission/patience/drain flows green (REQ-008/010/018); AllGuestsGone exactly once + NightStatsFinal
- [ ] Agenda semantics green: nearest-room tie-break by lowest RoomId, blocked-wait −0.2 penalty, wallet-empty exit (REQ-048/053/054)
- [ ] Crowding table, payment-modifier clamp, every EmittedEffect kind applied (REQ-009/023/042)
- [ ] VIP frequency/revisit statistics green (REQ-050/055); lodger snapshot round-trip (REQ-107)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Movement: `GuestTicksPerCell` from config; interpolation data (`NextCell`, `MoveProgress`) is view state, not physics. Use the `"guests"` RNG stream exclusively. Re-read CON-005 semantics before starting — most normative constants live there.

## Session log

| Date | Event |
|---|---|
