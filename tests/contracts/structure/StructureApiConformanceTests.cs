using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;
using static TavernIdler.Tests.Contracts.Structure.Sheets;

namespace TavernIdler.Tests.Contracts.Structure;

/// <summary>
/// CON-003 Structure API abstract conformance suite. Covers every bullet of the contract's
/// "Conformance tests" section: placement matrix + validation order, support/connectivity,
/// graph edges + version bump, REQ-098 (de)activation, refund equality, efficiency/capacity,
/// event ordering, prep-gating, snapshot round-trip, and ResetAll.
///
/// This class is ABSTRACT — xUnit never instantiates it, so nothing here runs until the
/// Structure domain ticket (TKT-011) supplies a concrete subclass implementing
/// <see cref="CreateHarness"/>. TKT-003 only defines the suite; no domain behavior lives here.
/// </summary>
public abstract class StructureApiConformanceTests
{
    /// Build a harness over the real Tavern aggregate seeded from <paramref name="scenario"/>.
    /// The returned harness MUST start in the Prep phase.
    protected abstract IStructureTestHarness CreateHarness(StructureScenario scenario);

    // ── helpers ─────────────────────────────────────────────────
    private static RoomTypeId T(string id) => new(id);

    private static Outcome<PlacementError>.Success Ok(Outcome<PlacementError> o) =>
        Assert.IsType<Outcome<PlacementError>.Success>(o);

    private static TError Err<TError>(Outcome<PlacementError> o) where TError : PlacementError
    {
        var failure = Assert.IsType<Outcome<PlacementError>.Failure>(o);
        return Assert.IsType<TError>(failure.Error);
    }

    private static RoomId PlaceOk(IStructureTestHarness h, string type, GridRect fp) =>
        Ok(h.Commands.PlaceRoom(T(type), fp)).Events.OfType<RoomPlaced>().Single().Room;

    /// A room whose efficiency stays 1.0 everywhere (optimum ≥ any footprint) — the neutral
    /// building block for structural tests that don't care about the efficiency curve.
    private static RoomTypeSheet Plain(string id, long buildCost = 100, IReadOnlyList<TierSpec>? tiers = null) =>
        Sheet(id, minArea: 1, maxArea: 100, optimumArea: 100, capacityPerCell: 1.0,
              buildCost: buildCost, tiers: tiers);

    private IStructureTestHarness Harness(
        IReadOnlyList<RoomTypeSheet> catalog,
        IReadOnlyList<RoomTypeId>? available = null,
        long gold = 1_000_000,
        double multiplier = 1.0,
        IReadOnlyList<TerrainFeature>? terrain = null) =>
        CreateHarness(StructureScenario.Default(catalog, available, gold, multiplier, terrain: terrain));

    // ════════════════════════════════════════════════════════════
    //  Placement matrix — every PlacementError variant provoked
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void PlaceRoom_outside_Prep_is_WrongPhase()
    {
        var h = Harness(new[] { Plain("taproom") });
        h.SetPrep(false);
        Err<PlacementError.WrongPhase>(h.Commands.PlaceRoom(T("taproom"), new GridRect(0, 0, 2, 1)));
    }

    [Fact]
    public void PlaceRoom_type_not_in_full_catalog_is_UnknownRoomType()
    {
        var h = Harness(new[] { Plain("taproom") });
        Err<PlacementError.UnknownRoomType>(h.Commands.PlaceRoom(T("nope"), new GridRect(0, 0, 2, 1)));
    }

    // Per the TKT-003 clarification: a type present in the full catalog but absent from the
    // currently-available (unlocked) subset is Locked, distinct from Unknown.
    [Fact]
    public void PlaceRoom_known_but_not_available_is_RoomTypeLocked()
    {
        var h = Harness(
            catalog: new[] { Plain("taproom"), Plain("vault") },
            available: new[] { T("taproom") });   // vault known but locked
        Err<PlacementError.RoomTypeLocked>(h.Commands.PlaceRoom(T("vault"), new GridRect(0, 0, 2, 1)));
    }

