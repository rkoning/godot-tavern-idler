using System;
using System.Collections.Generic;
using TavernIdler.Domains.Cycle;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Cycle;

/// <summary>
/// CON-002 abstract conformance suite for the night-cycle FSM
/// (<see cref="ICycleCommands"/> / <see cref="ICycleQueries"/> / <see cref="ICycleSnapshot"/>).
///
/// This class is ABSTRACT and defines no runnable tests on its own — xUnit does not
/// instantiate abstract classes. The Cycle domain ticket (TKT-010) provides a sealed
/// subclass implementing <see cref="CreateSut"/>, at which point every test below runs
/// against the real <c>NightCycle</c> aggregate. This ticket (TKT-002) only defines the
/// suite and the frozen port types it targets.
///
/// Coverage maps 1:1 to the CON-002 "Conformance tests" section:
///  - Full legal-transition walk with exact event sequences.
///  - Exhaustive command × illegal-phase matrix → Failure(WrongPhase), state unchanged.
///  - Service expiry (DrainBegan(Expired) exactly once; further ticks silent).
///  - Early close (DrainBegan(ClosedEarly); later expiry silent).
///  - DismissReport before NotifySettlementComputed → ReportNotPending.
///  - NightNumber increments only on StartService; Now monotonic across phases.
///  - Snapshot round-trip in Prep and Settlement; Capture during Service throws.
/// Plus the sub-state error variants named by the CON-002 error enum
/// (NotDraining, RunModeAlreadyInThatState) and the documented range guards.
/// </summary>
public abstract class CycleConformanceTests
{
    /// <summary>The three CON-002 driving ports of one cycle aggregate instance.</summary>
    public sealed record CycleSut(
        ICycleCommands Commands,
        ICycleQueries Queries,
        ICycleSnapshot Snapshot);

    /// <summary>
    /// Produce a fresh cycle aggregate configured with <paramref name="config"/>, positioned
    /// at run start: Phase.Prep, NightNumber 0, Now 0, run mode off. The three returned ports
    /// MUST be backed by the same instance so a command is visible through the queries.
    /// </summary>
    protected abstract CycleSut CreateSut(CycleConfig config);

    private const int ServiceDuration = 10;

    private CycleSut Fresh() => CreateSut(new CycleConfig(ServiceDuration));

    // ── SUT positioned in each phase / sub-state ─────────────────
    private CycleSut Prep() => Fresh();

    private CycleSut Service()
    {
        var s = Fresh();
        Ok(s.Commands.StartService());
        return s;
    }

    private CycleSut ServiceDraining()
    {
        var s = Service();
        Ok(s.Commands.CloseEarly());
        return s;
    }

    private CycleSut SettlementNoReport()
    {
        var s = ServiceDraining();
        Ok(s.Commands.NotifyAllGuestsGone());
        return s;
    }

    private CycleSut SettlementReportPending()
    {
        var s = SettlementNoReport();
        Ok(s.Commands.NotifySettlementComputed());
        return s;
    }

    // ── Outcome helpers ─────────────────────────────────────────
    private static IReadOnlyList<IDomainEvent> Ok(Outcome<CycleError> outcome) =>
        Assert.IsType<Outcome<CycleError>.Success>(outcome).Events;

    private static CycleError Err(Outcome<CycleError> outcome) =>
        Assert.IsType<Outcome<CycleError>.Failure>(outcome).Error;

    private static void AssertEvents(IReadOnlyList<IDomainEvent> actual, params IDomainEvent[] expected) =>
        Assert.Equal<IDomainEvent>(expected, actual);

