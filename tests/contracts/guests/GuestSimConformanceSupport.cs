using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Guests;
using TavernIdler.Domains.Structure;   // TraversalGraph, ServiceOffering, ServiceKind
using TavernIdler.Domains.Staffing;    // RoomStaffState
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Guests;

// ── CON-005 behavioral harness ───────────────────────────────────────────────
// The seam the abstract GuestSimConformanceTests drives. The Guests domain ticket (TKT-014)
// provides a concrete <see cref="IGuestSimTestHarness"/> from CreateSut(GuestWorld): it builds the
// real guest simulation seeded from GuestWorld.Seed (drawing arrivals/VIP rolls from the "guests"
// IRandom stream, CON-005), with the service-phase length = GuestWorld.ServiceDurationTicks, the
// content sheets = GuestWorld.Catalog, and the four CON-006 driven ports wired to the supplied
// doubles. TKT-005 only defines the suite + the frozen port types it targets — no domain behavior
// lives here.
public interface IGuestSimTestHarness
{
    IGuestSimCommands Commands { get; }
    IGuestView View { get; }
    IGuestPresence Presence { get; }
}

/// <summary>
/// A fully-specified, deterministic scenario for the guest simulation. Everything the SUT observes
/// is fixed here, so two SUTs built from equal <see cref="GuestWorld"/>s (with the same seed) must
/// produce identical event streams and view states (CON-005 determinism). The four driven ports are
/// concrete test doubles (below) whose configuration the suite controls and observes directly.
/// </summary>
public sealed record GuestWorld(
    long Seed,
    int ServiceDurationTicks,
    GuestCatalog Catalog,
    IStructureAccess Structure,
    IRoomServiceState RoomServices,
    ITransactions Transactions,
    IAttractionContext Attraction);

// ── Configurable driven-port doubles (CON-006) ───────────────────────────────

/// Structure view. <see cref="ActiveRooms"/> already excludes inactive rooms per CON-006.
/// <see cref="Deactivate"/> models a mid-night REQ-098 deactivation: the room drops out of
/// <see cref="ActiveRooms"/> and the room→cell map, and <see cref="TraversalGraph.Version"/> bumps
/// (its cells stay walkable so paths through them do not break).
public sealed class FakeStructureAccess : IStructureAccess
{
    public required TraversalGraph Graph { get; set; }
    public required CellCoord Entrance { get; init; }
    public required int TotalGuestCapacity { get; init; }
    public required IReadOnlyList<GuestRoomInfo> ActiveRooms { get; set; }

    public void Deactivate(RoomId room)
    {
        ActiveRooms = ActiveRooms.Where(r => r.Id != room).ToArray();
        var roomAtCell = Graph.RoomAtCell.Where(kv => kv.Value != room)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        Graph = new TraversalGraph(Graph.Version + 1, Graph.WalkableCells, roomAtCell, Graph.StairCells);
    }
}

/// Per-room staffing state + speed. Unknown room throws <see cref="KeyNotFoundException"/> (CON-006).
public sealed class FakeRoomServiceState : IRoomServiceState
{
    private readonly IReadOnlyDictionary<RoomId, (RoomStaffState State, double Speed)> _rooms;

    public FakeRoomServiceState(IReadOnlyDictionary<RoomId, (RoomStaffState, double)> rooms) =>
        _rooms = rooms.ToDictionary(kv => kv.Key, kv => (kv.Value.Item1, kv.Value.Item2));

    /// Every listed room defaults to Open / speed 1.0.
    public static FakeRoomServiceState AllOpen(IEnumerable<RoomId> rooms) =>
        new(rooms.ToDictionary(r => r, _ => (RoomStaffState.Open, 1.0)));

    public RoomStaffState State(RoomId room) =>
        _rooms.TryGetValue(room, out var v) ? v.State : throw new KeyNotFoundException(room.ToString());

    public double SpeedFactor(RoomId room) =>
        _rooms.TryGetValue(room, out var v) ? v.Speed : throw new KeyNotFoundException(room.ToString());
}

/// <summary>
/// A reference transactions double that prices, stocks, and books to a running ledger exactly as
/// CON-006 requires: <c>price = basePrice.MultiplyRounded(SatisfactionModifier × SpendingMultiplier)</c>;
/// <c>CannotAfford</c>/<c>SoldOut</c> charge nothing. It records every request so the suite can assert
/// the payment-modifier / spending-multiplier the sim stamped onto each transaction.
/// </summary>
public sealed class FakeTransactions : ITransactions
{
    private readonly Dictionary<string, Money> _prices = new();   // MenuItemId.Value → base price
    private readonly Dictionary<string, int> _stock = new();      // MenuItemId.Value → remaining (absent ⇒ infinite)
    private readonly Money _defaultPrice;
    private readonly List<TransactionRequest> _requests = new();

    public FakeTransactions(Money defaultPrice) => _defaultPrice = defaultPrice;

    public long LedgerTotal { get; private set; }
    public IReadOnlyList<TransactionRequest> Requests => _requests;

    public FakeTransactions WithPrice(string item, Money price) { _prices[item] = price; return this; }
    public FakeTransactions WithStock(string item, int qty) { _stock[item] = qty; return this; }

