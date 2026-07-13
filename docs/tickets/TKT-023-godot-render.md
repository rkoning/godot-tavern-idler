# TKT-023: Godot render adapters (structure + guests)

> Status: TODO
> Type: integration
> Domain: adapters (Godot)
> Traces to: REQ-001, REQ-002, REQ-010, REQ-043 (visible grid, agents, queue label, trait hover)
> Blocked by: TKT-022 | Blocks: —
> Session: —

## Goal

Pull-view render adapters (Decision D): tavern grid/rooms/circulation from IStructureQueries, guest sprites from IGuestView with MoveProgress interpolation, the outside queue with visible line + "+N waiting" label (REQ-010), and hover panels showing carrier traits (REQ-043).

## Contracts

| Contract | Role |
|---|---|
| CON-003 | consumes |
| CON-005 | consumes |
| CON-011 | consumes (trait names via queries) |
| CON-016 | consumes (binding rules) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/adapters/godot/render/**
scenes/render/**
```

## Acceptance criteria

- [ ] Rooms/circulation/entrance render from queries and update after mutations (graph-version-keyed refresh)
- [ ] Guest sprites interpolate between tick positions; activity states visible; inactive rooms visually distinct (REQ-098)
- [ ] Queue renders visible line + overflow label per REQ-010; trait hover shows sheet traits (REQ-043)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Reconcile-by-id pattern: keep a NodeId↔GuestId map, add/remove/update per frame. No game state in nodes. Touch-target sizes per C-001.

## Session log

| Date | Event |
|---|---|
