# CON-003: Structure API v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + domain events
> Provider: DOM-001 Structure
> Consumers: build UI adapter, structure render adapter, DOM-003 bridge (CON-006), DOM-005 bridge (CON-010), DOM-006 presence bridge (CON-012), DOM-007 feat router, app orchestrator, persistence adapter
> Conformance tests: `tests/contracts/structure/`

## Purpose

Commands and queries on the physical tavern: placement, demolish/move/upgrade, circulation, the traversal graph, room data, and structure events. Traces: REQ-001, REQ-066–075, REQ-097–100.

## Interface definition

```csharp
namespace TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

public abstract record PlacementError
{
    public sealed record WrongPhase() : PlacementError;                      // not Prep
    public sealed record OutOfLot() : PlacementError;
    public sealed record Overlap() : PlacementError;
    public sealed record Unsupported() : PlacementError;                     // REQ-067
    public sealed record Disconnected() : PlacementError;                    // REQ-068 (placement-time)
    public sealed record FootprintOutOfRange(int MinArea, int MaxArea) : PlacementError;
    public sealed record UnknownRoomType() : PlacementError;
    public sealed record RoomTypeLocked() : PlacementError;                  // not unlocked (REQ-035 gate)
    public sealed record TerrainRequired(RoomTypeId Type) : PlacementError;  // REQ-083(a)
    public sealed record NotOnExistingStructure() : PlacementError;          // move target (REQ-072)
    public sealed record MaxTierReached() : PlacementError;
    public sealed record InsufficientGold(Money Required, Money Available) : PlacementError;
    public sealed record UnknownRoom() : PlacementError;
    public sealed record CellNotEmpty() : PlacementError;                    // circulation build
    public sealed record NothingAtCell() : PlacementError;                   // circulation demolish
    private PlacementError() { }
}

public enum CirculationKind { Corridor, Stair }

public interface IStructureCommands
{
    Outcome<PlacementError> PlaceRoom(RoomTypeId type, GridRect footprint);
    Outcome<PlacementError> DemolishRoom(RoomId room);                       // REQ-070/073/098
    Outcome<PlacementError> MoveRoom(RoomId room, GridRect newFootprint);    // REQ-072
    Outcome<PlacementError> UpgradeRoom(RoomId room);                        // REQ-071/100, +1 tier
    Outcome<PlacementError> BuildCirculation(CirculationKind kind, CellCoord cell);   // REQ-074/099
    Outcome<PlacementError> DemolishCirculation(CellCoord cell);
    IReadOnlyList<IDomainEvent> ResetAll();                                  // prestige (REQ-037)
}

public interface IStructureQueries
{
    TraversalGraph Graph { get; }                    // immutable, versioned
    CellCoord Entrance { get; }                      // REQ-075
    IReadOnlyList<RoomInfo> Rooms { get; }
    RoomInfo GetRoom(RoomId id);                     // throws KeyNotFoundException if absent
    int TotalGuestCapacity { get; }                  // sum over ACTIVE rooms (REQ-008 input)
    Money NightlyUpkeepBill { get; }                 // sum over ALL built rooms
    StructureMetrics Metrics { get; }                // feat inputs (REQ-032/036)
    IReadOnlyList<RoomTypeSheet> AvailableRoomTypes { get; }   // unlock-filtered (CON-004)
}

public sealed record RoomInfo(
    RoomId Id,
    RoomTypeId Type,
    int Tier,                       // 1-based
    GridRect Footprint,
    bool Active,                    // REQ-098
    int Capacity,                   // floor(Area × sheet.CapacityPerCell), tier-modified
    double EfficiencyFactor,        // REQ-069 curve, (0, 1]
    Money PaidTotal,                // everything charged for this room (build + upgrades), for refunds
    Money NightlyUpkeep,
    IReadOnlyList<TraitId> Traits,
    bool Broadcaster,
    IReadOnlyList<ServiceOffering> Services,
    StaffRequirements Staffing);

public sealed record ServiceOffering(
    string ServiceId,               // unique within room type, e.g. "drink", "lodging", "spa-entry"
    ServiceKind Kind,
    int BaseDurationTicks,          // REQ-104
    Money? EntryFee);               // REQ-013 rooms; null otherwise

public enum ServiceKind { MenuConsumption, Lodging, RoomEntry, EmployeeService }

public sealed record StaffRequirements(IReadOnlyList<RoleRequirement> Roles);
public sealed record RoleRequirement(RoleId Role, int Min, int Max);   // REQ-057; Max ≥ Min ≥ 0, Max ≥ 1

public sealed record StructureMetrics(
    int MaxHeightCells,             // highest built cell Y + 1
    int RoomCount,
    IReadOnlyDictionary<RoomTypeId, int> RoomCountsByType,
    int CirculationCellCount);

public sealed record TraversalGraph(
    int Version,                                        // bumps on every structural change
    IReadOnlySet<CellCoord> WalkableCells,              // room + circulation + exterior ground (REQ-097)
    IReadOnlyDictionary<CellCoord, RoomId> RoomAtCell,  // room cells only
    IReadOnlySet<CellCoord> StairCells)
{
    /// Edges: horizontal neighbors both walkable; vertical (y↔y+1) only if BOTH are stairs.
    public IEnumerable<CellCoord> Neighbors(CellCoord c) { /* per rule above */ }
}

public interface IStructureSnapshot
{
    StructureSnapshot Capture();
    void Restore(StructureSnapshot snapshot);
}
public sealed record StructureSnapshot(int SchemaVersion /*1*/, string JsonPayload); // payload schema in CON-017

// ── Events ──────────────────────────────────────────────────
public sealed record RoomPlaced(RoomId Room, RoomTypeId Type, GridRect Footprint) : IDomainEvent;
public sealed record RoomDemolished(RoomId Room, Money Refunded) : IDomainEvent;       // → Staffing REQ-109, Economy
public sealed record RoomMoved(RoomId Room, GridRect From, GridRect To) : IDomainEvent; // → Staffing REQ-109
public sealed record RoomUpgraded(RoomId Room, int NewTier) : IDomainEvent;
public sealed record CirculationBuilt(CirculationKind Kind, CellCoord Cell) : IDomainEvent;
public sealed record CirculationDemolished(CellCoord Cell, Money Refunded) : IDomainEvent;
public sealed record RoomDeactivated(RoomId Room) : IDomainEvent;                       // REQ-098 → Staffing/Guests
public sealed record RoomReactivated(RoomId Room) : IDomainEvent;
public sealed record StructureChanged(int GraphVersion) : IDomainEvent;                 // last event of any mutation
public sealed record StructureReset() : IDomainEvent;
```