    public int StockOf(string item) => _stock.TryGetValue(item, out var q) ? q : int.MaxValue;

    public TransactionResult Execute(TransactionRequest request)
    {
        _requests.Add(request);
        var key = request.Item?.Value;

        if (key is not null && _stock.TryGetValue(key, out var remaining) && remaining <= 0)
            return new TransactionResult.SoldOut();

        var basePrice = key is not null && _prices.TryGetValue(key, out var p) ? p : _defaultPrice;
        var price = basePrice.MultiplyRounded(request.SatisfactionModifier * request.SpendingMultiplier);

        if (price > request.WalletAvailable)
            return new TransactionResult.CannotAfford();

        if (key is not null && _stock.ContainsKey(key))
            _stock[key] = remainingAfter(key);

        LedgerTotal += price.Amount;
        return new TransactionResult.Completed(price);

        int remainingAfter(string k) => _stock[k] - 1;
    }
}

/// Returns a fixed <see cref="AttractionInputs"/> (structure/menu/staffing are stable within a night
/// for these behavioral scenarios, satisfying the "stable within a tick" contract trivially).
public sealed class FakeAttractionContext : IAttractionContext
{
    private readonly AttractionInputs _inputs;
    public FakeAttractionContext(AttractionInputs inputs) => _inputs = inputs;
    public AttractionInputs Current() => _inputs;
}

// ── Scenario builders ────────────────────────────────────────────────────────

/// Compact assembly of guest content sheets, structure graphs, rooms, and attraction inputs so a
/// behavioral test states only what it exercises.
public static class Scenario
{
    // Content sheets ---------------------------------------------------------
    public static GuestAgendaItem Want(string serviceId, string? menuItem = null) =>
        new(serviceId, menuItem is null ? null : new MenuItemId(menuItem));

    public static GuestTypeSheet Type(
        string id,
        int baseWeight = 50,
        IEnumerable<GuestAgendaItem>? agenda = null,
        string crowdPref = "neutral",
        double crowdMag = 0.0,
        int queuePatience = 200,
        int blockedWait = 100,
        long wallet = 1000,
        IEnumerable<string>? traits = null,
        bool isVip = false,
        VipSpec? vip = null) =>
        new(new GuestTypeId(id), id, isVip, "sprite_" + id, baseWeight,
            Array.Empty<GuestAttractor>(),
            new CrowdingSpec(crowdPref, crowdMag),
            queuePatience, blockedWait,
            (agenda ?? new[] { Want("drink", "ale") }).ToArray(),
            new Money(wallet), new Money(wallet),
            (traits ?? Array.Empty<string>()).Select(t => new TraitId(t)).ToArray(),
            vip);

    public static GuestCatalog Catalog(params GuestTypeSheet[] types) => new(types);

    // Structure --------------------------------------------------------------
    public static ServiceOffering Service(string serviceId, ServiceKind kind, int baseDuration = 4, Money? entryFee = null) =>
        new(serviceId, kind, baseDuration, entryFee);

    public static GuestRoomInfo Room(int id, int cellX, int capacity, params ServiceOffering[] services) =>
        new(new RoomId(id), new GridRect(cellX, 0, 1, 1), capacity, 1.0, services);

    /// A ground-level walkable corridor [0..length) with each room's single cell mapped to its id.
    public static TraversalGraph GroundRow(int version, int length, IEnumerable<GuestRoomInfo> rooms)
    {
        var walk = new HashSet<CellCoord>();
        for (var x = 0; x < length; x++) walk.Add(new CellCoord(x, 0));
        var roomAtCell = new Dictionary<CellCoord, RoomId>();
        foreach (var r in rooms)
        {
            var cell = new CellCoord(r.Footprint.X, 0);
            walk.Add(cell);
            roomAtCell[cell] = r.Id;
        }
        return new TraversalGraph(version, walk, roomAtCell, new HashSet<CellCoord>());
    }

    /// Structure access over a ground-row tavern: entrance at (0,0), rooms strung along the row.
    public static FakeStructureAccess Structure(int totalCapacity, params GuestRoomInfo[] rooms) =>
        new()
        {
            Graph = GroundRow(1, Math.Max(2, rooms.Length + 2), rooms),
            Entrance = new CellCoord(0, 0),
            TotalGuestCapacity = totalCapacity,
            ActiveRooms = rooms,
        };

    // Attraction -------------------------------------------------------------
    public static AttractionInputs Attract(
        IEnumerable<string> availableTypes,
        double arrivalRateFactor = 1.0,
        long acclaim = 0,
        IReadOnlyDictionary<GuestTypeId, double>? venueWeights = null,
        CompositionSummary? composition = null) =>
        new(acclaim,
            availableTypes.Select(t => new GuestTypeSheetRef(new GuestTypeId(t))).ToArray(),
            venueWeights ?? new Dictionary<GuestTypeId, double>(),
            composition ?? EmptyComposition,
            arrivalRateFactor);

    public static readonly CompositionSummary EmptyComposition = new(
        new Dictionary<RoomTypeId, int>(),
        Array.Empty<MenuItemId>(),
        new Dictionary<RoleId, int>());
}
