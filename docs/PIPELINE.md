# Pipeline State

> Sessions: read this file first. Update it when a stage's status changes. Only the user may set a gate to PASSED.

**Current stage:** 1 — Ideation

| # | Stage | Command | Gate status | Gate passed on |
|---|---|---|---|---|
| 1 | Ideation | `/ideate` | NOT STARTED | — |
| 2 | Breakdown | `/breakdown` | BLOCKED | — |
| 3 | Architecture | `/architect` | BLOCKED | — |
| 4 | Contracts | `/contracts` | BLOCKED | — |
| 5 | Tickets | `/tickets` | BLOCKED | — |
| 6 | Implementation | `/implement` | BLOCKED | — |

Gate statuses: `NOT STARTED` → `IN PROGRESS` → `READY FOR REVIEW` → `PASSED` (user only) | `BLOCKED` (prior gate not passed)

## Gate criteria

- **Gate 1 (Ideation → Breakdown):** PDD exists; zero `OPEN` items in PDD scope/platform/stack sections; user states the PDD is complete enough to break down. Open questions may remain only if explicitly deferred by the user with a `DEFERRED` tag.
- **Gate 2 (Breakdown → Architecture):** every PDD requirement is assigned to exactly one system doc; user approves the system list.
- **Gate 3 (Architecture → Contracts):** every system maps to ≥1 domain; all flagged architecture decisions resolved by the user; user approves domain map.
- **Gate 4 (Contracts → Tickets):** every port and cross-domain interaction has a contract; all contracts user-approved and `FROZEN`; REGISTRY.md complete.
- **Gate 5 (Tickets → Implementation):** every requirement covered by ≥1 ticket; file-ownership audit shows no overlap between parallelizable tickets; dependency graph acyclic; user approves board.

## Log

<!-- Append one line per session: date, command run, what changed -->
