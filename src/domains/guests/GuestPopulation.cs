namespace TavernIdler.Domains.Guests;
using System.Linq;
using TavernIdler.Domains.Structure;   // ServiceKind, GuestRoomInfo, TraversalGraph
using TavernIdler.Domains.Staffing;    // RoomStaffState
using TavernIdler.Domains.Traits;      // EmittedEffect, BehaviorOutcome
using TavernIdler.Kernel;

// ── DOM-003: the guest simulation aggregate (implements CON-005 over CON-006) ────────────────
// Per Service phase: attraction-weighted trickle arrivals (+ independent per-night VIP rolls),
// capacity admission with a FIFO patience queue, BFS pathing to the nearest room offering each
// agenda want, per-room crowding → satisfaction → payment modifier, trait-effect application, and
// night statistics. All randomness is drawn from the single "guests" stream (CON-015, G4: never
// reseeded here — the orchestrator reseeds per night via the RNG adapter). Nothing here imports the
// engine; movement is game state, interpolation is view state.

public sealed class GuestPopulation : IGuestSimCommands, IGuestView, IGuestPresence
{
    private const int SnapshotSchemaVersion = 1;
    private const string RandomStream = "guests";
    private const int VisibleQueueLimit = 8;             // REQ-010: line shows this many, rest overflow
    private const double BlockedSkipPenalty = -0.2;      // REQ-053

    private enum SimPhase { Prep, Service, Settlement }

    private readonly GuestCatalog _catalog;
    private readonly int _serviceDurationTicks;
    private readonly int _guestTicksPerCell;
    private readonly IStructureAccess _structure;
    private readonly IRoomServiceState _roomServices;
    private readonly ITransactions _transactions;
    private readonly IAttractionContext _attraction;
    private readonly IRandom _random;

    private readonly List<GuestAgent> _agents = new();
    private readonly List<QueueEntry> _queue = new();
    private readonly List<(int Tick, GuestTypeSheet Vip)> _scheduledVips = new();
    private readonly Dictionary<GuestTypeId, bool> _vipVisited = new();

    private SimPhase _phase = SimPhase.Prep;
    private bool _draining;
    private bool _allGoneEmitted;
    private int _serviceElapsed;
    private int _nextGuestId = 1;

    // Per-night statistics (reset at BeginService).
    private int _totalAdmitted;
    private int _totalTurnedAwayQueue;
    private readonly Dictionary<GuestTypeId, int> _admittedByType = new();
    private int _maxConcurrent;
    private double _satisfactionSum;
    private int _satisfactionCount;
    private readonly List<string> _notableEvents = new();

    // Cached structure views, refreshed each tick (they can change mid-night, REQ-098).
    private TraversalGraph _graph;
    private IReadOnlyList<GuestRoomInfo> _activeRooms;

    public GuestPopulation(
        GuestCatalog catalog,
        int serviceDurationTicks,
        int guestTicksPerCell,
        IStructureAccess structure,
        IRoomServiceState roomServices,
        ITransactions transactions,
        IAttractionContext attraction,
        IRandomSource random)
    {
        if (serviceDurationTicks <= 0)
            throw new ArgumentOutOfRangeException(nameof(serviceDurationTicks));
        if (guestTicksPerCell <= 0)
            throw new ArgumentOutOfRangeException(nameof(guestTicksPerCell));

        _catalog = catalog;
        _serviceDurationTicks = serviceDurationTicks;
        _guestTicksPerCell = guestTicksPerCell;
        _structure = structure;
        _roomServices = roomServices;
        _transactions = transactions;
        _attraction = attraction;
        _random = random.GetStream(RandomStream);

        _graph = structure.Graph;
        _activeRooms = structure.ActiveRooms;
        foreach (var t in catalog.Types.Where(t => t.IsVip))
            _vipVisited[t.Id] = false;
    }

    private sealed class QueueEntry
    {
        public required GuestAgent Agent { get; init; }
        public required int Patience { get; set; }
    }

    // ════════════════════════════════════════════════════════════
    //  Commands (CON-005)
    // ════════════════════════════════════════════════════════════

