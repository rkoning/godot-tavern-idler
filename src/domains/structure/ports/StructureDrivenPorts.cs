namespace TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

// ── CON-004: Structure Driven Ports v1.0 (FROZEN 2026-07-13) ────────────────
// What Structure needs from outside: charging/refunding gold, the venue lot, and
// the room-type catalog — plus the room-sheet data schema.
// Traces: REQ-066, REQ-073, REQ-082–084, REQ-087, REQ-099, REQ-100, REQ-104.

// ── Charging (implemented over CON-007) ─────────────────────
public enum BuildCostKind { Room, Upgrade, Circulation }

public abstract record ChargeResult
{
    public sealed record Charged(Money AmountCharged) : ChargeResult;   // post-multiplier
    public sealed record InsufficientGold(Money Required, Money Available) : ChargeResult;
    private ChargeResult() { }
}

public interface IBuildLedger
{
    /// Applies the venue build-cost multiplier (REQ-087) to baseCost, then debits.
    ChargeResult TryCharge(Money baseCost, BuildCostKind kind);
    /// Credits the ledger. amount ≥ 0.
    void Refund(Money amount);
}

// ── Venue lot (implemented over CON-013 IVenueData) ─────────
public interface ILotConstraints
{
    GridRect Lot { get; }                              // REQ-082: full rectangle, origin (0,0)
    CellCoord Entrance { get; }                        // REQ-084: Lot ground cell (Y == 0)
    IReadOnlyList<TerrainFeature> Terrain { get; }     // REQ-083
}

public sealed record TerrainFeature(CellCoord Cell, TerrainEffect Effect);

public abstract record TerrainEffect
{
    public sealed record EnablesRoomType(RoomTypeId Room) : TerrainEffect;   // REQ-083(a)
    public sealed record ModifiesRoom(RoomStatModifier Modifier) : TerrainEffect; // REQ-083(b)
    private TerrainEffect() { }
}

public sealed record RoomStatModifier(
    double CapacityMultiplier,      // ≥ 0, default 1.0
    double ServiceSpeedMultiplier,  // ≥ 0, default 1.0
    double UpkeepMultiplier);       // ≥ 0, default 1.0

// ── Room content (implemented by content adapter) ───────────
public interface IRoomContent
{
    /// Full catalog from content JSON, filtered to currently available types
    /// (base set + Progression unlocks + venue exclusives; adapter composes
    /// CON-013 unlock/venue state with the JSON catalog).
    IReadOnlyList<RoomTypeSheet> AvailableRoomTypes();
}

public sealed record RoomTypeSheet(
    RoomTypeId Id,
    string DisplayName,
    int MinArea, int MaxArea,                 // REQ-069 footprint range (cells)
    int OptimumArea,                          // efficiency curve (CON-003 formula)
    double EfficiencyFalloffPerCell,          // ≥ 0
    double MinEfficiency,                     // (0, 1]
    double CapacityPerCell,                   // ≥ 0
    Money BuildCost,                          // base, pre-multiplier
    Money NightlyUpkeep,
    IReadOnlyList<TierSpec> Tiers,            // index 0 = tier 1 (base); REQ-071
    IReadOnlyList<ServiceOffering> Services,  // CON-003 type; REQ-066/104
    StaffRequirements Staffing,               // CON-003 type; REQ-057
    IReadOnlyList<TraitId> Traits,            // REQ-095
    bool Broadcaster,                         // REQ-047
    RoomTypeId? RequiresTerrainFeature);      // non-null → REQ-083(a) placement rule

public sealed record TierSpec(
    Money UpgradeCost,                        // tier 1: Money.Zero
    double CapacityMultiplier,                // vs base, ≥ 1.0
    double ServiceSpeedMultiplier,            // ≥ 1.0
    IReadOnlyList<RoleRequirement> StaffingMaxOverrides); // empty = inherit
