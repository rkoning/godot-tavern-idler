using System.Collections.Generic;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Structure;

/// <summary>
/// The seam the CON-003 abstract conformance suite drives. The Structure domain ticket
/// (TKT-011) provides a concrete <see cref="IStructureTestHarness"/> wrapping the real
/// <c>Tavern</c> aggregate wired to in-memory driven ports seeded from a <see cref="StructureScenario"/>.
///
/// Phase is abstracted as a bool (<see cref="SetPrep"/>) so the suite never references the
/// Cycle contract (CON-002); the harness maps it onto the injected <c>ICycleQueries</c> the
/// real aggregate consults.
/// </summary>
public interface IStructureTestHarness
{
    IStructureCommands Commands { get; }
    IStructureQueries Queries { get; }
    IStructureSnapshot Snapshot { get; }

    /// Gold currently held by the injected <see cref="IBuildLedger"/> (for charge/refund assertions).
    Money LedgerBalance { get; }

    /// Set the injected cycle phase: true = Prep (mutations allowed), false = not Prep.
    void SetPrep(bool isPrep);
}

/// <summary>
/// Fully specifies a tavern-under-test: the venue lot + entrance + terrain (the
/// <see cref="ILotConstraints"/> input), the room catalog split into the full known set and the
/// currently-available (unlocked) subset — see the TKT-003 RoomTypeLocked clarification — plus
/// the starting gold and venue build-cost multiplier for the ledger.
/// </summary>
public sealed record StructureScenario(
    GridRect Lot,
    CellCoord Entrance,
    IReadOnlyList<RoomTypeSheet> FullCatalog,
    IReadOnlyList<RoomTypeId>? Available,          // null = every FullCatalog type is available
    Money StartingGold,
    double BuildCostMultiplier,                    // REQ-087; 1.0 = no venue markup
    IReadOnlyList<TerrainFeature>? Terrain)        // null = no terrain features
{
    public static StructureScenario Default(
        IReadOnlyList<RoomTypeSheet> fullCatalog,
        IReadOnlyList<RoomTypeId>? available = null,
        long startingGold = 100_000,
        double buildCostMultiplier = 1.0,
        int lotWidth = 12,
        int lotHeight = 8,
        IReadOnlyList<TerrainFeature>? terrain = null) =>
        new(
            new GridRect(0, 0, lotWidth, lotHeight),
            new CellCoord(0, 0),
            fullCatalog,
            available,
            new Money(startingGold),
            buildCostMultiplier,
            terrain);
}

/// <summary>Canonical room-type sheets used across the structure conformance suites.</summary>
public static class Sheets
{
    public static TierSpec Tier1() =>
        new(Money.Zero, 1.0, 1.0, new List<RoleRequirement>());

    public static TierSpec Tier(long upgradeCost, double capMult = 1.0, double speedMult = 1.0) =>
        new(new Money(upgradeCost), capMult, speedMult, new List<RoleRequirement>());

    /// A flexible builder; unspecified fields take permissive defaults so a test only
    /// states the parameters it exercises.
    public static RoomTypeSheet Sheet(
        string id,
        int minArea = 1,
        int maxArea = 100,
        int optimumArea = 1,
        double efficiencyFalloffPerCell = 0.0,
        double minEfficiency = 1.0,
        double capacityPerCell = 1.0,
        long buildCost = 100,
        long nightlyUpkeep = 10,
        IReadOnlyList<TierSpec>? tiers = null,
        IReadOnlyList<ServiceOffering>? services = null,
        StaffRequirements? staffing = null,
        IReadOnlyList<TraitId>? traits = null,
        bool broadcaster = false,
        RoomTypeId? requiresTerrainFeature = null) =>
        new(
            new RoomTypeId(id),
            id,
            minArea, maxArea, optimumArea,
            efficiencyFalloffPerCell, minEfficiency, capacityPerCell,
            new Money(buildCost), new Money(nightlyUpkeep),
            tiers ?? new List<TierSpec> { Tier1() },
            services ?? new List<ServiceOffering>(),
            staffing ?? new StaffRequirements(new List<RoleRequirement>()),
            traits ?? new List<TraitId>(),
            broadcaster,
            requiresTerrainFeature);
}
