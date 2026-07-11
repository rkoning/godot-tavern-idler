---
description: "Stage 1 — iterative ideation to build the Project Design Document. Multi-session; rigorous questioning; zero assumptions."
---

# /ideate — Project Design Document

You are running stage 1 of the pipeline. Read `CLAUDE.md` and `docs/PIPELINE.md` first.

## Setup

- If `docs/design/PDD.md` does not exist, copy `templates/PDD.template.md` to `docs/design/PDD.md`, set PIPELINE.md stage 1 to IN PROGRESS, and start at zoom level 0.
- If it exists, this is a continuation session: read the whole PDD, summarize its current state to the user in a few sentences, list the OPEN questions, and ask where they want to zoom in today.

## The prime directive

**You may not assume anything about this project.** Not the platform, not the language, not that a game has a main menu, not that a web app has user accounts. Genre and framework conventions are suggestions you may offer — always labeled `PROPOSAL:` with a one-line rationale — never defaults you fill in. If the user says "the usual", ask what "the usual" means concretely.

## Method: progressive zoom

Work top-down. Don't descend a level until the user confirms the current one is settled (ask explicitly).

- **Zoom 0 — Vision & goals:** what is this, who is it for, why should it exist, what does success look like, what is explicitly out of scope.
- **Zoom 1 — The three pillars:** where it lives (platforms, hosting, distribution, target hardware), what it uses (language, engine/framework + versions, key libraries, tooling), what it does (major capabilities, user-facing flows).
- **Zoom 2 — Requirements:** decompose each capability into testable REQ-### rows. Chase edge cases — failure modes, empty states, limits, concurrency, persistence, accessibility, localization — by asking, not by inventing.
- **Zoom 3 — Fine grain:** per-requirement detail the user wants to nail down now rather than during breakdown.

## Questioning discipline

- Ask a handful of focused questions at a time, not a wall of forty.
- Prefer concrete alternatives over open prompts when it helps ("Single-player only, co-op, or competitive multiplayer? This decides netcode requirements.").
- When an answer implies hidden requirements, surface them: "Cross-platform saves imply a sync backend — is that in scope?"
- Every answer lands in the PDD immediately: goals table, decision tables (`DECIDED (user)`), REQ rows, or Open Questions. Every session appends to the Decision Log and the PIPELINE.md log.
- Requirements must be testable and unambiguous. If you write a REQ containing "etc.", "and so on", or "as appropriate", rewrite it or split it.

## Ending a session

1. Ensure everything discussed is in the PDD — never leave decisions only in chat.
2. Restate the OPEN questions and suggest what the next session should cover.
3. If the user says the PDD is complete: run the gate check from PIPELINE.md (no OPEN items in scope/platform/stack unless user-DEFERRED), then ask the user to confirm passing Gate 1. Only on their confirmation, mark it PASSED in PIPELINE.md and set the PDD status to READY FOR BREAKDOWN.
