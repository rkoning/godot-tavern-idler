# TKT-026: Headless end-to-end integration + save round-trip + architecture tests

> Status: TODO
> Type: integration
> Domain: cross-domain
> Traces to: REQ-003, REQ-005, REQ-031, REQ-035, REQ-044 (full-loop + persistence guarantees)
> Blocked by: TKT-017, TKT-018, TKT-019, TKT-020, TKT-021 | Blocks: —
> Session: —

## Goal

Engine-free full-stack tests wiring real domains + bridges + content + RNG + persistence through the real GameLoop: scripted multi-night sessions (build → stock → staff → night → settlement → prestige), the CON-017 full round-trip fixture, determinism at the system level (same seed ⇒ same 3-night ledger), and the architecture test that domain/app assemblies carry no Godot references.

## Contracts

| Contract | Role |
|---|---|
| CON-016 | consumes |
| CON-017 | implements (full round-trip fixture) |
| CON-002 | consumes |
| CON-003 | consumes |
| CON-005 | consumes |
| CON-007 | consumes |
| CON-009 | consumes |
| CON-011 | consumes |
| CON-013 | consumes |
| CON-015 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
tests/integration/**
```

## Acceptance criteria

- [ ] Scripted 3-night session runs to completion with plausible ledger/report assertions (REQ-003/005)
- [ ] Full save round-trip green with real domains (CON-017 suite fixture)
- [ ] Prestige end-to-end: reset + refund + codex/unlock persistence (REQ-033/035/037/044)
- [ ] System-level determinism (same seed ⇒ identical NightReports); no-fail-state smoke (20 nights broke, still playable — REQ-031)
- [ ] Architecture test green (no Godot refs in domains/app)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

These tests are the prototype's health check — CI runs them on every push. Keep scripts as readable scenario builders; they double as living documentation of the core loop.

## Session log

| Date | Event |
|---|---|
