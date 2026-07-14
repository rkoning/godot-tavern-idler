namespace TavernIdler.Domains.Structure;

using TavernIdler.Domains.Cycle;
using TavernIdler.Kernel;

/// <summary>Per-cell circulation prices (REQ-099); supplied by content/config, not by a contract.</summary>
public sealed record CirculationCosts(Money Corridor, Money Stair);

/// <summary>
/// DOM-001 aggregate root: the physical tavern. Owns the grid, rooms, circulation cells and the
/// derived traversal graph + active states. Implements CON-003 (commands, queries, snapshot,
/// events) over the CON-004 driven ports (ledger, lot, room content) and the CON-002 phase gate.
///
/// Pure C# — no engine types.
/// </summary>
public sealed class Tavern : IStructureCommands, IStructureQueries, IStructureSnapshot
{
    private readonly ICycleQueries _cycle;
    private readonly ILotConstraints _lot;
    private readonly IRoomContent _content;
    private readonly IBuildLedger _ledger;
    private readonly IReadOnlyDictionary<RoomTypeId, RoomTypeSheet> _catalog;
    private readonly CirculationCosts _circulationCosts;

    private readonly List<RoomState> _rooms = new();
    private readonly Dictionary<CellCoord, CirculationState> _circulation = new();
    private int _nextRoomId = 1;
    private int _version;
    private TraversalGraph _graph;

    /// <param name="fullCatalog">
    /// Every room type the game knows, unlocked or not. Needed to tell <c>UnknownRoomType</c> from
    /// <c>RoomTypeLocked</c> (TKT-003 clarification); the currently-buildable subset comes from
    /// <paramref name="content"/> and is re-read per command.
    /// </param>
    public Tavern(
        ICycleQueries cycle,
        ILotConstraints lot,
        IRoomContent content,
        IBuildLedger ledger,
        IReadOnlyList<RoomTypeSheet> fullCatalog,
        CirculationCosts circulationCosts)
    {
        _cycle = cycle;
        _lot = lot;
        _content = content;
        _ledger = ledger;
        _catalog = fullCatalog.ToDictionary(s => s.Id);
        _circulationCosts = circulationCosts;
        _graph = BuildGraph(_rooms, _circulation, _lot.Lot, version: 0);
    }

    // ════════════════════════════════════════════════════════════
    //  Queries (CON-003 IStructureQueries)
    // ════════════════════════════════════════════════════════════

    public TraversalGraph Graph => _graph;

    public CellCoord Entrance => _lot.Entrance;

    public IReadOnlyList<RoomInfo> Rooms => _rooms.Select(ToInfo).ToList();

    public RoomInfo GetRoom(RoomId id)
    {
        var room = _rooms.FirstOrDefault(r => r.Id == id)
            ?? throw new KeyNotFoundException($"No room with id {id.Value}.");
        return ToInfo(room);
    }

    public int TotalGuestCapacity => _rooms.Where(r => r.Active).Sum(CapacityOf);

    public Money NightlyUpkeepBill =>
        _rooms.Aggregate(Money.Zero, (bill, r) => bill + Sheet(r).NightlyUpkeep);

