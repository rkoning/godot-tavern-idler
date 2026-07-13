---
description: "Dispatch a parallelization wave: one headless /implement claude session per eligible ticket, each in its own worktree. Usage: /dispatch-wave [N]"
argument-hint: "[wave number, optional]"
---

# /dispatch-wave — Parallel wave dispatcher

You are the **dispatcher**. For the target wave, you launch one real, isolated `claude`
session per eligible ticket — each in its own git worktree/branch, running `/implement` in the
background — then monitor and report. You do **not** implement any ticket yourself.

Design of record: `docs/superpowers/specs/2026-07-13-dispatch-wave-design.md`.

Argument `$ARGUMENTS` is the wave number, or empty (auto-detect).

## 1. Preconditions — abort (dispatch nothing) if any fail

1. Read `docs/PIPELINE.md`: **Gate 5 must be PASSED**. If not, stop.
2. You must be in the **main worktree** on branch `main` with a clean tracked tree:
   - `git rev-parse --abbrev-ref HEAD` → `main`.
   - `git worktree list` → confirm the current directory is the primary (non-linked) worktree.
   - `git status --porcelain` → no tracked changes staged/unstaged. (`.dispatch/` is gitignored
     and expected.) If dirty, stop and tell the user to commit/stash first.
3. `git fetch origin` (never trust the local ref). If `origin/main` is ahead of local `main`,
   stop and tell the user to integrate first.

## 2. Select the wave and its eligible tickets

1. Read `docs/tickets/BOARD.md` — both the main ticket table (ID, Status, Blocked by) and the
   **Parallelization waves** table.
2. Determine the target wave:
   - If `$ARGUMENTS` names a wave, use it.
   - Else auto-detect: the **lowest** wave whose row contains ≥1 ticket that is `TODO` with all
     its Blocked-by tickets `DONE`. Report which wave you picked and why.
3. For each ticket in the wave row, classify:
   - **Eligible** = status `TODO` **and** every Blocked-by ticket is `DONE`.
   - Otherwise **skip** and record the reason (already IN PROGRESS/DONE/BLOCKED, or a blocker is
     not DONE).
4. If zero eligible tickets, report the per-ticket reasons and stop.

## 3. Prepare a worktree per eligible ticket (create-if-missing, never clobber)

For ticket `TKT-###` → worktree `..\ti-tkt###`, branch `tkt-###` (lowercase, drop the `TKT-`
prefix's dash: `TKT-002` → `ti-tkt002` / `tkt-002`). Using `git worktree list` to check state:

- **Missing:** `git worktree add ..\ti-tkt### -b tkt-###` (branches off current `main`). If the
  branch `tkt-###` already exists, use `git worktree add ..\ti-tkt### tkt-###` instead.
- **Exists:** reuse **only** if it is checked out on `tkt-###` and clean
  (`git -C ..\ti-tkt### status --porcelain` empty). If it is on the wrong branch or dirty,
  **skip that ticket** and report — never overwrite in-flight work.

Create the log dir once: `.dispatch\wave-<N>\`.

## 4. Launch — smoke-test first, then fan out

Build the per-ticket prompt (substitute `TKT-###` and the worktree path):

```
/implement TKT-###

--- dispatch addendum (you are a headless session; no human can answer prompts) ---
- You are running non-interactively in worktree <path> on branch tkt-###.
- If you would normally ask the user, make the safe, pipeline-compliant choice, or stop.
- On DONE: stage and commit ALL your changes on branch tkt-### with a descriptive message
  ending with the trailer "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>".
  DO NOT push.
- If the ticket cannot finish honestly, leave it BLOCKED per the ticket rules, commit
  nothing, and clearly explain why. Never weaken or skip a test to reach DONE.
```

Scoped allowlist (identical for every session):

```
--allowedTools "Read" "Glob" "Grep" "Edit" "Write" "Skill" "Task" "TaskCreate" "TaskUpdate" \
  "Bash(dotnet restore:*)" "Bash(dotnet build:*)" "Bash(dotnet test:*)" \
  "Bash(git status:*)" "Bash(git diff:*)" "Bash(git add:*)" "Bash(git commit:*)"
```

Launch each session as a **background** process whose working directory is the ticket's
worktree, redirecting output to its log, e.g. from the main repo:

```
( cd ../ti-tkt### && claude -p "<prompt>" <allowlist> ) > .dispatch/wave-<N>/tkt-###.log 2>&1
```

**Smoke test:** launch the **first** eligible ticket this way, wait ~15–20s, then read its log.
A healthy start shows the session working (reading files / running the ticket), not an immediate
crash (e.g. unknown-command or bad-flag error). If it crashed, **stop** — report the log so one
CLI/flag problem doesn't spawn a broken wave. If healthy, launch the remaining eligible tickets
in parallel the same way.

Note: the allowlist is default-deny — in headless mode any unlisted tool call is denied (not
prompted), so a session needing something unlisted stops rather than acting. That is intended.

## 5. Monitor and report

1. Immediately print a table: ticket · worktree · branch · log path · (launched / skipped+reason).
2. As background sessions finish, inspect each worktree to classify the outcome:
   - **DONE + committed** — ticket file shows DONE and `git -C ..\ti-tkt### log` has a new commit.
   - **BLOCKED** — ticket file shows BLOCKED; summarize the reason from its session log.
   - **Failed** — process exited without DONE/BLOCKED; point at the log.
3. When all have finished, print the **merge-back** reminder (this command does not merge):

   ```
   git checkout main
   git merge tkt-### …            # resolve BOARD.md row conflicts, keep every DONE row
   git push origin main
   git worktree remove ..\ti-tkt### ; git branch -d tkt-###
   ```

## Scope

- You are a dispatcher only: never edit ticket source/tests yourself, never merge, never push.
- Merging and cleanup stay manual (see the reminder above).
- If nothing is eligible or preconditions fail, report clearly and change nothing.
