---
description: "Post-pipeline change intake — add a new requirement, resolve conflicts, cascade through docs, contracts, and tickets. Usage: /requirement {short description}"
argument-hint: "short description of the new requirement"
---

# /requirement — Add a Requirement After the Pipeline

Change-intake for a project whose pipeline gates are already passed. Read `CLAUDE.md` and `docs/PIPELINE.md` first. This command may run any time after Gate 1; steps below that touch stages whose gates aren't passed yet are simply skipped (the requirement then flows through the normal remaining stages).

New requirement to intake: **$ARGUMENTS**

## Phase A — Capture (ideation rules apply)

1. Interview the user exactly as `/ideate` would: never assume, `PROPOSAL:` labels for suggestions, chase edge cases and hidden implications ("offline mode implies a local store — in scope?"). Produce one or more testable REQ-### rows (next free IDs), each with priority and source goal. If no existing goal covers it, propose a goal addition — user confirms.
2. Do not write anything yet; get the user to confirm the exact REQ wording.

## Phase B — Conflict scan

3. Scan the full PDD and every SYS doc for requirements the new REQ(s) conflict with, duplicate, partially overlap, or silently invalidate. Also check Non-goals — if the new REQ contradicts a recorded non-goal, that is a conflict.
4. Present findings as a conflict table: existing REQ/goal/non-goal, nature of conflict, resolution options (≥2 where sensible: amend old, amend new, split, supersede, reject new). Ask the user to resolve each one. No conflict is resolved unilaterally.
5. Apply resolutions: superseded REQs marked (never deleted), amended REQs get new wording with a decision-log entry, PDD and affected SYS docs updated in the same session. Assign the new REQ(s) to exactly one system each — if no system fits, propose a new SYS doc or a boundary change (user approves; run the relevant parts of `/breakdown` for it).

## Phase C — Architecture impact

6. Map the REQ(s) onto the domain map: served by an existing domain as-is, an existing domain with model changes, or a new domain. Update the affected DOM docs (Requirements served, model, ports). Consequential design choices get the standard ≥2-options treatment. New ports discovered here are listed for Phase D.

## Phase D — Contracts

7. Determine contract impact: **unchanged** (new REQ rides existing contracts), **new contracts** (draft per `/contracts` rigidity rules, user approves, freeze), or **changes to FROZEN contracts** — which MUST follow the change protocol: contract-change ticket, user approval, version bump, change-history entry listing every affected ticket, conformance tests updated by that ticket only. Update REGISTRY.md.

## Phase E — Tickets

8. Create tickets per `/tickets` rules: contract-definition/contract-change tickets first and blocking; then implementation tickets; integration tickets if wiring changes. Every new ticket traces to the new REQ(s). If a frozen contract changed, add re-verification tasks to affected DONE tickets' consumers (as new tickets — never reopen DONE tickets silently; list them for the user).
9. Re-run the stage-5 audits on the updated board: file-ownership disjointness against all TODO/IN PROGRESS tickets, DAG acyclicity, coverage. Update BOARD.md and its parallelization waves.

## Phase F — Handoff

10. Append a change record to the PDD decision log and the PIPELINE.md log: REQ IDs added, conflicts resolved, docs touched, contracts added/bumped, tickets created.
11. Summarize for the user and hand off: implementation proceeds with `/implement TKT-###` per the updated waves. Do not implement in this session.

Multi-session friendly: if interrupted, the change record notes the phase reached; resume there.
