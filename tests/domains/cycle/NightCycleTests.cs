using System;
using System.Collections.Generic;
using TavernIdler.Domains.Cycle;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Domains.Cycle;

/// <summary>
/// NightCycle unit tests (TKT-010). The CON-002 contract behaviour is asserted by
/// <see cref="NightCycleConformanceTests"/>; these cover aggregate-level details the
/// conformance suite leaves to the implementation: construction guards, state resets
/// between nights, and clock behaviour inside drain.
/// </summary>
public sealed class NightCycleTests
{
    private const int ServiceDuration = 8;

    private static NightCycle Fresh() => new(new CycleConfig(ServiceDuration));

    private static IReadOnlyList<IDomainEvent> Ok(Outcome<CycleError> outcome) =>
        Assert.IsType<Outcome<CycleError>.Success>(outcome).Events;

    // ── Construction (REQ-091: ServiceDurationTicks > 0) ─────────
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Construction_rejects_non_positive_service_duration(int ticks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NightCycle(new CycleConfig(ticks)));
    }

    [Fact]
    public void Construction_starts_the_run_in_prep_before_night_one()
    {
        var cycle = Fresh();
        Assert.Equal(Phase.Prep, cycle.Phase);
        Assert.Equal(0, cycle.NightNumber);
        Assert.Equal(0L, cycle.Now.Value);
        Assert.False(cycle.RunModeActive);
        Assert.False(cycle.IsDraining);
    }

    // ── Per-night state resets ───────────────────────────────────
    [Fact]
    public void Starting_a_new_night_resets_service_progress_and_drain()
    {
        var cycle = Fresh();
        Ok(cycle.StartService());
        cycle.Tick(ServiceDuration);        // expires into drain
        Ok(cycle.NotifyAllGuestsGone());
        Ok(cycle.NotifySettlementComputed());
        Ok(cycle.DismissReport());

        Ok(cycle.StartService());

        Assert.Equal(Phase.Service, cycle.Phase);
        Assert.False(cycle.IsDraining);
        Assert.Equal(0, cycle.ElapsedServiceTicks);
        Assert.Equal(ServiceDuration, cycle.RemainingServiceTicks);
    }

    [Fact]
    public void Second_night_expires_on_its_own_full_duration()
    {
        var cycle = Fresh();
        Ok(cycle.StartService());
        Ok(cycle.CloseEarly());             // night 1 cut short at elapsed 0
        Ok(cycle.NotifyAllGuestsGone());
        Ok(cycle.NotifySettlementComputed());
        Ok(cycle.DismissReport());

        Ok(cycle.StartService());
        Assert.Empty(cycle.Tick(ServiceDuration - 1));
        Assert.False(cycle.IsDraining);
        Assert.Equal(new DrainBegan(DrainReason.Expired), Assert.Single(cycle.Tick(1)));
    }

    // ── Run mode spans nights until cancelled (REQ-007) ──────────
    [Fact]
    public void Run_mode_stays_active_across_a_full_night()
    {
        var cycle = Fresh();
        Ok(cycle.EnableRunMode());

        Ok(cycle.StartService());
        Ok(cycle.CloseEarly());
        Ok(cycle.NotifyAllGuestsGone());
        Ok(cycle.NotifySettlementComputed());
        Ok(cycle.DismissReport());

        Assert.True(cycle.RunModeActive);
    }

    [Fact]
    public void Cancelling_run_mode_in_service_leaves_the_night_running()
    {
        var cycle = Fresh();
        Ok(cycle.StartService());
        Ok(cycle.EnableRunMode());

        Ok(cycle.CancelRunMode());

        Assert.False(cycle.RunModeActive);
        Assert.Equal(Phase.Service, cycle.Phase);
        Assert.False(cycle.IsDraining);
        Assert.Equal(ServiceDuration, cycle.RemainingServiceTicks);
    }

    // ── Clock during drain ───────────────────────────────────────
    [Fact]
    public void Ticking_during_drain_advances_the_clock_without_events()
    {
        var cycle = Fresh();
        Ok(cycle.StartService());
        cycle.Tick(3);
        Ok(cycle.CloseEarly());
        var before = cycle.Now.Value;

        Assert.Empty(cycle.Tick(5));

        Assert.Equal(before + 5, cycle.Now.Value);
        Assert.True(cycle.IsDraining);
        Assert.Equal(0, cycle.RemainingServiceTicks);
    }

    [Fact]
    public void Elapsed_service_ticks_never_exceed_the_configured_duration()
    {
        var cycle = Fresh();
        Ok(cycle.StartService());
        cycle.Tick(ServiceDuration + 6);

        Assert.Equal(ServiceDuration, cycle.ElapsedServiceTicks);
        Assert.Equal(0, cycle.RemainingServiceTicks);
    }

    // ── Restore clears transient service state ───────────────────
    [Fact]
    public void Restore_overwrites_in_flight_service_state()
    {
        var live = Fresh();
        Ok(live.StartService());
        live.Tick(4);                       // mid-service, night 1

        var snapshot = new CycleSnapshot(
            SchemaVersion: 1, Phase: Phase.Prep, NightNumber: 6,
            Now: 250L, RunModeActive: true, ReportPending: false);

        live.Restore(snapshot);

        Assert.Equal(Phase.Prep, live.Phase);
        Assert.False(live.IsDraining);
        Assert.Equal(6, live.NightNumber);
        Assert.Equal(250L, live.Now.Value);
        Assert.True(live.RunModeActive);
        Assert.Equal(0, live.ElapsedServiceTicks);

        Assert.Equal(
            new List<IDomainEvent> { new NightStarted(7), new ServiceBegan(7) },
            Ok(live.StartService()));
    }
}
