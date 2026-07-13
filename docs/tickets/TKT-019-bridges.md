# TKT-019: In-process bridges (all driven-port implementations)

> Status: TODO
> Type: implementation
> Domain: adapters (cross-domain)
> Traces to: REQ-004, REQ-008, REQ-024, REQ-057, REQ-058, REQ-061, REQ-063, REQ-087 (cross-domain arrows)
> Blocked by: TKT-010, TKT-011, TKT-012, TKT-013, TKT-014, TKT-015, TKT-016 | Blocks: TKT-022, TKT-026
> Session: —

## Goal

Every driven-port bridge from the DOM adapter tables: IBuildLedger + ILotConstraints (Structure→Economy/Progression), IStructureAccess + IRoomServiceState + ITransactions + IAttractionContext (Guests→Structure/Staffing/Economy/Progression composite), IRoomRequirements + IHireUnlocks (Staffing→Structure/Progression), IPresenceSource (Traits→Guests/Staffing/Structure/Economy), IRunCostModifiers (Economy→Progression). Thin, stateless views/translations — re-runs the driven-port conformance suites against REAL providers.

## Contracts

| Contract | Role |
|---|---|
| CON-004 | implements (IBuildLedger, ILotConstraints) |
| CON-006 | implements (all four) |
| CON-010 | implements (both) |
| CON-012 | implements (IPresenceSource) |
| CON-008 | implements (IRunCostModifiers) |
| CON-003 | consumes |
| CON-005 | consumes |
| CON-007 | consumes |
| CON-009 | consumes |
| CON-013 | consumes |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/adapters/bridges/**
tests/adapters/bridges/**
```

## Acceptance criteria

- [ ] Driven-port conformance suites green against real domain providers (fixture subclasses here)
- [ ] Bridge-equivalence tests green (values match underlying queries; CON-012 carrier ordering)
- [ ] No state in bridges beyond provider references; no re-entrancy (asserted)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Each bridge is <100 lines. IAttractionContext and IPresenceSource are the only composites — build them exactly per CON-006/CON-012 composition rules.

## Session log

| Date | Event |
|---|---|
