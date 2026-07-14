# TKT-010: Cycle domain implementation (NightCycle FSM)

> Status: DONE
> Type: implementation
> Domain: DOM-002 | System: SYS-002
> Traces to: REQ-003, REQ-005, REQ-006, REQ-007, REQ-016, REQ-017, REQ-091, REQ-101
> Blocked by: TKT-002 | Blocks: TKT-019
> Session: 2026-07-13

## Goal

A pure-C# `NightCycle` aggregate implementing `ICycleCommands`/`ICycleQueries`/`ICycleSnapshot` per CON-002 and DOM-002: the prep→service(→drain)→settlement FSM, night clock, run-mode flag, and snapshot. Passes the full CON-002 conformance suite via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-002 | implements |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/cycle/**   (EXCEPT src/domains/cycle/ports/** — read-only, owned by the contract ticket)
tests/domains/cycle/**
```

## Acceptance criteria

- [x] Full CON-002 FSM legality matrix green (every command × every phase)
- [x] Service expiry emits DrainBegan(Expired) exactly once (REQ-016); early close path (REQ-017)
- [x] Report gating: no Prep until DismissReport (REQ-101); snapshot round-trip Prep+Settlement, throws mid-service
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Smallest domain — a good first implementation ticket to validate the fixture pattern. No driven ports; config `ServiceDurationTicks` via constructor.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Started (worktree `tkt-010`). Status → IN PROGRESS; BOARD row updated. |
| 2026-07-13 | TDD: CON-002 fixture subclass (`tests/domains/cycle/NightCycleConformanceTests.cs`) + `NightCycleTests` unit tests written first → compile-RED (no `NightCycle`). Implemented `src/domains/cycle/NightCycle.cs` → GREEN. Suite: **118 passed / 0 failed / 0 skipped** (73 baseline + 45 cycle). |
| 2026-07-13 | Implementation decisions inside CON-002's stated semantics (no contract change): a second `NotifySettlementComputed` in the same settlement → `WrongPhase` (the contract's "exactly once per night" legality clause; the suite only pins it to a Failure); `ElapsedServiceTicks` is capped at `ServiceDurationTicks` and frozen once draining (contract pins `RemainingServiceTicks == 0` there but is silent on elapsed); ctor rejects `ServiceDurationTicks <= 0` (`ArgumentOutOfRangeException`) per the Ranges clause; `Restore` rejects a `Service`-phase snapshot (unreachable via `Capture`). `CycleError.SettlementNotTriggered` is unreachable given the legality table — left unused, contract untouched. |
| 2026-07-13 | contract-compliance: 1 Ownership PASS · 2 Frozen-doc integrity PASS · 3 Interface fidelity (CON-002) PASS · 4 Conformance tests (CON-002, 45 passed / 0 skipped; suite files unmodified) PASS · 5 Consumption fidelity (CON-001) PASS · 6 Domain purity PASS · 7 Registry sync N/A → **VERDICT: COMPLIANT**. Status → DONE; BOARD updated. Unblocks TKT-019. |
