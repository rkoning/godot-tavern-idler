# TKT-020: Content adapters + starter-venue content

> Status: TODO
> Type: implementation
> Domain: adapters (content)
> Traces to: REQ-052, REQ-066, REQ-081, REQ-089, REQ-093, REQ-094, REQ-105 (schemas + launch-scope content)
> Blocked by: TKT-003, TKT-004, TKT-005, TKT-006, TKT-007, TKT-008 | Blocks: TKT-022, TKT-026
> Session: —

## Goal

JSON content loaders for all five catalogs (rooms CON-004, guests CON-005, menu CON-008, staff CON-009, traits/rules CON-011, progression CON-014) with every validation rule (fail-fast, field-context errors, cross-file id checks), plus the actual starter-venue prototype content: starter venue sheet, an initial room/menu/staff set, a first cut of guest types and trait rules (placeholder-quality numbers; content design iterates later).

## Contracts

| Contract | Role |
|---|---|
| CON-004 | implements (IRoomContent) |
| CON-005 | implements (guest sheet loading) |
| CON-008 | implements (IMenuContent) |
| CON-009 | implements (staff catalog loading) |
| CON-011 | implements (rule catalog loading) |
| CON-014 | implements (IProgressionContent) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/adapters/content/**
content/**
tests/adapters/content/**
```

## Acceptance criteria

- [ ] Every schema validation rule from the six contracts enforced with a failing-file test
- [ ] Golden-file conformance tests green via fixture subclasses
- [ ] Starter content loads clean and satisfies cross-file references + REQ-092 patience band + REQ-088 (starter venue has a venue-bound milestone)
- [ ] Unlock/venue filtering composition (with stubbed CON-013 state) green
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Starter content is prototype scaffolding, not final design — keep rosters small (3–4 guest types, ~6 rules, ~5 rooms) but schema-complete. Content numbers are data, not contract.

## Session log

| Date | Event |
|---|---|
