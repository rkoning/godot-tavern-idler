using System.Collections.Generic;
using System.Linq;
using TavernIdler.Kernel;
using TavernIdler.Domains.Staffing;
using TavernIdler.Domains.Structure;

namespace TavernIdler.Tests.Contracts.Staffing.Driven;

/// <summary>
/// CON-010 (Staffing Driven Ports) abstract conformance suite. Abstract — the bridge ticket
/// (TKT-019, over CON-003 / CON-013) provides the sealed subclass. This suite asserts the
/// port-level read contract; the temporal properties in CON-010's Conformance section
/// (Get equivalence *across an upgrade*, UnlockedNamedHires shrinking *only at prestige*) are
/// bridge-behaviour assertions the implementer adds against its live CON-003/CON-013 backing.
/// </summary>
public abstract class RoomRequirementsConformanceTests
{
    protected abstract IRoomRequirements CreateRoomRequirements(IReadOnlyDictionary<RoomId, StaffRequirements> rooms);
    protected abstract IHireUnlocks CreateHireUnlocks(IReadOnlyList<NamedHireId> unlocked);

    private static StaffRequirements Req(params (string role, int min, int max)[] roles) =>
        new(roles.Select(r => new RoleRequirement(new RoleId(r.role), r.min, r.max)).ToList());

    [Fact]
    public void Get_returns_scripted_requirements()
    {
        var room = new RoomId(1);
        var sut = CreateRoomRequirements(new Dictionary<RoomId, StaffRequirements>
        {
            [room] = Req(("bartender", 1, 2), ("barmaid", 2, 4)),
        });
        var got = sut.Get(room);
        Assert.Equal(2, got.Roles.Count);
        Assert.Contains(got.Roles, r => r.Role == new RoleId("bartender") && r.Min == 1 && r.Max == 2);
    }

    [Fact]
    public void RoomsWithRequirements_lists_only_nonempty_and_empty_room_returns_empty_roles()
    {
        var staffed = new RoomId(1);
        var bare = new RoomId(2);
        var sut = CreateRoomRequirements(new Dictionary<RoomId, StaffRequirements>
        {
            [staffed] = Req(("bartender", 1, 2)),
            [bare] = new StaffRequirements(new List<RoleRequirement>()),
        });

        Assert.Empty(sut.Get(bare).Roles);
        Assert.Contains(staffed, sut.RoomsWithRequirements());
        Assert.DoesNotContain(bare, sut.RoomsWithRequirements());
    }

    [Fact]
    public void Get_throws_for_absent_room()
    {
        var sut = CreateRoomRequirements(new Dictionary<RoomId, StaffRequirements>());
        Assert.Throws<KeyNotFoundException>(() => sut.Get(new RoomId(99)));
    }

    [Fact]
    public void HireUnlocks_returns_scripted_set()
    {
        var sut = CreateHireUnlocks(new[] { new NamedHireId("old-tom") });
        Assert.Equal(new[] { new NamedHireId("old-tom") }, sut.UnlockedNamedHires());
    }
}
