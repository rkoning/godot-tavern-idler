using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Guests;
using TavernIdler.Domains.Structure;   // GuestRoomInfo, ServiceOffering, ServiceKind
using TavernIdler.Domains.Staffing;    // RoomStaffState
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Guests.Driven;

/// <summary>
/// CON-006 (Guests Driven Ports v1.0) abstract conformance suite: the four ports the guest sim reads
/// from — <see cref="IStructureAccess"/>, <see cref="IRoomServiceState"/>, <see cref="ITransactions"/>,
/// <see cref="IAttractionContext"/>. Covers every bullet of the contract's "Conformance tests"
/// section: structure equivalence (inactive excluded), every <see cref="TransactionResult"/> variant
/// with CON-007 pricing/rounding + ledger/stock deltas, satisfaction×spending composition, attraction
/// exclusions/multipliers/composition pass-through, and the re-entrancy ban.
///
/// Scenarios are scripted in engine-neutral terms (<see cref="DrivenWorld"/>) so the suite never
/// references the not-yet-frozen source contracts (CON-007 economy, CON-013 progression). The bridge
/// ticket (TKT-019) supplies a concrete <see cref="IHarness"/> that maps a <see cref="DrivenWorld"/>
/// onto stubbed CON-003/007/009/013 sources and returns the REAL bridges; expected values here are
/// derived from the CON-006 rules, so a passing bridge implements exactly those. ABSTRACT ⇒ nothing
/// runs until TKT-019 subclasses it.
/// </summary>
public abstract class GuestsDrivenPortsConformanceTests
{
    protected abstract IHarness CreateHarness(DrivenWorld world);

    /// The bridges under test over a scripted backing world, plus observation hooks for the ledger /
    /// stock (so transaction deltas are checkable) and a re-entrancy sentinel.
    public interface IHarness
    {
        IStructureAccess Structure { get; }
        IRoomServiceState RoomServices { get; }
        ITransactions Transactions { get; }
        IAttractionContext Attraction { get; }

        long LedgerBalance { get; }        // backing CON-007 ledger total
        int StockOf(MenuItemId item);      // backing CON-008 stock remaining

        /// True iff any port implementation called back into <see cref="IGuestSimCommands"/> during
        /// this suite's calls (CON-006 re-entrancy ban). A correct bridge never does ⇒ always false.
        bool Reentered { get; }
    }

    // ── engine-neutral scripting surface ────────────────────────
    public sealed record RoomDef(
        RoomId Id, GridRect Footprint, int Capacity, double Efficiency, bool Active,
        RoomStaffState StaffState, double SpeedFactor, IReadOnlyList<ServiceOffering> Services);

    public sealed record MenuDef(MenuItemId Item, Money BasePrice, int Stock);

    public sealed record AttractionSpec(
        long Acclaim,
        IReadOnlyList<GuestTypeId> BaseTypes,
        IReadOnlyList<GuestTypeId> Exclusions,
        IReadOnlyDictionary<GuestTypeId, double> VenueWeights,
        IReadOnlyDictionary<RoomTypeId, int> ActiveRoomCounts,
        IReadOnlyList<MenuItemId> StockedItems,
        IReadOnlyDictionary<RoleId, int> StaffedRoleCounts,
        double ArrivalRateFactor);

    public sealed record DrivenWorld(
        IReadOnlyList<RoomDef> Rooms,
        CellCoord Entrance,
        IReadOnlyList<MenuDef> Menu,
        long InitialLedger,
        AttractionSpec Attraction);

    // ── builders ────────────────────────────────────────────────
    private static ServiceOffering Svc(string id, ServiceKind kind = ServiceKind.MenuConsumption, int dur = 4) =>
        new(id, kind, dur, null);

    private static RoomDef Room(int id, int capacity, bool active = true,
        RoomStaffState staff = RoomStaffState.Open, double speed = 1.0, params ServiceOffering[] services) =>
        new(new RoomId(id), new GridRect(id, 0, 1, 1), capacity, 1.0, active, staff, speed,
            services.Length > 0 ? services : new[] { Svc("drink") });

    private static AttractionSpec Attraction(
        long acclaim = 0,
        IEnumerable<string>? baseTypes = null,
        IEnumerable<string>? exclusions = null,
        IReadOnlyDictionary<GuestTypeId, double>? venueWeights = null,
        IReadOnlyDictionary<RoomTypeId, int>? roomCounts = null,
        IEnumerable<string>? stocked = null,
        IReadOnlyDictionary<RoleId, int>? staffed = null,
        double arrivalRateFactor = 0.5) =>
        new(acclaim,
            (baseTypes ?? new[] { "dwarf" }).Select(t => new GuestTypeId(t)).ToArray(),
            (exclusions ?? Array.Empty<string>()).Select(t => new GuestTypeId(t)).ToArray(),
            venueWeights ?? new Dictionary<GuestTypeId, double>(),
            roomCounts ?? new Dictionary<RoomTypeId, int>(),
            (stocked ?? Array.Empty<string>()).Select(i => new MenuItemId(i)).ToArray(),
            staffed ?? new Dictionary<RoleId, int>(),
            arrivalRateFactor);

