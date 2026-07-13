# TKT-010: Cycle domain implementation (NightCycle FSM)

> Status: TODO
> Type: implementation
> Domain: DOM-002 | System: SYS-002
> Traces to: REQ-003, REQ-005, REQ-006, REQ-007, REQ-016, REQ-017, REQ-091, REQ-101
> Blocked by: TKT-002 | Blocks: TKT-019
> Session: —

## Goal

A pure-C# `NightCycle` aggregate implementing `ICycleCommands`/`ICycleQueries`/`ICycleSnapshot` per CON-002 and DOM-002: the prep→service(→drain)→settlement FSM, night clock, run-mode flag, and snapshot. Passes the full CON-002 conformance suite via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-002 | implements |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/cycle/**   (EXCEPT src/domains/cycle/ports/** — read-only, owned by the contract ticket)
tests/domains/cycle/**
```

## Acceptance criteria

- [ ] Full CON-002 FSM legality matrix green (every command × every phase)
- [ ] Service expiry emits DrainBegan(Expired) exactly once (REQ-016); early close path (REQ-017)
- [ ] Report gating: no Prep until DismissReport (REQ-101); snapshot round-trip Prep+Settlement, throws mid-service
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Smallest domain — a good first implementation ticket to validate the fixture pattern. No driven ports; config `ServiceDurationTicks` via constructor.

## Session log

| Date | Event |
|---|---|
