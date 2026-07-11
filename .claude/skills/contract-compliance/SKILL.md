---
name: contract-compliance
description: "Verify an implementation ticket complies with all frozen contracts before it can be marked DONE. Use at the end of every /implement session, or whenever contract adherence is in doubt."
---

# Contract Compliance Check

Run this before marking any ticket DONE. Output a compliance report; the ticket cannot close on any FAIL.

## Inputs

The current ticket (TKT-###), its contract table, `docs/contracts/REGISTRY.md`, and the diff of files changed this session (`git diff` + `git status` against the session start).

## Checks

1. **Ownership:** every changed file is inside the ticket's File Ownership block. Files in `tests/contracts/` or contract-generated interface files changed by a non-contract ticket → FAIL.
2. **Frozen-doc integrity:** `git diff` shows no edits to any `docs/contracts/CON-*.md` with status FROZEN, and no edits to REGISTRY.md rows for frozen contracts (non-contract-change tickets only).
3. **Interface fidelity:** for each contract the ticket implements, compare the code signatures against the contract's Interface Definition section — names, parameter types, return types, error types must match exactly. Renames, "improved" signatures, extra public surface not in the contract → FAIL.
4. **Conformance tests:** run the `tests/contracts/` suites for every implemented contract. All pass, zero skipped, and the test files are byte-identical to their pre-session state.
5. **Consumption fidelity:** for each consumed contract, grep the ticket's code for usage; verify it only calls operations the contract defines and handles every documented error mode (no swallowed error variants).
6. **Purity:** no file under `src/domains/` imports the engine/framework (grep the import/using/include lines against the known engine/framework module names from the PDD stack table).
7. **Registry sync:** if this is a contract-definition or contract-change ticket, REGISTRY.md and contract doc headers/versions/consumer lists are consistent.

## Report format

```
CONTRACT COMPLIANCE — TKT-### — {date}
[PASS/FAIL] 1. Ownership
[PASS/FAIL] 2. Frozen-doc integrity
[PASS/FAIL] 3. Interface fidelity (per contract: CON-### ...)
[PASS/FAIL] 4. Conformance tests (per contract: CON-### n passed)
[PASS/FAIL] 5. Consumption fidelity
[PASS/FAIL] 6. Domain purity
[PASS/FAIL] 7. Registry sync (or N/A)
VERDICT: COMPLIANT / NOT COMPLIANT — {failing items + required fix}
```

Append the report to the ticket's session log. On NOT COMPLIANT: fix and re-run, or set the ticket BLOCKED. Never mark DONE over a failing check, and never edit a contract or conformance test to convert a FAIL into a PASS.
