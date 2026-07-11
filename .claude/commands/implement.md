---
description: "Stage 6 — implement one ticket in this session. Usage: /implement TKT-###"
argument-hint: "TKT-###"
---

# /implement — Ticket Implementation

You are an implementation session for ticket **$ARGUMENTS**. Read `CLAUDE.md`, `docs/PIPELINE.md` (Gate 5 must be PASSED), then the ticket file in `docs/tickets/`.

## Start checklist

1. Ticket status must be TODO and all Blocked-by tickets DONE (check BOARD.md). If not, stop and report.
2. Set ticket status IN PROGRESS, add a session-log line, update your BOARD.md row.
3. Read every linked contract, the domain doc, and only the sections of other docs the ticket points to.

## Rules of engagement

- **Scope:** only what the ticket says. Discovered adjacent work → note it in the ticket's session log as a proposed new ticket; do not do it.
- **Files:** create/modify only paths in the ticket's File Ownership block. Contract-generated files and `tests/contracts/` are read-only unless this IS the contract-definition ticket.
- **Contracts:** consumed contracts are law. If a contract seems wrong or incomplete, STOP — do not work around it, do not "fix" it locally. Record the issue in the session log, set the ticket BLOCKED, and tell the user a contract-change ticket is needed.
- **TDD is mandatory:** use the superpowers **test-driven-development** skill — red, green, refactor; test first, always.
- **Subagents:** for tickets with multiple independent parts, use the superpowers **subagent-driven-development** skill to dispatch them.
- Domain code purity: nothing under `src/domains/` imports engine/framework modules. Adapters only under `src/adapters/`.

## Done checklist (all required before DONE)

1. All acceptance criteria checked off.
2. All unit tests pass; all conformance tests for implemented contracts pass, unmodified.
3. Run the **contract-compliance** skill (`.claude/skills/contract-compliance/`) and attach its result to the session log.
4. Full test suite of the repo still green (your changes broke nothing).
5. Ticket status DONE, session log completed, BOARD.md row updated.

Never weaken, skip, or delete a failing test to get to done. A ticket that can't finish honestly finishes as BLOCKED with an explanation.
