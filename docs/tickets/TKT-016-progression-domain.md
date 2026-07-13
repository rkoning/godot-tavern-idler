# TKT-016: Progression domain implementation (milestones, shop, prestige, venues)

> Status: TODO
> Type: implementation
> Domain: DOM-007 | System: SYS-007, SYS-008
> Traces to: REQ-029, REQ-031, REQ-032, REQ-033, REQ-034, REQ-035, REQ-036, REQ-037, REQ-038, REQ-039, REQ-076, REQ-077, REQ-078, REQ-079, REQ-080, REQ-081, REQ-082, REQ-083, REQ-084, REQ-085, REQ-086, REQ-087, REQ-088, REQ-089, REQ-090, REQ-112, REQ-113
> Blocked by: TKT-002, TKT-008 | Blocks: TKT-019
> Session: —

## Goal

All CON-013 aggregates implementing feat intake, pending→earned milestone flow with settlement commit, secret masking, the shop with prerequisite gating and Acclaim accounting, abilities (cooldown/uses/cost order), prestige (refund, un-own, discard pendings, keep lifetime/unlocks/codex), and venue data served from CON-014 content (stubbed loader).

## Contracts

| Contract | Role |
|---|---|
| CON-013 | implements |
| CON-014 | consumes (stubs in tests) |
| CON-002 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/progression/**   (EXCEPT src/domains/progression/ports/** — read-only, owned by the contract ticket)
tests/domains/progression/**
```

## Acceptance criteria

- [ ] Milestone lifecycle green incl. prestige-discards-pendings and one-time earning (REQ-029/032/113/021)
- [ ] Acclaim invariants green (Lifetime monotone; Available = Lifetime − Spent; refund at prestige) (REQ-033/039/077)
- [ ] Shop error order + tree gating (REQ-076); secret masking (REQ-112)
- [ ] Ability check order; per-night uses reset (REQ-080)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

FeatState accumulation rules are in CON-013 semantics (night stats replace; VIP/rule sets accumulate). Venue sheets are immutable content — the domain only tracks unlock state and the current run.

## Session log

| Date | Event |
|---|---|
