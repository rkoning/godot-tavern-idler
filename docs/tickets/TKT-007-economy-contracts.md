# TKT-007: Economy contracts (CON-007, CON-008)

> Status: DONE
> Type: contract-definition
> Domain: DOM-004 | System: SYS-004
> Traces to: REQ-004, REQ-011–015, REQ-019–022, REQ-025–028, REQ-030, REQ-105–106
> Blocked by: TKT-003, TKT-006 | Blocks: TKT-008, TKT-009, TKT-015, TKT-020
> Session: /implement TKT-007 (2026-07-16)

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-007 and CON-008 and CON-006 and CON-005 and CON-004 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-007 | defines |
| CON-008 | defines |
| CON-006 | consumes (TransactionRequest/Result) |
| CON-005 | consumes (NightGuestStats) |
| CON-004 | consumes (ChargeResult) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/economy/ports/**
tests/contracts/economy/**
```

## Acceptance criteria

- [x] Every interface/record/enum in the contracts' code blocks compiles verbatim (names, namespaces, signatures)
- [x] Abstract conformance suite covers every bullet of each contract's Conformance tests section
- [x] No behavior beyond type definitions; suites compile against interfaces only
- [x] Settlement golden scenarios (solvent / upkeep-shortfall / partial wages / arrears seniority) encoded in the suite
- [x] Menu JSON golden file + validation tests (CON-008)
- [x] All conformance tests for implemented contracts pass (`dotnet test`, suites in `tests/contracts/`)
- [x] contract-compliance skill check passes (`.claude/skills/contract-compliance`)
- [x] Unit tests written first (superpowers **test-driven-development** skill) and passing
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Read the CON docs top to bottom before writing code; the Interface definition section IS the contract.

## Session log

| Date | Event |
|---|---|
| 2026-07-16 | `/implement TKT-007` started. Blocked-by TKT-003, TKT-006 both DONE. Status → IN PROGRESS; BOARD updated. Read CON-007, CON-008 (defines) + CON-001/004/005/006/002 (consumes). Baseline suite 268 passed. Writing port surface (`src/domains/economy/ports/`) + abstract conformance suites (`tests/contracts/economy/` + `driven/`) via TDD (suites RED → ports GREEN). No domain behavior. |
| 2026-07-16 | **Done.** Wrote `EconomyApi.cs` (CON-007) + `EconomyDrivenPorts.cs` (CON-008) verbatim from the frozen contracts (`src/domains/economy/ports/`). Abstract suites: `EconomyConformanceTests.cs` (CON-007 behavioral — pricing table, stock/restock/`StockDepleted`/`SoldOut`/carryover, the 4 settlement golden scenarios, every `NightReport` field, `PayBackPay`, double-settlement + phase gates, `ResetGold`, snapshot round-trip) over `EconomyConformanceSupport.cs` (`IEconomyTestHarness`, `EconomyWorld`, `Econ` builders); `driven/MenuContentConformanceTests.cs` (CON-008 golden load + every validation rule incl. cross-file trait + unlock filtering) with golden `menu.sample.json`; `driven/RunCostModifiersConformanceTests.cs`. TDD: suites compile-RED (missing `TavernIdler.Domains.Economy`) → ports GREEN. All suites abstract (TKT-015/019/020 subclass) ⇒ full suite unchanged at **268 passed / 0 failed / 0 skipped**, 0 warnings. |
| 2026-07-16 | **Design notes (no contract change):** (a) `ExecuteTransaction`/`ChargeBuild`/`PostRefund` return no event list, so the harness exposes `DrainEvents()` — the collection channel CON-016 tick-step 6 uses to pick up `TransactionExecuted`/`StockDepleted`; it is a test seam, not new contract surface. (b) `NightReport.NetGold` formula is under-specified by CON-007; the report-fields test pins it with `restockCost:0` so `earned−upkeep−wages` **and** `closing−opening` both equal −280 (satisfiable under either reading — flag for TKT-015 if it diverges). (c) Partial-wage scenario is order-robust (same outcome under stop-at-first-unaffordable or continue), and rejects any cheapest-first reading. (d) `IRunCostModifiers` cross-prestige swap is deferred to the bridge implementer (TKT-019) per the CON-010 RoomRequirements precedent. No CON/REGISTRY edits; frozen contracts untouched. |

### contract-compliance report

```
CONTRACT COMPLIANCE — TKT-007 — 2026-07-16
[PASS] 1. Ownership — all changed files inside src/domains/economy/ports/** and tests/contracts/economy/**; docs/tickets/{BOARD,TKT-007}.md are the mandated status updates.
[PASS] 2. Frozen-doc integrity — no edits to any CON-*.md or to REGISTRY.md.
[PASS] 3. Interface fidelity — CON-007 (EconomyApi.cs) and CON-008 (EconomyDrivenPorts.cs) transcribed verbatim: every interface/record/enum name, namespace, member signature, and error variant matches the frozen Interface definition. No extra public surface.
[PASS] 4. Conformance tests — new economy suites are abstract (subclassed by TKT-015/019/020); full suite 268 passed / 0 failed / 0 skipped. No pre-existing conformance test modified.
[PASS] 5. Consumption fidelity — consumed types used in signatures/constructors only: TransactionResult (all 4 variants matched), ChargeResult (both variants matched), TransactionRequest, NightGuestStats, BuildCostKind, kernel Outcome/Money. No swallowed error modes.
[PASS] 6. Domain purity — src/domains/economy/ imports only TavernIdler.Kernel + TavernIdler.Domains.{Guests,Structure}; no Godot/engine references.
[PASS] 7. Registry sync — contract-definition of already-FROZEN CON-007/008 v1.0; REGISTRY rows + doc headers unchanged and consistent (no new/changed contract, no version bump).
VERDICT: COMPLIANT
```
