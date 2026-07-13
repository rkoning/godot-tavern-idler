using System.Collections.Generic;
using System.Linq;
using TavernIdler.Kernel;
using TavernIdler.Domains.Staffing;
using TavernIdler.Domains.Structure;

namespace TavernIdler.Tests.Contracts.Staffing;

/// <summary>
/// CON-009 (Staffing API) abstract conformance suite. Abstract — contributes no runnable tests
/// until TKT-012 provides a sealed subclass implementing <see cref="CreateSut"/> over the real
/// staffing aggregate (same contract-definition pattern as TKT-002/003/005).
/// </summary>
public abstract class StaffingApiConformanceTests
{
    protected abstract IStaffingTestHarness CreateSut(StaffCatalog catalog);

    private static readonly RoomId Bar = new(1);
    private static readonly RoomId Kitchen = new(2);

    private static StaffCatalog StandardCatalog() => new(
        new[]
        {
            StaffScenario.Role("bartender", 8, "sturdy"),
            StaffScenario.Role("barmaid", 6),
            StaffScenario.Role("cook", 10),
        },
        new[]
        {
            new NamedHireSpec(new NamedHireId("old-tom"), "Old Tom", new RoleId("bartender"),
                new Money(20), new[] { new TraitId("legendary") }, "perk-old-tom"),
        });

    private IStaffingTestHarness Prep(StaffRequirements? bar = null)
    {
        var h = CreateSut(StandardCatalog());
        h.SetInPrep(true);
        if (bar is not null) h.SetRoomRequirements(Bar, bar);
        return h;
    }

    private static void Fails(Outcome<StaffingError> outcome, StaffingError expected)
    {
        var failure = Assert.IsType<Outcome<StaffingError>.Failure>(outcome);
        Assert.Equal(expected, failure.Error);
    }

    private static Outcome<StaffingError>.Success Ok(Outcome<StaffingError> outcome)
        => Assert.IsType<Outcome<StaffingError>.Success>(outcome);

    private static EmployeeId HireOk(IStaffingTestHarness h, string role)
        => Ok(h.Commands.Hire(new RoleId(role))).Events.OfType<EmployeeHired>().Single().Id;

    // ── Error matrix: every StaffingError variant; failures mutate nothing ──
    [Fact]
    public void WrongPhase_when_not_in_prep()
    {
        var h = Prep();
        h.SetInPrep(false);
        var before = h.Queries.Roster.Count;
        Fails(h.Commands.Hire(new RoleId("bartender")), StaffingError.WrongPhase);
        Assert.Equal(before, h.Queries.Roster.Count);   // mutated nothing
    }

    [Fact]
    public void UnknownRole_rejected() => Fails(Prep().Commands.Hire(new RoleId("wizard")), StaffingError.UnknownRole);

    [Fact]
    public void UnknownNamedHire_rejected() => Fails(Prep().Commands.HireNamed(new NamedHireId("nobody")), StaffingError.UnknownNamedHire);

    [Fact]
    public void NamedHireLocked_when_perk_not_owned()
        => Fails(Prep().Commands.HireNamed(new NamedHireId("old-tom")), StaffingError.NamedHireLocked);

    [Fact]
    public void NamedHireAlreadyEmployed_on_second_hire()
    {
        var h = Prep();
        h.SetUnlockedNamedHires(new NamedHireId("old-tom"));
        Ok(h.Commands.HireNamed(new NamedHireId("old-tom")));
        Fails(h.Commands.HireNamed(new NamedHireId("old-tom")), StaffingError.NamedHireAlreadyEmployed);
    }

