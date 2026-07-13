# CON-006: Guests Driven Ports v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface
> Provider: DOM-003 Guests (interface owner); implementers: structure bridge (over CON-003), staffing bridge (over CON-009), economy bridge (over CON-007), attraction bridge (over CON-013/003/007)
> Consumers: DOM-003 domain code (caller); bridge tickets (implementers)
> Conformance tests: `tests/contracts/guests/driven/`

## Purpose

What the guest simulation needs from outside: walkable structure, room service states, transactions, and the attraction context. Traces: REQ-004, REQ-008, REQ-023–026, REQ-058, REQ-061, REQ-068, REQ-102, REQ-104.

## Interface definition

```csharp
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
```

## Semantics

- **`IStructureAccess`:** pure views over CON-003; `Graph` reference changes only when `TraversalGraph.Version` changes — DOM-003 may cache paths keyed on version. `ActiveRooms` excludes inactive (REQ-098) rooms.
- **`IRoomServiceState`:** values fixed from `EvaluateRoomStates` at service start (CON-009) except after mid-night deactivations; `State` for an unknown room throws `KeyNotFoundException` (bug). `Closed` rooms must also be treated as blocked regardless of speed value.
- **`ITransactions.Execute`:** synchronous, atomic. Pricing (CON-007): `price = sheetPrice.MultiplyRounded(SatisfactionModifier × SpendingMultiplier)`; result `Completed(Paid)` means the ledger was credited `Paid` at that instant (REQ-004) and any stock decremented; caller must debit the guest wallet by exactly `Paid`. `CannotAfford` and `SoldOut` charge nothing. Entrance fees (REQ-015): callers issue `EntranceFee` requests only when CON-007 queries report an active entrance fee.
- **`IAttractionContext.Current()`:** stable within a single tick; the bridge must not recompute mid-tick. `ArrivalRateFactor` comes from game config (tuning constant, playtest-set).
- All calls single-threaded from the Guests tick per CON-016; implementations must not call back into DOM-003 (re-entrancy ban).

## Conformance tests

`tests/contracts/guests/driven/` (run against each real bridge + reference stubs):

- Bridge equivalence: `IStructureAccess` values equal the underlying CON-003 queries for a scripted tavern; inactive rooms excluded.
- `ITransactions`: each result variant provoked via CON-007-backed bridge; `Completed.Paid` matches CON-007 pricing table incl. rounding; ledger delta equals `Paid`; `SoldOut`/`CannotAfford` leave ledger and stock unchanged.
- Spending multiplier and satisfaction modifier composition: request with 1.2 × 1.5 prices correctly vs sheet price.
- `IAttractionContext`: exclusions absent from `AvailableTypes`; multipliers pass through; composition summary matches scripted structure/menu/staffing state.
- Re-entrancy: bridges assert no callback into `IGuestSimCommands`.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
