# TKT-022: Godot bootstrap (project, GameLoopNode, composition root)

> Status: TODO
> Type: integration
> Domain: adapters (Godot)
> Traces to: REQ-091 (config wiring); PDD §4/§5 (Godot 4.7 .NET, hardware floor); C-003
> Blocked by: TKT-017, TKT-018, TKT-019, TKT-020, TKT-021 | Blocks: TKT-023, TKT-024, TKT-025
> Session: —

## Goal

A running Godot 4.7 .NET project: `project.godot`, the Godot csproj referencing the domain/app projects, the `GameLoopNode` autoload (frame accumulation → `IGameLoop.Advance` per CON-016), the composition root (content load → domain construction → bridge wiring → save load or StartRun), and a main scene that loads the render/prep/hud child scenes by fixed path convention (`res://scenes/render/render_root.tscn`, `res://scenes/prep/prep_root.tscn`, `res://scenes/hud/hud_root.tscn`) if present — so TKT-023/024/025 never touch shared files.

## Contracts

| Contract | Role |
|---|---|
| CON-016 | consumes (binding conventions — normative here) |
| CON-017 | consumes |
| CON-013 | consumes (StartRun) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
project.godot
icon.svg
src/adapters/godot/TavernIdler.Godot.csproj
src/adapters/godot/boot/**
scenes/main.tscn
```

## Acceptance criteria

- [ ] Game boots headless (godot --headless) through composition root without errors; fresh save starts at starter venue (REQ-089)
- [ ] Frame accumulation matches CON-016 (cap discards excess) — verified by an in-engine smoke test script
- [ ] Autosave fires on report dismissal via ISaveStore into user://saves/
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Keep GameLoopNode as the ONLY node referencing the app object. Child scenes loaded via ResourceLoader.Exists check — missing scenes are fine during development.

## Session log

| Date | Event |
|---|---|
