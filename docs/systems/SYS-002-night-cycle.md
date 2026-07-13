# SYS-002: Night Cycle

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-002 Cycle](../domains/DOM-002-cycle.md)

## Purpose

Owns the day/night cycle state machine: the prep → service → settlement phase sequence, explicit night start, run-to-next-night chaining, the fixed service duration, early close, and the phase-duration tuning constant.

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-13: does not compute settlement contents (SYS-004 does; this system only triggers the settlement phase); does not spawn or simulate guests (SYS-003 reacts to phase transitions); does not gate what the player may do in prep beyond signaling the phase (each system enforces its own prep-only rules).

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-003 | Discrete day/night cycles with start, cohort, settlement | Cycle skeleton |
| REQ-005 | Three phases in order: prep, service, settlement | Phase machine |
| REQ-006 | Service starts only on explicit player input (except REQ-007 mode) | |
| REQ-007 | Run-to-next-night: prep-skip chaining at normal speed, cancellable | No fast-forward exists |
| REQ-016 | Fixed service duration; expiry drains guests then settles | |
| REQ-017 | Early close: entries stop, night ends when guests leave | |
| REQ-091 | Duration is one global tuning constant, ~2–3 min | Config parameter |
| REQ-101 | Settlement report is player-dismissed; next prep only after dismissal | New 2026-07-13 (breakdown) |

## Interactions with other systems

`DECIDED (user)` 2026-07-13.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-001 Construction | out | Prep-phase signal gates build operations | — |
| SYS-003 Guest Simulation | both | Phase transitions start/stop arrivals; "all guests left" signal ends the night | — |
| SYS-004 Economy | both | Settlement phase trigger; prep gates stock purchase; settlement-complete resumes chain | — |
| SYS-005 Staffing | out | Prep-phase signal gates hiring/assignment (REQ-059) | — |

## System-specific detail

- **Settlement pacing (REQ-101):** settlement computes results (SYS-004), then presents the night report; the cycle holds until the player dismisses it.
- **Mid-service player actions:** early close (REQ-017) and active-ability use (REQ-080). Acclaim spending is prep-only (REQ-039 as amended 2026-07-13).
- **Run mode (REQ-007):** requirement stands as written; its edge semantics (report handling while chaining, early-close interaction, interrupts) are deferred per Q-048 and not needed for the single-venue prototype.

## Open questions

| ID | Question | Status |
|---|---|---|
| Q-048 | Run-mode edge semantics (report while chaining, early close, event/insolvency interrupts) | DEFERRED (user) → post-prototype |

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-13 | Settlement = dismissable report screen (REQ-101) | user |
| 2026-07-13 | Run-mode edge semantics deferred (Q-048) | user |
| 2026-07-13 | Boundary statement + interactions table confirmed | user |
