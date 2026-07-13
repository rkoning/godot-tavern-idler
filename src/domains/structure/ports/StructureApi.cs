namespace TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

// ── CON-003: Structure API v1.0 (FROZEN 2026-07-13) ─────────────────────────
// Port interfaces + value/error/event types for the physical tavern: placement,
// demolish/move/upgrade, circulation, the traversal graph, room data, and
// structure events. Traces: REQ-001, REQ-066–075, REQ-097–100.
// This file is the contract surface; no domain behavior lives here except the
// normative TraversalGraph.Neighbors edge rule.

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
    public IEnumerable<CellCoord> Neighbors(CellCoord c)
    {
        // Horizontal: source and neighbor both walkable.
        if (WalkableCells.Contains(c))
        {
            var left = new CellCoord(c.X - 1, c.Y);
            if (WalkableCells.Contains(left)) yield return left;
            var right = new CellCoord(c.X + 1, c.Y);
            if (WalkableCells.Contains(right)) yield return right;
        }

        // Vertical: source and neighbor both stairs.
        if (StairCells.Contains(c))
        {
            var down = new CellCoord(c.X, c.Y - 1);
            if (StairCells.Contains(down)) yield return down;
            var up = new CellCoord(c.X, c.Y + 1);
            if (StairCells.Contains(up)) yield return up;
        }
    }
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
