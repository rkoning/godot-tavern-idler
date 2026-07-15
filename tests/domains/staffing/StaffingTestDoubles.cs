using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Cycle;
using TavernIdler.Domains.Staffing;
using TavernIdler.Domains.Structure;   // StaffRequirements (CON-003)
using TavernIdler.Kernel;
using TavernIdler.Tests.Contracts.Staffing;   // StaffCatalog (test-support type)

namespace TavernIdler.Tests.Domains.Staffing;

/// <summary>In-memory <see cref="ICycleQueries"/>: only the phase gate matters to Staffing.</summary>
public sealed class FakeCycleQueries : ICycleQueries
{
    public Phase Phase { get; set; } = Phase.Prep;
    public bool IsDraining => false;
    public int NightNumber => 1;
    public Tick Now => new(0);
    public int ElapsedServiceTicks => 0;
    public int RemainingServiceTicks => 0;
    public bool RunModeActive => false;
}

/// <summary>
/// In-memory CON-010 <see cref="IRoomRequirements"/>. Rooms are registered explicitly; an
/// unregistered room throws <see cref="KeyNotFoundException"/> from <see cref="Get"/> (per the port
/// contract). <see cref="RoomsWithRequirements"/> lists only rooms with a non-empty role list.
/// </summary>
public sealed class FakeRoomRequirements : IRoomRequirements
{
    private readonly Dictionary<RoomId, StaffRequirements> _rooms = new();

    public void Set(RoomId room, StaffRequirements requirements) => _rooms[room] = requirements;
    public void Clear(RoomId room) => _rooms.Remove(room);

    public StaffRequirements Get(RoomId room) =>
        _rooms.TryGetValue(room, out var reqs)
            ? reqs
            : throw new KeyNotFoundException($"No requirements registered for room {room.Value}.");

    public IReadOnlyList<RoomId> RoomsWithRequirements() =>
        _rooms.Where(kv => kv.Value.Roles.Count > 0).Select(kv => kv.Key).ToList();
}

/// <summary>In-memory CON-010 <see cref="IHireUnlocks"/>: the currently-unlocked named hires.</summary>
public sealed class FakeHireUnlocks : IHireUnlocks
{
    public List<NamedHireId> Unlocked { get; } = new();
    public IReadOnlyList<NamedHireId> UnlockedNamedHires() => Unlocked.ToList();
}

/// <summary>
/// Real <see cref="Roster"/> aggregate wired to the in-memory CON-010 driven ports and the CON-002
/// phase gate. Maps the test-support <see cref="StaffCatalog"/> onto the domain's own catalog type.
/// </summary>
public sealed class StaffingHarness : IStaffingTestHarness
{
    private readonly FakeCycleQueries _cycle = new();
    private readonly FakeRoomRequirements _rooms = new();
    private readonly FakeHireUnlocks _unlocks = new();
    private readonly StaffRoster _roster;

    public StaffingHarness(StaffCatalog catalog)
    {
        _roster = new StaffRoster(_cycle, _rooms, _unlocks, Map(catalog));
    }

    public IStaffingCommands Commands => _roster;
    public IStaffingQueries Queries => _roster;

    public void SetInPrep(bool inPrep) => _cycle.Phase = inPrep ? Phase.Prep : Phase.Service;

    public void SetRoomRequirements(RoomId room, StaffRequirements requirements) => _rooms.Set(room, requirements);
    public void ClearRoomRequirements(RoomId room) => _rooms.Clear(room);

    public void SetUnlockedNamedHires(params NamedHireId[] hires)
    {
        _unlocks.Unlocked.Clear();
        _unlocks.Unlocked.AddRange(hires);
    }

    private static RosterCatalog Map(StaffCatalog catalog) => new(
        catalog.Roles
            .Select(r => new StaffRoleDef(r.Id, r.DisplayName, r.Wage, r.Traits))
            .ToList(),
        catalog.NamedHires
            .Select(n => new NamedHireDef(n.Id, n.DisplayName, n.Role, n.Wage, n.Traits))
            .ToList());
}
