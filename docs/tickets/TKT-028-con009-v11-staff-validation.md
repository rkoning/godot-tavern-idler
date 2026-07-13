# TKT-028: CON-009 v1.1 staff-content validation (contract-change)

> Status: TODO
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

- [ ] `LoadCatalog` seam gains a trait-registry context (e.g. `StaffCatalog LoadCatalog(string json, IReadOnlyCollection<TraitId> knownTraits)`) so the cross-file trait-existence rule is testable
- [ ] Golden `staff.sample.json` loads clean against a trait set containing its referenced traits
- [ ] New invalid-catalog cases each rejected: negative `wage` (role and named hire); zero-trait role/named hire; negative `paidService.price`; empty/duplicate ids; dangling `namedHire.role`; a trait id absent from the supplied registry; unknown JSON field
- [ ] Suite stays abstract (runnable once TKT-020 subclasses it); nothing currently green turns red; full suite green
- [ ] contract-compliance skill check passes
- [ ] TDD; BOARD row + status updated on start/finish

## Implementation notes

- The v0 suite from TKT-004 already asserts unique/empty ids and dangling-role rejection; this ticket adds the wage/traits/price/trait-existence/unknown-field cases and the trait-registry seam.
- Trait-existence is cross-file: `LoadCatalog` validates staff trait ids against the passed trait registry (the content adapter, TKT-020, supplies the loaded CON-011 traits). CON-009 v1.1 Semantics is normative.

## Session log

| Date | Event |
|---|---|
