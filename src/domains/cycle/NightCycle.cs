namespace TavernIdler.Domains.Cycle;
using TavernIdler.Kernel;

/// <summary>
/// The night-cycle aggregate (DOM-002): phase authority, night clock, drain sub-state,
/// run-mode flag and report gating, per CON-002 v1.0. Pure C#, single-threaded.
/// </summary>
public sealed class NightCycle : ICycleCommands, ICycleQueries, ICycleSnapshot
{
    private const int SnapshotSchemaVersion = 1;

    private static readonly IReadOnlyList<IDomainEvent> NoEvents = Array.Empty<IDomainEvent>();

    private readonly int serviceDurationTicks;

    private Phase phase = Phase.Prep;
    private bool draining;
    private int nightNumber;
    private long now;
    private int elapsedServiceTicks;
    private bool runModeActive;
    private bool reportPending;

    public NightCycle(CycleConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.ServiceDurationTicks <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(config), config.ServiceDurationTicks, "ServiceDurationTicks must be > 0.");
        }

        serviceDurationTicks = config.ServiceDurationTicks;
    }

    // ── ICycleQueries ───────────────────────────────────────────
    public Phase Phase => phase;
    public bool IsDraining => draining;
    public int NightNumber => nightNumber;
    public Tick Now => new(now);
    public int ElapsedServiceTicks => phase == Phase.Service ? elapsedServiceTicks : 0;
    public int RemainingServiceTicks =>
        phase == Phase.Service && !draining ? serviceDurationTicks - elapsedServiceTicks : 0;
    public bool RunModeActive => runModeActive;

    // ── ICycleCommands ──────────────────────────────────────────
    public Outcome<CycleError> StartService()
    {
        if (phase != Phase.Prep) return Failure(CycleError.WrongPhase);

        nightNumber++;
        phase = Phase.Service;
        draining = false;
        elapsedServiceTicks = 0;
        reportPending = false;

        return Success(new NightStarted(nightNumber), new ServiceBegan(nightNumber));
    }

    public Outcome<CycleError> CloseEarly()
    {
        if (phase != Phase.Service || draining) return Failure(CycleError.WrongPhase);

        draining = true;
        return Success(new DrainBegan(DrainReason.ClosedEarly));
    }

    public Outcome<CycleError> EnableRunMode() => SetRunMode(active: true);

    public Outcome<CycleError> CancelRunMode() => SetRunMode(active: false);

    private Outcome<CycleError> SetRunMode(bool active)
    {
        if (phase != Phase.Prep && phase != Phase.Service) return Failure(CycleError.WrongPhase);
        if (runModeActive == active) return Failure(CycleError.RunModeAlreadyInThatState);

        runModeActive = active;
        return active
            ? Success(new RunModeEnabled())
            : Success(new RunModeCancelled());
    }

    public Outcome<CycleError> NotifyAllGuestsGone()
    {
        if (phase != Phase.Service) return Failure(CycleError.WrongPhase);
        if (!draining) return Failure(CycleError.NotDraining);

        phase = Phase.Settlement;
        draining = false;
        elapsedServiceTicks = 0;

        return Success(new SettlementTriggered(nightNumber));
    }

    public Outcome<CycleError> NotifySettlementComputed()
    {
        // Legal in Settlement, exactly once per night — a second call is out of turn.
        if (phase != Phase.Settlement || reportPending) return Failure(CycleError.WrongPhase);

        reportPending = true;
        return Success();
    }

    public Outcome<CycleError> DismissReport()
    {
        if (phase != Phase.Settlement) return Failure(CycleError.WrongPhase);
        if (!reportPending) return Failure(CycleError.ReportNotPending);

        reportPending = false;
        phase = Phase.Prep;

        return Success(new ReportDismissed(nightNumber), new PrepBegan(nightNumber + 1));
    }

    public IReadOnlyList<IDomainEvent> Tick(int ticks)
    {
        if (ticks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks), ticks, "ticks must be >= 1.");
        }

        now += ticks;

        if (phase != Phase.Service || draining) return NoEvents;

        // Service expiry (REQ-016): the tick that reaches the duration enters drain.
        elapsedServiceTicks = Math.Min(serviceDurationTicks, elapsedServiceTicks + ticks);
        if (elapsedServiceTicks < serviceDurationTicks) return NoEvents;

        draining = true;
        return new IDomainEvent[] { new DrainBegan(DrainReason.Expired) };
    }

    // ── ICycleSnapshot ──────────────────────────────────────────
    public CycleSnapshot Capture()
    {
        if (phase == Phase.Service)
        {
            throw new InvalidOperationException(
                "Cycle snapshots are taken at phase boundaries only; Service is not one.");
        }

        return new CycleSnapshot(
            SnapshotSchemaVersion, phase, nightNumber, now, runModeActive, reportPending);
    }

    public void Restore(CycleSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.SchemaVersion != SnapshotSchemaVersion)
        {
            throw new NotSupportedException(
                $"Unsupported cycle snapshot schema version {snapshot.SchemaVersion}.");
        }

        if (snapshot.Phase == Phase.Service)
        {
            throw new ArgumentException(
                "Cycle snapshots cannot carry the Service phase.", nameof(snapshot));
        }

        phase = snapshot.Phase;
        nightNumber = snapshot.NightNumber;
        now = snapshot.Now;
        runModeActive = snapshot.RunModeActive;
        reportPending = snapshot.ReportPending;
        draining = false;
        elapsedServiceTicks = 0;
    }

    // ── Outcome helpers ─────────────────────────────────────────
    private static Outcome<CycleError> Success(params IDomainEvent[] events) =>
        new Outcome<CycleError>.Success(events.Length == 0 ? NoEvents : events);

    private static Outcome<CycleError> Failure(CycleError error) =>
        new Outcome<CycleError>.Failure(error);
}
