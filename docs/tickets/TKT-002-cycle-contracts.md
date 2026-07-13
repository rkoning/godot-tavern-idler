# TKT-002: Cycle contracts (CON-002)

> Status: TODO
> Type: contract-definition
> Domain: DOM-002 | System: SYS-002
> Traces to: REQ-003, REQ-005–007, REQ-016–017, REQ-091, REQ-101
> Blocked by: TKT-001 | Blocks: TKT-009, TKT-010, TKT-011, TKT-012, TKT-013, TKT-014, TKT-015, TKT-016
> Session: —

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-002 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-002 | defines |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/cycle/ports/**
tests/contracts/cycle/**
```

## Acceptance criteria

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Suite must include the exhaustive illegal-phase command matrix and exact event-sequence assertions from CON-002.

## Session log

| Date | Event |
|---|---|
