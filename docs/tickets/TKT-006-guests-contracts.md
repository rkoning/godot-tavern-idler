# TKT-006: Guests contracts (CON-005, CON-006)

> Status: DONE
> Type: contract-definition
> Domain: DOM-003 | System: SYS-003
> Traces to: REQ-002, REQ-008–010, REQ-018, REQ-023–024, REQ-048–055, REQ-092–093, REQ-102–104, REQ-107
> Blocked by: TKT-003, TKT-004, TKT-005 | Blocks: TKT-007, TKT-008, TKT-009, TKT-014, TKT-020
> Session: /implement TKT-006 (2026-07-13, interactive)

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-005 and CON-006 and CON-003 and CON-009 and CON-011 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-005 | defines |
| CON-006 | defines |
| CON-003 | consumes |
| CON-009 | consumes (RoomStaffState) |
| CON-011 | consumes (EmittedEffect) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/guests/ports/**
tests/contracts/guests/**
```

## Acceptance criteria

- [x] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [x] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [x] No behavior beyond type definitions; suites compile against interfaces only
- [x] Guest sheet JSON golden file + validation tests incl. REQ-092 patience-band check (CON-005)
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`) — full repo 268/0/0; the behavioral (CON-005) + driven (CON-006) suites are abstract by design and become runnable when TKT-014 / TKT-019 subclass them; the runnable catalog suite (CON-005) passes
- [x] contract-compliance skill check passes (see session log — COMPLIANT)
- [x] Unit tests written first (superpowers **test-driven-development** skill) — n/a for a contract-definition ticket with no domain behavior; suites are the deliverable, built compile-after-each-chunk with end-to-end satisfiability simulation (see resume log)
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Largest suite: determinism, queue, agenda, crowding table, payment-modifier bounds, effects, lodger cycle, VIP statistics. Driven-port suites (CON-006) run against reference stubs here; real bridges re-run them in TKT-019.

### CHECKPOINT 2026-07-13 — ports + catalog DONE; behavioral + driven suites REMAIN

**Done + verified** (on `main`, WIP commit `86d6dd0`; full suite 268/0/0):
- `src/domains/guests/ports/GuestsApi.cs` (CON-005) and `GuestsDrivenPorts.cs` (CON-006) — verbatim; compile against merged Structure/Staffing/Traits types.
- `tests/contracts/guests/GuestsConformanceSupport.cs` (test-support `GuestTypeSheet`/`GuestCatalog`/`GuestAttractor`/`CrowdingSpec`/`GuestAgendaItem`/`VipSpec`/`VipCondition`), `GuestCatalogConformanceTests.cs` (abstract; golden load + 9 isolated invalid cases incl. the REQ-092 patience band, VIP-condition kinds, crowding ranges), `guests.sample.json`.

**Remaining:**
1. **CON-005 behavioral suite** `tests/contracts/guests/GuestSimConformanceTests.cs` (abstract) — cover every CON-005 Conformance bullet: determinism (same seed ⇒ identical event stream), queue (FIFO / patience expiry / `OverflowCount` vs `VisibleLine` / `BeginDrain` disband), agenda walk (fulfill / each `BlockReason` / wallet-empty exit), crowding table (loves/neutral/hates × empty/half/full), payment modifier bounds (satisfaction −1/0/+1 ⇒ 0.5/1.0/1.5), effects (each `EmittedEffect` kind + each `BehaviorOutcome`), lodger cycle (buy → persists through `EndNight`/snapshot round-trip → departs at next `BeginService`), VIP (unmet ⇒ never; met ⇒ frequency ≈ `visitChancePerNight` over a 1000-night stub; `VipSatisfied` only if satisfaction > 0; REQ-055 revisit), `AllGuestsGone` exactly once + `NightStatsFinal`.
2. **CON-006 driven suite** `tests/contracts/guests/driven/…` (abstract) — `IStructureAccess` equivalence + inactive excluded; `ITransactions` each result variant + CON-007 pricing/rounding + ledger delta = `Paid` + `SoldOut`/`CannotAfford` no-op; multiplier × satisfaction composition; `IAttractionContext` exclusions/multipliers/composition; re-entrancy ban.

**Harness design:** add `IGuestSimTestHarness` + a scripted `GuestWorld` record (seed; config incl. `ServiceDurationTicks`, `ArrivalRateFactor`, base service durations; content sheets; and the four CON-006 driven ports as test doubles). The abstract suite's `CreateSut(GuestWorld)` builds the SUT; TKT-014 implements the harness. Keep suites abstract (0 runnable until TKT-014 subclasses).

**⚠ SATISFIABILITY (critical):** three conformance-test satisfiability bugs already hit this project (TKT-029 CON-003, TKT-030 CON-009), all latent because abstract suites never *run* until a domain-impl subclasses them. **Mentally simulate every scenario end-to-end and keep assertions within contract limits** (never assign past a room max, refuse enough to actually cross a threshold, etc.). Prefer observable invariants + determinism over exact scripted counts.

**Under-specified — design around, NO contract change:** arrival discretization (expected rate → discrete arrivals per tick) is not pinned by CON-005. Assert arrival/queue behavior via invariants (admitted while `Agents.Count < TotalGuestCapacity`, else queued FIFO) + determinism, not exact per-tick arrival counts. If a genuine contract gap appears, STOP and `/requirement` — do not guess.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Started (interactive). Wrote CON-005 + CON-006 port surfaces (verbatim) and the guest-catalog conformance suite (golden + REQ-092 band + validation) with support types. Full suite 268/0/0; committed WIP `86d6dd0`. **Checkpointed** with behavioral (CON-005) + driven (CON-006) conformance suites remaining — see the CHECKPOINT block in Implementation notes for the full plan, harness design, satisfiability warning, and the arrival-discretization design note. Resume with `/implement TKT-006`. |
| 2026-07-15 | Resumed from CHECKPOINT. Building the CON-005 behavioral suite + CON-006 driven suite (both abstract; TKT-014/TKT-019 subclass). Note: this contract-definition ticket has no runnable domain behavior, so classic red-green TDD does not apply to the suites themselves — the discipline here is compile-after-each-chunk + end-to-end mental satisfiability simulation of every abstract scenario (per the ⚠ SATISFIABILITY warning and the two hard rules in the session prompt: invariants + determinism over exact scripted counts; STOP+/requirement on any real contract gap). |
| 2026-07-15 | **DONE.** Wrote `tests/contracts/guests/GuestSimConformanceSupport.cs` (harness `IGuestSimTestHarness`, `GuestWorld`, four configurable CON-006 driven-port doubles incl. mutable `FakeStructureAccess.Deactivate`, `Scenario` DSL), abstract `GuestSimConformanceTests.cs` (CON-005: determinism [events + per-tick view], AllGuestsGone→NightStatsFinal + stats consistency, Capture Prep/Settlement guard, queue/capacity [conservation + FIFO + patience decrement + expiry + drain disband], agenda + all 5 BlockReasons, crowding table, payment-modifier bounds, every EmittedEffect + BehaviorOutcome, lodger cycle, VIP arrival/satisfaction/revisit), and `driven/GuestsDrivenPortsConformanceTests.cs` (CON-006: structure equivalence + inactive-excluded, IRoomServiceState pass-through + unknown-room throw, every TransactionResult variant + pricing/rounding + ledger/stock deltas + composition, IAttractionContext exclusions/multipliers/composition, re-entrancy ban). Compiled after each chunk; full suite **268 passed / 0 failed / 0 skipped** (suites abstract ⇒ 0 new runnable). No contract/registry/frozen-doc edits; consumed CON-003/009/011/001 types used read-only. Satisfiability decisions: queue asserted via conservation invariant + determinism (intra-tick admission order NOT pinned); crowding/modifier exactness via `TotalGuestCapacity==1` + clamped shocks; mid-night RoomInactive accepts RoomInactive∨NoSuchService; lodger EndNight-persistence a safe superset invariant with definitive guarantees from a Restored lodger. Contract-compliance: **COMPLIANT** (report below). |

### Contract-compliance report

```
CONTRACT COMPLIANCE — TKT-006 — 2026-07-15
[PASS] 1. Ownership — only tests/contracts/guests/** (3 new files) + this ticket doc changed
[PASS] 2. Frozen-doc integrity — no edits to any docs/contracts/CON-*.md or REGISTRY.md
[PASS] 3. Interface fidelity (CON-005 GuestsApi.cs, CON-006 GuestsDrivenPorts.cs) — committed at
        checkpoint, verbatim to the frozen code blocks, unmodified this session (git diff empty)
[PASS] 4. Conformance tests — full repo 268 passed / 0 failed / 0 skipped; CON-005 catalog suite
        runnable + green; CON-005 behavioral + CON-006 driven suites abstract by design (subclassed
        by TKT-014 / TKT-019); no consumed-contract test files touched
[PASS] 5. Consumption fidelity — CON-003/009/011/001 types used read-only (GuestRoomInfo,
        ServiceOffering, TraversalGraph, RoomStaffState, EmittedEffect/BehaviorOutcome, kernel ids);
        FakeRoomServiceState honors the CON-006 unknown-room KeyNotFoundException semantic
[PASS] 6. Domain purity — nothing under src/domains/ changed this session (tests-only)
[PASS] 7. Registry sync — N/A (no contract definition/version change; CON-005/006 already FROZEN,
        REGISTRY + doc conformance-test pointers already consistent)
VERDICT: COMPLIANT
```