    // ════════════════════════════════════════════════════════════
    //  Full legal-transition walk with exact event sequences
    // ════════════════════════════════════════════════════════════
    [Fact]
    public void Full_night_walk_emits_exact_event_sequences_and_state()
    {
        var s = Prep();
        var c = s.Commands;
        var q = s.Queries;

        // Initial run-start state.
        Assert.Equal(Phase.Prep, q.Phase);
        Assert.Equal(0, q.NightNumber);
        Assert.Equal(0L, q.Now.Value);
        Assert.False(q.IsDraining);
        Assert.False(q.RunModeActive);
        Assert.Equal(0, q.ElapsedServiceTicks);
        Assert.Equal(0, q.RemainingServiceTicks);

        // Prep → Service.
        AssertEvents(Ok(c.StartService()), new NightStarted(1), new ServiceBegan(1));
        Assert.Equal(Phase.Service, q.Phase);
        Assert.Equal(1, q.NightNumber);
        Assert.False(q.IsDraining);
        Assert.Equal(0, q.ElapsedServiceTicks);
        Assert.Equal(ServiceDuration, q.RemainingServiceTicks);

        // Tick partway through service — no phase events.
        Assert.Empty(c.Tick(4));
        Assert.Equal(4, q.ElapsedServiceTicks);
        Assert.Equal(ServiceDuration - 4, q.RemainingServiceTicks);
        Assert.Equal(4L, q.Now.Value);
        Assert.False(q.IsDraining);

        // Tick that reaches ServiceDurationTicks — same call enters drain.
        AssertEvents(c.Tick(6), new DrainBegan(DrainReason.Expired));
        Assert.Equal(Phase.Service, q.Phase);
        Assert.True(q.IsDraining);
        Assert.Equal(0, q.RemainingServiceTicks);
        Assert.Equal(10L, q.Now.Value);

        // Drain → Settlement.
        AssertEvents(Ok(c.NotifyAllGuestsGone()), new SettlementTriggered(1));
        Assert.Equal(Phase.Settlement, q.Phase);
        Assert.False(q.IsDraining);
        Assert.Equal(1, q.NightNumber);
        Assert.Equal(0, q.ElapsedServiceTicks);

        // Marking the report pending emits no domain event.
        Assert.Empty(Ok(c.NotifySettlementComputed()));
        Assert.Equal(Phase.Settlement, q.Phase);

        // Settlement → Prep. PrepBegan carries the NEXT night's number.
        AssertEvents(Ok(c.DismissReport()), new ReportDismissed(1), new PrepBegan(2));
        Assert.Equal(Phase.Prep, q.Phase);
        Assert.Equal(1, q.NightNumber); // unchanged — only StartService increments it

        // Second night begins.
        AssertEvents(Ok(c.StartService()), new NightStarted(2), new ServiceBegan(2));
        Assert.Equal(2, q.NightNumber);
    }

    // ════════════════════════════════════════════════════════════
    //  Exhaustive command × illegal-phase matrix
    // ════════════════════════════════════════════════════════════
    public enum Command
    {
        StartService,
        CloseEarly,
        EnableRunMode,
        CancelRunMode,
        NotifyAllGuestsGone,
        NotifySettlementComputed,
        DismissReport,
    }

    [Theory]
    // StartService is legal only in Prep.
    [InlineData(Command.StartService, "service")]
    [InlineData(Command.StartService, "settlement")]
    // CloseEarly is legal only in Service (non-draining).
    [InlineData(Command.CloseEarly, "prep")]
    [InlineData(Command.CloseEarly, "settlement")]
    // Run-mode toggles are legal in Prep or Service — illegal in Settlement.
    [InlineData(Command.EnableRunMode, "settlement")]
    [InlineData(Command.CancelRunMode, "settlement")]
    // NotifyAllGuestsGone is legal only in Service (draining).
    [InlineData(Command.NotifyAllGuestsGone, "prep")]
    [InlineData(Command.NotifyAllGuestsGone, "settlement")]
    // Settlement-only commands.
    [InlineData(Command.NotifySettlementComputed, "prep")]
    [InlineData(Command.NotifySettlementComputed, "service")]
    [InlineData(Command.DismissReport, "prep")]
    [InlineData(Command.DismissReport, "service")]
    public void Command_in_illegal_phase_fails_WrongPhase_and_changes_nothing(Command command, string phaseKey)
    {
        var s = Setup(phaseKey);
        var before = StateOf(s.Queries);

        Assert.Equal(CycleError.WrongPhase, Err(Invoke(s.Commands, command)));

        Assert.Equal(before, StateOf(s.Queries));
    }

