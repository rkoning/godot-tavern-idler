namespace TavernIdler.Domains.Guests;
using TavernIdler.Kernel;
using TavernIdler.Domains.Structure;   // TraversalGraph, ServiceOffering (CON-003)
using TavernIdler.Domains.Staffing;    // RoomStaffState (CON-009)

// ── Structure access (over CON-003 queries) ─────────────────
public interface IStructureAccess
{
    TraversalGraph Graph { get; }
    CellCoord Entrance { get; }
    int TotalGuestCapacity { get; }
    /// Active rooms only; includes services, capacity, efficiency.
    IReadOnlyList<GuestRoomInfo> ActiveRooms { get; }
}
public sealed record GuestRoomInfo(
    RoomId Id, GridRect Footprint, int Capacity, double EfficiencyFactor,
    IReadOnlyList<ServiceOffering> Services);

// ── Room service state (over CON-009 queries) ───────────────
public interface IRoomServiceState
{
    RoomStaffState State(RoomId room);   // Open | Degraded | Closed (REQ-058)
    /// REQ-061 staffing speed ≥ 0; Degraded rooms return < 1.0 per CON-009.
    double SpeedFactor(RoomId room);
}

// ── Transactions (over CON-007 commands) ────────────────────
public interface ITransactions
{
    TransactionResult Execute(TransactionRequest request);
}

public enum TransactionKind { MenuPurchase, Lodging, RoomEntryFee, EmployeeService, EntranceFee }

public sealed record TransactionRequest(
    GuestId Guest,
    TransactionKind Kind,
    MenuItemId? Item,                 // required iff MenuPurchase
    RoomId? Room,                     // required for Lodging/RoomEntryFee
    string? ServiceId,                // required for EmployeeService
    Money WalletAvailable,            // hard spend cap (REQ-051)
    double SatisfactionModifier,      // [0.5, 1.5] per CON-005
    double SpendingMultiplier);       // ≥ 0; product of active REQ-042(c) effects, default 1.0

public abstract record TransactionResult
{
    public sealed record Completed(Money Paid) : TransactionResult;   // ledger credited; guest debits wallet by Paid
    public sealed record SoldOut() : TransactionResult;               // REQ-026 → satisfaction penalty in DOM-003
    public sealed record NotOffered() : TransactionResult;            // no such item/service currently
    public sealed record CannotAfford() : TransactionResult;          // price > WalletAvailable; nothing charged
    private TransactionResult() { }
}

// ── Attraction context (bridge composes CON-013 + CON-003 + CON-007) ──
public interface IAttractionContext
{
    AttractionInputs Current();   // read at BeginService and each arrival tick
}

public sealed record AttractionInputs(
    long LifetimeAcclaim,                                          // REQ-024/050
    IReadOnlyList<GuestTypeSheetRef> AvailableTypes,               // base + venue exclusives − exclusions (REQ-085/086)
    IReadOnlyDictionary<GuestTypeId, double> VenueWeightMultipliers, // absent key = 1.0
    CompositionSummary Composition,
    double ArrivalRateFactor);                                     // config constant (REQ-102)

public sealed record GuestTypeSheetRef(GuestTypeId Id);            // sheet data itself loaded via content adapter

public sealed record CompositionSummary(
    IReadOnlyDictionary<RoomTypeId, int> ActiveRoomCounts,
    IReadOnlyList<MenuItemId> StockedItems,                        // stock > 0
    IReadOnlyDictionary<RoleId, int> StaffedRoleCounts);
