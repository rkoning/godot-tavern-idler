# TKT-030: Fix unsatisfiable CON-009 SpeedFactor conformance test (bug)

> Status: IN PROGRESS
> Type: bug
> Domain: DOM-005 | System: SYS-005
> Traces to: CON-009 (REQ-061 speed semantics); REQ-058
> Blocked by: — | Blocks: TKT-012 (unblocks it)
> Session: /bug (2026-07-13)

## Goal

Correct an unsatisfiable conformance test in the CON-009 suite so a deterministic `Roster` aggregate can pass it. **No contract change:** CON-009's interface/semantics are correct and unchanged; the test contradicted them. Ownership of the one test file is granted here for this fix (TKT-004 stays DONE; user-approved 2026-07-13).

## Contracts

| Contract | Role |
|---|---|
| CON-009 | conformance-test correction only — no interface/semantic change, no version bump |

## File ownership (exclusive)

```
tests/contracts/staffing/StaffingApiConformanceTests.cs
```

## Diagnosis

`SpeedFactor_min_over_degraded_closed` was unsatisfiable two ways against room `("bartender", min 2, max 5)`: (1) it asserted **8** successful `Assign`s into a **max-5** room, but CON-009 requires over-max assigns to fail with `RoomAtStaffingMax` (pinned by the sibling `RoomAtStaffingMax_when_role_full`); (2) after 5 working it refused only **1** (→ 4 ≥ min 2, still Open at 1.5) yet asserted ≤ 0.8 (Degraded). Latent until TKT-012 became the first subclass to run it (same class as TKT-029).

## Fix

Rewrite the test to keep `Assign`s within max, refuse enough to actually cross below min for the Degraded check, and add the Closed (speed 0) case. Covers min-staffed 1.0 / over-staffed 1.5 cap / degraded ≤ 0.8 / closed 0.

## Acceptance criteria

- [ ] Test satisfiable: all `Assign`s within max; refusals cross the min threshold
- [ ] Covers min-staffed 1.0, over-staffed 1.5 cap, degraded ≤ 0.8, closed 0
- [ ] Suite compiles; full repo suite green (abstract until TKT-012 subclasses)
- [ ] Runtime-verified by re-dispatching TKT-012 (recorded here)
- [ ] contract-compliance passes; BOARD + status updated

## Session log

| Date | Event |
|---|---|
| 2026-07-13 | Diagnosed (verified: over-max assigns + insufficient refusal); user approved the frozen-conformance edit. Fix applied to `SpeedFactor_min_over_degraded_closed`. |