    [Fact]
    public void PlaceRoom_footprint_area_out_of_range_is_FootprintOutOfRange()
    {
        var h = Harness(new[] { Sheet("hall", minArea: 4, maxArea: 9, optimumArea: 4, capacityPerCell: 1.0) });
        var err = Err<PlacementError.FootprintOutOfRange>(
            h.Commands.PlaceRoom(T("hall"), new GridRect(0, 0, 2, 1)));   // area 2 < min 4
        Assert.Equal(4, err.MinArea);
        Assert.Equal(9, err.MaxArea);
    }

    [Fact]
    public void PlaceRoom_beyond_lot_is_OutOfLot()
    {
        var h = Harness(new[] { Plain("taproom") });   // default lot 12×8
        Err<PlacementError.OutOfLot>(h.Commands.PlaceRoom(T("taproom"), new GridRect(11, 0, 3, 1)));
    }

    [Fact]
    public void PlaceRoom_overlapping_existing_is_Overlap()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 3, 1));
        Err<PlacementError.Overlap>(h.Commands.PlaceRoom(T("taproom"), new GridRect(1, 0, 3, 1)));
    }

    [Fact]
    public void PlaceRoom_requiring_absent_terrain_is_TerrainRequired()
    {
        // Spa requires a terrain feature enabling it; footprint covers no such cell.
        var h = Harness(new[] { Plain("taproom"), Sheet("spa", minArea: 1, maxArea: 100, optimumArea: 100,
            requiresTerrainFeature: T("spring")) });
        var err = Err<PlacementError.TerrainRequired>(
            h.Commands.PlaceRoom(T("spa"), new GridRect(0, 0, 2, 1)));
        Assert.Equal(T("spa"), err.Type);
    }

    [Fact]
    public void PlaceRoom_floating_is_Unsupported()
    {
        var h = Harness(new[] { Plain("taproom") });
        Err<PlacementError.Unsupported>(h.Commands.PlaceRoom(T("taproom"), new GridRect(0, 2, 2, 1)));
    }

    [Fact]
    public void PlaceRoom_supported_but_unreachable_is_Disconnected()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));               // ground floor, on entrance
        // Directly above, resting on the ground room (supported) but with no stair up (unreachable).
        Err<PlacementError.Disconnected>(h.Commands.PlaceRoom(T("taproom"), new GridRect(0, 1, 2, 1)));
    }

    [Fact]
    public void UpgradeRoom_at_max_tier_is_MaxTierReached()
    {
        var h = Harness(new[] { Plain("taproom") });                  // single tier ⇒ already max
        var id = PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        Err<PlacementError.MaxTierReached>(h.Commands.UpgradeRoom(id));
    }

    [Fact]
    public void PlaceRoom_when_unaffordable_is_InsufficientGold()
    {
        var h = Harness(new[] { Plain("taproom", buildCost: 500) }, gold: 100);
        var err = Err<PlacementError.InsufficientGold>(
            h.Commands.PlaceRoom(T("taproom"), new GridRect(0, 0, 2, 1)));
        Assert.Equal(new Money(500), err.Required);   // multiplier 1.0
        Assert.Equal(new Money(100), err.Available);
    }

    [Fact]
    public void Command_on_unknown_room_is_UnknownRoom()
    {
        var h = Harness(new[] { Plain("taproom") });
        var ghost = new RoomId(999);
        Err<PlacementError.UnknownRoom>(h.Commands.DemolishRoom(ghost));
        Err<PlacementError.UnknownRoom>(h.Commands.UpgradeRoom(ghost));
        Err<PlacementError.UnknownRoom>(h.Commands.MoveRoom(ghost, new GridRect(0, 0, 2, 1)));
    }

    [Fact]
    public void MoveRoom_onto_empty_space_is_NotOnExistingStructure()
    {
        var h = Harness(new[] { Plain("taproom") });
        var id = PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        Err<PlacementError.NotOnExistingStructure>(
            h.Commands.MoveRoom(id, new GridRect(6, 4, 2, 1)));       // floating, not on structure
    }

    [Fact]
    public void BuildCirculation_on_occupied_cell_is_CellNotEmpty()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        Err<PlacementError.CellNotEmpty>(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(0, 0)));
    }

    [Fact]
    public void DemolishCirculation_on_empty_cell_is_NothingAtCell()
    {
        var h = Harness(new[] { Plain("taproom") });
        Err<PlacementError.NothingAtCell>(h.Commands.DemolishCirculation(new CellCoord(3, 3)));
    }

    // Validation order: out-of-lot AND unaffordable ⇒ the earlier check (OutOfLot) wins.
    [Fact]
    public void Validation_order_reports_OutOfLot_before_InsufficientGold()
    {
        var h = Harness(new[] { Plain("taproom", buildCost: 10_000) }, gold: 0);
        Err<PlacementError.OutOfLot>(h.Commands.PlaceRoom(T("taproom"), new GridRect(11, 0, 5, 1)));
    }

    // ════════════════════════════════════════════════════════════
    //  Support & connectivity
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Elevated_room_reachable_via_stairs_is_accepted()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 3, 1));              // ground room, cells (0..2,0)
        Ok(h.Commands.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 0)));  // ground stair
        Ok(h.Commands.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 1)));  // upper stair
        // Elevated room rests on the ground room and abuts the upper stair ⇒ reachable.
        Ok(h.Commands.PlaceRoom(T("taproom"), new GridRect(1, 1, 2, 1)));             // cells (1,1),(2,1)
    }

    [Fact]
    public void Ground_rooms_connect_through_exterior_ground_gap_REQ097()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));              // touches entrance
        // Separated by exterior-ground cell (2,0); still connected via the walkable ground.
        Ok(h.Commands.PlaceRoom(T("taproom"), new GridRect(3, 0, 2, 1)));
    }

    // ════════════════════════════════════════════════════════════
    //  Graph edges & version bump
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Graph_has_horizontal_room_corridor_and_exterior_edges()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));              // room cells (0,0),(1,0)
        Ok(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(2, 0)));
        var g = h.Queries.Graph;
        // room ↔ room
        Assert.Contains(new CellCoord(1, 0), g.Neighbors(new CellCoord(0, 0)));
        // room ↔ corridor
        Assert.Contains(new CellCoord(2, 0), g.Neighbors(new CellCoord(1, 0)));
        // room ↔ exterior ground (3,0 is unbuilt ground, walkable per REQ-097)
        Assert.Contains(new CellCoord(3, 0), g.Neighbors(new CellCoord(2, 0)));
    }

    [Fact]
    public void Graph_vertical_edges_only_between_stairs()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        // Two stacked room cells (no stair): no vertical edge.
        PlaceOk(h, "taproom", new GridRect(0, 1, 2, 1));              // will be inactive/disconnected, still built
        Ok(h.Commands.BuildCirculation(CirculationKind.Stair, new CellCoord(2, 0)));
        Ok(h.Commands.BuildCirculation(CirculationKind.Stair, new CellCoord(2, 1)));
        var g = h.Queries.Graph;
        Assert.DoesNotContain(new CellCoord(0, 1), g.Neighbors(new CellCoord(0, 0)));   // room↔room vertical: none
        Assert.Contains(new CellCoord(2, 1), g.Neighbors(new CellCoord(2, 0)));         // stair↔stair vertical
    }

    [Fact]
    public void Graph_version_strictly_increases_on_every_mutation()
    {
        var h = Harness(new[] { Plain("taproom") });
        var v0 = h.Queries.Graph.Version;
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        var v1 = h.Queries.Graph.Version;
        Ok(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(2, 0)));
        var v2 = h.Queries.Graph.Version;
        Assert.True(v1 > v0);
        Assert.True(v2 > v1);
    }

    // ════════════════════════════════════════════════════════════
    //  REQ-098 — deactivation / reactivation
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Demolishing_support_deactivates_dependent_then_rebuild_reactivates()
    {
        var h = Harness(new[] { Plain("taproom") });
        var groundA = PlaceOk(h, "taproom", new GridRect(0, 0, 3, 1));
        Ok(h.Commands.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 0)));
        Ok(h.Commands.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 1)));
        var upper = PlaceOk(h, "taproom", new GridRect(1, 1, 2, 1));   // supported by groundA, reachable via stair
        Assert.True(h.Queries.GetRoom(upper).Active);
        var capacityWithUpper = h.Queries.TotalGuestCapacity;

        // Demolish the supporting ground room → upper loses support.
        var demo = Ok(h.Commands.DemolishRoom(groundA));
        Assert.Contains(demo.Events, e => e is RoomDeactivated rd && rd.Room == upper);
        Assert.False(h.Queries.GetRoom(upper).Active);
        Assert.Contains(h.Queries.Rooms, r => r.Id == upper);          // still present, just inactive
        Assert.True(h.Queries.TotalGuestCapacity < capacityWithUpper); // inactive excluded

        // Rebuild the support → upper reactivates.
        var rebuild = Ok(h.Commands.PlaceRoom(T("taproom"), new GridRect(0, 0, 3, 1)));
        Assert.Contains(rebuild.Events, e => e is RoomReactivated rr && rr.Room == upper);
        Assert.True(h.Queries.GetRoom(upper).Active);
    }

    // ════════════════════════════════════════════════════════════
    //  Refund equality
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Demolish_refunds_exactly_PaidTotal_including_multiplied_upgrades()
    {
        var tiers = new[] { Tier1(), Tier(200), Tier(300) };
        var h = Harness(new[] { Plain("taproom", buildCost: 100, tiers: tiers) }, gold: 100_000, multiplier: 2.0);
        var start = h.LedgerBalance;

        var id = PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));       // charged 200
        Ok(h.Commands.UpgradeRoom(id));                                // charged 400
        Ok(h.Commands.UpgradeRoom(id));                                // charged 600
        Assert.Equal(new Money(1200), h.Queries.GetRoom(id).PaidTotal);
        Assert.Equal(start - new Money(1200), h.LedgerBalance);

        var demo = Ok(h.Commands.DemolishRoom(id));
        var refunded = demo.Events.OfType<RoomDemolished>().Single().Refunded;
        Assert.Equal(new Money(1200), refunded);
        Assert.Equal(start, h.LedgerBalance);                           // fully restored
    }

    [Fact]
    public void Demolish_circulation_fully_refunds()
    {
        var h = Harness(new[] { Plain("taproom") }, gold: 100_000);
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        var beforeCirc = h.LedgerBalance;
        Ok(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(2, 0)));
        Assert.True(h.LedgerBalance <= beforeCirc);
        Ok(h.Commands.DemolishCirculation(new CellCoord(2, 0)));
        Assert.Equal(beforeCirc, h.LedgerBalance);
    }

    [Fact]
    public void Move_is_free()
    {
        var h = Harness(new[] { Plain("taproom") });
        var a = PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        PlaceOk(h, "taproom", new GridRect(2, 0, 2, 1));               // B occupies existing structure
        var balance = h.LedgerBalance;
        Ok(h.Commands.MoveRoom(a, new GridRect(2, 0, 2, 1)));          // swap onto existing structure (REQ-072)
        Assert.Equal(balance, h.LedgerBalance);                        // no charge, no refund
    }

    // ════════════════════════════════════════════════════════════
    //  Efficiency curve & capacity floor
    // ════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(2, 2, 1.0)]     // area 4 == optimum ⇒ 1.0
    [InlineData(3, 2, 0.8)]     // area 6, falloff 0.1 ⇒ 1 - 0.1*2 = 0.8
    [InlineData(3, 3, 0.5)]     // area 9 ⇒ 1 - 0.1*5 = 0.5 (== min)
    [InlineData(4, 3, 0.5)]     // area 12 ⇒ 1 - 0.1*8 = 0.2, floored to min 0.5
    public void Efficiency_curve_matches_formula(int width, int height, double expected)
    {
        var h = Harness(new[] { Sheet("hall", minArea: 1, maxArea: 100, optimumArea: 4,
            efficiencyFalloffPerCell: 0.1, minEfficiency: 0.5, capacityPerCell: 1.0) });
        var id = PlaceOk(h, "hall", new GridRect(0, 0, width, height));
        Assert.Equal(expected, h.Queries.GetRoom(id).EfficiencyFactor, precision: 6);
    }

    [Fact]
    public void Capacity_floors_area_times_capacity_per_cell()
    {
        var h = Harness(new[] { Sheet("den", minArea: 1, maxArea: 100, optimumArea: 100, capacityPerCell: 1.5) });
        var id = PlaceOk(h, "den", new GridRect(0, 0, 3, 1));           // area 3 × 1.5 = 4.5 ⇒ 4
        Assert.Equal(4, h.Queries.GetRoom(id).Capacity);
    }

    // ════════════════════════════════════════════════════════════
    //  Event ordering & failure atomicity
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Every_successful_mutation_ends_with_StructureChanged()
    {
        var h = Harness(new[] { Plain("taproom") });
        var place = Ok(h.Commands.PlaceRoom(T("taproom"), new GridRect(0, 0, 2, 1)));
        Assert.IsType<StructureChanged>(place.Events[^1]);
        var corridor = Ok(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(2, 0)));
        Assert.IsType<StructureChanged>(corridor.Events[^1]);
        var id = place.Events.OfType<RoomPlaced>().Single().Room;
        var demo = Ok(h.Commands.DemolishRoom(id));
        Assert.IsType<StructureChanged>(demo.Events[^1]);
    }

    [Fact]
    public void Failed_command_mutates_nothing()
    {
        var h = Harness(new[] { Plain("taproom") });
        PlaceOk(h, "taproom", new GridRect(0, 0, 3, 1));
        var version = h.Queries.Graph.Version;
        var roomCount = h.Queries.Rooms.Count;
        Err<PlacementError.Overlap>(h.Commands.PlaceRoom(T("taproom"), new GridRect(1, 0, 3, 1)));
        Assert.Equal(version, h.Queries.Graph.Version);                // no version bump
        Assert.Equal(roomCount, h.Queries.Rooms.Count);
    }

    // ════════════════════════════════════════════════════════════
    //  Prep gating
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void All_mutating_commands_require_Prep()
    {
        var h = Harness(new[] { Plain("taproom") });
        var id = PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        h.SetPrep(false);
        Err<PlacementError.WrongPhase>(h.Commands.PlaceRoom(T("taproom"), new GridRect(4, 0, 2, 1)));
        Err<PlacementError.WrongPhase>(h.Commands.DemolishRoom(id));
        Err<PlacementError.WrongPhase>(h.Commands.MoveRoom(id, new GridRect(4, 0, 2, 1)));
        Err<PlacementError.WrongPhase>(h.Commands.UpgradeRoom(id));
        Err<PlacementError.WrongPhase>(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(4, 0)));
        Err<PlacementError.WrongPhase>(h.Commands.DemolishCirculation(new CellCoord(4, 0)));
    }

    // ════════════════════════════════════════════════════════════
    //  Snapshot round-trip
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Snapshot_capture_then_restore_reproduces_rooms()
    {
        var h = Harness(new[] { Plain("taproom") });
        var kept = PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        Ok(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(2, 0)));
        var snapshot = h.Snapshot.Capture();
        var before = h.Queries.Rooms.Select(r => (r.Id, r.Type, r.Footprint, r.Active)).OrderBy(x => x.Id.Value).ToList();

        // Mutate away from the captured state…
        PlaceOk(h, "taproom", new GridRect(4, 0, 2, 1));
        Assert.Equal(2, h.Queries.Rooms.Count);

        // …then restore.
        h.Snapshot.Restore(snapshot);
        var after = h.Queries.Rooms.Select(r => (r.Id, r.Type, r.Footprint, r.Active)).OrderBy(x => x.Id.Value).ToList();
        Assert.Equal(before, after);
        Assert.Contains(h.Queries.Rooms, r => r.Id == kept);
        Assert.Contains(new CellCoord(2, 0), h.Queries.Graph.WalkableCells);   // corridor restored
    }

    // ════════════════════════════════════════════════════════════
    //  ResetAll (prestige)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void ResetAll_clears_structure_without_refunds()
    {
        var h = Harness(new[] { Plain("taproom") }, gold: 100_000);
        PlaceOk(h, "taproom", new GridRect(0, 0, 2, 1));
        Ok(h.Commands.BuildCirculation(CirculationKind.Corridor, new CellCoord(2, 0)));
        var balanceAfterBuild = h.LedgerBalance;

        var events = h.Commands.ResetAll();

        Assert.Empty(h.Queries.Rooms);
        Assert.Contains(events, e => e is StructureReset);
        Assert.IsType<StructureChanged>(events[^1]);
        Assert.Equal(balanceAfterBuild, h.LedgerBalance);              // no refunds on reset (REQ-037)
    }
}