    private static TransactionRequest MenuBuy(int guest, string item, long wallet,
        double satisfactionModifier = 1.0, double spendingMultiplier = 1.0) =>
        new(new GuestId(guest), TransactionKind.MenuPurchase, new MenuItemId(item), null, null,
            new Money(wallet), satisfactionModifier, spendingMultiplier);

    // ════════════════════════════════════════════════════════════
    //  IStructureAccess — equivalence + inactive excluded
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Structure_access_exposes_active_rooms_only_and_forwards_capacity_and_entrance()
    {
        var world = new DrivenWorld(
            Rooms: new[]
            {
                Room(1, capacity: 4, active: true, services: Svc("drink")),
                Room(2, capacity: 6, active: true, services: Svc("meal", ServiceKind.MenuConsumption)),
                Room(3, capacity: 9, active: false, services: Svc("spa", ServiceKind.RoomEntry)),   // inactive
            },
            Entrance: new CellCoord(0, 0),
            Menu: Array.Empty<MenuDef>(),
            InitialLedger: 0,
            Attraction: Attraction());

        var h = CreateHarness(world);
        var active = h.Structure.ActiveRooms;

        // Only the active rooms are visible, in the same set.
        Assert.Equal(new[] { 1, 2 }, active.Select(r => r.Id.Value).OrderBy(v => v).ToArray());
        Assert.DoesNotContain(active, r => r.Id == new RoomId(3));

        var r1 = active.Single(r => r.Id == new RoomId(1));
        Assert.Equal(4, r1.Capacity);
        Assert.Equal(new[] { "drink" }, r1.Services.Select(s => s.ServiceId).ToArray());

        // Total capacity forwards the CON-003 sum over ACTIVE rooms; the entrance passes through.
        Assert.Equal(10, h.Structure.TotalGuestCapacity);   // 4 + 6 (room 3 inactive)
        Assert.Equal(new CellCoord(0, 0), h.Structure.Entrance);
    }

    // ════════════════════════════════════════════════════════════
    //  IRoomServiceState — pass-through + unknown-room throws
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Room_service_state_forwards_state_and_speed_and_throws_for_unknown_rooms()
    {
        var world = new DrivenWorld(
            Rooms: new[]
            {
                Room(1, capacity: 4, staff: RoomStaffState.Open, speed: 1.0),
                Room(2, capacity: 4, staff: RoomStaffState.Degraded, speed: 0.5),
            },
            Entrance: new CellCoord(0, 0),
            Menu: Array.Empty<MenuDef>(),
            InitialLedger: 0,
            Attraction: Attraction());

        var h = CreateHarness(world);

        Assert.Equal(RoomStaffState.Open, h.RoomServices.State(new RoomId(1)));
        Assert.Equal(RoomStaffState.Degraded, h.RoomServices.State(new RoomId(2)));
        Assert.Equal(0.5, h.RoomServices.SpeedFactor(new RoomId(2)), precision: 6);
        Assert.Throws<KeyNotFoundException>(() => h.RoomServices.State(new RoomId(99)));
    }

    // ════════════════════════════════════════════════════════════
    //  ITransactions — result variants, pricing/rounding, deltas
    // ════════════════════════════════════════════════════════════

    private DrivenWorld MenuWorld(long ledger = 0, params MenuDef[] menu) =>
        new(new[] { Room(1, capacity: 4, services: Svc("drink")) }, new CellCoord(0, 0), menu, ledger, Attraction());

    [Fact]
    public void Menu_purchase_credits_the_ledger_and_decrements_stock_by_the_rounded_price()
    {
        var world = MenuWorld(ledger: 100, new MenuDef(new MenuItemId("ale"), new Money(10), Stock: 3));
        var h = CreateHarness(world);

        var result = h.Transactions.Execute(MenuBuy(1, "ale", wallet: 1000));

        var paid = Assert.IsType<TransactionResult.Completed>(result).Paid;
        Assert.Equal(new Money(10), paid);            // 10 × (1.0 × 1.0), rounded
        Assert.Equal(110, h.LedgerBalance);           // credited exactly Paid
        Assert.Equal(2, h.StockOf(new MenuItemId("ale")));
    }

    [Fact]
    public void Pricing_composes_satisfaction_and_spending_with_half_away_rounding()
    {
        // Base 10 × (1.2 satisfaction × 1.5 spending) = 18 exactly; and 7 × 1.25 = 8.75 → 9 (half away).
        var world = MenuWorld(ledger: 0,
            new MenuDef(new MenuItemId("ale"), new Money(10), Stock: 5),
            new MenuDef(new MenuItemId("stew"), new Money(7), Stock: 5));
        var h = CreateHarness(world);

        var ale = Assert.IsType<TransactionResult.Completed>(
            h.Transactions.Execute(MenuBuy(1, "ale", 1000, satisfactionModifier: 1.2, spendingMultiplier: 1.5)));
        Assert.Equal(new Money(10).MultiplyRounded(1.2 * 1.5), ale.Paid);
        Assert.Equal(new Money(18), ale.Paid);

        var stew = Assert.IsType<TransactionResult.Completed>(
            h.Transactions.Execute(MenuBuy(2, "stew", 1000, satisfactionModifier: 1.25, spendingMultiplier: 1.0)));
        Assert.Equal(new Money(7).MultiplyRounded(1.25), stew.Paid);
        Assert.Equal(new Money(9), stew.Paid);        // 8.75 rounds half-away-from-zero to 9
    }