    [Fact]
    public void UnknownEmployee_on_dismiss_and_assign()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 2)));
        var ghost = new EmployeeId(999);
        Fails(h.Commands.Dismiss(ghost), StaffingError.UnknownEmployee);
        Fails(h.Commands.Assign(ghost, Bar), StaffingError.UnknownEmployee);
    }

    [Fact]
    public void UnknownRoom_when_room_has_no_requirements()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 2)));
        var e = HireOk(h, "bartender");
        Fails(h.Commands.Assign(e, Kitchen), StaffingError.UnknownRoom);   // Kitchen never registered
    }

    [Fact]
    public void RoomAtStaffingMax_when_role_full()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 1)));   // max 1
        Ok(h.Commands.Assign(HireOk(h, "bartender"), Bar));
        Fails(h.Commands.Assign(HireOk(h, "bartender"), Bar), StaffingError.RoomAtStaffingMax);
    }

    [Fact]
    public void RoleNotAcceptedByRoom_when_role_absent_from_requirements()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 2)));   // no barmaid slot
        Fails(h.Commands.Assign(HireOk(h, "barmaid"), Bar), StaffingError.RoleNotAcceptedByRoom);
    }

    [Fact]
    public void NotAssigned_when_unassigning_pool_employee()
    {
        var h = Prep();
        Fails(h.Commands.Unassign(HireOk(h, "bartender")), StaffingError.NotAssigned);
    }

    // ── State evaluation (REQ-058) ──
    [Fact]
    public void RoomState_closed_degraded_open_and_refusal_flip()
    {
        var h = Prep(StaffScenario.Req(("bartender", 2, 3)));   // min 2
        var a = HireOk(h, "bartender");
        Ok(h.Commands.Assign(a, Bar));
        h.Commands.EvaluateRoomStates();
        Assert.Equal(RoomStaffState.Degraded, h.Queries.State(Bar));   // 1 present, below min 2

        var b = HireOk(h, "bartender");
        Ok(h.Commands.Assign(b, Bar));
        h.Commands.EvaluateRoomStates();
        Assert.Equal(RoomStaffState.Open, h.Queries.State(Bar));       // mins met

        h.Commands.SetRefusals(new[] { a, b }, refusing: true);
        h.Commands.EvaluateRoomStates();
        Assert.Equal(RoomStaffState.Closed, h.Queries.State(Bar));     // 0 non-refusing in a required role
    }

    // ── Speed (REQ-061) ──
    [Fact]
    public void SpeedFactor_min_over_degraded_closed()
    {
        var h = Prep(StaffScenario.Req(("bartender", 2, 5)));
        var a = HireOk(h, "bartender");
        var b = HireOk(h, "bartender");
        Ok(h.Commands.Assign(a, Bar));
        Ok(h.Commands.Assign(b, Bar));
        h.Commands.EvaluateRoomStates();
        Assert.Equal(1.0, h.Queries.SpeedFactor(Bar), 3);              // staffed == requiredMin

        for (var i = 0; i < 6; i++) Ok(h.Commands.Assign(HireOk(h, "bartender"), Bar));
        h.Commands.EvaluateRoomStates();
        Assert.Equal(1.5, h.Queries.SpeedFactor(Bar), 3);             // clamped up

        h.Commands.SetRefusals(new[] { a }, refusing: true);          // drop below min ⇒ Degraded
        h.Commands.EvaluateRoomStates();
        Assert.True(h.Queries.SpeedFactor(Bar) <= 0.8 + 1e-9);       // degraded cap
    }

    // ── REQ-109: room removed orphans assignees ──
    [Fact]
    public void OnRoomRemoved_orphans_assignee_keeps_wage()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 2)));
        var e = HireOk(h, "bartender");
        Ok(h.Commands.Assign(e, Bar));

        var events = h.Commands.OnRoomRemoved(Bar);
        Assert.Contains(events.OfType<EmployeeOrphaned>(), o => o.Id == e && o.FormerRoom == Bar);
        Assert.Null(h.Queries.Roster.Single(r => r.Id == e).AssignedRoom);   // unassigned
        Assert.Contains(h.Queries.WageBill(), w => w.Employee == e);         // still paid
    }

    // ── Named hires happy path ──
    [Fact]
    public void HireNamed_succeeds_once_when_unlocked()
    {
        var h = Prep();
        h.SetUnlockedNamedHires(new NamedHireId("old-tom"));
        var hired = Ok(h.Commands.HireNamed(new NamedHireId("old-tom"))).Events.OfType<EmployeeHired>().Single();
        Assert.Equal(new NamedHireId("old-tom"), hired.NamedHire);
    }

    // ── Wage bill contents ──
    [Fact]
    public void WageBill_includes_assigned_unassigned_refusing_excludes_dismissed()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 3)));
        var assigned = HireOk(h, "bartender");
        var pool = HireOk(h, "bartender");
        var refusing = HireOk(h, "bartender");
        var dismissed = HireOk(h, "bartender");
        Ok(h.Commands.Assign(assigned, Bar));
        h.Commands.SetRefusals(new[] { refusing }, refusing: true);
        Ok(h.Commands.Dismiss(dismissed));

        var billed = h.Queries.WageBill().Select(w => w.Employee).ToHashSet();
        Assert.Contains(assigned, billed);
        Assert.Contains(pool, billed);
        Assert.Contains(refusing, billed);
        Assert.DoesNotContain(dismissed, billed);
    }

    // ── Assignment invariants: implicit reassign emits Unassigned+Assigned ──
    [Fact]
    public void Reassign_implicitly_unassigns_first()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 2)));
        h.SetRoomRequirements(Kitchen, StaffScenario.Req(("bartender", 1, 2)));
        var e = HireOk(h, "bartender");
        Ok(h.Commands.Assign(e, Bar));
        var events = Ok(h.Commands.Assign(e, Kitchen)).Events;
        Assert.Contains(events.OfType<EmployeeUnassigned>(), u => u.Id == e);
        Assert.Contains(events.OfType<EmployeeAssigned>(), a => a.Id == e && a.Room == Kitchen);
    }

    // ── Snapshot round-trip ──
    [Fact]
    public void Snapshot_round_trip_preserves_roster_assignments_refusals()
    {
        var h = Prep(StaffScenario.Req(("bartender", 1, 3)));
        var assigned = HireOk(h, "bartender");
        var refusing = HireOk(h, "bartender");
        Ok(h.Commands.Assign(assigned, Bar));
        h.Commands.SetRefusals(new[] { refusing }, refusing: true);
        var snap = h.Commands.Capture();

        var h2 = Prep(StaffScenario.Req(("bartender", 1, 3)));
        h2.Commands.Restore(snap);

        var restored = h2.Queries.Roster;
        Assert.Equal(2, restored.Count);
        Assert.Equal(Bar, restored.Single(r => r.Id == assigned).AssignedRoom);
        Assert.True(restored.Single(r => r.Id == refusing).Refusing);
    }
}
