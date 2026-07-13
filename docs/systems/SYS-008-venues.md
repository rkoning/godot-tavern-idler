# SYS-008: Venues

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-007 Progression](../domains/DOM-007-progression.md) (Venues merged into Progression — user, 2026-07-13)

## Purpose

Owns venue definitions and their application to a run: the venue sheet (lot dimensions, entrance position, terrain features, guest-pool modifiers, exclusive content, economy multipliers, venue milestones), the fixed starter venue, and venue-locked-per-run semantics.

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-13: does not implement building (SYS-001 builds within the lot this system defines); does not draw guests (SYS-003 applies this system's pool modifiers); does not own the prestige venue-choice flow (SYS-007) or milestone detection — it defines which milestones are venue-bound. Launch venue roster is deferred content design (Q-047); first build milestone is a single-venue prototype on the starter venue only.

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-081 | Venue definition sheet (lot, entrance, terrain, pool modifiers, exclusives, multipliers, milestones) | Owns the schema |
| REQ-082 | Full-rectangle lot, venue-specific width and max height | Constraint consumed by SYS-001 |
| REQ-083 | Terrain feature cells: enable room types or modify covering rooms | |
| REQ-084 | Entrance at venue-defined ground cell | |
| REQ-085 | Guest-pool weight multipliers + full exclusions | Applied to REQ-024 draw in SYS-003 |
| REQ-086 | Venue-exclusive guest types, rooms, menu items, VIPs | |
| REQ-087 | Economy multipliers on build costs and restock costs only | Applied in SYS-004 |
| REQ-088 | Every venue has ≥1 venue-only milestone | Detection in SYS-007 |
| REQ-089 | Fixed starter venue for every fresh save | Prototype scope: starter venue only |
| REQ-090 | Venue fixed per run; switch only via prestige choice | |

## Interactions with other systems

`DECIDED (user)` 2026-07-13.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-001 Construction | out | Lot rectangle, terrain features, entrance position | — |
| SYS-003 Guest Simulation | out | Pool multipliers, exclusions, exclusive guest types/VIPs | — |
| SYS-004 Economy | out | Build/restock cost multipliers; exclusive menu items | — |
| SYS-007 Acclaim & Prestige | both | Venue unlocks + prestige choice in; venue-milestone definitions out | — |

## System-specific detail

_To be filled during mini-ideation._

## Open questions

| ID | Question | Status |
|---|---|---|

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-13 |