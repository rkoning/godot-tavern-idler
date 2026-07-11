---
description: "Generate a non-technical status report: goals, requirements, and delivery progress — no developer-facing detail."
---

# /stakeholders — Stakeholder Status Report

Read-only command: it changes no pipeline docs except writing the report file. Read `docs/PIPELINE.md`, `docs/design/PDD.md`, all SYS docs, and `docs/tickets/BOARD.md` + ticket files (for status only).

## Audience rule

The reader is a non-developer stakeholder. **Exclude all developer-facing detail**: no domain/contract/port/adapter language, no ticket IDs, no file paths, no architecture terms, no test counts. Requirement IDs (REQ-###) may appear as stable reference numbers only. Plain language throughout — describe features and outcomes, not implementation.

## Deriving status (do this mechanically, report it plainly)

For each requirement, trace REQ → its tickets → BOARD.md statuses, then translate:

| Ticket state of the REQ's tickets | Report as |
|---|---|
| No tickets yet (pre-Gate 5) | Planned — not yet scheduled |
| All TODO | Scheduled |
| Any IN PROGRESS / REVIEW | In development |
| All DONE | Delivered |
| Any BLOCKED | Attention needed (+ one plain-language sentence why) |

Project-level status comes from `docs/PIPELINE.md` (which stage, what that means in plain words — e.g., "requirements are final and construction has begun").

## Report content, in order

1. **Project summary** — vision, current phase in one plain paragraph, overall progress (n of m requirements delivered; MUST-priority progress called out separately).
2. **Goals** — each goal from the PDD with a status roll-up of its requirements (on track / in progress / delivered / at risk) and one sentence of narrative.
3. **What's delivered** — completed requirements grouped by capability/system name, described as outcomes ("Players can save and resume mid-run"), not tasks.
4. **In development / up next** — same style, honestly labeled with the wave-derived ordering (as "now / next / later", never wave numbers).
5. **Attention needed** — blocked or at-risk items, why in plain terms, and what decision (if any) is being waited on from stakeholders. Include unresolved Open Questions from the PDD that are `OPEN` and stakeholder-answerable.
6. **Recent changes** — requirement additions/changes from `/requirement` and notable fixes from `/bug` since the last report, described by their user-visible effect.
7. **Scope guardrails** — the PDD's non-goals, restated briefly so expectations stay anchored.

## Output

- Write to `docs/reports/STAKEHOLDER-{YYYY-MM-DD}.md` (create `docs/reports/` if absent). Never overwrite a previous report — history is part of the value.
- Also give the user a short in-chat digest (5–8 sentences).
- If numbers don't add up during tracing (REQ with no system, ticket with no REQ), report honestly in an appendix line and flag it to the user as a pipeline hygiene issue — do not silently fix docs from this command.
