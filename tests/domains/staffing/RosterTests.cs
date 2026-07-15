using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Cycle;
using TavernIdler.Domains.Staffing;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Domains.Staffing;

/// <summary>
/// Focused unit tests for the <see cref="Roster"/> aggregate (DOM-005), covering behaviour the
/// CON-009 conformance suite does not pin directly: the read-side views (HireCatalog / presence /
/// role counts), event-emission discipline, prestige reset, deactivation, and snapshot fidelity.
/// The abstract CON-009 suite is exercised separately by <see cref="RosterConformanceTests"/>.
/// </summary>
public sealed class RosterTests
{
    private static readonly RoomId Bar = new(1);
    private static readonly RoomId Kitchen = new(2);

    private readonly FakeCycleQueries _cycle = new();
    private readonly FakeRoomRequirements _rooms = new();
    private readonly FakeHireUnlocks _unlocks = new();
    private readonly StaffRoster _roster;

    public RosterTests()
    {
        _roster = new StaffRoster(_cycle, _rooms, _unlocks, Catalog());
        _cycle.Phase = Phase.Prep;
    }

    private static RosterCatalog Catalog() => new(
        new[]
        {
            new StaffRoleDef(new RoleId("bartender"), "Bartender", new Money(8), new[] { new TraitId("sturdy") }),
            new StaffRoleDef(new RoleId("barmaid"), "Barmaid", new Money(6), new[] { new TraitId("cheery") }),
        },
        new[]
        {
            new NamedHireDef(new NamedHireId("old-tom"), "Old Tom", new RoleId("bartender"),
                new Money(20), new[] { new TraitId("legendary"), new TraitId("sturdy") }),
        });

    private static StaffRequirements Req(params (string role, int min, int max)[] roles) =>
        new(roles.Select(r => new RoleRequirement(new RoleId(r.role), r.min, r.max)).ToList());

    private static Outcome<StaffingError>.Success Ok(Outcome<StaffingError> outcome)
        => Assert.IsType<Outcome<StaffingError>.Success>(outcome);

    private EmployeeId Hire(string role) =>
        Ok(_roster.Hire(new RoleId(role))).Events.OfType<EmployeeHired>().Single().Id;

    // ── HireCatalog: roles + unlocked named hires only ──
    [Fact]
    public void HireCatalog_lists_roles_and_only_unlocked_named_hires()
    {
        var locked = _roster.HireCatalog();
        Assert.Contains(locked, h => h.Role == new RoleId("bartender") && h.NamedHire is null && h.Wage == new Money(8));
        Assert.DoesNotContain(locked, h => h.NamedHire == new NamedHireId("old-tom"));

        _unlocks.Unlocked.Add(new NamedHireId("old-tom"));
        var unlocked = _roster.HireCatalog();
        Assert.Contains(unlocked, h => h.NamedHire == new NamedHireId("old-tom")
            && h.Role == new RoleId("bartender") && h.Wage == new Money(20) && h.DisplayName == "Old Tom");
    }

    // ── HireNamed adopts the named hire's role, wage and traits ──
    [Fact]
    public void HireNamed_adopts_role_wage_and_traits_from_catalog()
    {
        _unlocks.Unlocked.Add(new NamedHireId("old-tom"));
        var id = Ok(_roster.HireNamed(new NamedHireId("old-tom"))).Events.OfType<EmployeeHired>().Single().Id;

        var info = _roster.Roster.Single(e => e.Id == id);
        Assert.Equal(new RoleId("bartender"), info.Role);
        Assert.Equal(new NamedHireId("old-tom"), info.NamedHire);
        Assert.Equal(new Money(20), info.Wage);
        Assert.Contains(new TraitId("legendary"), info.Traits);
        Assert.Contains(_roster.WageBill(), w => w.Employee == id && w.Wage == new Money(20) && w.Role == new RoleId("bartender"));
    }

    // ── CurrentPresence: assigned employees only, carrying their traits ──
    [Fact]
    public void CurrentPresence_contains_only_assigned_employees_with_traits()
    {
        _rooms.Set(Bar, Req(("bartender", 1, 3)));
        var assigned = Hire("bartender");
        var pooled = Hire("bartender");
        Ok(_roster.Assign(assigned, Bar));

        var presence = _roster.CurrentPresence();
        Assert.Contains(presence, p => p.Id == assigned && p.Room == Bar && p.Traits.Contains(new TraitId("sturdy")));
        Assert.DoesNotContain(presence, p => p.Id == pooled);
    }

    // ── StaffedRoleCounts: count of assigned employees per role ──
    [Fact]
    public void StaffedRoleCounts_counts_assigned_by_role()
    {
        _rooms.Set(Bar, Req(("bartender", 1, 3), ("barmaid", 1, 2)));
        Ok(_roster.Assign(Hire("bartender"), Bar));
        Ok(_roster.Assign(Hire("bartender"), Bar));
        Ok(_roster.Assign(Hire("barmaid"), Bar));
        _ = Hire("barmaid");   // pooled, not assigned → not counted

        var counts = _roster.StaffedRoleCounts();
        Assert.Equal(2, counts[new RoleId("bartender")]);
        Assert.Equal(1, counts[new RoleId("barmaid")]);
    }

