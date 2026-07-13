# TKT-008: Progression contracts (CON-013, CON-014)

> Status: TODO
> Type: contract-definition
> Domain: DOM-007 | System: SYS-007, SYS-008
> Traces to: REQ-029, REQ-031–039, REQ-076–090, REQ-112–113
> Blocked by: TKT-003, TKT-006, TKT-007 | Blocks: TKT-009, TKT-016, TKT-020
> Session: —

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-013 and CON-014 and CON-005 and CON-003 and CON-007 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-013 | defines |
| CON-014 | defines |
| CON-005 | consumes |
| CON-003 | consumes |
| CON-007 | consumes (MilestoneAward) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/progression/ports/**
tests/contracts/progression/**
```

## Acceptance criteria

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] Milestone condition truth-table tests per condition kind; progression.json golden file + validation tests incl. acyclic-prerequisites and REQ-088 checks (CON-014)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Read the CON docs top to bottom before writing code; the Interface definition section IS the contract.

## Session log

| Date | Event |
|---|---|
