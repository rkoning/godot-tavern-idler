---
description: "Dispatch a parallelization wave: one headless /implement claude session per eligible IMPLEMENTATION ticket, each in its own worktree. Contract tickets are held for interactive runs. Usage: /dispatch-wave [N]"
argument-hint: "[wave number, optional]"
---

# /dispatch-wave — Parallel wave dispatcher

You are the **dispatcher**. For the target wave, you launch one real, isolated `claude`
session per **auto-dispatchable** eligible ticket — each in its own git worktree/branch, running
`/implement` in the background — then monitor and report. You do **not** implement any ticket,
merge, or push.

Design of record: `docs/superpowers/specs/2026-07-13-dispatch-wave-design.md`.
Hardened 2026-07-13 for HEV-002 (sandbox + ticket-type gating) and HEV-005 (worktree location).

Argument `$ARGUMENTS` is the wave number, or empty (auto-detect).

**Path convention (derive once, use everywhere):** `TKT-###` → branch `tkt-###`, worktree
`.claude/worktrees/tkt-###` (lowercase; drop only the `TKT-` dash — `TKT-010` → `tkt-010`).
`.claude/worktrees/` is gitignored, so worktrees live in-repo without polluting `git status`.

## 1. Preconditions — abort (dispatch nothing) if any fail

1. Read `docs/PIPELINE.md`: **Gate 5 must be PASSED**. If not, stop.
2. You must be in the **main worktree** on branch `main` with a clean tracked tree:
   - `git rev-parse --abbrev-ref HEAD` → `main`; `git worktree list` → current dir is primary.
   - `git status --porcelain` empty (`.dispatch/` and `.claude/worktrees/` are gitignored). If
     dirty, stop and tell the user to commit/stash first.
3. `git fetch origin`. If `origin/main` is ahead of local `main`, stop and tell the user to integrate.

## 2. Select the wave and its dispatchable tickets

1. Read `docs/tickets/BOARD.md` — the main ticket table (ID, **Type**, Status, Blocked by) and the
   **Parallelization waves** table.
2. Classify every ticket in the candidate wave row:
   - **Eligible** = status `TODO` **and** every Blocked-by ticket is `DONE`.
   - **Auto-dispatchable** = eligible **and** Type ∈ {`implementation`, `integration`, `build-config`}.
   - **Hold (interactive)** = eligible but Type ∈ {`contract-definition`, `contract-change`} — these
     are the tickets most likely to surface contract gaps needing a human decision (HEV-002). **Do
     not** auto-dispatch them; report them as "run interactively with `/implement TKT-###`".
   - **Skip** = not eligible; record why (DONE / IN PROGRESS / BLOCKED / blocker not DONE).
3. Target wave: if `$ARGUMENTS` names a wave, use it. Else auto-detect the **lowest** wave with ≥1
   **auto-dispatchable** ticket. Report the wave and the full classification.
4. If zero auto-dispatchable tickets, report the per-ticket reasons (incl. any held-for-interactive)
   and stop.

## 3. Prepare a worktree per auto-dispatchable ticket (create-if-missing, never clobber)

Using `git worktree list` to check state, for each `TKT-###`:

- **Missing:** `git worktree add .claude/worktrees/tkt-### -b tkt-###` (off current `main`). If the
  branch `tkt-###` already exists, use `git worktree add .claude/worktrees/tkt-### tkt-###`.
- **Exists:** reuse **only** if checked out on `tkt-###` and clean
  (`git -C .claude/worktrees/tkt-### status --porcelain` empty). If wrong branch or dirty, **skip
  that ticket** and report — never overwrite in-flight work.

Create the log dir once: `.dispatch/wave-<N>/`.

## 4. Launch — smoke-test first, then fan out

Per-ticket prompt (substitute `TKT-###` and the worktree path):

```
/implement TKT-###

--- dispatch addendum (headless session; no human can answer prompts) ---
- You run non-interactively in worktree <path> on branch tkt-###. If you would normally ask
  the user, make the safe, pipeline-compliant choice, or STOP.
- WRITE SCOPE (hard): modify ONLY paths inside this ticket's File Ownership block (+ new test
  files it owns). You may NOT edit docs/contracts/** (frozen contracts), docs/contracts/REGISTRY.md,
  docs/design/PDD.md, docs/PIPELINE.md, or any other ticket's file. You may NOT run /requirement.
- If the ticket seems to need a contract change or any out-of-ownership edit, DO NOT do it and DO
  NOT fabricate approval: set the ticket BLOCKED, explain, commit nothing.
- On DONE: stage + commit ALL your changes on branch tkt-### with a message ending
  "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>". DO NOT push.
- Never weaken, skip, or delete a test to reach DONE.
```

Scoped allowlist (identical for every session; default-deny — unlisted tool calls are denied,
not prompted, so a session needing something unlisted stops rather than acting):

```
--allowedTools "Read" "Glob" "Grep" "Edit" "Write" "Skill" "Task" "TaskCreate" "TaskUpdate" \
  "Bash(dotnet restore:*)" "Bash(dotnet build:*)" "Bash(dotnet test:*)" \
  "Bash(git status:*)" "Bash(git diff:*)" "Bash(git add:*)" "Bash(git commit:*)"
```

Launch each as a **background** process with the worktree as its working directory:

```
( cd .claude/worktrees/tkt-### && claude -p "<prompt>" <allowlist> ) > .dispatch/wave-<N>/tkt-###.log 2>&1
```

**Smoke test:** launch the **first** auto-dispatchable ticket, wait ~15–20s, read its log. A
healthy start shows work (reading files / running the ticket), not an immediate crash
(unknown-command / bad-flag). If it crashed, **stop** and report the log — don't spawn a broken
wave. If healthy, launch the rest in parallel the same way.

## 5. Monitor, govern, report

1. Print a table immediately: ticket · type · worktree · branch · log · (launched / held / skipped+reason).
2. As background sessions finish, classify each: **DONE+committed** (ticket DONE and
   `git -C .claude/worktrees/tkt-### log` has a new commit) · **BLOCKED** (summarize from log) ·
   **Failed** (exited without DONE/BLOCKED — point at the log).
3. **Pre-merge governance sweep (HEV-004) — run per finished branch before recommending a merge:**
   `git diff --name-only main...tkt-###` and **flag** any path under `docs/contracts/`, matching
   `REGISTRY`, `docs/design/PDD`, or **outside that ticket's File Ownership block**. Any hit ⇒ mark
   the branch ⚠ GOVERNANCE and tell the user to review it before merging (do not present it as
   clean).
4. When all finish, print the **merge-back** reminder (this command never merges):

   ```
   git checkout main
   git merge tkt-### …            # resolve BOARD.md row conflicts, keep every DONE row
   git push origin main
   git worktree remove .claude/worktrees/tkt-### ; git branch -d tkt-###
   ```

## Scope

- Dispatcher only: never edit ticket source/tests, never merge, never push, never run /requirement.
- Contract-definition and contract-change tickets are **held for interactive `/implement`**, not
  auto-dispatched.
- Merging + cleanup stay manual (reminder above). If nothing is auto-dispatchable or a precondition
  fails, report clearly and change nothing.
