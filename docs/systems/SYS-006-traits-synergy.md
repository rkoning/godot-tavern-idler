# SYS-006: Traits & Synergy

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-006 Traits](../domains/DOM-006-traits.md)

## Purpose

Owns the trait model and rule engine: trait definitions on carriers (guest types, employees, rooms, menu items), trait×trait rules with their effect classes, activation scoping (same-room, tavern-wide, broadcasters), per-rule stacking, discovery and the persistent codex, and the launch rule-density target.

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-13: does not define carriers themselves (guest/employee/room/item sheets in SYS-003/005/001/004 each include a trait list field this system interprets); does not apply consequences directly — it emits effects that SYS-003 (satisfaction, behavior events) and SYS-004 (spending multipliers) execute; does not own milestone detection for synergy feats (SYS-007).

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-040 | Rules are trait×trait pairs; ≥1 participant must be a guest | Core rule shape |
| REQ-041 | Same-room by default; designated entities tavern-wide | |
| REQ-042 | Effect classes: satisfaction modifier, behavior event, spending multiplier | Executed by SYS-003/004 |
| REQ-043 | Traits always visible; rules hidden until observed; trait-level reveal | |
| REQ-044 | Discovered knowledge persists across prestiges | Save-scope: lifetime |
| REQ-045 | Per-rule: binary vs. participant-count scaling | |
| REQ-046 | Per-rule reach: same-room or tavern-wide | |
| REQ-047 | Broadcaster rooms make occupants' same-room effects tavern-wide | Flag on SYS-001 room sheet |
| REQ-094 | ~20–30 rules; 2–3 per ordinary guest type via shared traits | Content density target |
| REQ-095 | Traits carried by guests, employees, rooms, menu items; any count; shared | Trait registry |
| REQ-096 | Rule endpoints reference traits only, never carrier types | |
| REQ-110 | Timing by effect class: continuous modifiers; once-per-episode events | New 2026-07-13 (breakdown) |
| REQ-111 | Discovery = first activation, immediate and permanent | New 2026-07-13 (breakdown) |

## Interactions with other systems

`DECIDED (user)` 2026-07-13.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-001 Construction | in | Room trait lists, broadcaster flags, room occupancy topology | — |
| SYS-003 Guest Simulation | both | Guest traits + presence in; satisfaction/behavior-event effects out | — |
| SYS-004 Economy | both | Menu item traits in; spending multipliers out | — |
| SYS-005 Staffing | in | Employee traits + room presence | — |
| SYS-007 Acclaim & Prestige | out | Synergy-feat signals for milestones; codex persists through prestige | — |

## System-specific detail

- **Co-presence episode (REQ-110):** the span during which two carriers satisfy a rule's reach condition (same room, or tavern-wide for broadcast/tavern-wide rules). Modifiers are active states over the episode; a behavior event rolls once when the episode begins. Leaving and re-entering starts a new episode.
- **Discovery pipeline (REQ-111 + REQ-043/044):** first activation → permanent trait-level codex entry (lifetime save scope, survives prestige).

## Open questions

| ID | Question | Status |
|---|---|---|

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-13 | Timing by effect class (REQ-110) | user |
| 2026-07-13 | Discovery = first activation (REQ-111) | user |
| 2026-07-13 | Boundary statement + interactions table confirmed | user |
