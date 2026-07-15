namespace TavernIdler.Domains.Staffing;

using TavernIdler.Domains.Cycle;
using TavernIdler.Domains.Structure;   // StaffRequirements / RoleRequirement (CON-003)
using TavernIdler.Kernel;

/// <summary>
/// DOM-005 aggregate root: the tavern's staff roster (the "Roster" aggregate of TKT-012; named
/// <c>StaffRoster</c> because CON-009 exposes a query property named <c>Roster</c>, which a type
/// named <c>Roster</c> could not declare). Implements CON-009 (commands, queries, snapshot, events)
/// over the CON-010 driven ports (<see cref="IRoomRequirements"/>, <see cref="IHireUnlocks"/>) and
/// the CON-002 phase gate (<see cref="ICycleQueries"/>).
///
/// Owns hiring (ordinary + named), prep-gated assignment with per-role maxima, the
/// Open/Degraded/Closed room-state evaluation (refusing staff excluded), speed factors, orphaning
/// on room removal, and the wage bill. Pure C# — no engine types.
/// </summary>
public sealed class StaffRoster : IStaffingCommands, IStaffingQueries
{
    private readonly ICycleQueries _cycle;
    private readonly IRoomRequirements _rooms;
    private readonly IHireUnlocks _unlocks;
    private readonly RosterCatalog _catalog;
    private readonly IReadOnlyDictionary<RoleId, StaffRoleDef> _roleDefs;
    private readonly IReadOnlyDictionary<NamedHireId, NamedHireDef> _namedDefs;

    private readonly List<Employee> _employees = new();
    // Room-state cache: recomputed only at EvaluateRoomStates / OnRoom* calls, never per query.
    private readonly Dictionary<RoomId, RoomEval> _stateCache = new();
    private int _nextEmployeeId = 1;

    public StaffRoster(ICycleQueries cycle, IRoomRequirements rooms, IHireUnlocks unlocks, RosterCatalog catalog)
    {
        _cycle = cycle;
        _rooms = rooms;
        _unlocks = unlocks;
        _catalog = catalog;
        _roleDefs = catalog.Roles.ToDictionary(r => r.Id);
        _namedDefs = catalog.NamedHires.ToDictionary(n => n.Id);
    }

    // ════════════════════════════════════════════════════════════
    //  Commands (CON-009 IStaffingCommands)
    // ════════════════════════════════════════════════════════════

    public Outcome<StaffingError> Hire(RoleId role)                          // REQ-062
    {
        if (!IsPrep) return Fail(StaffingError.WrongPhase);
        if (!_roleDefs.TryGetValue(role, out var def)) return Fail(StaffingError.UnknownRole);

        var emp = new Employee(NextId(), role, namedHire: null, def.DisplayName, def.Wage, def.Traits);
        _employees.Add(emp);
        return Success(new EmployeeHired(emp.Id, role, null));
    }

    public Outcome<StaffingError> HireNamed(NamedHireId hire)                // REQ-063
    {
        if (!IsPrep) return Fail(StaffingError.WrongPhase);
        if (!_namedDefs.TryGetValue(hire, out var def)) return Fail(StaffingError.UnknownNamedHire);
        if (!_unlocks.UnlockedNamedHires().Contains(hire)) return Fail(StaffingError.NamedHireLocked);
        if (_employees.Any(e => e.NamedHire == hire)) return Fail(StaffingError.NamedHireAlreadyEmployed);

        var emp = new Employee(NextId(), def.Role, hire, def.DisplayName, def.Wage, def.Traits);
        _employees.Add(emp);
        return Success(new EmployeeHired(emp.Id, def.Role, hire));
    }

    public Outcome<StaffingError> Dismiss(EmployeeId employee)              // REQ-108
    {
        if (!IsPrep) return Fail(StaffingError.WrongPhase);
        var emp = Find(employee);
        if (emp is null) return Fail(StaffingError.UnknownEmployee);

        // Removes from roster immediately → wage accrual stops. Any arrears remain payable via
        // CON-007 (dismissal does not erase debt); that is the Economy domain's concern, not ours.
        _employees.Remove(emp);
        return Success(new EmployeeDismissed(employee));
    }

