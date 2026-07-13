# TKT-017: RNG adapter (CON-015 implementation)

> Status: TODO
> Type: implementation
> Domain: adapters (shared)
> Traces to: REQ-024, REQ-050, REQ-102, REQ-110 (deterministic draws)
> Blocked by: TKT-001 | Blocks: TKT-022, TKT-026
> Session: —

## Goal

A deterministic `IRandomSource` implementation (explicit algorithm, e.g. xoshiro256**, seeded per stream by hash(seed, name), reseeded per night by hash(seed, name, nightNumber)) that passes the CON-015 conformance suite cross-platform.

## Contracts

| Contract | Role |
|---|---|
| CON-015 | implements |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/adapters/random/**
tests/adapters/random/**
```

## Acceptance criteria

- [ ] CON-015 suite green via fixture subclass (determinism, independence, night reseeding, bounds, idempotence)
- [ ] No System.Random (platform drift); algorithm documented in code
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Keep it dependency-free; the domains project must not reference this — it is injected at composition (CON-016).

## Session log

| Date | Event |
|---|---|
