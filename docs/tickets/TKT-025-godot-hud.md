# TKT-025: Godot HUD, night report, progression & codex UI

> Status: TODO
> Type: integration
> Domain: adapters (Godot)
> Traces to: REQ-006, REQ-007, REQ-017, REQ-022, REQ-039, REQ-043, REQ-101, REQ-112 (cycle controls, report, milestones, codex)
> Blocked by: TKT-022 | Blocks: —
> Session: —

## Goal

The cycle HUD (start night, early close, run-mode toggle, night/phase/clock display via ICycleQueries + IGameLoop intents), the dismissable night-report screen (REQ-022/101), the milestone list with secret masking (REQ-112), Acclaim balances, the prestige flow with venue choice (REQ-038), and the codex screen (discovered rules, trait registry).

## Contracts

| Contract | Role |
|---|---|
| CON-002 | consumes |
| CON-007 | consumes (NightReport) |
| CON-011 | consumes (codex) |
| CON-013 | consumes |
| CON-016 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/adapters/godot/hud/**
scenes/hud/**
```

## Acceptance criteria

- [ ] Night start only via explicit control (REQ-006); run mode toggle; early close (REQ-017)
- [ ] Report blocks progression to Prep until dismissed (REQ-101); every REQ-022 field displayed
- [ ] Milestones list with ??? masking (REQ-112); prestige flow offers only unlocked venues (REQ-038)
- [ ] Codex shows discovered rules only; total count as progress (REQ-043)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Prestige confirmation must warn about mid-service night abandonment (REQ-113).

## Session log

| Date | Event |
|---|---|
