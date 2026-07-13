using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Structure.Driven;

/// <summary>
/// CON-004 abstract conformance suite for <see cref="IRoomContent"/> and the room-sheet JSON
/// schema: the golden-file load, every schema-validation rule (fail-fast with a diagnostic that
/// names the offending field), and unlock filtering. The content adapter ticket (TKT-020)
/// supplies the concrete loader/adapter via the abstract hooks below. Abstract ⇒ nothing runs
/// until then. The golden document lives beside this file as <c>rooms.sample.json</c>.
/// </summary>
public abstract class RoomContentConformanceTests
{
    /// Parse a rooms.json document into sheets. MUST throw fail-fast on any schema violation,
    /// with an exception message naming the offending field (per CON-004).
    protected abstract IReadOnlyList<RoomTypeSheet> LoadCatalog(string roomsJson);

    /// Build an <see cref="IRoomContent"/> composing the golden catalog with unlock/venue state:
    /// base rooms are always available; ids in <paramref name="unlockedSpecials"/> additionally appear.
    protected abstract IRoomContent CreateContent(string roomsJson, IReadOnlyCollection<RoomTypeId> unlockedSpecials);

    /// A room id present in the golden catalog but NOT in the base (always-available) set — it
    /// surfaces only when its unlock is owned. For <c>rooms.sample.json</c> this is <c>vault</c>.
    protected abstract RoomTypeId LockedSpecialInGolden { get; }