    // ── OnRoomDeactivated keeps assignees in place (reactivation must be able to restore them) ──
    [Fact]
    public void OnRoomDeactivated_does_not_orphan_assignees()
    {
        _rooms.Set(Bar, Req(("bartender", 1, 2)));
        var e = Hire("bartender");
        Ok(_roster.Assign(e, Bar));

        var events = _roster.OnRoomDeactivated(Bar);

        Assert.DoesNotContain(events.OfType<EmployeeOrphaned>(), o => o.Id == e);
        Assert.Equal(Bar, _roster.Roster.Single(r => r.Id == e).AssignedRoom);   // still assigned
    }

    // ── EvaluateRoomStates emits RoomStaffStateChanged only on a transition ──
    [Fact]
    public void EvaluateRoomStates_emits_change_event_only_on_transition()
    {
        _rooms.Set(Bar, Req(("bartender", 2, 3)));   // min 2
        Ok(_roster.Assign(Hire("bartender"), Bar));   // only 1 present ⇒ Degraded

        var first = _roster.EvaluateRoomStates();
        Assert.Contains(first.OfType<RoomStaffStateChanged>(),
            c => c.Room == Bar && c.State == RoomStaffState.Degraded);   // first evaluation ⇒ change from no prior state

        var second = _roster.EvaluateRoomStates();   // no change since last evaluation
        Assert.DoesNotContain(second.OfType<RoomStaffStateChanged>(), c => c.Room == Bar);
    }

    // ── SetRefusals flips flags but is not itself a state recompute (no events) ──
    [Fact]
    public void SetRefusals_returns_no_events_and_only_flips_flags()
    {
        _rooms.Set(Bar, Req(("bartender", 1, 2)));
        var e = Hire("bartender");
        Ok(_roster.Assign(e, Bar));

        var events = _roster.SetRefusals(new[] { e }, refusing: true);

        Assert.Empty(events);
        Assert.True(_roster.Roster.Single(r => r.Id == e).Refusing);
    }

    // ── SetRefusals is legal outside Prep (event reaction) ──
    [Fact]
    public void SetRefusals_is_legal_during_service()
    {
        _rooms.Set(Bar, Req(("bartender", 1, 2)));
        var e = Hire("bartender");
        _cycle.Phase = Phase.Service;

        _roster.SetRefusals(new[] { e }, refusing: true);
        Assert.True(_roster.Roster.Single(r => r.Id == e).Refusing);
    }

    // ── ResetAll clears the roster, emits StaffReset, and never reuses employee ids ──
    [Fact]
    public void ResetAll_clears_roster_emits_reset_and_does_not_reuse_ids()
    {
        var before = Hire("bartender");

        var events = _roster.ResetAll();
        Assert.Contains(events, e => e is StaffReset);
        Assert.Empty(_roster.Roster);

        var after = Hire("bartender");
        Assert.NotEqual(before, after);   // ids monotone across prestige
    }

    // ── Dismiss removes from roster and stops wage accrual ──
    [Fact]
    public void Dismiss_removes_employee_and_stops_wages()
    {
        var e = Hire("bartender");
        Ok(_roster.Dismiss(e));

        Assert.DoesNotContain(_roster.Roster, r => r.Id == e);
        Assert.DoesNotContain(_roster.WageBill(), w => w.Employee == e);
    }

    // ── OnRoomRemoved emits a single closing state (room gone) and orphans assignees ──
    [Fact]
    public void OnRoomRemoved_orphans_all_assignees_of_that_room()
    {
        _rooms.Set(Bar, Req(("bartender", 1, 3)));
        var a = Hire("bartender");
        var b = Hire("bartender");
        Ok(_roster.Assign(a, Bar));
        Ok(_roster.Assign(b, Bar));

        var orphaned = _roster.OnRoomRemoved(Bar).OfType<EmployeeOrphaned>().Select(o => o.Id).ToHashSet();
        Assert.Contains(a, orphaned);
        Assert.Contains(b, orphaned);
        Assert.Null(_roster.Roster.Single(r => r.Id == a).AssignedRoom);
        Assert.Null(_roster.Roster.Single(r => r.Id == b).AssignedRoom);
    }

    // ── Snapshot preserves named-hire identity, role, wage and traits across a fresh aggregate ──
    [Fact]
    public void Snapshot_round_trip_preserves_named_hire_details()
    {
        _rooms.Set(Bar, Req(("bartender", 1, 3)));
        _unlocks.Unlocked.Add(new NamedHireId("old-tom"));
        var tom = Ok(_roster.HireNamed(new NamedHireId("old-tom"))).Events.OfType<EmployeeHired>().Single().Id;
        Ok(_roster.Assign(tom, Bar));
        var snap = _roster.Capture();

        var fresh = new StaffRoster(new FakeCycleQueries(), new FakeRoomRequirements(), new FakeHireUnlocks(), Catalog());
        fresh.Restore(snap);

        var info = fresh.Roster.Single(r => r.Id == tom);
        Assert.Equal(new NamedHireId("old-tom"), info.NamedHire);
        Assert.Equal(new RoleId("bartender"), info.Role);
        Assert.Equal(new Money(20), info.Wage);
        Assert.Equal(Bar, info.AssignedRoom);
        Assert.Contains(new TraitId("legendary"), info.Traits);
    }

    // ── Capture is legal only in Prep / Settlement, not during Service ──
    [Fact]
    public void Capture_is_rejected_during_service()
    {
        _cycle.Phase = Phase.Service;
        Assert.ThrowsAny<InvalidOperationException>(() => _roster.Capture());
    }
}