    public IReadOnlyList<IDomainEvent> BeginService()
    {
        var events = new List<IDomainEvent>();

        // Prior-night lodgers check out (REQ-107) before the new night's bookkeeping is reset.
        foreach (var lodger in _agents.Where(a => a.Phase == AgentPhase.Lodging).ToList())
        {
            events.Add(new GuestLeft(lodger.Id, lodger.Sheet.Id, LeaveReason.LodgingCheckout, lodger.Satisfaction));
            _agents.Remove(lodger);
        }

        _phase = SimPhase.Service;
        _draining = false;
        _allGoneEmitted = false;
        _serviceElapsed = 0;
        _scheduledVips.Clear();

        _totalAdmitted = 0;
        _totalTurnedAwayQueue = 0;
        _admittedByType.Clear();
        _maxConcurrent = 0;
        _satisfactionSum = 0;
        _satisfactionCount = 0;
        _notableEvents.Clear();

        RefreshStructure();
        RollVips();
        return events;
    }

    public IReadOnlyList<IDomainEvent> Tick(int ticks)
    {
        if (ticks < 1) throw new ArgumentOutOfRangeException(nameof(ticks));
        var events = new List<IDomainEvent>();
        for (var i = 0; i < ticks; i++) TickOnce(events);
        return events;
    }

    public IReadOnlyList<IDomainEvent> BeginDrain()
    {
        var events = new List<IDomainEvent>();
        _draining = true;

        foreach (var entry in _queue)
        {
            events.Add(new GuestLeftQueue(entry.Agent.Id, QueueLeaveReason.Disbanded));
            _totalTurnedAwayQueue++;
        }
        _queue.Clear();

        CheckAllGuestsGone(events);
        return events;
    }

    public IReadOnlyList<IDomainEvent> EndNight()
    {
        // After settlement: only lodgers remain (all other agents left during drain). Return to Prep so
        // Capture is legal again and the next BeginService can start a fresh night. Lodgers are kept.
        _phase = SimPhase.Prep;
        _draining = false;
        return Array.Empty<IDomainEvent>();
    }