    public Outcome<StaffingError> Assign(EmployeeId employee, RoomId room)  // REQ-056/059
    {
        if (!IsPrep) return Fail(StaffingError.WrongPhase);
        var emp = Find(employee);
        if (emp is null) return Fail(StaffingError.UnknownEmployee);

        StaffRequirements reqs;
        try { reqs = _rooms.Get(room); }
        catch (KeyNotFoundException) { return Fail(StaffingError.UnknownRoom); }

        var roleReq = reqs.Roles.FirstOrDefault(r => r.Role == emp.Role);
        if (roleReq is null) return Fail(StaffingError.RoleNotAcceptedByRoom);

        // Maxima count every current occupant of the role (refusing or not — refusal frees no slot),
        // excluding the mover itself so re-assign within the same room is idempotent on occupancy.
        var occupancy = _employees.Count(e => e.Id != emp.Id && e.AssignedRoom == room && e.Role == emp.Role);
        if (occupancy >= roleReq.Max) return Fail(StaffingError.RoomAtStaffingMax);

        var events = new List<IDomainEvent>();
        if (emp.AssignedRoom is not null)                                    // implicit reassign: one event pair
            events.Add(new EmployeeUnassigned(emp.Id));
        emp.AssignedRoom = room;
        events.Add(new EmployeeAssigned(emp.Id, room));
        return new Outcome<StaffingError>.Success(events);
    }

    public Outcome<StaffingError> Unassign(EmployeeId employee)
    {
        if (!IsPrep) return Fail(StaffingError.WrongPhase);
        var emp = Find(employee);
        if (emp is null) return Fail(StaffingError.UnknownEmployee);
        if (emp.AssignedRoom is null) return Fail(StaffingError.NotAssigned);

        emp.AssignedRoom = null;
        return Success(new EmployeeUnassigned(employee));
    }

    public IReadOnlyList<IDomainEvent> EvaluateRoomStates()                  // at ServiceBegan (REQ-058)
    {
        var events = new List<IDomainEvent>();
        foreach (var room in _rooms.RoomsWithRequirements())
            events.AddRange(RecomputeRoom(room));
        return events;
    }

    public IReadOnlyList<IDomainEvent> OnRoomRemoved(RoomId room)            // REQ-109 (demolish/move)
    {
        var events = new List<IDomainEvent>();
        foreach (var emp in _employees.Where(e => e.AssignedRoom == room))
        {
            events.Add(new EmployeeOrphaned(emp.Id, room));                  // keeps wages (still on roster)
            emp.AssignedRoom = null;
        }
        _stateCache.Remove(room);                                           // the room no longer exists
        return events;
    }

    public IReadOnlyList<IDomainEvent> OnRoomDeactivated(RoomId room)        // REQ-098 interaction
    {
        // A deactivated room may later reactivate (REQ-098), so its staff are NOT orphaned — that
        // would be irreversible. CON-009 only asks this to re-compute the single room's staff state,
        // which is unchanged by (de)activation, so this is effectively a cache refresh.
        return RecomputeRoom(room);
    }

    public IReadOnlyList<IDomainEvent> SetRefusals(IReadOnlyList<EmployeeId> employees, bool refusing) // REQ-028
    {
        var targets = employees.ToHashSet();
        foreach (var emp in _employees.Where(e => targets.Contains(e.Id)))
            emp.Refusing = refusing;
        // No refusal event exists in CON-009, and room-state is recomputed only by EvaluateRoomStates
        // / OnRoom* — not here — so this reaction emits nothing.
        return Array.Empty<IDomainEvent>();
    }

    public IReadOnlyList<IDomainEvent> ResetAll()                           // prestige (REQ-037)
    {
        _employees.Clear();
        _stateCache.Clear();
        // _nextEmployeeId is deliberately NOT reset: employee ids are never reused across a save.
        return new IDomainEvent[] { new StaffReset() };
    }

