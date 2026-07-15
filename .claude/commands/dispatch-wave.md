---
description: "Dispatch a parallelization wave: one headless /implement claude session per eligible IMPLEMENTATION ticket, each in its own worktree. Contract tickets are held for interactive runs. Usage: /dispatch-wave [N]"
argument-hint: "[wave number, optional]"
---

# /dispatch-wave — Parallel wave dispatcher

You are the **dispatcher**. For the target wave, you first **plan and validate** every candidate
ticket *with the user present* (§3), then launch one real, isolated `claude` session per
**validated-clean** ticket — each in its own git worktree/branch, running `/implement` in the
background — then monitor and report. You do **not** implement any ticket, merge, or push.

**Plan then dispatch (HEV-006):** never fan out headless agents on tickets whose requirements
haven't been validated. A headless session has no human to ask, so every missing requirement,
ambiguous acceptance criterion, or open issue must be surfaced and resolved *before* dispatch — not
discovered inside an isolated worktree where it can only stall, guess, or self-BLOCK.

Design of record: `docs/superpowers/specs/2026-07-13-dispatch-wave-design.md`.
Hardened 2026-07-13 for HEV-002 (sandbox + ticket-type gating) and HEV-005 (worktree location);
2026-07-15 for HEV-006 (pre-dispatch plan + interactive requirement validation).

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

## 3. Plan & validate every candidate — interactively, before any fan-out (HEV-006)

This stage runs **in the main interactive session with the user present**. Nothing here writes to a
worktree or dispatches an agent. Its job is to catch missing/incomplete requirements *now*, while a
human can answer, instead of inside a headless session that can only stall or self-BLOCK.

1. **Produce a change plan per auto-dispatchable ticket.** For each one, read the ticket and every
   contract / requirement / design doc it references, and produce a **read-only** plan covering:
   - every file the ticket will create or modify (must stay within its File Ownership block);
   - the contracts/ports it touches and the tests it will add;
   - **every gap:** missing or incomplete requirements, ambiguous or untestable acceptance
     criteria, unresolved open questions, and any issue that would make a headless run guess.

   For a large wave you may fan out one **read-only planning agent per ticket** in parallel to build
   these plans faster. Planning agents get a read-only allowlist only — **no** worktree, no
   `Edit`/`Write`, no dispatch:

   ```
   --allowedTools "Read" "Glob" "Grep" "Skill" "Task"
   ```

   Each planning agent returns its plan + gap list as text; it changes nothing.

2. **Report the consolidated plan + gaps to the user**, per ticket. A ticket with zero gaps is a
   candidate to dispatch; a ticket with gaps is **held** until they are resolved.

3. **Resolve every gap interactively with the user.** For each raised item, take exactly one path:
   - **Clarification** the user answers verbally → record it as *Resolved context* to inject into the
     dispatch prompt (§5). No doc edit needed.
   - **Requirement / contract / passed-stage gap** (needs a doc or contract change) → do **not** edit
     passed-stage docs or frozen contracts here. Route it through `/requirement` (or `/bug`). Keep the
     ticket **held**; it becomes eligible in a later wave once the change lands.
   - **Acceptance-criteria edit** → allowed **only with the user's explicit consent**, and only to the
     ticket file, made now before dispatch. Never edit acceptance criteria silently, without consent,
     or inside a headless session. After editing, re-confirm the ticket is still eligible.

4. **Classify the outcome** and report it:
   - **Validated-clean** = no open gaps (any clarifications captured as Resolved context; any
     consented acceptance-criteria edits applied). → proceeds to §4.
   - **Held** = an unresolved gap, a pending `/requirement`, or user chose to defer. → not dispatched;
     report why.

   Only **validated-clean** tickets continue to §4. If none survive, report the held reasons and stop.

## 4. Prepare a worktree per validated-clean ticket (create-if-missing, never clobber)

Using `git worktree list` to check state, for each `TKT-###`:

- **Missing:** `git worktree add .claude/worktrees/tkt-### -b tkt-###` (off current `main`). If the
  branch `tkt-###` already exists, use `git worktree add .claude/worktrees/tkt-### tkt-###`.
- **Exists:** reuse **only** if checked out on `tkt-###` and clean
  (`git -C .claude/worktrees/tkt-### status --porcelain` empty). If wrong branch or dirty, **skip
  that ticket** and report — never overwrite in-flight work.

Create the log dir once: `.dispatch/wave-<N>/`.

## 5. Launch — smoke-test first, then fan out

Only **validated-clean** tickets from §3 are launched. Per-ticket prompt (substitute `TKT-###`, the
worktree path, and the ticket's *Resolved context* gathered in §3 — omit that block if none):

```
/implement TKT-###

--- resolved context (decisions made with the user during pre-dispatch validation) ---
<verbatim clarifications the user gave for this ticket in §3; treat as authoritative. If empty,
the ticket had no open questions.>

--- dispatch addendum (headless session; no human can answer prompts) ---
- You run non-interactively in worktree <path> on branch tkt-###. If you would normally ask
  the user, make the safe, pipeline-compliant choice, or STOP. Do not re-open questions already
  answered in the resolved-context block above.
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

**Smoke test:** launch the **first** validated-clean ticket, wait ~15–20s, read its log. A
healthy start shows work (reading files / running the ticket), not an immediate crash
(unknown-command / bad-flag). If it crashed, **stop** and report the log — don't spawn a broken
wave. If healthy, launch the rest in parallel the same way.

## 6. Monitor, govern, report

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
  The **one** exception is a consented acceptance-criteria edit to a ticket file during §3
  validation — only with the user's explicit approval, never silently.
- Plan and validate every candidate with the user (§3) **before** any fan-out. Tickets with
  unresolved requirement gaps are **held**, not dispatched; route doc/contract gaps to `/requirement`.
- Contract-definition and contract-change tickets are **held for interactive `/implement`**, not
  auto-dispatched.
- Merging + cleanup stay manual (reminder above). If nothing is auto-dispatchable or a precondition
  fails, report clearly and change nothing.
