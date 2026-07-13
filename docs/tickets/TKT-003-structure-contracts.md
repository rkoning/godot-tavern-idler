# TKT-003: Structure contracts (CON-003, CON-004)

> Status: TODO
> Type: contract-definition
> Domain: DOM-001 | System: SYS-001
> Traces to: REQ-001, REQ-066–075, REQ-097–100
> Blocked by: TKT-001 | Blocks: TKT-004, TKT-006, TKT-007, TKT-008, TKT-009, TKT-011, TKT-020
> Session: —

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-003 and CON-004 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-003 | defines |
| CON-004 | defines |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/structure/ports/**
tests/contracts/structure/**
```

## Acceptance criteria

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] Room sheet JSON golden file + validation-rule tests included (CON-004)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

`TraversalGraph.Neighbors` edge rule (vertical = stair↔stair only) is normative — encode it in suite tests. Includes the sample rooms.json golden file as test data (content adapter later loads real content).

## Session log

| Date | Event |
|---|---|
