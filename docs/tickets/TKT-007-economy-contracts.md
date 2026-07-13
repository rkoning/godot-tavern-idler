# TKT-007: Economy contracts (CON-007, CON-008)

> Status: TODO
> Type: contract-definition
> Domain: DOM-004 | System: SYS-004
> Traces to: REQ-004, REQ-011–015, REQ-019–022, REQ-025–028, REQ-030, REQ-105–106
> Blocked by: TKT-003, TKT-006 | Blocks: TKT-008, TKT-009, TKT-015, TKT-020
> Session: —

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-007 and CON-008 and CON-006 and CON-005 and CON-004 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-007 | defines |
| CON-008 | defines |
| CON-006 | consumes (TransactionRequest/Result) |
| CON-005 | consumes (NightGuestStats) |
| CON-004 | consumes (ChargeResult) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/economy/ports/**
tests/contracts/economy/**
```

## Acceptance criteria

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] Settlement golden scenarios (solvent / upkeep-shortfall / partial wages / arrears seniority) encoded in the suite
- [ ] Menu JSON golden file + validation tests (CON-008)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Read the CON docs top to bottom before writing code; the Interface definition section IS the contract.

## Session log

| Date | Event |
|---|---|