## Semantics

- **Phase gate:** all commands except `ResetAll` are Prep-only (REQ-005, SYS-001); checked against injected `ICycleQueries` → `WrongPhase`.
- **Placement validation order** (first failure wins): WrongPhase → UnknownRoomType → RoomTypeLocked → FootprintOutOfRange → OutOfLot → Overlap → TerrainRequired → Unsupported → Disconnected → InsufficientGold. Charging (via CON-004 `IBuildLedger`) happens last; a failed charge leaves the grid untouched.
- **Costs & refunds:** `PlaceRoom` charges `sheet.BuildCost` (ledger applies venue multiplier; the *post-multiplier* charged amount accumulates into `PaidTotal`). `UpgradeRoom` charges the tier's cost the same way. Demolish refunds `PaidTotal` (REQ-073/100); circulation refunds its per-cell paid cost (REQ-099). Moves are free.
- **REQ-098:** demolish/move always succeed structurally; rooms losing support/connectivity emit `RoomDeactivated` (not destroyed); any mutation that restores them emits `RoomReactivated`. Inactive rooms keep upkeep accruing and stay in `Rooms`.
- **Graph:** recomputed on mutation; `Version` strictly increases; `StructureChanged` is always the final event of a successful mutation's list. Exterior ground cells = all `y == 0` lot cells not covered by a room/circulation cell (REQ-097; built ground cells are walkable as their built kind).
- **Efficiency (REQ-069, resolves DOM001-Q1):** `EfficiencyFactor = Area <= OptimumArea ? 1.0 : Math.Max(sheet.MinEfficiency, 1.0 - sheet.EfficiencyFalloffPerCell * (Area - OptimumArea))`. Applied by consumers to service speed, not capacity.
- **Capacity:** `Capacity = (int)Math.Floor(Footprint.Area * CapacityPerCell(tier))`; inactive rooms contribute 0 to `TotalGuestCapacity`.
- **ResetAll** (prestige) clears rooms/circulation, emits `StructureReset` + `StructureChanged`; no refunds (REQ-037 handles gold via CON-007 `ResetGold`).
- Queries are cheap, non-allocating where possible, and reflect the last completed command. Single-threaded per CON-016.

## Conformance tests

`tests/contracts/structure/`:

- Placement matrix: each `PlacementError` variant provoked exactly; validation order asserted (e.g. out-of-lot + unaffordable reports `OutOfLot`).
- Support/connectivity: floating room rejected; elevated room reachable only via stairs accepted; REQ-097 ground-gap connectivity accepted.
- Graph edges: horizontal room↔room, room↔corridor, room↔exterior-ground; vertical only stair↔stair; version bump on every mutation.
- REQ-098 flow: demolish supporting room → dependent emits `RoomDeactivated`, capacity excludes it; rebuild support → `RoomReactivated`.
- Refund equality: place(+upgrade×2) then demolish refunds exactly `PaidTotal` incl. multiplied amounts; circulation full refund; move refunds nothing.
- Efficiency formula table across optimum/falloff/min values; capacity floor behavior.
- Event ordering: every successful mutation ends with `StructureChanged`; failures emit nothing.
- Prep-gating: all mutating commands fail with `WrongPhase` in Service/Settlement.
- Snapshot round-trip: capture → restore → identical `Rooms`, `Graph.Version` semantics preserved.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
