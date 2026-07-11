---
description: "Diagnose a reported bug, reproduce it with failing tests, fix it, and update docs. Usage: /bug {description of the bug}"
argument-hint: "what happens, what you expected, how to trigger it"
---

# /bug — Bug Diagnosis and Fix

Read `CLAUDE.md` and `docs/PIPELINE.md` first. Bug report: **$ARGUMENTS**

## Phase A — Understand the report

1. If the report is missing any of: observed behavior, expected behavior, or reproduction steps — ask the user before touching code.
2. Establish expected behavior from the docs, not from intuition: find the REQ/CON that governs this behavior. Three possible verdicts:
   - **Code violates a contract or REQ** → real bug, proceed.
   - **Code matches the docs; the user wants different behavior** → not a bug. Stop and route to `/requirement` (behavior change) — say so explicitly.
   - **Docs are silent or ambiguous** → treat the user's expected behavior as a proposed clarification; confirm it, then proceed and patch the doc gap in Phase E.

## Phase B — Diagnose

3. Reproduce first: run the existing suite, trace the failure path, read the relevant domain/adapter code and its contracts. Identify the root cause, not just the symptom site. State the diagnosis to the user in 2–3 sentences (cause, where, why tests missed it) before fixing.
4. Create a bug ticket `docs/tickets/TKT-###-bug-{slug}.md` (type: `bug`, from the ticket template): traces to the violated REQ/CON, diagnosis in Implementation notes, File Ownership = the files the fix needs. Check BOARD.md — if any IN PROGRESS ticket owns those files, coordinate: mark the bug ticket BLOCKED on it rather than editing files another session owns. Add the row to BOARD.md.

## Phase C — Failing tests first (mandatory)

5. Use the superpowers **test-driven-development** skill. Write test(s) that reproduce the bug and fail for the right reason — at the lowest level that captures it (domain unit test preferred; adapter/integration test if the bug lives in wiring). Run them; show the failure.
6. If the bug is a contract violation, check whether `tests/contracts/` should have caught it. If the conformance suite has a gap, note it — closing that gap belongs to a contract-change ticket (frozen conformance tests are not editable here); propose it to the user.

## Phase D — Fix

7. Minimal fix at the root cause. All rules of `/implement` apply: only owned files, consumed contracts are law (if the correct fix requires changing a frozen contract, STOP → contract-change ticket via the change protocol), domain purity preserved.
8. New tests pass, full suite green, nothing weakened or deleted. Run the **contract-compliance** skill; attach the report to the bug ticket.

## Phase E — Documentation

9. Update docs affected by the fix: the bug ticket (status DONE, session log with diagnosis → fix summary), BOARD.md, and — only where the bug revealed a doc gap or ambiguity (Phase A verdict 3) — the governing REQ/SYS/DOM doc, with a decision-log entry `DECIDED (user)` for the clarified behavior. Never rewrite doc history to pretend the ambiguity didn't exist; append.
10. Summarize: root cause, fix, tests added, docs touched, and any follow-up tickets proposed (conformance gap, contract change, adjacent latent bugs noticed but not fixed).

One bug per session. Adjacent bugs found along the way become new `/bug` candidates listed in the summary, not extra fixes.
