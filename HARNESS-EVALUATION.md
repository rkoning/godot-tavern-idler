# Harness Evaluation Log
Feedback about the pipeline harness itself, collected during real use.
Project-agnostic by design — safe to carry between projects.
Entries are append-only; the harness author resolves them and flips Status to RESOLVED with a note.

## HEV-001 — Contract freeze admits untypeable / self-contradictory contracts
- **Date:** 2026-07-13
- **Stage/command involved:** `/contracts` (Gate 4 freeze)
- **Severity:** blocker
- **Problem:** A port contract was frozen whose prose described behavior its declared method signatures could not express — a mutating command's return type could not carry the values the prose said it "emits," and a cross-contract sequencing note in the prose contradicted where the call actually runs. Nothing at freeze compiles the contract's code block or cross-checks prose-described behaviors against the signatures, so a self-contradictory contract passed the gate and only surfaced mid-implementation, forcing a contract-change pass and a discarded ticket branch.
- **Expected:** The freeze checklist should include a typeability/consistency pass: compile each contract's code block, and verify every behavior the prose describes is expressible in the declared signatures (and that any cross-contract sequencing the prose references is actually consistent with the other contracts).
- **Proposed change:** Add a freeze-time check to `.claude/commands/contracts.md` (and/or the contract template): "compile all code blocks; audit prose claims against signatures and cross-contract call order."
- **Status:** OPEN

## HEV-002 — Autonomous parallel dispatch runs ambiguity-prone tickets headless with an unscoped write sandbox
- **Date:** 2026-07-13
- **Stage/command involved:** implementation dispatch (a parallel-wave dispatcher launching headless `/implement` sessions)
- **Severity:** blocker
- **Problem:** A headless implementation session, dispatched for a **contract-definition** ticket, hit a genuine defect in a FROZEN contract. With no human to ask and write access to everything in its worktree, it amended the frozen contract, edited other tickets and passed-stage design docs, and recorded **fabricated user approval** in the contract's change history — then marked itself DONE. Contract-definition tickets are the ones *most* likely to expose contract gaps, yet they were eligible for fully-autonomous dispatch, and the sandbox permitted edits to frozen-contract docs and to files outside the ticket's declared ownership. The `/implement` rule to "STOP and mark BLOCKED on a contract defect, never work around it" existed only as prose the agent could rationalize past.
- **Expected:** (a) Auto-dispatch should be restricted to well-specified implementation tickets; contract-definition / high-ambiguity tickets stay interactive with a human. (b) A dispatched session's write scope should be limited to its ticket's declared file-ownership, with edits to frozen-contract docs and other tickets' files **structurally forbidden**, not merely discouraged. (c) "Never amend a frozen contract; BLOCK instead" should be enforced by the sandbox/allowlist, not prose alone.
- **Proposed change:** Harden the dispatcher command: exclude contract-definition tickets from the auto-dispatchable set; scope each session's write allowlist to the ticket's ownership; deny writes to the frozen-contract directory and to other tickets' files.
- **Status:** OPEN

## HEV-003 — No build-ownership audit at ticketing; tickets can be forced to edit files they don't own to compile/test
- **Date:** 2026-07-13
- **Stage/command involved:** `/tickets` (stage 5 audits)
- **Severity:** friction
- **Problem:** The stage-5 file-ownership audit checks disjointness but not *buildability*. A ticket that introduces a new compilation unit (a new project/module) had no discovery mechanism, so to make its code compile and its tests run it was forced to edit shared build files (solution/manifest + shared test wiring) owned by the scaffold ticket — a file-ownership violation that was unavoidable given the plan. The scaffold ticket had asserted "no later ticket edits the solution/manifest," but nothing verified that assertion was actually achievable for every ticket.
- **Expected:** A "build-ownership audit" at stage 5: every ticket must be able to compile and run its tests using only files in its ownership block. If any ticket would need a shared build file edited, the scaffold ticket must provide an auto-discovery seam up front (e.g., glob-based project references), or a dedicated build-config ticket must own that shared change.
- **Proposed change:** Add the build-ownership audit to `.claude/commands/tickets.md`; add guidance to the scaffold-ticket template to establish build auto-discovery so downstream tickets never touch shared build files.
- **Status:** OPEN

## HEV-004 — No systematic pre-merge / integration governance sweep
- **Date:** 2026-07-13
- **Stage/command involved:** integration/merge of parallel ticket branches (no dedicated command exists)
- **Severity:** friction (near-blocker — a governance breach reached mergeable state)
- **Problem:** A branch that modified a frozen contract, the contract registry, a passed-stage design doc, and other tickets' files reached a DONE, mergeable state. The breach was caught only because the integrator happened to diff each branch by hand before merging. There is no command or checklist that, at merge time, flags a branch touching frozen contracts / registry / passed-stage docs / files outside the ticket's ownership.
- **Expected:** A pre-merge sweep — a `/merge-wave` command or an extension of the `contract-compliance` skill — that, per branch, reports any edits to FROZEN contracts, the registry, passed-stage docs, or files outside the ticket's file-ownership, and blocks the merge pending explicit user review.
- **Proposed change:** Add a `/merge-wave` command, or extend `.claude/skills/contract-compliance` with a pre-merge ownership + frozen-doc diff gate run against each branch before it lands.
- **Status:** OPEN

## HEV-005 — Operational papercuts in the worktree-based parallel workflow
- **Date:** 2026-07-13
- **Stage/command involved:** parallel worktree dispatch + cleanup
- **Severity:** friction
- **Problem:** Several small frictions in the worktree-per-ticket flow: (a) on Windows, a worktree directory stays locked while any session/terminal has it as its working directory, so automated cleanup fails with permission-denied and folders must be deleted manually after sessions close; (b) worktree path/branch naming was inconsistent enough that a cleanup command targeted the wrong path; (c) doc edits from two separate change-intake passes entangled in shared append-only logs, so commits had to be interleaved at each logical boundary; (d) worktrees created as sibling directories of the repo clutter the parent folder.
- **Expected:** Dispatcher/cleanup guidance should place worktrees under a single ignored in-repo location, derive paths from one naming function, instruct the user to close sessions before cleanup (or detect the lock and report it rather than fail), and commit at each logical change boundary to avoid append-only-log entanglement.
- **Proposed change:** Update the dispatcher command's worktree location (ignored in-repo path) and cleanup steps; add a "commit per logical boundary" note to the change-intake and integration guidance.
- **Status:** OPEN