    protected static string GoldenRoomsJson([CallerFilePath] string? thisFile = null) =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(thisFile!)!, "rooms.sample.json"));

    // ── Golden-file load ────────────────────────────────────────
    [Fact]
    public void Golden_file_loads_to_expected_taproom_sheet()
    {
        var taproom = LoadCatalog(GoldenRoomsJson()).Single(s => s.Id == new RoomTypeId("taproom"));

        Assert.Equal("Tap Room", taproom.DisplayName);
        Assert.Equal(6, taproom.MinArea);
        Assert.Equal(24, taproom.MaxArea);
        Assert.Equal(12, taproom.OptimumArea);
        Assert.Equal(0.04, taproom.EfficiencyFalloffPerCell, precision: 6);
        Assert.Equal(0.4, taproom.MinEfficiency, precision: 6);
        Assert.Equal(1.5, taproom.CapacityPerCell, precision: 6);
        Assert.Equal(new Money(200), taproom.BuildCost);
        Assert.Equal(new Money(10), taproom.NightlyUpkeep);
        Assert.False(taproom.Broadcaster);
        Assert.Null(taproom.RequiresTerrainFeature);

        Assert.Equal(2, taproom.Tiers.Count);
        Assert.Equal(Money.Zero, taproom.Tiers[0].UpgradeCost);
        Assert.Equal(new Money(500), taproom.Tiers[1].UpgradeCost);
        var override1 = Assert.Single(taproom.Tiers[1].StaffingMaxOverrides);
        Assert.Equal(new RoleId("barmaid"), override1.Role);
        Assert.Equal(5, override1.Max);

        var drink = Assert.Single(taproom.Services);
        Assert.Equal("drink", drink.ServiceId);
        Assert.Equal(ServiceKind.MenuConsumption, drink.Kind);
        Assert.Equal(40, drink.BaseDurationTicks);
        Assert.Null(drink.EntryFee);

        Assert.Equal(2, taproom.Staffing.Roles.Count);
        Assert.Contains(taproom.Staffing.Roles, r => r.Role == new RoleId("bartender") && r.Min == 1 && r.Max == 1);
        Assert.Contains(new TraitId("rowdy-friendly"), taproom.Traits);
    }

    [Fact]
    public void Golden_file_room_entry_service_carries_entry_fee()
    {
        var vault = LoadCatalog(GoldenRoomsJson()).Single(s => s.Id == new RoomTypeId("vault"));
        var entry = Assert.Single(vault.Services);
        Assert.Equal(ServiceKind.RoomEntry, entry.Kind);
        Assert.Equal(new Money(25), entry.EntryFee);
        Assert.True(vault.Broadcaster);
    }

    // ── Schema-validation rules (each rejects with a field-naming diagnostic) ──
    private void AssertRejected(string badJson, string offendingFieldToken)
    {
        var ex = Assert.ThrowsAny<Exception>(() => LoadCatalog(badJson));
        Assert.Contains(offendingFieldToken, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // A single valid room, mutated per rule below.
    private static string Doc(string room) => $"{{ \"rooms\": [ {room} ] }}";

    private const string ValidRoomTemplate =
        """
        { "id": "%ID%", "displayName": "R", "minArea": %MIN%, "maxArea": %MAX%, "optimumArea": %OPT%,
          "efficiencyFalloffPerCell": 0.05, "minEfficiency": 0.5, "capacityPerCell": 1.0,
          "buildCost": 100, "nightlyUpkeep": 10,
          "tiers": [ { "upgradeCost": %TIER1COST%, "capacityMultiplier": 1.0, "serviceSpeedMultiplier": 1.0,
                       "staffingMaxOverrides": [ %OVERRIDE% ] } ],
          "services": [ { "serviceId": "s", "kind": "%KIND%", "baseDurationTicks": 40, "entryFee": null } ],
          "staffing": [ { "role": "bartender", "min": 1, "max": 1 } ],
          "traits": [], "broadcaster": false, "requiresTerrainFeature": null %EXTRA% }
        """;

    private static string Room(
        string id = "r", int min = 4, int max = 9, int opt = 6,
        int tier1Cost = 0, string @override = "", string kind = "MenuConsumption", string extra = "") =>
        ValidRoomTemplate
            .Replace("%ID%", id).Replace("%MIN%", min.ToString()).Replace("%MAX%", max.ToString())
            .Replace("%OPT%", opt.ToString()).Replace("%TIER1COST%", tier1Cost.ToString())
            .Replace("%OVERRIDE%", @override).Replace("%KIND%", kind).Replace("%EXTRA%", extra);

    [Fact]
    public void Rejects_duplicate_ids()
    {
        var doc = $"{{ \"rooms\": [ {Room(id: "dup")}, {Room(id: "dup")} ] }}";
        AssertRejected(doc, "dup");
    }

    [Fact]
    public void Rejects_min_area_above_optimum()
    {
        AssertRejected(Doc(Room(min: 7, opt: 6, max: 9)), "optimumArea");
    }

    [Fact]
    public void Rejects_optimum_above_max_area()
    {
        AssertRejected(Doc(Room(min: 4, opt: 10, max: 9)), "maxArea");
    }

    [Fact]
    public void Rejects_tier1_nonzero_upgrade_cost()
    {
        AssertRejected(Doc(Room(tier1Cost: 5)), "upgradeCost");
    }

    [Fact]
    public void Rejects_staffing_override_role_absent_from_base_staffing()
    {
        var badOverride = "{ \"role\": \"sommelier\", \"min\": 1, \"max\": 2 }";
        AssertRejected(Doc(Room(@override: badOverride)), "sommelier");
    }

    [Fact]
    public void Rejects_unknown_service_kind()
    {
        AssertRejected(Doc(Room(kind: "Teleport")), "Teleport");
    }

    [Fact]
    public void Rejects_unknown_json_field()
    {
        AssertRejected(Doc(Room(extra: ", \"wizardry\": true")), "wizardry");
    }

    // ── Unlock filtering (stubbed CON-013 state) ────────────────
    [Fact]
    public void Locked_special_room_is_excluded_until_unlocked()
    {
        var golden = GoldenRoomsJson();
        var locked = LockedSpecialInGolden;

        var withoutUnlock = CreateContent(golden, Array.Empty<RoomTypeId>())
            .AvailableRoomTypes().Select(s => s.Id).ToList();
        Assert.DoesNotContain(locked, withoutUnlock);

        var withUnlock = CreateContent(golden, new[] { locked })
            .AvailableRoomTypes().Select(s => s.Id).ToList();
        Assert.Contains(locked, withUnlock);
    }
}
