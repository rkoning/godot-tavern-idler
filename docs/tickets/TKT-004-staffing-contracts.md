# TKT-004: Staffing contracts (CON-009, CON-010)

> Status: TODO
> Type: contract-definition
> Domain: DOM-005 | System: SYS-005
> Traces to: REQ-056–065, REQ-108–109
> Blocked by: TKT-003 | Blocks: TKT-006, TKT-009, TKT-012, TKT-020
> Session: —

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-009 and CON-010 and CON-003 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-009 | defines |
| CON-010 | defines |
| CON-003 | consumes (StaffRequirements) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/staffing/ports/**
tests/contracts/staffing/**
```

## Acceptance criteria

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] Staff content JSON golden file + validation tests included (CON-009)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Read the CON docs top to bottom before writing code; the Interface definition section IS the contract.

## Session log

| Date | Event |
|---|---|