    private CycleSut Setup(string phaseKey) => phaseKey switch
    {
        "prep" => Prep(),
        "service" => Service(),
        "settlement" => SettlementReportPending(),
        _ => throw new ArgumentOutOfRangeException(nameof(phaseKey), phaseKey, "unknown phase key"),
    };

    private static Outcome<CycleError> Invoke(ICycleCommands c, Command command) => command switch
    {
        Command.StartService => c.StartService(),
        Command.CloseEarly => c.CloseEarly(),
        Command.EnableRunMode => c.EnableRunMode(),
        Command.CancelRunMode => c.CancelRunMode(),
        Command.NotifyAllGuestsGone => c.NotifyAllGuestsGone(),
        Command.NotifySettlementComputed => c.NotifySettlementComputed(),
        Command.DismissReport => c.DismissReport(),
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    private readonly record struct CycleState(
        Phase Phase, bool IsDraining, int NightNumber, long Now, bool RunMode,
        int Elapsed, int Remaining);

    private static CycleState StateOf(ICycleQueries q) => new(
        q.Phase, q.IsDraining, q.NightNumber, q.Now.Value, q.RunModeActive,
        q.ElapsedServiceTicks, q.RemainingServiceTicks);

    // ════════════════════════════════════════════════════════════
    //  Service expiry
    // ════════════════════════════════════════════════════════════
    [Fact]
    public void Tick_reaching_duration_enters_drain_exactly_once()
    {
        var s = Service();

        Assert.Empty(s.Commands.Tick(ServiceDuration - 1)); // elapsed 9, still open
        Assert.False(s.Queries.IsDraining);
        Assert.Equal(1, s.Queries.RemainingServiceTicks);

        AssertEvents(s.Commands.Tick(1), new DrainBegan(DrainReason.Expired)); // reaches 10
        Assert.True(s.Queries.IsDraining);
        Assert.Equal(0, s.Queries.RemainingServiceTicks);

        // Further ticks after drain emit nothing new; remaining stays 0.
        Assert.Empty(s.Commands.Tick(5));
        Assert.True(s.Queries.IsDraining);
        Assert.Equal(0, s.Queries.RemainingServiceTicks);
    }

    [Fact]
    public void Single_tick_overshooting_duration_emits_one_DrainBegan()
    {
        var s = Service();
        AssertEvents(s.Commands.Tick(ServiceDuration + 3), new DrainBegan(DrainReason.Expired));
        Assert.True(s.Queries.IsDraining);
        Assert.Equal(0, s.Queries.RemainingServiceTicks);
    }

    // ════════════════════════════════════════════════════════════
    //  Early close
    // ════════════════════════════════════════════════════════════
    [Fact]
    public void CloseEarly_enters_drain_and_later_expiry_is_silent()
    {
        var s = Service();

        AssertEvents(Ok(s.Commands.CloseEarly()), new DrainBegan(DrainReason.ClosedEarly));
        Assert.True(s.Queries.IsDraining);
        Assert.Equal(0, s.Queries.RemainingServiceTicks);

        // Expiry after an early close emits nothing (already draining).
        Assert.Empty(s.Commands.Tick(ServiceDuration + 5));
        Assert.True(s.Queries.IsDraining);
    }

    [Fact]
    public void CloseEarly_while_already_draining_is_WrongPhase()
    {
        var s = ServiceDraining();
        Assert.Equal(CycleError.WrongPhase, Err(s.Commands.CloseEarly()));
    }

    // ════════════════════════════════════════════════════════════
    //  Report gating (REQ-101)
    // ════════════════════════════════════════════════════════════
    [Fact]
    public void DismissReport_before_settlement_computed_is_ReportNotPending()
    {
        var s = SettlementNoReport();
        Assert.Equal(CycleError.ReportNotPending, Err(s.Commands.DismissReport()));
        Assert.Equal(Phase.Settlement, s.Queries.Phase);
    }

    [Fact]
    public void NotifySettlementComputed_is_legal_only_once_per_night()
    {
        var s = SettlementNoReport();
        Ok(s.Commands.NotifySettlementComputed());
        // A second call in the same settlement is not legal (report already pending).
        Assert.IsType<Outcome<CycleError>.Failure>(s.Commands.NotifySettlementComputed());
    }

    // ════════════════════════════════════════════════════════════
    //  Drain-only signal
    // ════════════════════════════════════════════════════════════
    [Fact]
    public void NotifyAllGuestsGone_outside_drain_is_NotDraining()
    {
        var s = Service(); // in Service but not draining
        Assert.Equal(CycleError.NotDraining, Err(s.Commands.NotifyAllGuestsGone()));
        Assert.Equal(Phase.Service, s.Queries.Phase);
        Assert.False(s.Queries.IsDraining);
    }

    // ════════════════════════════════════════════════════════════
    //  Run mode
    // ════════════════════════════════════════════════════════════
    [Theory]
    [InlineData("prep")]
    [InlineData("service")]
    public void Run_mode_toggles_and_emits_events(string phaseKey)
    {
        var s = Setup(phaseKey);

        AssertEvents(Ok(s.Commands.EnableRunMode()), new RunModeEnabled());
        Assert.True(s.Queries.RunModeActive);

        AssertEvents(Ok(s.Commands.CancelRunMode()), new RunModeCancelled());
        Assert.False(s.Queries.RunModeActive);
    }

    [Theory]
    [InlineData("prep")]
    [InlineData("service")]
    public void Enabling_run_mode_when_already_active_is_RunModeAlreadyInThatState(string phaseKey)
    {
        var s = Setup(phaseKey);
        Ok(s.Commands.EnableRunMode());
        Assert.Equal(CycleError.RunModeAlreadyInThatState, Err(s.Commands.EnableRunMode()));
        Assert.True(s.Queries.RunModeActive);
    }

    [Theory]
    [InlineData("prep")]
    [InlineData("service")]
    public void Cancelling_run_mode_when_inactive_is_RunModeAlreadyInThatState(string phaseKey)
    {
        var s = Setup(phaseKey);
        Assert.Equal(CycleError.RunModeAlreadyInThatState, Err(s.Commands.CancelRunMode()));
        Assert.False(s.Queries.RunModeActive);
    }

    // ════════════════════════════════════════════════════════════
    //  Tick legality & clock
    // ════════════════════════════════════════════════════════════
    [Fact]
    public void Tick_advances_clock_in_every_phase_without_phase_events()
    {
        var prep = Prep();
        Assert.Empty(prep.Commands.Tick(3));
        Assert.Equal(3L, prep.Queries.Now.Value);
        Assert.Equal(Phase.Prep, prep.Queries.Phase);

        var settlement = SettlementReportPending();
        var before = settlement.Queries.Now.Value;
        Assert.Empty(settlement.Commands.Tick(2));
        Assert.Equal(before + 2, settlement.Queries.Now.Value);
        Assert.Equal(Phase.Settlement, settlement.Queries.Phase);
    }

    [Fact]
    public void Now_is_monotonic_across_phase_transitions()
    {
        var s = Prep();
        var c = s.Commands;
        var q = s.Queries;
        long last = q.Now.Value;

        void Advance(Action step)
        {
            step();
            Assert.True(q.Now.Value >= last, $"Now went backwards: {q.Now.Value} < {last}");
            last = q.Now.Value;
        }

        Advance(() => c.Tick(2));
        Advance(() => Ok(c.StartService()));
        Advance(() => c.Tick(3));
        Advance(() => Ok(c.CloseEarly()));
        Advance(() => c.Tick(1));
        Advance(() => Ok(c.NotifyAllGuestsGone()));
        Advance(() => c.Tick(1));
        Advance(() => Ok(c.NotifySettlementComputed()));
        Advance(() => Ok(c.DismissReport()));
        Advance(() => c.Tick(4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Tick_below_one_throws(int ticks)
    {
        var s = Prep();
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Commands.Tick(ticks));
    }

    // ════════════════════════════════════════════════════════════
    //  Snapshot round-trip
    // ════════════════════════════════════════════════════════════
    [Fact]
    public void Snapshot_round_trip_in_prep_preserves_state()
    {
        // Reach a non-trivial Prep: finish night 1, advance the clock, enable run mode.
        var s = Prep();
        var c = s.Commands;
        Ok(c.StartService());
        c.Tick(3);
        Ok(c.CloseEarly());
        Ok(c.NotifyAllGuestsGone());
        Ok(c.NotifySettlementComputed());
        Ok(c.DismissReport());          // back in Prep, night 1 complete, Now == 3
        Ok(c.EnableRunMode());

        var snap = s.Snapshot.Capture();
        Assert.Equal(1, snap.SchemaVersion);
        Assert.Equal(Phase.Prep, snap.Phase);
        Assert.Equal(1, snap.NightNumber);
        Assert.Equal(3L, snap.Now);
        Assert.True(snap.RunModeActive);
        Assert.False(snap.ReportPending);

        var restored = Prep();
        restored.Snapshot.Restore(snap);
        var q = restored.Queries;
        Assert.Equal(Phase.Prep, q.Phase);
        Assert.Equal(1, q.NightNumber);
        Assert.Equal(3L, q.Now.Value);
        Assert.True(q.RunModeActive);

        // Behaviour continues correctly from the restored state.
        AssertEvents(Ok(restored.Commands.StartService()), new NightStarted(2), new ServiceBegan(2));
    }

    [Fact]
    public void Snapshot_round_trip_in_settlement_preserves_pending_report()
    {
        var s = SettlementReportPending();
        s.Commands.Tick(2); // advance the clock inside settlement

        var snap = s.Snapshot.Capture();
        Assert.Equal(1, snap.SchemaVersion);
        Assert.Equal(Phase.Settlement, snap.Phase);
        Assert.True(snap.ReportPending);

        var restored = Prep();
        restored.Snapshot.Restore(snap);
        var q = restored.Queries;
        Assert.Equal(Phase.Settlement, q.Phase);
        Assert.Equal(snap.NightNumber, q.NightNumber);
        Assert.Equal(snap.Now, q.Now.Value);
        Assert.Equal(snap.RunModeActive, q.RunModeActive);

        // Report pending survived the round-trip: DismissReport is now legal.
        AssertEvents(
            Ok(restored.Commands.DismissReport()),
            new ReportDismissed(snap.NightNumber),
            new PrepBegan(snap.NightNumber + 1));
    }

    [Fact]
    public void Capture_during_service_throws()
    {
        Assert.Throws<InvalidOperationException>(() => Service().Snapshot.Capture());
        Assert.Throws<InvalidOperationException>(() => ServiceDraining().Snapshot.Capture());
    }

    [Fact]
    public void Restore_rejects_unsupported_schema_version()
    {
        var s = Prep();
        var bad = new CycleSnapshot(
            SchemaVersion: 2, Phase: Phase.Prep, NightNumber: 1,
            Now: 0, RunModeActive: false, ReportPending: false);
        Assert.Throws<NotSupportedException>(() => s.Snapshot.Restore(bad));
    }
}
