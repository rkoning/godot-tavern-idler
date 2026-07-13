# SYS-005: Staffing

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-005 Staffing](../domains/DOM-005-staffing.md)

## Purpose

Owns employees: hiring, roles and wages, room assignment, staffing-requirement evaluation (closed vs. degraded rooms), assignment locking during service, ordinary vs. rare named hires, and the implicit staff cap.

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-13: does not define which roles a room needs (SYS-001 room sheets do; this system fills and evaluates them); does not pay wages (SYS-004 deducts at settlement and owns insolvency); does not price employee services (SYS-004 transaction catalog); does not evaluate employee trait rules (SYS-006); does not unlock named hires (SYS-007 perks do).

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-056 | Employee assigned to at most one room; unassigned = idle but paid | Amended 2026-07-13 per REQ-109 |
| REQ-057 | Room staffing requirements as roles with min–max counts | Schema in SYS-001 room sheet; evaluated here |
| REQ-058 | Zero-staffed required role → room closed; below-min but all present → degraded service | Room state consumed by SYS-003 |
| REQ-059 | Assignments changeable only in prep; locked during service | Phase from SYS-002 |
| REQ-060 | Ordinary employees interchangeable per role; rare unique named hires exist | |
| REQ-061 | Above-minimum staffing: speed + synergy participants, no capacity/satisfaction bonus | |
| REQ-062 | Any ordinary role hireable in any prep; no candidate scarcity | |
| REQ-063 | Named hires unlocked via perks, then hireable in prep | Perk ownership from SYS-007 |
| REQ-064 | Flat wage per role; fixed wage per named/special hire | Amounts billed by SYS-004 |
| REQ-065 | No separate staff cap; implicit via room staffing maxima | Unassigned pool (REQ-109) is uncapped |
| REQ-108 | Free dismissal in prep; no severance, wages stop | New 2026-07-13 (breakdown) |
| REQ-109 | Demolish/move orphans staff into paid unassigned pool | New 2026-07-13 (breakdown) |

## Interactions with other systems

`DECIDED (user)` 2026-07-13.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-001 Construction | in | Room staffing requirements and maxima (per tier) | — |
| SYS-002 Night Cycle | in | Prep-phase signal gates hiring/assignment | — |
| SYS-003 Guest Simulation | out | Room open/degraded/closed states; service speed effects | — |
| SYS-004 Economy | both | Wage bill; insolvency refusal state back | — |
| SYS-006 Traits & Synergy | out | Employee trait lists; staffed presence as synergy participants | — |
| SYS-007 Acclaim & Prestige | in | Perk-unlocked named/special employees; staff removal at prestige | — |

## System-specific detail

- **Employee lifecycle:** hire (prep, REQ-062/063) → assign (prep, REQ-059) → work (service) → paid (settlement, via SYS-004) → possibly unassigned (REQ-109) or dismissed (REQ-108). Unassigned employees draw wages, so idle staff is a deliberate cost, not a bug.
- **Room state evaluation (REQ-058):** computed at service start from assignments; closed/degraded states are published to SYS-003 for agenda blocking and wait penalties.

## Open questions

| ID | Question | Status |
|---|---|---|

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-13 | Free dismissal in prep (REQ-108) | user |
| 2026-07-13 | Unassigned pool on demolish; REQ-056 relaxed (REQ-109) | user |
| 2026-07-13 | Boundary statement + interactions table confirmed | user |