    // ════════════════════════════════════════════════════════════
    //  Snapshot (CON-009 IStaffingCommands; payload shape owned here per CON-017)
    // ════════════════════════════════════════════════════════════

    public StaffingSnapshot Capture()
    {
        if (_cycle.Phase == Phase.Service)
            throw new InvalidOperationException("Staffing snapshot is only legal in Prep or Settlement.");
        return new StaffingSnapshot(StaffingSnapshotJson.SchemaVersion,
            StaffingSnapshotJson.Serialize(_employees, _nextEmployeeId));
    }

    public void Restore(StaffingSnapshot snapshot)
    {
        var restored = StaffingSnapshotJson.Deserialize(snapshot);
        _employees.Clear();
        _employees.AddRange(restored.Employees.Select(Rebuild));
        _nextEmployeeId = restored.NextEmployeeId;
        _stateCache.Clear();
    }

    // ════════════════════════════════════════════════════════════
    //  Queries (CON-009 IStaffingQueries)
    // ════════════════════════════════════════════════════════════

    public IReadOnlyList<EmployeeInfo> Roster => _employees.Select(ToInfo).ToList();

    public IReadOnlyList<WageLineView> WageBill() =>                        // REQ-019/064 input to CON-007
        _employees.Select(e => new WageLineView(e.Id, e.Role, e.Wage)).ToList();

    public RoomStaffState State(RoomId room) => Evaluated(room).State;      // rooms with requirements only

    public double SpeedFactor(RoomId room) => Evaluated(room).Speed;       // REQ-061

    public IReadOnlyList<StaffPresenceEntry> CurrentPresence() =>          // for CON-012 bridge; assigned only
        _employees
            .Where(e => e.AssignedRoom is not null)
            .Select(e => new StaffPresenceEntry(e.Id, e.AssignedRoom!.Value, e.Traits))
            .ToList();

    public IReadOnlyList<HireableView> HireCatalog()                       // roles + unlocked named hires
    {
        var views = _catalog.Roles
            .Select(r => new HireableView(r.Id, null, r.DisplayName, r.Wage))
            .ToList();
        foreach (var hire in _unlocks.UnlockedNamedHires())
            if (_namedDefs.TryGetValue(hire, out var def))
                views.Add(new HireableView(def.Role, hire, def.DisplayName, def.Wage));
        return views;
    }

    public IReadOnlyDictionary<RoleId, int> StaffedRoleCounts() =>         // composition input (CON-006)
        _employees
            .Where(e => e.AssignedRoom is not null)
            .GroupBy(e => e.Role)
            .ToDictionary(g => g.Key, g => g.Count());

    // ════════════════════════════════════════════════════════════
    //  Internals
    // ════════════════════════════════════════════════════════════

    private bool IsPrep => _cycle.Phase == Phase.Prep;

    private EmployeeId NextId() => new(_nextEmployeeId++);

    private Employee? Find(EmployeeId id) => _employees.FirstOrDefault(e => e.Id == id);

    private static Outcome<StaffingError> Fail(StaffingError error) => new Outcome<StaffingError>.Failure(error);

    private static Outcome<StaffingError> Success(IDomainEvent evt) =>
        new Outcome<StaffingError>.Success(new[] { evt });

    private EmployeeInfo ToInfo(Employee e) =>
        new(e.Id, e.Role, e.NamedHire, e.DisplayName, e.Wage, e.AssignedRoom, e.Refusing, e.Traits);

    /// Reads the room's cached evaluation; computes on demand only when the room has never been
    /// evaluated (keeps queries side-effect-free — the cache is populated by EvaluateRoomStates / OnRoom*).
    private RoomEval Evaluated(RoomId room) =>
        _stateCache.TryGetValue(room, out var cached) ? cached : Compute(room);

