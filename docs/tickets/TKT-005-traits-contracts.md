# TKT-005: Traits contracts (CON-011, CON-012)

> Status: TODO
> Type: contract-definition
> Domain: DOM-006 | System: SYS-006
> Traces to: REQ-040–047, REQ-094–096, REQ-110–111
> Blocked by: TKT-001 | Blocks: TKT-006, TKT-009, TKT-013, TKT-020
> Session: —

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-011 and CON-012 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-011 | defines |
| CON-012 | defines |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/traits/ports/**
tests/contracts/traits/**
```

## Acceptance criteria

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] Rule catalog JSON golden file + validation tests included (CON-011)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Effect ordering (Ended→Began→Triggered), episode/EpisodeId semantics, and the CountScaling formulas in CON-011 are normative — the suite asserts them.

## Session log

| Date | Event |
|---|---|
