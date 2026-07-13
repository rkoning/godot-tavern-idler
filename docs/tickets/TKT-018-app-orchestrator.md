# TKT-018: App orchestrator (IGameLoop, routing, sequences)

> Status: TODO
> Type: implementation
> Domain: app layer (cross-domain)
> Traces to: REQ-005, REQ-006, REQ-007, REQ-021, REQ-037, REQ-113 (sequences)
> Blocked by: TKT-009 | Blocks: TKT-022, TKT-026
> Session: —

## Goal

The engine-free `GameLoop` implementing CON-016: the 6-step tick pipeline, the exhaustive event-routing table, the settlement and prestige sequences, run-mode auto-advance, frame-time accumulation with the MaxTicksPerFrame cap. Verified entirely against the instrumented fake domains in the read-only CON-016 conformance suite.

## Contracts

| Contract | Role |
|---|---|
| CON-016 | implements |
| CON-002 | consumes |
| CON-003 | consumes |
| CON-005 | consumes |
| CON-007 | consumes |
| CON-009 | consumes |
| CON-011 | consumes |
| CON-013 | consumes |
| CON-017 | consumes (autosave hooks) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/app/**   (EXCEPT src/app/ports/** — read-only, owned by TKT-009)
tests/app/**
```

## Acceptance criteria

- [ ] Tick-order and full routing-table suites green against the fakes
- [ ] Settlement sequence green (awards → RunSettlement input assembly → EndNights → NotifySettlementComputed → autosave hook)
- [ ] Prestige sequence green incl. mid-service path and codex exemption
- [ ] Frame accumulation + cap green
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

The orchestrator holds NO game rules — if you find yourself writing a conditional about gameplay, it belongs in a domain. Depth-first event routing in emission order.

## Session log

| Date | Event |
|---|---|