    /// Recomputes a single room's evaluation, updates the cache, and emits RoomStaffStateChanged
    /// only when the state actually changed (or on first evaluation of the room).
    private IReadOnlyList<IDomainEvent> RecomputeRoom(RoomId room)
    {
        RoomEval computed;
        try { computed = Compute(room); }
        catch (KeyNotFoundException) { _stateCache.Remove(room); return Array.Empty<IDomainEvent>(); }

        var changed = !_stateCache.TryGetValue(room, out var prev) || prev.State != computed.State;
        _stateCache[room] = computed;
        return changed
            ? new IDomainEvent[] { new RoomStaffStateChanged(room, computed.State) }
            : Array.Empty<IDomainEvent>();
    }

    /// CON-009 state + speed for one room, counting only non-refusing assignees (REQ-028/058/061).
    private RoomEval Compute(RoomId room)
    {
        var reqs = _rooms.Get(room);                                       // KeyNotFoundException if absent
        var working = _employees.Where(e => e.AssignedRoom == room && !e.Refusing).ToList();

        var anyRequiredRoleEmpty = false;
        var allMinimaMet = true;
        var requiredMinTotal = 0;
        foreach (var role in reqs.Roles)
        {
            requiredMinTotal += role.Min;
            var count = working.Count(e => e.Role == role.Role);
            if (count == 0) anyRequiredRoleEmpty = true;
            if (count < role.Min) allMinimaMet = false;
        }

        var state = anyRequiredRoleEmpty ? RoomStaffState.Closed
            : allMinimaMet ? RoomStaffState.Open
            : RoomStaffState.Degraded;

        double speed;
        if (state == RoomStaffState.Closed)
        {
            speed = 0.0;
        }
        else
        {
            var staffedTotal = working.Count;
            var ratio = requiredMinTotal > 0
                ? (double)staffedTotal / requiredMinTotal
                : staffedTotal > 0 ? double.PositiveInfinity : 0.0;
            speed = Math.Clamp(ratio, 0.5, 1.5);
            if (state == RoomStaffState.Degraded) speed = Math.Min(speed, 0.8);
        }

        return new RoomEval(state, speed);
    }

    private Employee Rebuild(StaffingSnapshotJson.EmployeeRecord dto)
    {
        var assignedRoom = dto.AssignedRoom is { } roomValue ? new RoomId(roomValue) : (RoomId?)null;

        if (dto.NamedHire is { } namedHireId)
        {
            var hire = new NamedHireId(namedHireId);
            var def = _namedDefs[hire];                                    // catalog is authoritative for name/wage/traits
            return new Employee(new EmployeeId(dto.Id), def.Role, hire, def.DisplayName, def.Wage, def.Traits)
            {
                AssignedRoom = assignedRoom,
                Refusing = dto.Refusing,
            };
        }

        var role = new RoleId(dto.Role);
        var roleDef = _roleDefs[role];
        return new Employee(new EmployeeId(dto.Id), role, namedHire: null, roleDef.DisplayName, roleDef.Wage, roleDef.Traits)
        {
            AssignedRoom = assignedRoom,
            Refusing = dto.Refusing,
        };
    }

    private readonly record struct RoomEval(RoomStaffState State, double Speed);

    /// Mutable employee record (identity + catalog-derived attributes fixed; assignment/refusal vary).
    internal sealed class Employee
    {
        public Employee(EmployeeId id, RoleId role, NamedHireId? namedHire,
            string displayName, Money wage, IReadOnlyList<TraitId> traits)
        {
            Id = id;
            Role = role;
            NamedHire = namedHire;
            DisplayName = displayName;
            Wage = wage;
            Traits = traits;
        }

        public EmployeeId Id { get; }
        public RoleId Role { get; }
        public NamedHireId? NamedHire { get; }
        public string DisplayName { get; }
        public Money Wage { get; }
        public IReadOnlyList<TraitId> Traits { get; }
        public RoomId? AssignedRoom { get; set; }
        public bool Refusing { get; set; }
    }
}
