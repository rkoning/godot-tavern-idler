---
description: "Stage 5 — break requirements into parallel-safe ticket docs with a blocking contract ticket per domain."
---

# /tickets — Ticket Breakdown

You are running stage 5. Read `CLAUDE.md` and `docs/PIPELINE.md`; verify Gate 4 is PASSED.

## Goal

Produce `docs/tickets/TKT-###-{name}.md` files (from `templates/ticket.template.md`) and `docs/tickets/BOARD.md` such that independent Claude Code sessions can each take one ticket and work in parallel without conflicts or duplicated work.

## Ticket structure per domain

1. **TKT (contract-definition, blocking):** the first ticket in every domain. It materializes that domain's contracts into code: port interface files, shared types, event definitions, and the conformance test suites in `tests/contracts/`. It blocks every other ticket in the domain. It owns the contract-generated files forever — implementation tickets consume them read-only.
2. **Implementation tickets:** domain logic per aggregate/feature, then adapter tickets binding ports to the engine/framework. Sized for one focused session (roughly: one aggregate, one adapter, one vertical slice).
3. **Integration tickets:** wire adapters + domains together per the architecture; end-to-end tests. These come last and depend on the tickets they integrate.

## Parallel-safety rules (enforce mechanically)

- **File ownership audit:** every ticket lists exclusive owned paths. Before finishing this stage, cross-check all tickets: any two tickets that could be in progress simultaneously (i.e., neither blocks the other transitively) must have disjoint ownership sets. If they collide, re-slice.
- **Dependency graph:** blocked-by/blocks fields form a DAG. Verify no cycles. Derive parallelization waves (wave 1 = contract tickets, wave 2 = everything unblocked after wave 1, ...) and write them into BOARD.md.
- **Coverage audit:** every REQ traced by ≥1 ticket; every contract implemented by exactly one ticket and consumed by the rest.

## Method

1. Read DOM docs + contracts. Draft the full ticket list (title, type, domain, traces, blocks) as a `PROPOSAL` with the wave diagram. User approves the slicing before files are written.
2. Write ticket files. Each must be self-sufficient: a fresh session with only CLAUDE.md + the ticket + linked contracts/docs must be able to do the work without asking the user design questions. Acceptance criteria reference REQ/CON IDs; include the standard criteria from the template (TDD, conformance tests, contract-compliance check).
3. Fill BOARD.md and the waves section.

## Gate 5 check

Run all three audits above, present results, user passes Gate 5 → PIPELINE.md updated. Implementation may begin: `/implement TKT-###`, one ticket per session, waves in order.
