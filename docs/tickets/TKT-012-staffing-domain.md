# TKT-012: Staffing domain implementation (Roster)

> Status: TODO
> Type: implementation
> Domain: DOM-005 | System: SYS-005
> Traces to: REQ-056, REQ-057, REQ-058, REQ-059, REQ-060, REQ-061, REQ-062, REQ-063, REQ-064, REQ-065, REQ-108, REQ-109
> Blocked by: TKT-002, TKT-004 | Blocks: TKT-019
> Session: —

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

- [ ] REQ-058 state table green incl. refusal flipping a room to Closed
- [ ] Speed factor formula (clamp 0.5–1.5, degraded cap 0.8, closed 0) green
- [ ] Orphaning on OnRoomRemoved keeps employees paid (REQ-109); dismissal stops wages, keeps arrears payable (REQ-108)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Room-state cache recomputed only at EvaluateRoomStates / OnRoom* calls, not per query.

## Session log

| Date | Event |
|---|---|
