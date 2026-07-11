---
description: "Stage 2 — break the PDD into per-system requirement docs (SYS-*.md)."
---

# /breakdown — System Breakdown

You are running stage 2. Read `CLAUDE.md` and `docs/PIPELINE.md`; verify Gate 1 is PASSED. If not, stop and offer `/ideate`.

## Goal

Partition every PDD requirement into system docs (`docs/systems/SYS-###-{name}.md`, from `templates/system.template.md`). A system is a cohesive area of responsibility (e.g., "Inventory", "Rendering", "Billing") — coarser than a domain, finer than the project.

## Method

1. Read the full PDD. Build a draft partition: propose a system list with one-line responsibilities and which REQ IDs each would own. Label the whole thing `PROPOSAL` and present it for approval **before creating any files**. Offer at least one alternative partition if a plausible one exists (e.g., splitting or merging systems), with tradeoffs.
2. On approval, create each SYS doc. Every REQ must land in **exactly one** system. If a REQ seems to belong to two, split the REQ in the PDD (new IDs, old one marked superseded) — with user confirmation.
3. For each system, run a mini-ideation pass with the user: boundary statement (what it does NOT do), interactions with other systems, system-level detail and edge cases. New requirements discovered here get new REQ IDs in the PDD and a row in the system doc. Never-assume rules apply exactly as in `/ideate`.
4. Update links: PDD "Children" header lists all SYS docs; each SYS doc links the PDD; the PDD requirements table gets its System column filled.

## Gate 2 check (end of stage)

- Every REQ assigned to exactly one system (audit the PDD table — no blanks, no duplicates).
- Every system doc has a boundary statement and an interactions table.
- Present the audit result and ask the user to pass Gate 2. Only on their confirmation, update PIPELINE.md and mark SYS docs APPROVED.

This stage may span multiple sessions; on resume, summarize state and continue with unprocessed systems.
