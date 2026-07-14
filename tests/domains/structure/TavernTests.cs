using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Cycle;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;
using static TavernIdler.Tests.Contracts.Structure.Sheets;

namespace TavernIdler.Tests.Domains.Structure;

/// <summary>
/// Unit tests for the <see cref="Tavern"/> aggregate — the behavior the CON-003 conformance suite
/// does not pin down: transitive support (REQ-067/099), circulation as support, move/swap
/// semantics (REQ-072), terrain gating (REQ-083a), metrics, upkeep, and snapshot detail.
/// </summary>
public sealed class TavernTests
{
    // ── fixture ─────────────────────────────────────────────────
    private readonly FakeCycleQueries _cycle = new();
    private FakeRoomContent _content = null!;
    private InMemoryBuildLedger _ledger = null!;

    private static RoomTypeId T(string id) => new(id);

    private static RoomTypeSheet Plain(string id, long buildCost = 100, long upkeep = 10,
        IReadOnlyList<TierSpec>? tiers = null, double capacityPerCell = 1.0) =>
        Sheet(id, minArea: 1, maxArea: 100, optimumArea: 100, capacityPerCell: capacityPerCell,
              buildCost: buildCost, nightlyUpkeep: upkeep, tiers: tiers);

    private Tavern Build(
        IReadOnlyList<RoomTypeSheet> catalog,
        IReadOnlyList<RoomTypeId>? available = null,
        long gold = 100_000,
        double multiplier = 1.0,
        IReadOnlyList<TerrainFeature>? terrain = null,
        CellCoord? entrance = null)
    {
        _ledger = new InMemoryBuildLedger(new Money(gold), multiplier);
        _content = new FakeRoomContent(catalog, available);
        return new Tavern(
            _cycle,
            new FixedLot(new GridRect(0, 0, 12, 8), entrance ?? new CellCoord(0, 0), terrain),
            _content,
            _ledger,
            catalog,
            TavernHarness.Costs);
    }

    private static Outcome<PlacementError>.Success Ok(Outcome<PlacementError> o) =>
        Assert.IsType<Outcome<PlacementError>.Success>(o);

    private static TError Err<TError>(Outcome<PlacementError> o) where TError : PlacementError =>
        Assert.IsType<TError>(Assert.IsType<Outcome<PlacementError>.Failure>(o).Error);

    private static RoomId PlaceOk(Tavern t, string type, GridRect fp) =>
        Ok(t.PlaceRoom(T(type), fp)).Events.OfType<RoomPlaced>().Single().Room;

    // ── support (REQ-067, REQ-099) ──────────────────────────────

    [Fact]
    public void Every_bottom_cell_of_a_room_must_be_supported()
    {
        var t = Build(new[] { Plain("taproom") });
        PlaceOk(t, "taproom", new GridRect(0, 0, 3, 1));            // support under x = 0..2 only
        // Footprint (1,1)-(3,1) overhangs to x = 3, where nothing is beneath.
        Err<PlacementError.Unsupported>(t.PlaceRoom(T("taproom"), new GridRect(1, 1, 3, 1)));
    }

    [Fact]
    public void Circulation_cells_provide_support_REQ099()
    {
        var t = Build(new[] { Plain("taproom") });
        PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        Ok(t.BuildCirculation(CirculationKind.Corridor, new CellCoord(2, 0)));
        Ok(t.BuildCirculation(CirculationKind.Corridor, new CellCoord(3, 0)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(4, 0)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(4, 1)));
        // Rests entirely on the corridor cells (2,0) and (3,0); reachable from the stair at (4,1).
        Ok(t.PlaceRoom(T("taproom"), new GridRect(2, 1, 2, 1)));
    }

