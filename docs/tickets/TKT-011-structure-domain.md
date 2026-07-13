# TKT-011: Structure domain implementation (Tavern aggregate)

> Status: TODO
> Type: implementation
> Domain: DOM-001 | System: SYS-001
> Traces to: REQ-001, REQ-066, REQ-067, REQ-068, REQ-069, REQ-070, REQ-071, REQ-072, REQ-073, REQ-074, REQ-075, REQ-097, REQ-098, REQ-099, REQ-100
> Blocked by: TKT-002, TKT-003 | Blocks: TKT-019
> Session: —

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

- [ ] Placement validation order exactly as CON-003 (first failure wins) — full error matrix green
- [ ] Graph edge rules (horizontal walkable, vertical stair↔stair, REQ-097 exterior ground) green
- [ ] REQ-098 deactivate/reactivate flows; refund equality incl. upgrades (REQ-073/100)
- [ ] Every successful mutation's event list ends with StructureChanged; failures emit nothing
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Keep the graph immutable + versioned; recompute on mutation only. `PaidTotal` accumulates post-multiplier charged amounts returned by `IBuildLedger.TryCharge`.

## Session log

| Date | Event |
|---|---|
