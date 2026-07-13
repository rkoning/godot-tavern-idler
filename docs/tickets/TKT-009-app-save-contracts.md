# TKT-009: App & Save contracts (CON-016, CON-017)

> Status: TODO
> Type: contract-definition
> Domain: app layer (cross-domain)
> Traces to: REQ-005–007, REQ-021, REQ-037, REQ-113 (sequences); REQ-035, REQ-044 (save scopes)
> Blocked by: TKT-002, TKT-003, TKT-004, TKT-005, TKT-006, TKT-007, TKT-008 | Blocks: TKT-018, TKT-021
> Session: —

## Goal

`IGameLoop`, `GameConfig` (CON-016) and `ISaveStore` + envelope records (CON-017) exist in `src/app/ports/`, with the conformance suites for tick order, event routing, settlement/prestige sequences, frame accumulation (against instrumented fake domains defined inside the suite), and the save-envelope rules (atomicity, versioning, scope split — against stub payloads).

## Contracts

| Contract | Role |
|---|---|
| CON-016 | defines |
| CON-017 | defines |
| CON-002 | consumes |
| CON-003 | consumes |
| CON-005 | consumes |
| CON-007 | consumes |
| CON-009 | consumes |
| CON-011 | consumes |
| CON-013 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/app/ports/**
tests/contracts/app/**
tests/contracts/save/**
```

## Acceptance criteria

- [ ] Interfaces compile verbatim from CON-016/017 code blocks
- [ ] Routing-table suite covers every row of the CON-016 event routing table
- [ ] Save suite covers round-trip, prestige scope split, atomicity, version refusal, integrity refusal, golden envelope
- [ ] Fake-domain instrumentation lives inside the suite files (read-only for TKT-018/021)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

The suite's fake domains are the specification of call order — TKT-018 must satisfy them without modifying them. Architecture test (no Godot refs in domains/app) also lives here.

## Session log

| Date | Event |
|---|---|
