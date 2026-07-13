# SYS-003: Guest Simulation

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-003 Guests](../domains/DOM-003-guests.md)

## Purpose

Owns guests as simulated agents: guest type definitions, the nightly attraction draw, arrival/admission against capacity, the outside queue, agenda execution and movement, wallets, satisfaction, crowding response, VIP visit logic, and departure.

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-13: does not define rooms or connectivity (walks the graph SYS-001 provides); does not price or record transactions (requests them from SYS-004, which owns gold); does not evaluate trait rules (SYS-006 applies effects onto guests); does not decide phase timing (reacts to SYS-002); does not own milestone detection (SYS-007 observes guest feats).

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-002 | Guests are visible simulated agents | |
| REQ-008 | Concurrent-guest cap from room count/capacity | Reads room data from SYS-001 |
| REQ-009 | Per-type crowding preference modifies satisfaction and payment | |
| REQ-010 | Off-screen queue with "+N waiting" label | |
| REQ-018 | Queued guests enter as capacity frees; per-guest patience | |
| REQ-023 | Satisfaction modifies transaction gold only | Applied when SYS-004 prices a transaction |
| REQ-024 | Weighted random arrival pool from tavern composition + lifetime Acclaim | Venue modifiers from SYS-008 |
| REQ-048 | Agenda-driven guests; ordered wants-list; leave when done/blocked | |
| REQ-049 | Anonymous ordinary types; unique named VIPs | |
| REQ-050 | VIP visit conditions + per-night chance | Conditions read build/menu/venue/Acclaim state |
| REQ-051 | Finite type-dependent wallet | |
| REQ-052 | Consolidated guest-type definition sheet | Owns the schema |
| REQ-053 | Blocked agenda item: wait (patience-bounded), penalty, skip | |
| REQ-054 | Zero wallet → leave immediately | |
| REQ-055 | Unsatisfied VIP may revisit; milestone not consumed | |
| REQ-092 | Patience values 10–30% of service duration | Tuning band |
| REQ-093 | Launch scope ~10 ordinary types + ~5 VIPs | Content scope marker |
| REQ-102 | Attraction-driven arrival trickle; no fixed cohort size | New 2026-07-13 (breakdown) |
| REQ-103 | Crowding reacts to per-room occupancy | New 2026-07-13 (breakdown) |
| REQ-104 | Per-service base durations with open modifier system | New 2026-07-13 (breakdown); base field on SYS-001 room sheet |
| REQ-107 | Lodgers persist through settlement/prep; leave at next service start | New 2026-07-13 (breakdown); payment side is REQ-012 (SYS-004) |

## Interactions with other systems

`DECIDED (user)` 2026-07-13.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-001 Construction | in | Capacity, connectivity/pathing graph, entrance position, room services | — |
| SYS-002 Night Cycle | both | Phase start/stop; reports "all guests gone" for night end | — |
| SYS-004 Economy | both | Requests transactions (menu, lodging, fees, services); wallet debits; satisfaction → payment modifier | — |
| SYS-005 Staffing | in | Staffed/degraded/closed room states affect agenda fulfillment and waits | — |
| SYS-006 Traits & Synergy | both | Supplies guest traits and room presence; receives satisfaction/behavior/spending effects | — |
| SYS-007 Acclaim & Prestige | both | Lifetime Acclaim feeds attraction weights and VIP conditions; guest feats observed for milestones | — |
| SYS-008 Venues | in | Guest-pool weight multipliers, exclusions, venue-exclusive types | — |

## System-specific detail

- **Arrival model (REQ-024 + REQ-102):** one attraction function (composition + lifetime Acclaim + venue modifiers) drives both the type-weighted draw and the arrival rate; guests trickle in for the whole service phase and overflow into the queue.
- **Crowding (REQ-103):** evaluated against the guest's current room, not the tavern; satisfaction and payment effects (REQ-009) both use the room-local value.
- **Occupancy timing (REQ-104):** a guest fulfilling a want occupies the room for the service's base duration × modifiers (staffing speed REQ-061, traits, perks). This duration is what capacity pressure and queue drain fall out of.

## Open questions

| ID | Question | Status |
|---|---|---|

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-13 | Attraction-driven trickle (REQ-102) | user |
| 2026-07-13 | Per-room crowding (REQ-103) | user |
| 2026-07-13 | Per-service durations + modifiers (REQ-104) | user |
| 2026-07-13 | Lodgers persist to next service start (REQ-107) | user |
| 2026-07-13 | Boundary statement + interactions table confirmed | user |
