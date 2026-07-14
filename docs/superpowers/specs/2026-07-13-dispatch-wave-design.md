# Design: `/dispatch-wave` command

> Date: 2026-07-13
> Status: APPROVED (user, 2026-07-13)
> Kind: harness tooling (not part of the game design→implementation pipeline)
> Amended 2026-07-13 (hardening, HEV-002/004/005): worktrees now live under the gitignored
> in-repo `.claude/worktrees/tkt-###` (not sibling `..\ti-tkt###`); only `implementation` /
> `integration` / `build-config` tickets are auto-dispatched — `contract-definition` /
> `contract-change` are held for interactive `/implement`; the dispatch prompt hard-scopes writes
> to the ticket's ownership and forbids frozen-contract / cross-ticket edits and `/requirement`;
> the report runs a per-branch pre-merge governance sweep flagging out-of-ownership or
> frozen-doc edits. The command file is the source of truth for these details.

## Purpose

Given a parallelization wave from `docs/tickets/BOARD.md`, launch **one real isolated
`claude` session per eligible ticket** — each in its own git worktree/branch, running
`/implement`, in the background — then report status. Automates the manual per-worktree
recipe for wave 2+ so the user does not have to open a terminal per ticket.

## Invocation

`/dispatch-wave [N]`

- `N` given → dispatch wave `N`.
- `N` omitted → auto-detect the **lowest** wave in BOARD's *Parallelization waves* table that
  still has ≥1 eligible ticket; report which wave was picked.

## Eligibility & preconditions

Abort with a clear message (dispatch nothing) if:

- Not run from the **main** worktree, not on `main`, or the tracked working tree is dirty.
- Gate 5 is not PASSED in `docs/PIPELINE.md`.

Always `git fetch` first (staleness rule). A ticket in the target wave is **eligible** only if
its BOARD status is `TODO` and every Blocked-by ticket is `DONE`. Ineligible tickets are
**skipped with a reported reason** (already IN PROGRESS/DONE/BLOCKED, or blockers unmet); the
rest still dispatch.

## Worktree handling (create-if-missing, never clobber)

- Convention: worktree `..\ti-tkt###`, branch `tkt-###`, branched from current `main`.
- Missing → `git worktree add ..\ti-tkt### -b tkt-###`.
- Existing → reuse **only** if it is on branch `tkt-###` and clean; otherwise skip that ticket
  and report (never overwrite the user's in-flight work).

## Launch (per eligible ticket, background, parallel)

Working directory = the ticket's worktree. Command:

```
claude -p "<implement prompt + dispatch addendum>" --allowedTools <scoped set>
```

Prompt = `/implement TKT-###` plus a dispatch addendum: runs headless (no human to prompt),
make the safe pipeline-compliant choice or stop; on DONE, stage + commit all changes on
`tkt-###` with a `Co-Authored-By` trailer and **do not push**; if it cannot finish honestly,
leave the ticket BLOCKED, commit nothing, report why.

**Scoped allowlist** (default-deny everything else): `Read, Glob, Grep, Edit, Write, Skill,
Task, TaskCreate, TaskUpdate`, and narrow Bash — `dotnet restore/build/test`,
`git status/diff/add/commit`. No `git push`, no arbitrary shell, no network. In headless mode a
non-allowlisted tool call is denied (not prompted), so a session that needs something unlisted
stops rather than acting.

Commit authorization is carried by the **prompt**, not memory: each worktree has a different
filesystem path, so the repo's commit-authorization memory does not load in the sub-session — an
explicit prompt instruction is the source of truth.

Per-ticket stdout/stderr → `.dispatch\wave-N\tkt-###.log` in the main repo (`.dispatch/` is
gitignored).

## Smoke-test-then-fan-out

Launch the **first** eligible ticket, wait briefly, and check its log for a healthy start (no
immediate crash such as an unknown-command error). If healthy, launch the rest in parallel; if
not, abort and report so a CLI/flag problem fails one session, not the whole wave.

## Monitoring & reporting

After launch: list each session (ticket, worktree, branch, log path). As background sessions
finish, summarize per-ticket outcome — DONE+committed / BLOCKED / failed — then print the
merge-back instructions.

## Out of scope (YAGNI)

Merging branches back to `main` and pushing remain **manual** (existing recipe). Dispatch stops
at "all sessions finished + how to merge." A future `/merge-wave` could automate it.

## Known caveats

1. Relies on `claude -p "/implement …" --allowedTools …` running a project slash command
   headlessly. Intended CLI behavior, but validated per-run by the smoke test rather than
   assumed.
2. Autonomous TDD with no human watching each red-green step; contained by the allowlist, the
   no-mid-wave-gate nature of wave tickets, and the "BLOCKED not hack" rule.
