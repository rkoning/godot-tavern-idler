# TKT-002: Cycle contracts (CON-002)

> Status: DONE
> Type: contract-definition
> Domain: DOM-002 | System: SYS-002
> Traces to: REQ-003, REQ-005–007, REQ-016–017, REQ-091, REQ-101
> Blocked by: TKT-001 | Blocks: TKT-009, TKT-010, TKT-011, TKT-012, TKT-013, TKT-014, TKT-015, TKT-016
> Session: 2026-07-13 /implement TKT-002

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-002 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-002 | defines |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/cycle/ports/**
tests/contracts/cycle/**
```

## Acceptance criteria

- [x] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures) — `src/domains/cycle/ports/CycleApi.cs`, byte-for-byte copy of the CON-002 Interface definition
- [x] Abstract conformance suite covers every bullet of each contract's Conformance tests section — `tests/contracts/cycle/CycleConformanceTests.cs` (abstract; runs when TKT-010 subclasses via a fixture)
- [x] No behavior beyond type definitions; suites compile against interfaces only — ports file is pure type defs; suite calls only the three ports
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`) — full suite 37/37 (kernel); CON-002 abstract suite = 0 runnable here, validated at 35/35 against a throwaway reference FSM then deleted (see log)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`) — COMPLIANT (report below)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing — RED (12× CS0234/CS0246, CON-002 types missing) → GREEN observed
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Suite must include the exhaustive illegal-phase command matrix and exact event-sequence assertions from CON-002.

## Contract-compliance report

```
CONTRACT COMPLIANCE — TKT-002 — 2026-07-13
[PASS] 1. Ownership — changed source/test files are only src/domains/cycle/ports/CycleApi.cs
        and tests/contracts/cycle/CycleConformanceTests.cs, both inside the ownership block.
        The throwaway reference impl/fixture was deleted before close (not in the tree).
        Doc edits (TKT-002 ticket, BOARD.md) are session bookkeeping. bin/obj are artifacts.
[PASS] 2. Frozen-doc integrity — git status shows no edits under docs/contracts/
        (no CON-*.md, no REGISTRY.md row touched).
[PASS] 3. Interface fidelity —
        CON-002 (defines): CycleApi.cs is a byte-for-byte copy of the contract Interface
                definition — namespace TavernIdler.Domains.Cycle; enums Phase/DrainReason/
                CycleError; CycleConfig; ICycleCommands/ICycleQueries/ICycleSnapshot; record
                CycleSnapshot; 8 event records. No extra public surface.
[PASS] 4. Conformance tests —
        CON-002: tests/contracts/cycle abstract suite defined; execution deferred to TKT-010
                (fixture subclass) per the ticket, exactly as CON-015 defers to TKT-017.
                Nothing skipped incorrectly. Suite proven runnable+passing (35/35) against a
                throwaway faithful FSM this session, then that scratch removed.
        Full suite (kernel CON-001/015): 37 passed, 0 skipped, unchanged.
[PASS] 5. Consumption fidelity — consumes CON-001 only: uses Outcome<CycleError>, IDomainEvent,
        Tick exactly as defined. Type-definition ticket → no behavior, no error variants to
        handle/swallow. N/A → PASS.
[PASS] 6. Domain purity — grep of src/domains for Godot/UnityEngine using-directives: none.
        CycleApi.cs imports only TavernIdler.Kernel.
[PASS] 7. Registry sync — REGISTRY row for CON-002 (v1.0, FROZEN, provider DOM-002, consumer
        list) is consistent with the contract header and the now-existing conformance path
        tests/contracts/cycle/. No registry change required by this ticket.
VERDICT: COMPLIANT
```

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Session started; status TODO→IN PROGRESS. Blocker TKT-001 confirmed DONE (BOARD). Read CON-002 (defines), CON-001 (consumes), DOM-002, TKT-010 (downstream consumer). |
| 2026-07-13 | TDD: wrote abstract `CycleConformanceTests` (full night walk w/ exact event sequences; exhaustive command × illegal-phase matrix → WrongPhase + state-unchanged; expiry-once & overshoot; early close + silent later expiry; ReportNotPending; NotDraining; run-mode toggle/RunModeAlreadyInThatState in Prep+Service; Tick clock-in-every-phase & monotonic Now & ≥1 guard; snapshot round-trip Prep+Settlement, Capture-during-Service throws, bad-schema Restore throws). Build → RED (12× CS0234/CS0246). |
| 2026-07-13 | Added `cycle/ports/CycleApi.cs` verbatim from CON-002 (Phase, DrainReason, CycleError, CycleConfig, ICycleCommands/Queries/Snapshot, CycleSnapshot, 8 events). Build GREEN, 0 warnings (Release). Full suite 37/37 (kernel unchanged); abstract cycle suite 0 runnable (no fixture — matches Random/TKT-001 precedent). |
| 2026-07-13 | Suite validation (throwaway): added an internal reference `NightCycle` FSM + public fixture, ran the suite → 35/35 PASS, proving the fixture pattern works and the suite does not over-constrain TKT-010. Deleted the scratch file; committed tree is ports + abstract suite only; re-ran → 37/37, Release 0 warnings. |
| 2026-07-13 | contract-compliance run — see report below. |