    public StructureMetrics Metrics
    {
        get
        {
            var builtCells = BuiltCells(_rooms, _circulation).ToList();
            return new StructureMetrics(
                MaxHeightCells: builtCells.Count == 0 ? 0 : builtCells.Max(c => c.Y) + 1,
                RoomCount: _rooms.Count,
                RoomCountsByType: _rooms.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count()),
                CirculationCellCount: _circulation.Count);
        }
    }

    public IReadOnlyList<RoomTypeSheet> AvailableRoomTypes => _content.AvailableRoomTypes();

    // ════════════════════════════════════════════════════════════
    //  Commands (CON-003 IStructureCommands)
    // ════════════════════════════════════════════════════════════

    public Outcome<PlacementError> PlaceRoom(RoomTypeId type, GridRect footprint)
    {
        // Validation order is normative (CON-003): first failure wins.
        if (!IsPrep) return Fail(new PlacementError.WrongPhase());
        if (!_catalog.TryGetValue(type, out var sheet)) return Fail(new PlacementError.UnknownRoomType());
        if (!IsAvailable(type)) return Fail(new PlacementError.RoomTypeLocked());
        if (footprint.Area < sheet.MinArea || footprint.Area > sheet.MaxArea)
            return Fail(new PlacementError.FootprintOutOfRange(sheet.MinArea, sheet.MaxArea));
        if (!InLot(footprint)) return Fail(new PlacementError.OutOfLot());
        if (Cells(footprint).Any(IsBuilt)) return Fail(new PlacementError.Overlap());
        if (!TerrainSatisfied(sheet, footprint)) return Fail(new PlacementError.TerrainRequired(sheet.Id));

        var candidate = new RoomState(new RoomId(_nextRoomId), type, tier: 1, footprint, active: true, Money.Zero);
        var trial = _rooms.Append(candidate).ToList();
        if (!Grounded(trial, _circulation).Contains(candidate.Id))
            return Fail(new PlacementError.Unsupported());
        if (!Connected(trial, _circulation).Contains(candidate.Id))
            return Fail(new PlacementError.Disconnected());

        switch (_ledger.TryCharge(sheet.BuildCost, BuildCostKind.Room))
        {
            case ChargeResult.InsufficientGold insufficient:
                return Fail(new PlacementError.InsufficientGold(insufficient.Required, insufficient.Available));
            case ChargeResult.Charged charged:
                return Mutate(events =>
                {
                    candidate.PaidTotal = charged.AmountCharged;
                    _rooms.Add(candidate);
                    _nextRoomId++;
                    events.Add(new RoomPlaced(candidate.Id, candidate.Type, candidate.Footprint));
                });
            default:
                throw new InvalidOperationException("Unhandled ChargeResult.");
        }
    }

    public Outcome<PlacementError> DemolishRoom(RoomId room)
    {
        if (!IsPrep) return Fail(new PlacementError.WrongPhase());
        var target = Find(room);
        if (target is null) return Fail(new PlacementError.UnknownRoom());

        return Mutate(events =>
        {
            _rooms.Remove(target);
            _ledger.Refund(target.PaidTotal);                       // REQ-073/100: full refund
            events.Add(new RoomDemolished(target.Id, target.PaidTotal));
        });
    }

    public Outcome<PlacementError> MoveRoom(RoomId room, GridRect newFootprint)
    {
        if (!IsPrep) return Fail(new PlacementError.WrongPhase());
        var target = Find(room);
        if (target is null) return Fail(new PlacementError.UnknownRoom());

        var sheet = Sheet(target);
        if (newFootprint.Area < sheet.MinArea || newFootprint.Area > sheet.MaxArea)
            return Fail(new PlacementError.FootprintOutOfRange(sheet.MinArea, sheet.MaxArea));
        if (!InLot(newFootprint)) return Fail(new PlacementError.OutOfLot());

        // REQ-072: a move lands either on free cells or exactly onto one other room — the swap case
        // (that room takes the mover's vacated footprint). Anything else (a partial overlap, or
        // circulation in the way) is an Overlap.
        var targetCells = Cells(newFootprint).ToHashSet();
        var moverCells = Cells(target.Footprint).ToHashSet();
        if (targetCells.Any(c => _circulation.ContainsKey(c) && !moverCells.Contains(c)))
            return Fail(new PlacementError.Overlap());

        var occupants = _rooms
            .Where(r => r.Id != target.Id && Cells(r.Footprint).Any(targetCells.Contains))
            .ToList();
        RoomState? partner = null;
        if (occupants.Count == 1
            && occupants[0].Footprint == newFootprint
            && occupants[0].Footprint.Width == target.Footprint.Width
            && occupants[0].Footprint.Height == target.Footprint.Height)
        {
            partner = occupants[0];
        }
        else if (occupants.Count > 0)
        {
            return Fail(new PlacementError.Overlap());
        }

        // The mover must come to rest on the structure (REQ-072). Support/connectivity broken for
        // OTHER rooms is permitted — they go inactive instead (REQ-098).
        var trial = _rooms.Select(r =>
            r.Id == target.Id ? r.With(newFootprint)
            : partner is not null && r.Id == partner.Id ? r.With(target.Footprint)
            : r).ToList();
        if (!Grounded(trial, _circulation).Contains(target.Id))
            return Fail(new PlacementError.NotOnExistingStructure());

        var from = target.Footprint;
        return Mutate(events =>
        {
            target.Footprint = newFootprint;
            events.Add(new RoomMoved(target.Id, from, newFootprint));
            if (partner is not null)
            {
                var partnerFrom = partner.Footprint;
                partner.Footprint = from;
                events.Add(new RoomMoved(partner.Id, partnerFrom, from));
            }
        });
    }

    public Outcome<PlacementError> UpgradeRoom(RoomId room)
    {
        if (!IsPrep) return Fail(new PlacementError.WrongPhase());
        var target = Find(room);
        if (target is null) return Fail(new PlacementError.UnknownRoom());

        var sheet = Sheet(target);
        if (target.Tier >= sheet.Tiers.Count) return Fail(new PlacementError.MaxTierReached());

        var cost = sheet.Tiers[target.Tier].UpgradeCost;            // Tiers[i] describes tier i+1
        switch (_ledger.TryCharge(cost, BuildCostKind.Upgrade))
        {
            case ChargeResult.InsufficientGold insufficient:
                return Fail(new PlacementError.InsufficientGold(insufficient.Required, insufficient.Available));
            case ChargeResult.Charged charged:
                return Mutate(events =>
                {
                    target.Tier++;                                   // REQ-100: in place, footprint unchanged
                    target.PaidTotal += charged.AmountCharged;
                    events.Add(new RoomUpgraded(target.Id, target.Tier));
                });
            default:
                throw new InvalidOperationException("Unhandled ChargeResult.");
        }
    }

    public Outcome<PlacementError> BuildCirculation(CirculationKind kind, CellCoord cell)
    {
        if (!IsPrep) return Fail(new PlacementError.WrongPhase());
        if (!_lot.Lot.Contains(cell)) return Fail(new PlacementError.OutOfLot());
        if (IsBuilt(cell)) return Fail(new PlacementError.CellNotEmpty());

        var cost = kind == CirculationKind.Stair ? _circulationCosts.Stair : _circulationCosts.Corridor;
        switch (_ledger.TryCharge(cost, BuildCostKind.Circulation))
        {
            case ChargeResult.InsufficientGold insufficient:
                return Fail(new PlacementError.InsufficientGold(insufficient.Required, insufficient.Available));
            case ChargeResult.Charged charged:
                return Mutate(events =>
                {
                    _circulation[cell] = new CirculationState(kind, charged.AmountCharged);
                    events.Add(new CirculationBuilt(kind, cell));
                });
            default:
                throw new InvalidOperationException("Unhandled ChargeResult.");
        }
    }

    public Outcome<PlacementError> DemolishCirculation(CellCoord cell)
    {
        if (!IsPrep) return Fail(new PlacementError.WrongPhase());
        if (!_circulation.TryGetValue(cell, out var built)) return Fail(new PlacementError.NothingAtCell());

        return Mutate(events =>
        {
            _circulation.Remove(cell);
            _ledger.Refund(built.Paid);                              // REQ-099: full refund
            events.Add(new CirculationDemolished(cell, built.Paid));
        });
    }

    /// Prestige (REQ-037): clears the build, refunds nothing. Not phase-gated.
    public IReadOnlyList<IDomainEvent> ResetAll()
    {
        var events = new List<IDomainEvent>();
        _rooms.Clear();
        _circulation.Clear();
        events.Add(new StructureReset());
        Recompute();                                                 // room ids are never reused
        events.Add(new StructureChanged(_graph.Version));
        return events;
    }

    // ════════════════════════════════════════════════════════════
    //  Snapshot (CON-003 IStructureSnapshot; payload shape owned here per CON-017)
    // ════════════════════════════════════════════════════════════

    public StructureSnapshot Capture() =>
        new(StructureSnapshotJson.SchemaVersion,
            StructureSnapshotJson.Serialize(_rooms, _circulation, _nextRoomId, _version));

    public void Restore(StructureSnapshot snapshot)
    {
        var payload = StructureSnapshotJson.Deserialize(snapshot);
        _rooms.Clear();
        _rooms.AddRange(payload.Rooms);
        _circulation.Clear();
        foreach (var (cell, state) in payload.Circulation) _circulation[cell] = state;
        _nextRoomId = payload.NextRoomId;
        _version = Math.Max(_version, payload.GraphVersion);         // versions only ever increase
        Recompute();
    }

    // ════════════════════════════════════════════════════════════
    //  Internals
    // ════════════════════════════════════════════════════════════

    private bool IsPrep => _cycle.Phase == Phase.Prep;

    private static Outcome<PlacementError> Fail(PlacementError error) =>
        new Outcome<PlacementError>.Failure(error);

    private RoomState? Find(RoomId id) => _rooms.FirstOrDefault(r => r.Id == id);

    private RoomTypeSheet Sheet(RoomState room) => _catalog[room.Type];

    private bool IsAvailable(RoomTypeId type) => _content.AvailableRoomTypes().Any(s => s.Id == type);

    private bool InLot(GridRect footprint) => Cells(footprint).All(_lot.Lot.Contains);

    private bool IsBuilt(CellCoord cell) =>
        _circulation.ContainsKey(cell) || _rooms.Any(r => r.Footprint.Contains(cell));

    /// REQ-083(a): the footprint must cover a terrain cell that enables this room type. The venue
    /// names the enabled type on the feature; the sheet's <c>RequiresTerrainFeature</c> is the
    /// feature key, so either naming is accepted.
    private bool TerrainSatisfied(RoomTypeSheet sheet, GridRect footprint)
    {
        if (sheet.RequiresTerrainFeature is not { } required) return true;
        return _lot.Terrain.Any(f =>
            footprint.Contains(f.Cell)
            && f.Effect is TerrainEffect.EnablesRoomType enables
            && (enables.Room == sheet.Id || enables.Room == required));
    }

    /// Applies a validated mutation, then recomputes the graph + active states, emitting the
    /// (de)activation deltas and the closing StructureChanged (CON-003 event ordering).
    private Outcome<PlacementError> Mutate(Action<List<IDomainEvent>> apply)
    {
        var before = _rooms.ToDictionary(r => r.Id, r => r.Active);
        var events = new List<IDomainEvent>();
        apply(events);
        Recompute();

        foreach (var room in _rooms)
        {
            if (!before.TryGetValue(room.Id, out var wasActive) || wasActive == room.Active) continue;
            events.Add(room.Active
                ? new RoomReactivated(room.Id)                      // REQ-098
                : new RoomDeactivated(room.Id));
        }

        events.Add(new StructureChanged(_graph.Version));
        return new Outcome<PlacementError>.Success(events);
    }

    /// Rebuilds the traversal graph (version bumped) and re-derives every room's active flag:
    /// a room is active iff it is grounded (REQ-067) and reachable from the entrance (REQ-068).
    private void Recompute()
    {
        _version++;
        _graph = BuildGraph(_rooms, _circulation, _lot.Lot, _version);
        var active = Grounded(_rooms, _circulation);
        active.IntersectWith(Connected(_rooms, _circulation));
        foreach (var room in _rooms) room.Active = active.Contains(room.Id);
    }

    private RoomInfo ToInfo(RoomState room)
    {
        var sheet = Sheet(room);
        return new RoomInfo(
            room.Id,
            room.Type,
            room.Tier,
            room.Footprint,
            room.Active,
            CapacityOf(room),
            EfficiencyOf(sheet, room.Footprint),
            room.PaidTotal,
            sheet.NightlyUpkeep,
            sheet.Traits,
            sheet.Broadcaster,
            sheet.Services,
            StaffingOf(sheet, room.Tier));
    }

    /// CON-003: floor(Area × CapacityPerCell), tier-modified.
    private int CapacityOf(RoomState room)
    {
        var sheet = Sheet(room);
        var perCell = sheet.CapacityPerCell * sheet.Tiers[room.Tier - 1].CapacityMultiplier;
        return (int)Math.Floor(room.Footprint.Area * perCell);
    }

    /// CON-003 (REQ-069): 1.0 up to the optimum, then linear falloff with a floor.
    private static double EfficiencyOf(RoomTypeSheet sheet, GridRect footprint) =>
        footprint.Area <= sheet.OptimumArea
            ? 1.0
            : Math.Max(sheet.MinEfficiency,
                       1.0 - sheet.EfficiencyFalloffPerCell * (footprint.Area - sheet.OptimumArea));

    /// REQ-057/071: a tier's staffing-max overrides replace the base requirement for that role.
    private static StaffRequirements StaffingOf(RoomTypeSheet sheet, int tier)
    {
        var overrides = sheet.Tiers[tier - 1].StaffingMaxOverrides;
        if (overrides.Count == 0) return sheet.Staffing;
        var roles = sheet.Staffing.Roles
            .Select(role => overrides.FirstOrDefault(o => o.Role == role.Role) ?? role)
            .ToList();
        return new StaffRequirements(roles);
    }

    // ── grid math ───────────────────────────────────────────────

    private static IEnumerable<CellCoord> Cells(GridRect rect)
    {
        for (var x = rect.X; x < rect.X + rect.Width; x++)
            for (var y = rect.Y; y < rect.Y + rect.Height; y++)
                yield return new CellCoord(x, y);
    }

    private static IEnumerable<CellCoord> BuiltCells(
        IEnumerable<RoomState> rooms, IReadOnlyDictionary<CellCoord, CirculationState> circulation) =>
        rooms.SelectMany(r => Cells(r.Footprint)).Concat(circulation.Keys);

    private static TraversalGraph BuildGraph(
        IReadOnlyList<RoomState> rooms,
        IReadOnlyDictionary<CellCoord, CirculationState> circulation,
        GridRect lot,
        int version)
    {
        var roomAtCell = new Dictionary<CellCoord, RoomId>();
        foreach (var room in rooms)
            foreach (var cell in Cells(room.Footprint))
                roomAtCell[cell] = room.Id;

        var walkable = new HashSet<CellCoord>(roomAtCell.Keys);
        walkable.UnionWith(circulation.Keys);

        // REQ-097: unbuilt ground-level lot cells are traversable exterior ground.
        for (var x = lot.X; x < lot.X + lot.Width; x++)
        {
            var ground = new CellCoord(x, 0);
            if (!roomAtCell.ContainsKey(ground) && !circulation.ContainsKey(ground)) walkable.Add(ground);
        }

        var stairs = circulation
            .Where(kv => kv.Value.Kind == CirculationKind.Stair)
            .Select(kv => kv.Key)
            .ToHashSet();

        return new TraversalGraph(version, walkable, roomAtCell, stairs);
    }

    /// REQ-067/099: rooms rest on ground or on built cells beneath — support chains to the ground,
    /// and circulation cells both need and provide it.
    private static HashSet<RoomId> Grounded(
        IReadOnlyList<RoomState> rooms, IReadOnlyDictionary<CellCoord, CirculationState> circulation)
    {
        var groundedRooms = new HashSet<RoomId>();
        var groundedCells = new HashSet<CellCoord>();
        bool progressed;
        do
        {
            progressed = false;

            foreach (var cell in circulation.Keys)
            {
                if (groundedCells.Contains(cell)) continue;
                if (cell.Y == 0 || groundedCells.Contains(new CellCoord(cell.X, cell.Y - 1)))
                {
                    groundedCells.Add(cell);
                    progressed = true;
                }
            }

            foreach (var room in rooms)
            {
                if (groundedRooms.Contains(room.Id)) continue;
                var bottom = room.Footprint.Y;
                var supported = Enumerable
                    .Range(room.Footprint.X, room.Footprint.Width)
                    .All(x => bottom == 0 || groundedCells.Contains(new CellCoord(x, bottom - 1)));
                if (!supported) continue;

                groundedRooms.Add(room.Id);
                groundedCells.UnionWith(Cells(room.Footprint));
                progressed = true;
            }
        }
        while (progressed);

        return groundedRooms;
    }

    /// REQ-068: reachable from the entrance over the traversal graph (rooms, circulation and
    /// exterior ground are all traversable; vertical movement only via stairs).
    private HashSet<RoomId> Connected(
        IReadOnlyList<RoomState> rooms, IReadOnlyDictionary<CellCoord, CirculationState> circulation)
    {
        var graph = BuildGraph(rooms, circulation, _lot.Lot, version: 0);
        var reached = new HashSet<CellCoord>();
        var frontier = new Queue<CellCoord>();
        if (graph.WalkableCells.Contains(_lot.Entrance))
        {
            reached.Add(_lot.Entrance);
            frontier.Enqueue(_lot.Entrance);
        }

        while (frontier.Count > 0)
        {
            foreach (var next in graph.Neighbors(frontier.Dequeue()))
                if (reached.Add(next))
                    frontier.Enqueue(next);
        }

        return rooms
            .Where(r => Cells(r.Footprint).Any(reached.Contains))
            .Select(r => r.Id)
            .ToHashSet();
    }

    // ── mutable internal state ──────────────────────────────────

    internal sealed class RoomState(RoomId id, RoomTypeId type, int tier, GridRect footprint, bool active, Money paidTotal)
    {
        public RoomId Id { get; } = id;
        public RoomTypeId Type { get; } = type;
        public int Tier { get; set; } = tier;
        public GridRect Footprint { get; set; } = footprint;
        public bool Active { get; set; } = active;
        public Money PaidTotal { get; set; } = paidTotal;

        public RoomState With(GridRect footprint) => new(Id, Type, Tier, footprint, Active, PaidTotal);
    }

    internal sealed record CirculationState(CirculationKind Kind, Money Paid);
}
