# Project Pipeline Rules

This repo uses a gated design→implementation pipeline. **Read `docs/PIPELINE.md` first in every session** to learn the current stage and what is allowed.

## Stages and gates

1. **Ideation** (`/ideate`) → `docs/design/PDD.md`
2. **Breakdown** (`/breakdown`) → `docs/systems/SYS-*.md`
3. **Architecture** (`/architect`) → `docs/domains/DOM-*.md`
4. **Contracts** (`/contracts`) → `docs/contracts/CON-*.md`
5. **Tickets** (`/tickets`) → `docs/tickets/TKT-*.md`
6. **Implementation** (`/implement TKT-###`) → code + tests

A stage's outputs may not be created until `docs/PIPELINE.md` shows the prior gate **PASSED**. Only the user passes a gate. If asked to do work belonging to a stage whose gate isn't open, say so and offer to run the correct command instead.

**Post-pipeline changes:** new or changed requirements after gates are passed go through `/requirement` — never edit passed-stage docs ad hoc. It re-runs the affected stages' rules for just that change (conflict scan → doc updates → contracts via change protocol → new tickets). Defects go through `/bug`: the docs decide what counts as a bug (code violating a REQ/contract); if the code matches the docs but the user wants different behavior, that's a `/requirement`, not a fix.

**If a `/command` isn't available in your client** (e.g., a Cowork session where repo commands don't register): read the matching file in `.claude/commands/` and follow it exactly as if it had been invoked.

## Non-negotiable rules (all stages)

- **Never assume requirements.** Conventions of a genre, platform, or framework are not requirements. You may propose (clearly labeled `PROPOSAL`), but nothing becomes a requirement until the user confirms it. Unconfirmed items go in the doc's Open Questions section.
- **Every decision is recorded** with one of: `DECIDED (user)`, `PROPOSED (awaiting user)`, `OPEN`. Never silently upgrade a proposal to decided.
- **IDs and links.** Requirements `REQ-###`, systems `SYS-###`, domains `DOM-###`, contracts `CON-###`, tickets `TKT-###`. Every doc links its parent(s) and children. When you create or split an artifact, update the links on both ends in the same session.
- **Docs are the source of truth.** If code and a contract disagree, the code is wrong. If two docs disagree, stop and ask the user.

## Architecture rules (stages 3+)

- Domain-driven design with hexagonal (ports & adapters) architecture.
- Domain logic is written in the base programming language only — **no imports from the UI framework or engine inside `src/domains/`**. Engine/framework code lives in adapters.
- Ports are interfaces owned by the domain. Adapters implement ports and live in `src/adapters/`.
- For consequential architecture choices (e.g., event-driven vs. FSM, sync vs. async boundaries, storage models), present ≥2 concrete options with tradeoffs and let the user choose. Do not pick unilaterally.

## Contract rules (stages 4+)

- Every interaction that crosses a domain boundary, a port, or a ticket boundary MUST go through a contract in `docs/contracts/`.
- A contract with status `FROZEN` may not be edited. To change one: open a `contract-change` ticket, get user approval, bump the contract version, list every affected ticket in the change record. Never edit a frozen contract in an implementation session.
- `docs/contracts/REGISTRY.md` is the index of all contracts. Check it before defining anything new — no duplicate or overlapping contracts.

## Implementation rules (stage 6)

- One session = one ticket. Do only what the ticket says.
- **File ownership is exclusive.** Only touch files your ticket owns (plus new test files for that code). Shared/contract-generated files are owned by the contract ticket, nobody else.
- Use the superpowers **test-driven-development** skill for all code and the **subagent-driven-development** skill for multi-part tickets. These are defaults, not options.
- Use the `contract-compliance` skill (in `.claude/skills/`) before marking any ticket complete.
- All contract-conformance tests must pass before a ticket is `DONE`. Never weaken a test to make it pass.
- Update the ticket file (status, notes) and `docs/tickets/BOARD.md` when you start and when you finish.
