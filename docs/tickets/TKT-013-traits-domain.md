# TKT-013: Traits domain implementation (rule engine)

> Status: TODO
> Type: implementation
> Domain: DOM-006 | System: SYS-006
> Traces to: REQ-040, REQ-041, REQ-042, REQ-043, REQ-044, REQ-045, REQ-046, REQ-047, REQ-094, REQ-095, REQ-096, REQ-110, REQ-111
> Blocked by: TKT-002, TKT-005 | Blocks: TKT-019
> Session: —

## Goal

`RuleBook`/`Codex`/`EpisodeLedger` implementing CON-011, pulling `PresenceSnapshot` via CON-012 (stubbed): episode diffing, reach + broadcaster logic, per-rule stacking, once-per-episode behavior rolls via the `"traits"` RNG stream, discovery, and the lifetime codex.

## Contracts

| Contract | Role |
|---|---|
| CON-011 | implements |
| CON-012 | consumes (stubs in tests) |
| CON-015 | consumes |
| CON-002 | consumes (Tick service-only gate) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/traits/**   (EXCEPT src/domains/traits/ports/** — read-only, owned by the contract ticket)
tests/domains/traits/**
```

## Acceptance criteria

- [ ] Episode open/close/re-entry with new EpisodeId; behavior rolls once per episode (seeded determinism)
- [ ] REQ-040 guest-participation gate; broadcaster widening (REQ-047)
- [ ] CountScaling formulas with caps; effect ordering Ended→Began→Triggered
- [ ] Discovery exactly once ever; codex survives simulated prestige (REQ-044)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Pair counting: qualifying pairs per rule per reach-scope; diff against previous tick's episode set. Keep it allocation-light — this runs every tick.

## Session log

| Date | Event |
|---|---|