    public IReadOnlyList<IDomainEvent> ApplyEffects(IReadOnlyList<EmittedEffect> effects)
    {
        var events = new List<IDomainEvent>();
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case EmittedEffect.SatisfactionModifierBegan m:
                    foreach (var a in Targets(m.Targets)) a.SatisfactionDrifts[m.EpisodeId] = m.SatisfactionRatePerTick;
                    break;
                case EmittedEffect.SatisfactionModifierEnded e:
                    foreach (var a in _agents) a.SatisfactionDrifts.Remove(e.EpisodeId);
                    break;
                case EmittedEffect.SpendingMultiplierBegan m:
                    foreach (var a in Targets(m.Targets)) a.SpendingEpisodes[m.EpisodeId] = m.Factor;
                    break;
                case EmittedEffect.SpendingMultiplierEnded e:
                    foreach (var a in _agents) a.SpendingEpisodes.Remove(e.EpisodeId);
                    break;
                case EmittedEffect.BehaviorEventTriggered b:
                    ApplyBehavior(b, events);
                    break;
            }
        }
        return events;
    }

    public IReadOnlyList<IDomainEvent> ClearAll()
    {
        // Prestige: everyone leaves, including lodgers (REQ-037). No per-guest events — the run resets.
        _agents.Clear();
        _queue.Clear();
        _scheduledVips.Clear();
        foreach (var id in _vipVisited.Keys.ToList()) _vipVisited[id] = false;
        _phase = SimPhase.Prep;
        _draining = false;
        return Array.Empty<IDomainEvent>();
    }

    public GuestsSnapshot Capture()
    {
        if (_phase == SimPhase.Service)
            throw new InvalidOperationException(
                "GuestsSnapshot.Capture is legal only in Prep or Settlement, not during Service (CON-005).");

        var lodgers = _agents
            .Where(a => a.Phase == AgentPhase.Lodging)
            .Select(a => new LodgerRecord(a.Id, a.Sheet.Id, a.Room!.Value, a.Wallet, a.Satisfaction))
            .ToArray();

        var vipStates = _catalog.Types
            .Where(t => t.IsVip)
            .Select(t => new VipState(t.Id, _vipVisited.TryGetValue(t.Id, out var v) && v))
            .ToArray();

        return new GuestsSnapshot(SnapshotSchemaVersion, lodgers, vipStates, _nextGuestId);
    }

    public void Restore(GuestsSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != SnapshotSchemaVersion)
            throw new NotSupportedException($"Unsupported GuestsSnapshot schema version {snapshot.SchemaVersion}.");

        _agents.Clear();
        _queue.Clear();
        _scheduledVips.Clear();

        foreach (var lodger in snapshot.Lodgers)
        {
            var sheet = SheetFor(lodger.Type);
            _agents.Add(new GuestAgent
            {
                Id = lodger.Id,
                Sheet = sheet,
                IsVip = sheet.IsVip,
                AdmittedThisNight = false,
                Wallet = lodger.WalletRemaining,
                Satisfaction = lodger.Satisfaction,
                Cell = _structure.Entrance,
                Phase = AgentPhase.Lodging,
                Room = lodger.LodgingRoom,
            });
        }

        _vipVisited.Clear();
        foreach (var t in _catalog.Types.Where(t => t.IsVip)) _vipVisited[t.Id] = false;
        foreach (var v in snapshot.VipStates) _vipVisited[v.Vip] = v.VisitedThisNight;

        _nextGuestId = snapshot.NextGuestIdValue;
    }

    // ════════════════════════════════════════════════════════════
    //  Tick pipeline
    // ════════════════════════════════════════════════════════════

    private void TickOnce(List<IDomainEvent> events)
    {
        if (_phase != SimPhase.Service) return;   // no advancement once settled (or before service)

        RefreshStructure();
        InterruptDeactivatedServices(events);
        ExpireBursts();
        ApplyDrift();

        foreach (var agent in _agents.ToList())
            AdvanceAgent(agent, events);

        if (!_draining)
            ProcessArrivals(events);
        AdmitFromQueue(events);
        DecrementQueuePatience(events);

        _serviceElapsed++;
        _maxConcurrent = Math.Max(_maxConcurrent, _agents.Count(a => a.AdmittedThisNight));

        CheckAllGuestsGone(events);
    }

    private void RefreshStructure()
    {
        _graph = _structure.Graph;
        _activeRooms = _structure.ActiveRooms;
    }

    /// A room deactivated mid-service (REQ-098) cannot fulfill the in-progress service: interrupt it
    /// (no WantFulfilled), returning the guest to pursue the want afresh — which then finds no active
    /// provider (BlockReason.NoSuchService) unless another room offers it.
    private void InterruptDeactivatedServices(List<IDomainEvent> events)
    {
        var active = _activeRooms.Select(r => r.Id).ToHashSet();
        foreach (var agent in _agents)
            if (agent.Phase == AgentPhase.Serving && agent.Room is { } room && !active.Contains(room))
            {
                agent.Phase = AgentPhase.Selecting;
                agent.Room = null;
                agent.Consuming = null;
            }
    }

    private void ExpireBursts()
    {
        foreach (var agent in _agents)
            agent.Bursts.RemoveAll(b => b.ExpiryElapsed <= _serviceElapsed);
    }

    private void ApplyDrift()
    {
        foreach (var agent in _agents)
            if (agent.SatisfactionDrifts.Count > 0)
                agent.Satisfaction = Math.Clamp(agent.Satisfaction + agent.SatisfactionDrifts.Values.Sum(), -1.0, 1.0);
    }

    // ════════════════════════════════════════════════════════════
    //  Agent advancement (movement + agenda + service)
    // ════════════════════════════════════════════════════════════

    private void AdvanceAgent(GuestAgent agent, List<IDomainEvent> events)
    {
        if (!_agents.Contains(agent)) return;   // removed earlier this tick (e.g. behavior event)

        switch (agent.Phase)
        {
            case AgentPhase.Serving:
                agent.ServiceRemaining--;
                if (agent.ServiceRemaining <= 0) CompleteService(agent, events);
                break;

            case AgentPhase.Walking:
                StepMovement(agent);
                if (agent.Path.Count == 0)
                {
                    agent.Phase = AgentPhase.Selecting;
                    Engage(agent, events);
                }
                break;

            case AgentPhase.Selecting:
                Engage(agent, events);
                break;

            case AgentPhase.Blocked:
                agent.BlockedWaitRemaining--;
                Engage(agent, events);   // re-check: the block may have cleared
                if (agent.Phase == AgentPhase.Blocked && agent.BlockedWaitRemaining <= 0)
                {
                    agent.Satisfaction = Math.Clamp(agent.Satisfaction + BlockedSkipPenalty, -1.0, 1.0);
                    agent.AgendaIndex++;
                    agent.CurrentBlock = null;
                    agent.Phase = AgentPhase.Selecting;
                    Engage(agent, events);   // pursue the next want (or leave)
                }
                break;

            case AgentPhase.Lodging:
                break;   // parked until BeginService
        }
    }

    private void StepMovement(GuestAgent agent)
    {
        agent.MoveProgress += 1.0 / _guestTicksPerCell;
        while (agent.MoveProgress >= 1.0 && agent.Path.Count > 0)
        {
            agent.Cell = agent.Path[0];
            agent.Path.RemoveAt(0);
            agent.MoveProgress -= 1.0;
        }
        if (agent.Path.Count == 0) agent.MoveProgress = 0.0;
    }

    /// Evaluate the current agenda want: leave when done, else path to / serve at / block on the
    /// nearest room offering the wanted service.
    private void Engage(GuestAgent agent, List<IDomainEvent> events)
    {
        if (agent.AgendaIndex >= agent.Sheet.Agenda.Count)
        {
            LeaveAgent(agent, LeaveReason.AgendaComplete, events);
            return;
        }

        var want = agent.Sheet.Agenda[agent.AgendaIndex];
        var providers = ProvidersFor(want.ServiceId);
        if (providers.Count == 0)
        {
            BlockWant(agent, want.ServiceId, BlockReason.NoSuchService, events);
            return;
        }

        var path = GuestPathfinding.PathToNearestRoom(_graph, agent.Cell, providers);
        if (path is null)
        {
            BlockWant(agent, want.ServiceId, BlockReason.NoSuchService, events);   // no reachable provider
            return;
        }

        if (path.Count > 0)
        {
            agent.Path = path.ToList();
            agent.MoveProgress = 0.0;
            agent.Room = _graph.RoomAtCell[path[^1]];
            agent.Phase = AgentPhase.Walking;
            agent.CurrentBlock = null;
            return;
        }

        // Standing on a provider cell — attempt the service there.
        var room = _graph.RoomAtCell[agent.Cell];
        var info = _activeRooms.First(r => r.Id == room);
        var offering = info.Services.First(s => s.ServiceId == want.ServiceId);
        agent.Room = room;

        if (_roomServices.State(room) == RoomStaffState.Closed)
        {
            BlockWant(agent, want.ServiceId, BlockReason.RoomClosed, events);
            return;
        }
        if (OccupantsOf(room) >= info.Capacity)
        {
            BlockWant(agent, want.ServiceId, BlockReason.RoomFull, events);
            return;
        }

        switch (Charge(agent, offering, want, room))
        {
            case TransactionResult.Completed c:
                agent.Wallet -= c.Paid;
                StartService(agent, room, offering, want, info);
                break;
            case TransactionResult.SoldOut:
                BlockWant(agent, want.ServiceId, BlockReason.SoldOut, events);
                break;
            case TransactionResult.CannotAfford:
                LeaveAgent(agent, LeaveReason.WalletEmpty, events);
                break;
            case TransactionResult.NotOffered:
                BlockWant(agent, want.ServiceId, BlockReason.NoSuchService, events);
                break;
        }
    }

    private void StartService(GuestAgent agent, RoomId room, ServiceOffering offering, GuestAgendaItem want, GuestRoomInfo info)
    {
        agent.Phase = AgentPhase.Serving;
        agent.Room = room;
        agent.ServiceId = offering.ServiceId;
        agent.ServingLodging = offering.Kind == ServiceKind.Lodging;
        agent.Consuming = offering.Kind == ServiceKind.MenuConsumption ? want.MenuItem : null;
        agent.ServiceRemaining = ServiceDuration(offering, room, info);
        agent.CurrentBlock = null;
    }

    private int ServiceDuration(ServiceOffering offering, RoomId room, GuestRoomInfo info)
    {
        // REQ-104: ceil(BaseDurationTicks / (roomSpeedFactor × efficiencyFactor × traitPerkModifiers)).
        // traitPerkModifiers = 1.0 at this scope (G5): no port yet feeds a service-duration modifier.
        var denom = _roomServices.SpeedFactor(room) * info.EfficiencyFactor * 1.0;
        if (denom <= 0.0) return offering.BaseDurationTicks;   // defensive; Closed rooms are blocked upstream
        return Math.Max(1, (int)Math.Ceiling(offering.BaseDurationTicks / denom));
    }

    private void CompleteService(GuestAgent agent, List<IDomainEvent> events)
    {
        var room = agent.Room!.Value;
        var info = _activeRooms.FirstOrDefault(r => r.Id == room);

        // Crowding (REQ-009/103): r = Occupants/Capacity with the served guest INCLUDED (G6). The guest
        // is still in Serving here, so OccupantsOf counts it.
        if (info is not null && info.Capacity > 0)
        {
            var r = (double)OccupantsOf(room) / info.Capacity;
            var crowd = agent.Sheet.Crowding;
            var delta = crowd.Preference switch
            {
                "loves" => crowd.Magnitude * r,
                "hates" => -crowd.Magnitude * r,
                _ => 0.0,
            };
            agent.Satisfaction = Math.Clamp(agent.Satisfaction + delta, -1.0, 1.0);
        }

        events.Add(new WantFulfilled(agent.Id, agent.ServiceId!, room));
        agent.Consuming = null;

        if (agent.ServingLodging)
        {
            agent.Phase = AgentPhase.Lodging;   // stays, keeps occupying the room (REQ-107)
            return;
        }

        agent.AgendaIndex++;
        agent.Phase = AgentPhase.Selecting;
        agent.ServiceId = null;
        // Next want pursued on the following tick's Selecting pass.
    }

    private void BlockWant(GuestAgent agent, string serviceId, BlockReason reason, List<IDomainEvent> events)
    {
        var fresh = agent.Phase != AgentPhase.Blocked;
        if (fresh || agent.CurrentBlock != reason)
            events.Add(new WantBlocked(agent.Id, serviceId, reason));
        if (fresh)
            agent.BlockedWaitRemaining = agent.Sheet.BlockedWaitTicks;
        agent.CurrentBlock = reason;
        agent.Phase = AgentPhase.Blocked;
    }

    private void LeaveAgent(GuestAgent agent, LeaveReason reason, List<IDomainEvent> events)
    {
        events.Add(new GuestLeft(agent.Id, agent.Sheet.Id, reason, agent.Satisfaction));
        if (agent.IsVip && agent.Satisfaction > 0.0)
        {
            events.Add(new VipSatisfied(agent.Sheet.Id, agent.Satisfaction));
            _notableEvents.Add($"VIP {agent.Sheet.Id.Value} left satisfied ({agent.Satisfaction:0.##}).");
        }
        _satisfactionSum += agent.Satisfaction;
        _satisfactionCount++;
        _agents.Remove(agent);
    }

    private TransactionResult Charge(GuestAgent agent, ServiceOffering offering, GuestAgendaItem want, RoomId room)
    {
        // G9: MenuConsumption → MenuPurchase, Lodging → Lodging. RoomEntry/EmployeeService best-effort.
        return offering.Kind switch
        {
            ServiceKind.MenuConsumption when want.MenuItem is null => new TransactionResult.Completed(Money.Zero),
            ServiceKind.MenuConsumption => Execute(agent, TransactionKind.MenuPurchase, want.MenuItem, null, null),
            ServiceKind.Lodging => Execute(agent, TransactionKind.Lodging, null, room, null),
            ServiceKind.RoomEntry when offering.EntryFee is null => new TransactionResult.Completed(Money.Zero),
            ServiceKind.RoomEntry => Execute(agent, TransactionKind.RoomEntryFee, null, room, null),
            ServiceKind.EmployeeService => Execute(agent, TransactionKind.EmployeeService, null, null, offering.ServiceId),
            _ => new TransactionResult.Completed(Money.Zero),
        };
    }

    private TransactionResult Execute(GuestAgent agent, TransactionKind kind, MenuItemId? item, RoomId? room, string? serviceId) =>
        _transactions.Execute(new TransactionRequest(
            agent.Id, kind, item, room, serviceId, agent.Wallet, agent.SatisfactionModifier, agent.SpendingMultiplier));

    // ════════════════════════════════════════════════════════════
    //  Arrivals, admission, queue
    // ════════════════════════════════════════════════════════════

    private void RollVips()
    {
        var inputs = _attraction.Current();
        foreach (var vip in _catalog.Types.Where(t => t.IsVip && t.Vip is not null))
        {
            _vipVisited[vip.Id] = false;
            if (!VipConditionsMet(vip.Vip!, inputs)) continue;
            if (_random.NextDouble() < vip.Vip!.VisitChancePerNight)
            {
                var half = Math.Max(1, _serviceDurationTicks / 2);   // arrive in the first half (CON-005)
                _scheduledVips.Add((_random.NextInt(half), vip));
            }
        }
    }

    private bool VipConditionsMet(VipSpec spec, AttractionInputs inputs) =>
        spec.Conditions.All(c => c.Kind switch
        {
            "lifetimeAcclaimAtLeast" => inputs.LifetimeAcclaim >= (c.Value ?? long.MaxValue),
            "menuHasItem" => c.Id is not null && inputs.Composition.StockedItems.Contains(new MenuItemId(c.Id)),
            "hasRoomType" => c.Id is not null
                && inputs.Composition.ActiveRoomCounts.TryGetValue(new RoomTypeId(c.Id), out var n) && n > 0,
            // venueIs: venue identity is not in the CON-006 attraction inputs at this scope; not gated here.
            "venueIs" => true,
            _ => false,
        });

    private void ProcessArrivals(List<IDomainEvent> events)
    {
        foreach (var (tick, vip) in _scheduledVips.Where(s => s.Tick == _serviceElapsed).ToList())
            SpawnArrival(vip, isVip: true, events);
        _scheduledVips.RemoveAll(s => s.Tick == _serviceElapsed);

        var inputs = _attraction.Current();
        var count = Discretize(ExpectedArrivals(inputs));
        for (var i = 0; i < count; i++)
        {
            var type = PickArrivalType(inputs);
            if (type is not null) SpawnArrival(type, isVip: false, events);
        }
    }

    private void SpawnArrival(GuestTypeSheet sheet, bool isVip, List<IDomainEvent> events)
    {
        var agent = CreateAgent(sheet);
        if (isVip)
        {
            events.Add(new VipVisited(sheet.Id));
            _vipVisited[sheet.Id] = true;
            _notableEvents.Add($"VIP {sheet.Id.Value} visited.");
        }
        AdmitOrQueue(agent, events);
    }

    private GuestAgent CreateAgent(GuestTypeSheet sheet)
    {
        var wallet = sheet.WalletMax > sheet.WalletMin
            ? new Money(sheet.WalletMin.Amount + _random.NextInt((int)(sheet.WalletMax.Amount - sheet.WalletMin.Amount + 1)))
            : sheet.WalletMin;

        return new GuestAgent
        {
            Id = new GuestId(_nextGuestId++),
            Sheet = sheet,
            IsVip = sheet.IsVip,
            AdmittedThisNight = false,
            Wallet = wallet,
            Satisfaction = 0.0,
            Cell = _structure.Entrance,
            Phase = AgentPhase.Selecting,
        };
    }

    private void AdmitOrQueue(GuestAgent agent, List<IDomainEvent> events)
    {
        // Admit directly only when a slot is free AND nobody is already queued (else the newcomer must
        // fall in behind the line to preserve FIFO); otherwise queue. REQ-008/010/018.
        if (_agents.Count < Capacity && _queue.Count == 0)
        {
            Admit(agent, events);
        }
        else
        {
            _queue.Add(new QueueEntry { Agent = agent, Patience = agent.Sheet.QueuePatienceTicks });
            events.Add(new GuestQueued(agent.Id, agent.Sheet.Id));
        }
    }

    private void Admit(GuestAgent agent, List<IDomainEvent> events)
    {
        agent.AdmittedThisNight = true;
        agent.Cell = _structure.Entrance;
        agent.Phase = AgentPhase.Selecting;
        _agents.Add(agent);
        events.Add(new GuestAdmitted(agent.Id, agent.Sheet.Id));
        _totalAdmitted++;
        _admittedByType[agent.Sheet.Id] = (_admittedByType.TryGetValue(agent.Sheet.Id, out var n) ? n : 0) + 1;
    }

    private void AdmitFromQueue(List<IDomainEvent> events)
    {
        while (_agents.Count < Capacity && _queue.Count > 0)
        {
            var entry = _queue[0];
            _queue.RemoveAt(0);
            events.Add(new GuestLeftQueue(entry.Agent.Id, QueueLeaveReason.Admitted));
            Admit(entry.Agent, events);
        }
    }

    private void DecrementQueuePatience(List<IDomainEvent> events)
    {
        foreach (var entry in _queue) entry.Patience--;
        for (var i = _queue.Count - 1; i >= 0; i--)
            if (_queue[i].Patience <= 0)
            {
                events.Add(new GuestLeftQueue(_queue[i].Agent.Id, QueueLeaveReason.PatienceExpired));
                _totalTurnedAwayQueue++;
                _queue.RemoveAt(i);
            }
    }

    private double ExpectedArrivals(AttractionInputs inputs)
    {
        var sum = 0.0;
        foreach (var reference in inputs.AvailableTypes)
        {
            var sheet = _catalog.Types.FirstOrDefault(t => t.Id == reference.Id);
            if (sheet is null || sheet.IsVip) continue;   // VIPs arrive via the independent roll, not the trickle
            sum += EffectiveWeight(sheet, inputs);
        }
        return inputs.ArrivalRateFactor * sum;
    }

    private static double EffectiveWeight(GuestTypeSheet sheet, AttractionInputs inputs)
    {
        double weight = sheet.BaseWeight;
        foreach (var a in sheet.Attractors)
        {
            if (a.Kind == "menuItem" && inputs.Composition.StockedItems.Contains(new MenuItemId(a.Id)))
                weight += a.Weight;
            else if (a.Kind == "roomType"
                && inputs.Composition.ActiveRoomCounts.TryGetValue(new RoomTypeId(a.Id), out var n) && n > 0)
                weight += a.Weight;
        }
        var mult = inputs.VenueWeightMultipliers.TryGetValue(sheet.Id, out var m) ? m : 1.0;
        return weight * mult;
    }

    /// Discretize an expected arrival rate into an integer count whose mean equals the expectation and
    /// which can exceed 1 per tick (G2): the integer part is deterministic, the fractional remainder is
    /// a single Bernoulli draw from the "guests" stream. An integer rate consumes no randomness.
    private int Discretize(double expected)
    {
        if (expected <= 0.0) return 0;
        var whole = Math.Floor(expected);
        var frac = expected - whole;
        var count = (int)whole;
        if (frac > 0.0 && _random.NextDouble() < frac) count++;
        return count;
    }

    private GuestTypeSheet? PickArrivalType(AttractionInputs inputs)
    {
        var candidates = inputs.AvailableTypes
            .Select(reference => _catalog.Types.FirstOrDefault(t => t.Id == reference.Id))
            .Where(sheet => sheet is not null && !sheet.IsVip)
            .Select(sheet => (Sheet: sheet!, Weight: EffectiveWeight(sheet!, inputs)))
            .Where(c => c.Weight > 0.0)
            .ToList();

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0].Sheet;   // single provider ⇒ no draw

        var total = candidates.Sum(c => c.Weight);
        var roll = _random.NextDouble() * total;
        var cumulative = 0.0;
        foreach (var c in candidates)
        {
            cumulative += c.Weight;
            if (roll < cumulative) return c.Sheet;
        }
        return candidates[^1].Sheet;
    }

    // ════════════════════════════════════════════════════════════
    //  Effects, terminal sequence, helpers
    // ════════════════════════════════════════════════════════════

    private IEnumerable<GuestAgent> Targets(IReadOnlyList<GuestId> ids)
    {
        // G12: ignore effects targeting unknown/departed guests.
        foreach (var id in ids)
        {
            var agent = _agents.FirstOrDefault(a => a.Id == id);
            if (agent is not null) yield return agent;
        }
    }

    private void ApplyBehavior(EmittedEffect.BehaviorEventTriggered b, List<IDomainEvent> events)
    {
        switch (b.Outcome)
        {
            case BehaviorOutcome.GuestsLeave:
                foreach (var a in Targets(b.Targets).ToList())
                    LeaveAgent(a, LeaveReason.BehaviorEvent, events);
                break;
            case BehaviorOutcome.SpendingBurst burst:
                foreach (var a in Targets(b.Targets))
                    a.Bursts.Add(new Burst(burst.Factor, _serviceElapsed + burst.DurationTicks));
                break;
            case BehaviorOutcome.SatisfactionShock shock:
                foreach (var a in Targets(b.Targets))
                    a.Satisfaction = Math.Clamp(a.Satisfaction + shock.Delta, -1.0, 1.0);
                break;
        }
    }

    private void CheckAllGuestsGone(List<IDomainEvent> events)
    {
        if (_allGoneEmitted || !_draining || _phase != SimPhase.Service) return;
        if (_agents.Any(a => a.Phase != AgentPhase.Lodging) || _queue.Count > 0) return;

        _allGoneEmitted = true;
        _phase = SimPhase.Settlement;
        events.Add(new AllGuestsGone());
        events.Add(new NightStatsFinal(BuildStats()));
    }

    private NightGuestStats BuildStats() =>
        new(_totalAdmitted,
            _totalTurnedAwayQueue,
            new Dictionary<GuestTypeId, int>(_admittedByType),
            _satisfactionCount > 0 ? _satisfactionSum / _satisfactionCount : 0.0,
            _maxConcurrent,
            _notableEvents.ToArray());

    private int Capacity => _structure.TotalGuestCapacity;

    private IReadOnlySet<RoomId> ProvidersFor(string serviceId) =>
        _activeRooms.Where(r => r.Services.Any(s => s.ServiceId == serviceId)).Select(r => r.Id).ToHashSet();

    private int OccupantsOf(RoomId room) => _agents.Count(a => a.OccupiedRoom == room);

    private GuestTypeSheet SheetFor(GuestTypeId type) =>
        _catalog.Types.FirstOrDefault(t => t.Id == type)
        ?? new GuestTypeSheet(type, type.Value, false, "", 0,
            Array.Empty<GuestAttractor>(), new CrowdingSpec("neutral", 0.0), 0, 0,
            Array.Empty<GuestAgendaItem>(), Money.Zero, Money.Zero, Array.Empty<TraitId>(), null);

    // ════════════════════════════════════════════════════════════
    //  Views (CON-005 pull model)
    // ════════════════════════════════════════════════════════════

    public IReadOnlyList<GuestAgentView> Agents => _agents.Select(a => a.ToView()).ToArray();

    public QueueView Queue =>
        new(_queue.Take(VisibleQueueLimit)
                .Select(e => new QueuedGuestView(e.Agent.Id, e.Agent.Sheet.Id, e.Patience)).ToArray(),
            Math.Max(0, _queue.Count - VisibleQueueLimit));

    public IReadOnlyDictionary<RoomId, RoomOccupancy> Occupancy =>
        _structure.ActiveRooms.ToDictionary(r => r.Id, r => new RoomOccupancy(OccupantsOf(r.Id), r.Capacity));

    public IReadOnlyList<GuestPresenceEntry> CurrentPresence() =>
        _agents.Select(a => new GuestPresenceEntry(a.Id, a.Sheet.Id, a.OccupiedRoom, a.Sheet.Traits, a.Consuming)).ToArray();
}
