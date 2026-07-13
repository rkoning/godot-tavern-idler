# SYS-001: Construction

> Status: APPROVED (Gate 2 PASSED 2026-07-13)
> Parent: [PDD](../design/PDD.md)
> Children: [DOM-001 Structure](../domains/DOM-001-structure.md)

## Purpose

Owns the physical tavern structure: the 2D build grid, placement of rooms as rectangles, room type definitions (footprint, capacity, costs, tiers), structural support and connectivity rules, circulation (corridors/stairs), the entrance, and demolish/move/upgrade operations.

**Boundary (what it does NOT do)** — `DECIDED (user)` 2026-07-12: does not simulate guests or pathfind them (SYS-003 consumes connectivity data); does not own the gold ledger (build/demolish transactions post to SYS-004); does not assign or manage employees (SYS-005 assigns into staffing slots this system defines); does not define venue lots or terrain (SYS-008 supplies them as constraints); does not evaluate trait rules (SYS-006 reads room trait lists and broadcaster flags).

## Requirements owned

Copied by reference, not duplicated — the PDD row is canonical.

| REQ ID | Summary | Notes for this system |
|---|---|---|
| REQ-001 | Free 2D grid; rooms as rectangles on contiguous cells | Core placement model |
| REQ-066 | Room type definition sheet (footprint range, capacity, costs, services, staffing reqs, trait list, broadcaster flag) | Owns the schema; other systems read fields |
| REQ-067 | Structural support: no floating rooms | Placement validation |
| REQ-068 | Connectivity: reachable via traversable cells (rooms + circulation) | Amended 2026-07-12; validated here, walked by SYS-003 |
| REQ-069 | Variable room sizes; efficiency falls past optimum size | |
| REQ-070 | Rooms can be demolished | |
| REQ-071 | Room upgrade tiers | |
| REQ-072 | Move/swap rooms onto existing structure only | |
| REQ-073 | Demolish refunds full build cost | Refund posts to SYS-004 |
| REQ-074 | Corridors + stairs traversable like rooms; not sole connectivity carrier | Amended 2026-07-12 |
| REQ-075 | One fixed ground-level entrance; queue forms outside | Entrance position from SYS-008 |
| REQ-097 | Ground-level gaps traversable; gapped ground rooms connected | New 2026-07-12 (breakdown) |
| REQ-098 | Breaking demolish/move allowed; affected rooms inactive until restored | New 2026-07-12 (breakdown) |
| REQ-099 | Circulation cells: per-cell cost, full refund, provide support | New 2026-07-12 (breakdown) |
| REQ-100 | Upgrades in place, footprint unchanged; refund includes upgrade spend | New 2026-07-12 (breakdown) |

## Interactions with other systems

`DECIDED (user)` 2026-07-12.

| Other system | Direction | Nature of interaction | Contract (stage 4) |
|---|---|---|---|
| SYS-002 Night Cycle | in | Build/demolish/move/upgrade allowed only during prep (REQ-005) | — |
| SYS-003 Guest Simulation | out | Room capacity, connectivity graph, entrance location for movement/admission | — |
| SYS-004 Economy | both | Build/upgrade/demolish costs and refunds; per-night upkeep; venue cost multipliers applied | — |
| SYS-005 Staffing | out | Room staffing requirements (roles, min–max) and staffing maxima per tier | — |
| SYS-006 Traits & Synergy | out | Room trait lists and broadcaster flags | — |
| SYS-008 Venues | in | Lot dimensions, terrain feature cells, entrance position constrain placement | — |

## System-specific detail

- **Traversal model (REQ-068/074/097):** the walkable graph = room cells + circulation cells + ground-level exterior cells. Connectivity validation and SYS-003 pathfinding use the same graph. Circulation is an efficiency/layout choice, not a hard requirement, except to reach elevated rooms (stairs are the only vertical traversal).
- **Structural invariants are placement-time only (REQ-067 + REQ-098):** placement/move requires support and connectivity; later operations may invalidate other rooms, which flip to *inactive* (closed to guests, agenda-blocking per REQ-053) instead of being destroyed. Inactive rooms reactivate automatically when support/connectivity is restored.
- **Cost symmetry (REQ-073/099/100):** everything built refunds fully — rooms, circulation cells, upgrade tiers. There is no sunk structural cost; rebuilding layouts is free by design.

## Open questions

| ID | Question | Status |
|---|---|---|

## Decision log

| Date | Decision | Chosen by |
|---|---|---|
| 2026-07-12 | REQ assignment per approved 8-system partition | user |
| 2026-07-12 | Guests traverse rooms and circulation alike (REQ-068/074 conflict resolved) | user |
| 2026-07-12 | Ground-level exterior gaps traversable (REQ-097) | user |
| 2026-07-12 | Breaking demolish/move allowed → inactive rooms (REQ-098) | user |
| 2026-07-12 | Circulation: per-cell cost, full refund, supports (REQ-099) | user |
| 2026-07-12 | Upgrades in place; refund includes tiers (REQ-100) | user |
| 2026-07-12 | Boundary statement + interactions table confirmed | user |
