<!-- HARNESS:BEGIN (managed by the harness plugin — do not edit inside this block; run /harness:init refresh after plugin updates) -->
# Project Pipeline Rules

This repo uses the **harness** plugin's gated design→implementation pipeline. **Read `docs/PIPELINE.md` first in every session** to learn the current stage and what is allowed.

## Stages and gates

1. **Ideation** (`/harness:ideate`) → `docs/design/PDD.md`
2. **Breakdown** (`/harness:breakdown`) → `docs/systems/SYS-*.md`
3. **Architecture** (`/harness:architect`) → `docs/domains/DOM-*.md`
4. **Contracts** (`/harness:contracts`) → `docs/contracts/CON-*.md`
5. **Tickets** (`/harness:tickets`) → `docs/tickets/TKT-*.md`
6. **Implementation** (`/harness:implement TKT-###`) → code + tests

A stage's outputs may not be created until `docs/PIPELINE.md` shows the prior gate **PASSED**. Only the user passes a gate. If asked to do work belonging to a stage whose gate isn't open, say so and offer to run the correct command instead.

**Post-pipeline changes:** new or changed requirements go through `/harness:requirement` — never edit passed-stage docs ad hoc. Defects go through `/harness:bug`: the docs decide what counts as a bug (code violating a REQ/contract); if code matches the docs but different behavior is wanted, that's a requirement change, not a fix. Status reporting for non-developers: `/harness:stakeholders`. Feedback about the harness itself: `/harness:evaluation`. Parallel implementation waves are dispatched with `/harness:dispatch-wave` — implementation/integration/build-config tickets only; contract-definition and contract-change tickets always run interactively.

**If a `/harness:*` command isn't available in your client:** the plugin's command files live under the installed plugin root (`commands/*.md`); read the matching file and follow it exactly as if it had been invoked.

## Non-negotiable rules (all stages)

- **Never assume requirements.** Conventions of a genre, platform, or framework are not requirements. You may propose (clearly labeled `PROPOSAL`), but nothing becomes a requirement until the user confirms it. Unconfirmed items go in the doc's Open Questions section.
- **Every decision is recorded** with one of: `DECIDED (user)`, `PROPOSED (awaiting user)`, `OPEN`. Never silently upgrade a proposal to decided.
- **IDs and links.** Requirements `REQ-###`, systems `SYS-###`, domains `DOM-###`, contracts `CON-###`, tickets `TKT-###`. Every doc links its parent(s) and children. When you create or split an artifact, update the links on both ends in the same session.
- **Docs are the source of truth.** If code and a contract disagree, the code is wrong. If two docs disagree, stop and ask the user.

## Architecture rules (stages 3+)

- Domain-driven design with hexagonal (ports & adapters) architecture.
- Domain logic is written in the base programming language only — **no imports from the UI framework or engine inside `src/domains/`**. Engine/framework code lives in adapters.
- Ports are interfaces owned by the domain. Adapters implement ports and live in `src/adapters/`.
- For consequential architecture choices, present ≥2 concrete options with tradeoffs and let the user choose. Do not pick unilaterally.

## Contract rules (stages 4+)

- Every interaction that crosses a domain boundary, a port, or a ticket boundary MUST go through a contract in `docs/contracts/`.
- A contract with status `FROZEN` may not be edited. To change one: `contract-change` ticket, user approval, version bump, change record listing every affected ticket. Never edit a frozen contract in an implementation session.
- `docs/contracts/REGISTRY.md` is the index of all contracts. Check it before defining anything new — no duplicate or overlapping contracts.

## Implementation rules (stage 6)

- One session = one ticket. Do only what the ticket says.
- **File ownership is exclusive.** Only touch files your ticket owns (plus new test files for that code). Shared/contract-generated files are owned by the contract ticket, nobody else.
- Use the superpowers **test-driven-development** skill for all code and the **subagent-driven-development** skill for multi-part tickets. These are defaults, not options.
- Run the plugin's **contract-compliance** skill before marking any ticket complete.
- All contract-conformance tests must pass before a ticket is `DONE`. Never weaken a test to make it pass.
- Update the ticket file (status, notes) and `docs/tickets/BOARD.md` when you start and when you finish.
<!-- HARNESS:END -->

<!-- Project-specific instructions go below this line and survive /harness:init refresh. -->

## Environment — Windows + PowerShell

This project is developed on **Windows** with **PowerShell (5.1)** as the primary shell. Use only PowerShell-supported syntax and commands. Do NOT reach for Linux/macOS/bash idioms.

- No bash heredocs (`<<'EOF'`), no `$(...)` command substitution in the bash sense, no `&&`/`||` chaining — use `;` and `if ($?) { ... }`.
- No POSIX tools like `printf`, `cat <<`, `/dev/null`, forward-slash-only paths. Use PowerShell equivalents (`Out-File`, `$null`, `Join-Path`, etc.).
- For multi-line strings (e.g. commit messages) use a here-string `@'...'@` written to a file, then `git commit -F <file>`. Note `Out-File -Encoding utf8` adds a BOM; use `utf8NoBOM` (PS 6+) or `[IO.File]::WriteAllText(...)` if a clean file is needed.
- Paths use `K:\Projects\godot-tavern-idler\...` (backslashes) or forward slashes where the tool accepts them — but stay Windows-native.

## Git — commit/push policy (OVERRIDES the global no-commit rule)

In **this repository only**, the user's global "never commit or push" rule does NOT apply. Instead:

- **Always commit changes** — after completing work, commit it. Do not leave changes sitting unstaged/uncommitted waiting for the user.
- **You MAY always push to any non-`main` branch** without asking.
- **`main` is protected:** the ONLY actions that require explicit user input are **merging into `main`** and **pushing to `main`**. Never do either without the user's go-ahead.
