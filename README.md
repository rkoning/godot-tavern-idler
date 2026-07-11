# Claude Project Harness

A template repo for building software (or games) from design to implementation using pipelined, parallel Claude Code sessions.

## How it works

The pipeline moves through six gated stages. Each stage is a slash command run in Claude Code. A stage cannot begin until the previous stage's gate is passed (tracked in `docs/PIPELINE.md`).

| Stage | Command | Output |
|---|---|---|
| 1. Ideation | `/ideate` | `docs/design/PDD.md` — the Project Design Document |
| 2. Breakdown | `/breakdown` | `docs/systems/SYS-*.md` — per-system requirement docs |
| 3. Architecture | `/architect` | `docs/domains/DOM-*.md` — per-domain architecture docs (DDD, ports & adapters) |
| 4. Contracts | `/contracts` | `docs/contracts/CON-*.md` + `docs/contracts/REGISTRY.md` |
| 5. Tickets | `/tickets` | `docs/tickets/TKT-*.md` + `docs/tickets/BOARD.md` |
| 6. Implementation | `/implement TKT-###` | Code, tests, and a completed ticket — run many in parallel |

### Change intake (post-pipeline)

| Command | Purpose |
|---|---|
| `/requirement {description}` | Add a new requirement after gates are passed: capture via ideation rules, scan PDD + system docs for conflicts, resolve with the user, cascade updates through domains → contracts (change protocol for frozen ones) → new tickets. |
| `/bug {description}` | Diagnose against the docs (contract violation = bug; doc-compliant behavior = routed to `/requirement`), reproduce with failing tests first, fix at the root cause, run contract-compliance, update the governing docs and board. |
| `/stakeholders` | Non-technical status report: goals, requirements, and delivery progress derived from the ticket board — outcomes language only, no implementation detail. Saved to `docs/reports/`, dated, never overwritten. |
| `/harness-evaluation {note}` | Log a problem with the harness itself (not the project) to `HARNESS-EVALUATION.md` — project-agnostic, append-only, for iterating on the harness later. |

## Setup for a new project

1. Copy this repo (or `git clone` + remove origin).
2. Install the [superpowers plugin](https://github.com/obra/superpowers) — implementation sessions require its test-driven-development and subagent-driven-development skills.
3. Open the repo in Claude Code and run `/ideate`.

## Core principles

- **Never assume.** Claude may suggest, but every requirement is confirmed by you and recorded with a decision status. Unconfirmed items live in Open Questions, not in requirements.
- **Traceability.** Every artifact has an ID (REQ, SYS, DOM, CON, TKT) and links to its parents. A ticket traces back to the requirement that motivated it.
- **Rigid contracts.** All interaction between parallel sessions happens through frozen, versioned contracts. Changing one requires the change protocol in `CLAUDE.md`.
- **Domain-driven, hexagonal.** Business/game logic lives in the base language with no framework/engine imports. Ports define what the domain needs/offers; adapters bind them to the engine or framework.
- **Parallel-safe.** Each ticket declares exclusive file ownership. Two in-progress tickets never own the same file.

## Repo layout

```
CLAUDE.md                  Pipeline rules — loaded by every session
.claude/commands/          The six stage commands
.claude/skills/            contract-compliance enforcement skill
docs/PIPELINE.md           Stage state and gate checklist
docs/design/               PDD (stage 1)
docs/systems/              System docs (stage 2)
docs/domains/              Domain architecture docs (stage 3)
docs/contracts/            Contracts + registry (stage 4)
docs/tickets/              Tickets + board (stage 5)
templates/                 Document templates used by the commands
src/                       Created during implementation per architecture docs
```
