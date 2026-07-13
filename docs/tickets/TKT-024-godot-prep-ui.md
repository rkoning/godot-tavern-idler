# TKT-024: Godot prep/management UI (build, stock, hire/assign)

> Status: TODO
> Type: integration
> Domain: adapters (Godot)
> Traces to: REQ-001, REQ-025, REQ-039, REQ-059, REQ-062, REQ-070, REQ-071, REQ-072 (prep interactions); C-001
> Blocked by: TKT-022 | Blocks: —
> Session: —

## Goal

Prep-phase interaction UI: room placement/move/demolish/upgrade (calling IStructureCommands and surfacing PlacementError variants as user feedback), circulation building, stock purchase screen (IEconomyCommands/Queries), hire/dismiss/assign UI (IStaffingCommands/Queries), and the Acclaim shop purchase surface (IProgressionCommands.Purchase).

## Contracts

| Contract | Role |
|---|---|
| CON-003 | consumes |
| CON-007 | consumes |
| CON-009 | consumes |
| CON-013 | consumes |
| CON-002 | consumes (phase-aware enablement) |
| CON-016 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/adapters/godot/prep/**
scenes/prep/**
```

## Acceptance criteria

- [ ] Every PlacementError variant surfaces as distinct, comprehensible feedback
- [ ] All controls disabled outside Prep (domain gates remain authoritative)
- [ ] Stock, hire/assign, and shop flows work end-to-end against real domains in an in-engine smoke scene
- [ ] Touch-friendly target sizes (C-001)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

UI never bypasses Outcome results — a Failure renders feedback, never retries silently.

## Session log

| Date | Event |
|---|---|
