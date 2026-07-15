# TKT-006: Guests contracts (CON-005, CON-006)

> Status: IN PROGRESS
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

- [ ] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [ ] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [ ] No behavior beyond type definitions; suites compile against interfaces only
- [ ] Guest sheet JSON golden file + validation tests incl. REQ-092 patience-band check (CON-005)
- [ ] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [ ] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [ ] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [ ] Ticket status + BOARD.md row updated on start and finish

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
