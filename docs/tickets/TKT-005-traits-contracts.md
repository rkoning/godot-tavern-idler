# TKT-005: Traits contracts (CON-011, CON-012)

> Status: DONE
> Type: contract-definition
> Domain: DOM-006 | System: SYS-006
> Traces to: REQ-040–047, REQ-094–096, REQ-110–111
> Blocked by: TKT-001 | Blocks: TKT-006, TKT-009, TKT-013, TKT-020
> Session: /implement TKT-005 (2026-07-13)
> Targets CON-011 **v1.1** (amended 2026-07-13 via `/requirement`) and CON-012 v1.0.

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-011 and CON-012 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-011 | defines |
| CON-012 | defines |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/traits/ports/**
tests/contracts/traits/**
```

## Acceptance criteria

- [x] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures) — `src/domains/traits/ports/TraitsApi.cs` (CON-011 v1.1), `TraitsDrivenPorts.cs` (CON-012 v1.0)
- [x] Abstract conformance suite covers every bullet of each contract's Conformance tests section — `TraitsApiConformanceTests` (lifecycle/reach/REQ-040/stacking/churn/EndNight/behavior/discovery/ordering), `PresenceSourceConformanceTests` (composition/ordering/inactive/walking/consumed-item/stability)
- [x] No behavior beyond type definitions; suites compile against interfaces only
- [x] Rule catalog JSON golden file + validation tests included (CON-011) — `traits.sample.json` + `TraitsCatalogConformanceTests` (golden load + all schema rules incl. v1.1 binary↔scaling symmetry)
- [x] All conformance tests for implemented contracts pass — suites are abstract (run once subclassed by TKT-013/019/020, per the TKT-002/003 contract-definition pattern); full repo suite green, 0 skipped
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`) — COMPLIANT (report in session log)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing — suites authored first → compile-RED (types missing) → ports added → GREEN
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Effect ordering (Ended→Began→Triggered), episode/EpisodeId semantics, and the CountScaling formulas in CON-011 are normative — the suite asserts them.

**CON-011 v1.1 (must be reflected in the conformance suite):**
- `EndNight()` closes open episodes internally and emits **no** effects; it returns an empty list. The suite asserts closure via a following `BeginNight` reopening with fresh `EpisodeId`s — it must not assert `…Ended` effects from `EndNight`.
- Binary continuous effects author `factor` (SpendingMultiplier) / `ratePerTick` (SatisfactionModifier); schema validation is symmetric (binary params require `Binary`, scaling params require `CountScaling`). Golden catalog + validation tests cover both directions.
- Episode churn keys on the qualifying pair **set** (count *or* membership); a count-preserving membership swap closes/reopens with a new `EpisodeId` and `Targets` = currently-qualifying guests, with no behavior re-roll while any pair persists. Pairs are unordered distinct-carrier.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | CON-011 amended to v1.1 via `/requirement` (user-approved) before this ticket was implemented; ticket now targets v1.1. A prior autonomous attempt that self-amended the frozen contract was rejected and discarded — this ticket implements v1.1 from scratch. |
| 2026-07-13 | `/implement TKT-005` started in worktree `tkt-005` (branched from local HEAD 82c752a). Status → IN PROGRESS; BOARD row updated. Baseline suite 73/0. |
| 2026-07-13 | Implemented CON-011 v1.1 + CON-012 v1.0 port surface verbatim (`TraitsApi.cs`, `TraitsDrivenPorts.cs`) and abstract conformance suites (`TraitsApiConformanceTests`, `TraitsCatalogConformanceTests` + `traits.sample.json`, `driven/PresenceSourceConformanceTests`, `TraitsConformanceSupport`). TDD: suites first (compile-RED, types missing) → ports (GREEN). Harness seams: `ITraitsTestHarness` (settable presence + scripted `IRandomSource` "traits" stream), catalog `LoadCatalog`→`ITraitsQueries`, and an engine-neutral `PresenceScenario` for the driven suite (keeps TKT-005 free of not-yet-coded CON-005/008/009/003 types). v1.1 specifics asserted: `EndNight` empty return + reopen-with-fresh-id; binary `factor`/`ratePerTick` vs scaling params (symmetric validation); episode churn on count **and** count-preserving membership swap (new EpisodeId, current Targets, no behavior re-roll). Full suite **73 passed / 0 failed / 0 skipped** (abstract suites contribute 0 runnable until subclassed — same as TKT-002/003). |
| 2026-07-13 | **CONTRACT COMPLIANCE — TKT-005** — [PASS] 1 Ownership (only `src/domains/traits/ports/**`, `tests/contracts/traits/**` + process docs) · [PASS] 2 Frozen-doc integrity (0 edits to `docs/contracts/`) · [PASS] 3 Interface fidelity (CON-011 v1.1 & CON-012 v1.0 verbatim; no extra public surface) · [PASS] 4 Conformance tests (73/0/0; new suites abstract) · [PASS] 5 Consumption fidelity (CON-001 kernel + CON-015 random used per surface) · [PASS] 6 Domain purity (ports import only `TavernIdler.Kernel`) · [PASS] 7 Registry sync (REGISTRY already lists CON-011 v1.1 / CON-012 v1.0 FROZEN; unchanged). **VERDICT: COMPLIANT.** Status → DONE; BOARD updated. Unblocks TKT-006, TKT-009, TKT-013, TKT-020. |
