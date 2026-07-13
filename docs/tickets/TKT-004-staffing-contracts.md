# TKT-004: Staffing contracts (CON-009, CON-010)

> Status: DONE
> Type: contract-definition
> Domain: DOM-005 | System: SYS-005
> Traces to: REQ-056–065, REQ-108–109
> Blocked by: TKT-003 | Blocks: TKT-006, TKT-009, TKT-012, TKT-020
> Session: /implement TKT-004 (2026-07-13)

## Goal

The port interfaces, error/event/value types, and abstract conformance suites for CON-009 and CON-010 and CON-003 and CON-001 exist in code, matching the frozen contract documents exactly. No domain behavior is implemented. This ticket owns those files forever; the domain implementation ticket consumes them read-only and plugs into the suites via a fixture subclass.

## Contracts

| Contract | Role |
|---|---|
| CON-009 | defines |
| CON-010 | defines |
| CON-003 | consumes (StaffRequirements) |
| CON-001 | consumes |

Consumed contracts are read-only. Implemented contracts must pass their conformance tests.

## File ownership (exclusive)

Only these paths may be created/modified by this ticket's session:

```
src/domains/staffing/ports/**
tests/contracts/staffing/**
```

## Acceptance criteria

- [x] Every interface/record/enum in the contracts' code blocks compiles verbatim — `src/domains/staffing/ports/StaffingApi.cs` (CON-009), `StaffingDrivenPorts.cs` (CON-010)
- [x] Abstract conformance suite covers every bullet — `StaffingApiConformanceTests` (error matrix incl. mutate-nothing, REQ-058 state table + refusal flip, REQ-061 speed min/over/degraded/closed, REQ-109 orphaning, named-hire happy path, wage-bill contents, implicit-reassign event pair, snapshot round-trip); `driven/RoomRequirementsConformanceTests` (Get equivalence, empty vs `RoomsWithRequirements`, absent→`KeyNotFoundException`, `IHireUnlocks`). CON-010's *temporal* bullets (Get equivalence across an upgrade; unlocks shrink only at prestige) are bridge-behaviour, left to TKT-019's subclass — noted in the suite.
- [x] No behavior beyond type definitions; suites compile against interfaces only (harness/catalog support types are test-only)
- [x] Staff content JSON golden file + validation tests included — `staff.sample.json` + `StaffCatalogConformanceTests` (golden load + reject duplicate/empty/dangling ids). **Clarification (no contract edit):** CON-009 doesn't enumerate staff-schema validation like CON-011 does; suite asserts only structurally-implied invariants — tighter rules would be a CON-009 `/requirement`.
- [x] All conformance tests for implemented contracts pass — abstract (0 runnable until subclassed, per TKT-002/003/005); full suite 73 passed / 0 failed / 0 skipped via `.sln`
- [x] contract-compliance skill check passes — COMPLIANT (report below)
- [x] Unit tests written first (TDD) — suites authored first → compile-RED (CS0234/CS0246, staffing types missing) → ports added → GREEN
- [x] Ticket status + BOARD.md row updated on start and finish

## Implementation notes

Read the CON docs top to bottom before writing code; the Interface definition section IS the contract.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Started (interactive). TKT-003 DONE (provides consumed `StaffRequirements`/`RoleRequirement`). Status → IN PROGRESS; BOARD updated. Baseline suite 73/0. |
| 2026-07-13 | TDD: wrote conformance suites + support harness first (`StaffingConformanceSupport` with `IStaffingTestHarness` seam + test-only `StaffCatalog`; `StaffingApiConformanceTests`; `driven/RoomRequirementsConformanceTests`; `StaffCatalogConformanceTests` + `staff.sample.json`) → compile-RED (staffing port types missing). Added `StaffingApi.cs` (CON-009) + `StaffingDrivenPorts.cs` (CON-010) verbatim → GREEN. Golden JSON read via `[CallerFilePath]` (no `Tests.csproj` edit — that file is TKT-027-owned). Full suite **73 passed / 0 failed / 0 skipped** (Release, `.sln`); staffing suites abstract → 0 runnable until TKT-012/019/020 subclass. |
| 2026-07-13 | **Clarification recorded (no contract edit):** CON-009 shows the staff-content JSON schema but does not enumerate validation rules (unlike CON-011). The catalog suite asserts only structurally-implied invariants (unique role/named-hire ids, named-hire→existing role, non-empty ids). If the user wants tighter/explicit rules, that is a CON-009 clarification via `/requirement`. Also: CON-010's temporal conformance bullets (upgrade equivalence, prestige-only unlock shrink) are bridge-behaviour deferred to TKT-019's subclass. |
| 2026-07-13 | **Affected by CON-009 v1.1** (later same day): the catalog under-specification flagged above was resolved via `/requirement` — CON-009 → v1.1 adds explicit staff-content validation rules. This ticket stays DONE; its catalog conformance suite (`StaffCatalogConformanceTests.cs`, `staff.sample.json`) is updated by **TKT-028** (ownership transferred there), not reopened here. |
| 2026-07-13 | **CONTRACT COMPLIANCE — TKT-004** — [PASS] 1 Ownership (`src/domains/staffing/ports/**`, `tests/contracts/staffing/**` + process docs) · [PASS] 2 Frozen-doc integrity (0 `docs/contracts/`/REGISTRY edits) · [PASS] 3 Interface fidelity (CON-009 & CON-010 verbatim; no extra public port surface) · [PASS] 4 Conformance tests (73/0/0; new suites abstract) · [PASS] 5 Consumption fidelity (CON-003 `StaffRequirements`/`RoleRequirement` + CON-001 kernel used per surface) · [PASS] 6 Domain purity (ports import only `TavernIdler.Kernel` + `TavernIdler.Domains.Structure`) · [PASS] 7 Registry sync (REGISTRY already lists CON-009/010 v1.0 FROZEN; unchanged). **VERDICT: COMPLIANT.** Status → DONE; BOARD updated. Unblocks TKT-006, TKT-009, TKT-012, TKT-020. |
