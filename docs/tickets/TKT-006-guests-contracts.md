# TKT-006: Guests contracts (CON-005, CON-006)

> Status: TODO
> Type: contract-definition
> Domain: DOM-003 | System: SYS-003
> Traces to: REQ-002, REQ-008–010, REQ-018, REQ-023–024, REQ-048–055, REQ-092–093, REQ-102–104, REQ-107
> Blocked by: TKT-003, TKT-004, TKT-005 | Blocks: TKT-007, TKT-008, TKT-009, TKT-014, TKT-020
> Session: —

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-005 and CON-006 and CON-003 and CON-009 and CON-011 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-005 | defines |
| CON-006 | defines |
| CON-003 | consumes |
| CON-009 | consumes (RoomStaffState) |
| CON-011 | consumes (EmittedEffect) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/guests/ports/**
tests/contracts/guests/**
```

## Acceptance criteria

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] Guest sheet JSON golden file + validation tests incl. REQ-092 patience-band check (CON-005)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Largest suite: determinism, queue, agenda, crowding table, payment-modifier bounds, effects, lodger cycle, VIP statistics. Driven-port suites (CON-006) run against reference stubs here; real bridges re-run them in TKT-019.

## Session log

| Date | Event |
|---|---|
