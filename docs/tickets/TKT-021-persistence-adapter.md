# TKT-021: Persistence adapter (CON-017 ISaveStore)

> Status: TODO
> Type: implementation
> Domain: adapters (persistence)
> Traces to: REQ-035, REQ-044 (scope split); PDD §4 (local saves + Steam Auto-Cloud)
> Blocked by: TKT-009 | Blocks: TKT-022, TKT-026
> Session: —

## Goal

`ISaveStore` over the filesystem: envelope (de)serialization with System.Text.Json, atomic tmp+rename writes, version refusal, strict unknown-field rejection, slot listing. Cross-reference integrity validation runs at load. Passes the CON-017 suite with stub payloads; the full-stack round-trip lands in TKT-026.

## Contracts

| Contract | Role |
|---|---|
| CON-017 | implements |
| CON-002 | consumes (snapshot types) |
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
src/adapters/persistence/**
tests/adapters/persistence/**
```

## Acceptance criteria

- [ ] CON-017 suite green (atomicity, versioning, scope split, integrity refusal, golden envelope byte-stability)
- [ ] Save directory path injectable (tests use temp dirs; Godot boot passes user://saves/)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

No Steamworks code — Auto-Cloud syncs the directory externally (Q-010 deferral).

## Session log

| Date | Event |
|---|---|
