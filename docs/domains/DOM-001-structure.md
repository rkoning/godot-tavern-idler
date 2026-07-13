# DOM-001: Structure

> Status: APPROVED (Gate 3 PASSED 2026-07-13)
> Parents: [PDD](../design/PDD.md), [SYS-001 Construction](../systems/SYS-001-construction.md)
> Contracts: [CON-003](../contracts/CON-003-structure-api.md), [CON-004](../contracts/CON-004-structure-driven-ports.md)
> Tickets: — (added in stage 5)

## Bounded context

Models the physical tavern: the build grid, rooms, circulation, and the rules that make a layout legal (support, connectivity) or partially inactive. This domain is the authority on *what exists where* and *what is traversable/reachable*; it knows nothing about the guests walking the graph or the money paying for it.

Ubiquitous language:

- **Grid / Cell** — the venue lot rectangle of buildable cells (dimensions supplied by the current venue).
- **Room** — a placed rectangle of contiguous cells with a `RoomType` and `Tier`.
- **Room sheet (`RoomType`)** — footprint range, guest capacity, build cost, upkeep, services offered, staffing requirements (roles, min–max, maxima per tier), trait list, broadcaster flag, per-service base durations (REQ-066/104).
- **Circulation** — corridor (horizontal) and stair (vertical) cells; per-cell cost; traversable and supporting (REQ-074/099).
- **Support** — every built cell rests on ground or built cells directly beneath (REQ-067).
- **Connectivity** — reachability from the entrance over the traversal graph (REQ-068).
- **Traversal graph** — room cells + circulation cells + ground-level exterior cells (REQ-097); the single graph used for validation here and pathfinding in Guests.
- **Inactive room** — a room whose support/connectivity was broken by a later operation; closed to guests until restored (REQ-098).
- **Entrance** — the single fixed ground-level cell (position venue-defined, REQ-075/084).
- **Terrain feature** — venue-defined cell enabling or modifying rooms whose footprint covers it (REQ-083).

Boundary: does not pathfind guests (DOM-003 walks the graph), hold gold (build charges/refunds go through a driven ledger port to DOM-004), assign employees (DOM-005 fills the slots this domain's sheets define), evaluate trait rules (DOM-006 reads trait lists/broadcaster flags), or define venues (DOM-007 supplies lot constraints).

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|
| REQ-001 | SYS-001 | Grid model; room placement as contiguous-cell rectangles |
| REQ-066 | SYS-001 | Owns the `RoomType` sheet schema (incl. REQ-104 base durations field) |
| REQ-067 | SYS-001 | Support validation on place/move |
| REQ-068 | SYS-001 | Connectivity validation over the traversal graph |
| REQ-069 | SYS-001 | Size-range placement; efficiency curve past optimum size on the sheet |
| REQ-070 | SYS-001 | `DemolishRoom` command |
| REQ-071 | SYS-001 | `UpgradeRoom` command; tier specs on the sheet |
| REQ-072 | SYS-001 | Move/swap restricted to cells already in the built structure |
| REQ-073 | SYS-001 | Demolish computes full refund, posted via the ledger port |
| REQ-074 | SYS-001 | Circulation cells in the traversal graph; not sole connectivity carrier |
| REQ-075 | SYS-001 | Single entrance cell; exposed to Guests for admission/queue position |
| REQ-097 | SYS-001 | Ground-level exterior cells in the traversal graph |
| REQ-098 | SYS-001 | Breaking ops permitted; affected rooms flip inactive; auto-reactivation on restore |
| REQ-099 | SYS-001 | Circulation per-cell cost, full refund, counts as support |
| REQ-100 | SYS-001 | In-place upgrades; refund = base + upgrade spend |

## Domain model

Pure C# — no engine types.

- **Aggregate: `Tavern`** — root; owns the grid, all `Room` entities, circulation cells, and the derived traversal graph + active/inactive states. All mutations validate support/connectivity and recompute affected derived state.
- **Entities:** `Room` (id, type, tier, footprint, active flag).
- **Value objects:** `CellCoord`, `GridRect`, `RoomTypeSheet`, `TierSpec`, `TraversalGraph` (immutable snapshot handed to consumers), `PlacementError`.
- **Domain events:** `RoomPlaced`, `RoomDemolished`, `RoomMoved`, `RoomUpgraded`, `CirculationBuilt`, `CirculationDemolished`, `RoomDeactivated`, `RoomReactivated`, `StructureChanged` (graph version bump).

`RoomDemolished`/`RoomMoved` carry the room id so Staffing can orphan assignees (REQ-109) and Progression can evaluate build feats.

## Architecture decisions

Global decisions (orchestration, time, save, presentation, Steamworks, shared kernel) are recorded in [DOM-002](DOM-002-cycle.md) — user-chosen 2026-07-13.

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| Graph exposure | Immutable versioned graph snapshot vs live queries per cell | Immutable snapshot (`TraversalGraph` + version) | Guests path over a stable structure per tick; recompute only on `StructureChanged` | follows Decision A/D (user 2026-07-13); shape refined at contracts |

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| `StructureCommandsPort` | driving | Place/demolish/move/upgrade rooms, build/demolish circulation; prep-gated (checks `CycleQueriesPort` state supplied by caller); returns events or `PlacementError` | CON-003 |
| `StructureQueriesPort` | driving | Traversal graph snapshot, entrance cell, rooms + sheets (capacity, services, base durations, staffing requirements/maxima, traits, broadcaster, upkeep), active states, structure metrics for feats (e.g., height) | CON-003 |
| `StructureSnapshotPort` | driving | Serialize/restore built state | CON-003 |
| `BuildLedgerPort` | driven | Charge build/upgrade cost, post demolish refund (implemented over DOM-004; venue multipliers applied on the Economy side per REQ-087) | CON-004 |
| `LotConstraintPort` | driven | Current venue's lot rectangle, terrain feature cells, entrance position (implemented over DOM-007) | CON-004 |
| `RoomContentPort` | driven | Room type catalog (content data incl. unlock filtering input from Progression) | CON-004 |

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| Build UI adapter | calls `StructureCommandsPort` | Godot input/UI (placement, demolish, move, upgrade) | TKT-### (stage 5) |
| Structure render adapter | reads `StructureQueriesPort` | Godot TileMap/Node2D (pull view-model, Decision D) | TKT-### (stage 5) |
| Economy bridge | `BuildLedgerPort` | in-process call into DOM-004 driving port | TKT-### (stage 5) |
| Venue bridge | `LotConstraintPort` | in-process call into DOM-007 queries | TKT-### (stage 5) |
| Room content adapter | `RoomContentPort` | data files (JSON/Godot resources converted outside the domain) | TKT-### (stage 5) |

## Source layout

```
src/domains/structure/       pure domain code + ports
src/adapters/structure/      adapter implementations
tests/domains/structure/     unit tests
tests/contracts/structure/   contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
| DOM001-Q1 | Efficiency-past-optimum curve shape (REQ-069) | RESOLVED 2026-07-13 → linear falloff with floor; formula in CON-003 semantics, params (`optimumArea`, `efficiencyFalloffPerCell`, `minEfficiency`) on the CON-004 room sheet |