    [Fact]
    public void Sold_out_and_cannot_afford_charge_nothing_and_leave_stock_untouched()
    {
        var world = MenuWorld(ledger: 500,
            new MenuDef(new MenuItemId("ale"), new Money(10), Stock: 0),      // sold out
            new MenuDef(new MenuItemId("wine"), new Money(100), Stock: 2));   // priced above wallet below
        var h = CreateHarness(world);

        var soldOut = h.Transactions.Execute(MenuBuy(1, "ale", wallet: 1000));
        Assert.IsType<TransactionResult.SoldOut>(soldOut);

        var cannotAfford = h.Transactions.Execute(MenuBuy(2, "wine", wallet: 50));   // 100 > 50
        Assert.IsType<TransactionResult.CannotAfford>(cannotAfford);

        // Neither touched the ledger or stock.
        Assert.Equal(500, h.LedgerBalance);
        Assert.Equal(0, h.StockOf(new MenuItemId("ale")));
        Assert.Equal(2, h.StockOf(new MenuItemId("wine")));
    }

    [Fact]
    public void Unknown_item_is_not_offered_and_changes_nothing()
    {
        var world = MenuWorld(ledger: 42, new MenuDef(new MenuItemId("ale"), new Money(10), Stock: 3));
        var h = CreateHarness(world);

        Assert.IsType<TransactionResult.NotOffered>(h.Transactions.Execute(MenuBuy(1, "ghost-item", wallet: 1000)));
        Assert.Equal(42, h.LedgerBalance);
        Assert.Equal(3, h.StockOf(new MenuItemId("ale")));
    }

    // ════════════════════════════════════════════════════════════
    //  IAttractionContext — exclusions / multipliers / composition
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Attraction_context_excludes_excluded_types_and_passes_through_the_rest()
    {
        var venueWeights = new Dictionary<GuestTypeId, double> { [new GuestTypeId("elf")] = 2.5 };
        var roomCounts = new Dictionary<RoomTypeId, int> { [new RoomTypeId("taproom")] = 2 };
        var staffed = new Dictionary<RoleId, int> { [new RoleId("barkeep")] = 1 };

        var world = new DrivenWorld(
            Rooms: new[] { Room(1, capacity: 4) },
            Entrance: new CellCoord(0, 0),
            Menu: Array.Empty<MenuDef>(),
            InitialLedger: 0,
            Attraction: Attraction(
                acclaim: 250,
                baseTypes: new[] { "dwarf", "elf", "goblin" },
                exclusions: new[] { "goblin" },
                venueWeights: venueWeights,
                roomCounts: roomCounts,
                stocked: new[] { "ale" },
                staffed: staffed,
                arrivalRateFactor: 0.75));

        var inputs = CreateHarness(world).Attraction.Current();

        Assert.Equal(250, inputs.LifetimeAcclaim);
        Assert.Equal(0.75, inputs.ArrivalRateFactor, precision: 6);

        var available = inputs.AvailableTypes.Select(t => t.Id.Value).OrderBy(v => v).ToArray();
        Assert.Equal(new[] { "dwarf", "elf" }, available);          // goblin excluded
        Assert.DoesNotContain(new GuestTypeId("goblin"), inputs.AvailableTypes.Select(t => t.Id));

        Assert.Equal(2.5, inputs.VenueWeightMultipliers[new GuestTypeId("elf")], precision: 6);
        Assert.Equal(2, inputs.Composition.ActiveRoomCounts[new RoomTypeId("taproom")]);
        Assert.Equal(new[] { new MenuItemId("ale") }, inputs.Composition.StockedItems);
        Assert.Equal(1, inputs.Composition.StaffedRoleCounts[new RoleId("barkeep")]);
    }

    // ════════════════════════════════════════════════════════════
    //  Re-entrancy ban (CON-006 semantics)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void No_port_call_re_enters_the_guest_simulation()
    {
        var world = MenuWorld(ledger: 0, new MenuDef(new MenuItemId("ale"), new Money(10), Stock: 5));
        var h = CreateHarness(world);

        // Exercise every port.
        _ = h.Structure.ActiveRooms;
        _ = h.Structure.TotalGuestCapacity;
        _ = h.RoomServices.State(new RoomId(1));
        _ = h.Transactions.Execute(MenuBuy(1, "ale", wallet: 1000));
        _ = h.Attraction.Current();

        Assert.False(h.Reentered, "driven-port implementations must not call back into IGuestSimCommands");
    }
}