    [Fact]
    public void Support_is_transitive_from_the_ground_up()
    {
        var t = Build(new[] { Plain("taproom") });
        var ground = PlaceOk(t, "taproom", new GridRect(0, 0, 4, 1));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(4, 0)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(4, 1)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(4, 2)));
        var first = PlaceOk(t, "taproom", new GridRect(2, 1, 2, 1));
        var second = PlaceOk(t, "taproom", new GridRect(2, 2, 2, 1));   // rests on `first`
        Assert.True(t.GetRoom(second).Active);

        // Demolishing the ground room ungrounds `first`, and `second` rests on an ungrounded room:
        // the whole stack loses support (REQ-067 support chains to the ground).
        Ok(t.DemolishRoom(ground));
        Assert.False(t.GetRoom(first).Active);
        Assert.False(t.GetRoom(second).Active);
    }

    // ── terrain gating (REQ-083a) ───────────────────────────────

    [Fact]
    public void Room_requiring_terrain_is_accepted_when_its_footprint_covers_an_enabling_cell()
    {
        var terrain = new List<TerrainFeature>
        {
            new(new CellCoord(3, 0), new TerrainEffect.EnablesRoomType(T("spa"))),
        };
        var t = Build(
            new[] { Sheet("spa", minArea: 1, maxArea: 100, optimumArea: 100, requiresTerrainFeature: T("spring")) },
            terrain: terrain);
        Ok(t.PlaceRoom(T("spa"), new GridRect(2, 0, 2, 1)));            // covers (3,0)
    }

    [Fact]
    public void Room_requiring_terrain_is_rejected_when_the_enabling_cell_is_outside_the_footprint()
    {
        var terrain = new List<TerrainFeature>
        {
            new(new CellCoord(7, 0), new TerrainEffect.EnablesRoomType(T("spa"))),
        };
        var t = Build(
            new[] { Sheet("spa", minArea: 1, maxArea: 100, optimumArea: 100, requiresTerrainFeature: T("spring")) },
            terrain: terrain);
        Err<PlacementError.TerrainRequired>(t.PlaceRoom(T("spa"), new GridRect(0, 0, 2, 1)));
    }

    [Fact]
    public void Terrain_that_only_modifies_stats_does_not_enable_a_terrain_gated_room()
    {
        var terrain = new List<TerrainFeature>
        {
            new(new CellCoord(1, 0), new TerrainEffect.ModifiesRoom(new RoomStatModifier(1.0, 1.2, 1.0))),
        };
        var t = Build(
            new[] { Sheet("spa", minArea: 1, maxArea: 100, optimumArea: 100, requiresTerrainFeature: T("spring")) },
            terrain: terrain);
        Err<PlacementError.TerrainRequired>(t.PlaceRoom(T("spa"), new GridRect(0, 0, 2, 1)));
    }

    // ── move / swap (REQ-072, REQ-098) ──────────────────────────

    [Fact]
    public void Move_onto_another_room_of_the_same_shape_swaps_the_two()
    {
        var t = Build(new[] { Plain("taproom"), Plain("kitchen") });
        var a = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        var b = PlaceOk(t, "kitchen", new GridRect(2, 0, 2, 1));

        var moved = Ok(t.MoveRoom(a, new GridRect(2, 0, 2, 1)));
        var events = moved.Events.OfType<RoomMoved>().ToList();
        Assert.Equal(2, events.Count);
        Assert.Equal(new GridRect(2, 0, 2, 1), t.GetRoom(a).Footprint);
        Assert.Equal(new GridRect(0, 0, 2, 1), t.GetRoom(b).Footprint);
        Assert.IsType<StructureChanged>(moved.Events[^1]);
    }

    [Fact]
    public void Move_onto_a_differently_shaped_room_is_Overlap()
    {
        var t = Build(new[] { Plain("taproom"), Plain("kitchen") });
        var a = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        PlaceOk(t, "kitchen", new GridRect(2, 0, 3, 1));
        Err<PlacementError.Overlap>(t.MoveRoom(a, new GridRect(2, 0, 3, 1)));   // 3×1 ≠ 2×1
    }

    [Fact]
    public void Move_that_breaks_support_deactivates_the_dependent_room_REQ098()
    {
        var t = Build(new[] { Plain("taproom") });
        var ground = PlaceOk(t, "taproom", new GridRect(0, 0, 3, 1));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 0)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 1)));
        var upper = PlaceOk(t, "taproom", new GridRect(1, 1, 2, 1));

        // Move the supporting ground room out from under `upper` (REQ-098: permitted, not blocked).
        var moved = Ok(t.MoveRoom(ground, new GridRect(5, 0, 3, 1)));
        Assert.Contains(moved.Events, e => e is RoomDeactivated d && d.Room == upper);
        Assert.False(t.GetRoom(upper).Active);
        Assert.True(t.GetRoom(ground).Active);

        // Moving it back restores support.
        var back = Ok(t.MoveRoom(ground, new GridRect(0, 0, 3, 1)));
        Assert.Contains(back.Events, e => e is RoomReactivated r && r.Room == upper);
        Assert.True(t.GetRoom(upper).Active);
    }

    [Fact]
    public void Move_onto_the_rooms_own_cells_is_allowed()
    {
        var t = Build(new[] { Plain("taproom") });
        var a = PlaceOk(t, "taproom", new GridRect(0, 0, 3, 1));
        Ok(t.MoveRoom(a, new GridRect(0, 0, 3, 1)));
        Assert.Equal(new GridRect(0, 0, 3, 1), t.GetRoom(a).Footprint);
    }

    [Fact]
    public void Move_onto_a_floating_target_is_NotOnExistingStructure()
    {
        var t = Build(new[] { Plain("taproom") });
        var a = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        Err<PlacementError.NotOnExistingStructure>(t.MoveRoom(a, new GridRect(6, 3, 2, 1)));
        Assert.Equal(new GridRect(0, 0, 2, 1), t.GetRoom(a).Footprint);   // unmoved
    }

    [Fact]
    public void Move_out_of_the_lot_is_OutOfLot()
    {
        var t = Build(new[] { Plain("taproom") });
        var a = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        Err<PlacementError.OutOfLot>(t.MoveRoom(a, new GridRect(11, 0, 3, 1)));
    }

    // ── circulation (REQ-074, REQ-099) ──────────────────────────

    [Fact]
    public void BuildCirculation_outside_the_lot_is_OutOfLot()
    {
        var t = Build(new[] { Plain("taproom") });
        Err<PlacementError.OutOfLot>(t.BuildCirculation(CirculationKind.Corridor, new CellCoord(12, 0)));
    }

    [Fact]
    public void DemolishCirculation_on_a_room_cell_is_NothingAtCell()
    {
        var t = Build(new[] { Plain("taproom") });
        PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        Err<PlacementError.NothingAtCell>(t.DemolishCirculation(new CellCoord(1, 0)));
    }

    [Fact]
    public void Circulation_refund_is_the_amount_actually_charged_under_a_venue_multiplier()
    {
        var t = Build(new[] { Plain("taproom") }, multiplier: 2.0);
        PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        var before = _ledger.Balance;
        var built = Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(2, 0)));
        Assert.Equal(before - TavernHarness.Costs.Stair.MultiplyRounded(2.0), _ledger.Balance);
        Assert.Contains(built.Events, e => e is CirculationBuilt);

        var demolished = Ok(t.DemolishCirculation(new CellCoord(2, 0)));
        var refunded = demolished.Events.OfType<CirculationDemolished>().Single().Refunded;
        Assert.Equal(TavernHarness.Costs.Stair.MultiplyRounded(2.0), refunded);
        Assert.Equal(before, _ledger.Balance);
    }

    [Fact]
    public void BuildCirculation_when_unaffordable_is_InsufficientGold_and_builds_nothing()
    {
        var t = Build(new[] { Plain("taproom") }, gold: 100);
        PlaceOk(t, "taproom", new GridRect(0, 0, 1, 1));           // spends 100
        var version = t.Graph.Version;
        Err<PlacementError.InsufficientGold>(t.BuildCirculation(CirculationKind.Corridor, new CellCoord(1, 0)));
        Assert.Equal(version, t.Graph.Version);
        Assert.DoesNotContain(new CellCoord(1, 0), t.Graph.StairCells);
        Assert.Equal(0, t.Metrics.CirculationCellCount);
    }

    // ── upgrades (REQ-071, REQ-100) ─────────────────────────────

    [Fact]
    public void Upgrade_raises_tier_capacity_and_PaidTotal_and_ends_with_StructureChanged()
    {
        var tiers = new[] { Tier1(), Tier(200, capMult: 2.0) };
        var t = Build(new[] { Plain("taproom", buildCost: 100, tiers: tiers, capacityPerCell: 1.0) });
        var id = PlaceOk(t, "taproom", new GridRect(0, 0, 3, 1));
        Assert.Equal(3, t.GetRoom(id).Capacity);
        var version = t.Graph.Version;

        var up = Ok(t.UpgradeRoom(id));
        Assert.Equal(2, up.Events.OfType<RoomUpgraded>().Single().NewTier);
        Assert.IsType<StructureChanged>(up.Events[^1]);
        Assert.Equal(2, t.GetRoom(id).Tier);
        Assert.Equal(6, t.GetRoom(id).Capacity);                   // area 3 × 1.0 × 2.0
        Assert.Equal(new Money(300), t.GetRoom(id).PaidTotal);     // 100 build + 200 upgrade
        Assert.True(t.Graph.Version > version);
        Assert.Equal(new GridRect(0, 0, 3, 1), t.GetRoom(id).Footprint);   // in place (REQ-100)
    }

    [Fact]
    public void Failed_upgrade_charge_leaves_the_tier_unchanged()
    {
        var tiers = new[] { Tier1(), Tier(5_000) };
        var t = Build(new[] { Plain("taproom", buildCost: 100, tiers: tiers) }, gold: 200);
        var id = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        var err = Err<PlacementError.InsufficientGold>(t.UpgradeRoom(id));
        Assert.Equal(new Money(5_000), err.Required);
        Assert.Equal(new Money(100), err.Available);
        Assert.Equal(1, t.GetRoom(id).Tier);
        Assert.Equal(new Money(100), t.GetRoom(id).PaidTotal);
    }

    // ── queries ─────────────────────────────────────────────────

    [Fact]
    public void Failed_placement_charge_leaves_the_grid_untouched()
    {
        var t = Build(new[] { Plain("taproom", buildCost: 500) }, gold: 100);
        Err<PlacementError.InsufficientGold>(t.PlaceRoom(T("taproom"), new GridRect(0, 0, 2, 1)));
        Assert.Empty(t.Rooms);
        Assert.Equal(new Money(100), _ledger.Balance);
    }

    [Fact]
    public void GetRoom_on_an_unknown_id_throws()
    {
        var t = Build(new[] { Plain("taproom") });
        Assert.Throws<KeyNotFoundException>(() => t.GetRoom(new RoomId(42)));
    }

    [Fact]
    public void NightlyUpkeepBill_includes_inactive_rooms_but_capacity_does_not()
    {
        var t = Build(new[] { Plain("taproom", upkeep: 10, capacityPerCell: 1.0) });
        var ground = PlaceOk(t, "taproom", new GridRect(0, 0, 3, 1));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 0)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 1)));
        var upper = PlaceOk(t, "taproom", new GridRect(1, 1, 2, 1));
        Assert.Equal(5, t.TotalGuestCapacity);                     // 3 + 2
        Assert.Equal(new Money(20), t.NightlyUpkeepBill);

        Ok(t.DemolishRoom(ground));
        Assert.False(t.GetRoom(upper).Active);
        Assert.Equal(0, t.TotalGuestCapacity);                     // inactive contributes nothing
        Assert.Equal(new Money(10), t.NightlyUpkeepBill);          // …but still costs upkeep
    }

    [Fact]
    public void Metrics_report_height_room_counts_and_circulation()
    {
        var t = Build(new[] { Plain("taproom"), Plain("kitchen") });
        PlaceOk(t, "taproom", new GridRect(0, 0, 3, 1));
        PlaceOk(t, "kitchen", new GridRect(3, 0, 2, 1));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(5, 0)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(5, 1)));
        PlaceOk(t, "taproom", new GridRect(3, 1, 2, 1));

        var m = t.Metrics;
        Assert.Equal(2, m.MaxHeightCells);                         // highest built cell y = 1
        Assert.Equal(3, m.RoomCount);
        Assert.Equal(2, m.RoomCountsByType[T("taproom")]);
        Assert.Equal(1, m.RoomCountsByType[T("kitchen")]);
        Assert.Equal(2, m.CirculationCellCount);
    }

    [Fact]
    public void AvailableRoomTypes_is_re_read_from_content_on_every_access()
    {
        var t = Build(new[] { Plain("taproom"), Plain("vault") }, available: new[] { T("taproom") });
        Assert.Equal(new[] { T("taproom") }, t.AvailableRoomTypes.Select(s => s.Id));

        _content.Available = new[] { T("taproom"), T("vault") };   // a new unlock lands
        Assert.Equal(new[] { T("taproom"), T("vault") }, t.AvailableRoomTypes.Select(s => s.Id));
        Ok(t.PlaceRoom(T("vault"), new GridRect(0, 0, 2, 1)));     // now placeable
    }

    // ── snapshot & reset ────────────────────────────────────────

    [Fact]
    public void Snapshot_round_trip_preserves_tier_paid_total_and_activity()
    {
        var tiers = new[] { Tier1(), Tier(200) };
        var t = Build(new[] { Plain("taproom", buildCost: 100, tiers: tiers) });
        var ground = PlaceOk(t, "taproom", new GridRect(0, 0, 3, 1));
        Ok(t.UpgradeRoom(ground));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 0)));
        Ok(t.BuildCirculation(CirculationKind.Stair, new CellCoord(3, 1)));
        var upper = PlaceOk(t, "taproom", new GridRect(1, 1, 2, 1));
        Ok(t.DemolishRoom(ground));                                // `upper` goes inactive
        var snapshot = t.Capture();
        var versionAtCapture = t.Graph.Version;

        var restored = Build(new[] { Plain("taproom", buildCost: 100, tiers: tiers) });
        restored.Restore(snapshot);

        var room = restored.GetRoom(upper);
        Assert.False(room.Active);
        Assert.Equal(1, room.Tier);
        Assert.Equal(new Money(100), room.PaidTotal);
        Assert.Equal(new CellCoord(3, 1), restored.Graph.StairCells.Single(c => c.Y == 1));
        Assert.True(restored.Graph.Version > 0);

        // Restoring into the same aggregate keeps the version strictly increasing (CON-003).
        t.Restore(snapshot);
        Assert.True(t.Graph.Version > versionAtCapture);
    }

    [Fact]
    public void Restored_state_keeps_issuing_fresh_room_ids()
    {
        var t = Build(new[] { Plain("taproom") });
        var first = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        var snapshot = t.Capture();

        var restored = Build(new[] { Plain("taproom") });
        restored.Restore(snapshot);
        var next = PlaceOk(restored, "taproom", new GridRect(2, 0, 2, 1));
        Assert.NotEqual(first, next);
    }

    [Fact]
    public void ResetAll_is_not_phase_gated_and_does_not_reuse_room_ids()
    {
        var t = Build(new[] { Plain("taproom") });
        var first = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        _cycle.Phase = Phase.Settlement;

        var events = t.ResetAll();
        Assert.Contains(events, e => e is StructureReset);
        Assert.IsType<StructureChanged>(events[^1]);
        Assert.Empty(t.Rooms);
        Assert.Equal(0, t.Metrics.CirculationCellCount);

        _cycle.Phase = Phase.Prep;
        var next = PlaceOk(t, "taproom", new GridRect(0, 0, 2, 1));
        Assert.NotEqual(first, next);                              // ids are never reused (kernel)
    }
}
