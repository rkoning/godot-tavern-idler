# TKT-028: CON-009 v1.1 staff-content validation (contract-change)

> Status: DONE
> Type: contract-change
> Domain: DOM-005 | System: SYS-005
> Traces to: CON-009 v1.1; REQ-060/063/064/095 (staff content)
> Blocked by: TKT-004 | Blocks: TKT-020
> Session: —

## Goal

Bring the CON-009 conformance suite into line with **CON-009 v1.1** (approved 2026-07-13 via `/requirement`; raised while implementing TKT-004). The contract doc + REGISTRY are already updated — this ticket carries the conformance-test changes so the frozen suite asserts every v1.1 validation rule. It defines no domain behavior; TKT-020 implements the loader that satisfies the updated suite.

v1.1 is **additive validation, no type-signature change**: nothing about the port interfaces changes.

## Contracts

| Contract | Role |
|---|---|
| CON-009 | updates conformance suite to v1.1 (change protocol) |

## File ownership (exclusive)

Ownership of the CON-009 catalog conformance artifacts is transferred to this ticket for this change (created by TKT-004, which stays DONE):

```
tests/contracts/staffing/StaffCatalogConformanceTests.cs
tests/contracts/staffing/staff.sample.json
```

## Acceptance criteria

- [x] `LoadCatalog` seam gains a trait-registry context — `LoadCatalog(string json, IReadOnlyCollection<TraitId> knownTraits)`
- [x] Golden `staff.sample.json` loads clean against a trait set (sturdy/soothing/legendary)
- [x] New invalid-catalog cases each rejected — 14 `MemberData` cases, each violating exactly ONE rule vs a valid baseline (negative role/hire wage; zero-trait role/hire; negative price; empty serviceId; empty/duplicate ids; dangling `namedHire.role`; trait not in registry; unknown JSON field; empty displayName/unlockPerk) so a loader can't pass by implementing a subset
- [x] Suite stays abstract (runnable once TKT-020 subclasses it); full suite green (238/0/0), nothing turned red
- [x] contract-compliance skill check passes — COMPLIANT (report below)
- [x] TDD (abstract-suite update, compile-checked); BOARD row + status updated on start/finish

## Implementation notes

- The v0 suite from TKT-004 already asserts unique/empty ids and dangling-role rejection; this ticket adds the wage/traits/price/trait-existence/unknown-field cases and the trait-registry seam.
- Trait-existence is cross-file: `LoadCatalog` validates staff trait ids against the passed trait registry (the content adapter, TKT-020, supplies the loaded CON-011 traits). CON-009 v1.1 Semantics is normative.

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Interactive (dispatch held this contract-change ticket). Updated `StaffCatalogConformanceTests.cs` to CON-009 v1.1: added the `knownTraits` param to `LoadCatalog`, fed the golden test a trait set, and replaced the v1.0 theories with 14 isolated `MemberData` cases (one rule violated per case vs a valid baseline). Suite stays abstract (TKT-020 subclasses); full suite **238/0/0**. `staff.sample.json` unchanged. |
| 2026-07-13 | **CONTRACT COMPLIANCE — TKT-028** — [PASS] 1 Ownership (only `tests/contracts/staffing/StaffCatalogConformanceTests.cs` [granted from TKT-004] + own ticket/BOARD) · [PASS] 2 Frozen-doc integrity (CON-009.md/REGISTRY not touched here — the v1.1 doc/REGISTRY changes were the earlier `/requirement` commit) · [N/A] 3 Interface fidelity (no port impl) · [PASS] 4 Conformance tests (238/0/0; suite abstract) · [N/A] 5 Consumption · [PASS] 6 Purity (no `src/domains` change) · [PASS] 7 Registry sync (CON-009 already 1.1). **VERDICT: COMPLIANT.** Status → DONE; BOARD updated. Unblocks TKT-020 (staff loader implements v1.1 + subclasses this suite). |
