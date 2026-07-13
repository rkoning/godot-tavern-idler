namespace TavernIdler.Domains.Cycle;
using TavernIdler.Kernel;

public enum Phase { Prep, Service, Settlement }

public enum DrainReason { Expired, ClosedEarly }

public enum CycleError
{
    WrongPhase,          // command not legal in current phase
    RunModeAlreadyInThatState,
    NotDraining,         // NotifyAllGuestsGone outside drain
    ReportNotPending,    // DismissReport before settlement computed
    SettlementNotTriggered
}

public sealed record CycleConfig(int ServiceDurationTicks); // REQ-091 tuning constant, > 0

public interface ICycleCommands
{
    Outcome<CycleError> StartService();            // Prep → Service (REQ-006)
    Outcome<CycleError> CloseEarly();              // Service → draining (REQ-017)
    Outcome<CycleError> EnableRunMode();           // Prep or Service (REQ-007)
    Outcome<CycleError> CancelRunMode();
    Outcome<CycleError> NotifyAllGuestsGone();     // draining → Settlement
    Outcome<CycleError> NotifySettlementComputed();// marks report pending (REQ-101)
    Outcome<CycleError> DismissReport();           // Settlement → Prep (REQ-101)
    IReadOnlyList<IDomainEvent> Tick(int ticks);   // advances clock; ticks ≥ 1
}

public interface ICycleQueries
{
    Phase Phase { get; }
    bool IsDraining { get; }              // true only during Service after expiry/early close
    int NightNumber { get; }              // 1-based, increments at StartService
    Tick Now { get; }                     // absolute sim time
    int ElapsedServiceTicks { get; }      // 0 outside Service
    int RemainingServiceTicks { get; }    // 0 once draining
    bool RunModeActive { get; }
}

public interface ICycleSnapshot
{
    CycleSnapshot Capture();              // legal in Prep or Settlement only
    void Restore(CycleSnapshot snapshot);
}

public sealed record CycleSnapshot(
    int SchemaVersion,     // 1
    Phase Phase,           // Prep or Settlement only
    int NightNumber,
    long Now,
    bool RunModeActive,
    bool ReportPending);

// ── Events ──────────────────────────────────────────────────
public sealed record NightStarted(int NightNumber) : IDomainEvent;
public sealed record ServiceBegan(int NightNumber) : IDomainEvent;
public sealed record DrainBegan(DrainReason Reason) : IDomainEvent;
public sealed record SettlementTriggered(int NightNumber) : IDomainEvent;
public sealed record ReportDismissed(int NightNumber) : IDomainEvent;
public sealed record PrepBegan(int NightNumber) : IDomainEvent;       // next night's number
public sealed record RunModeEnabled() : IDomainEvent;
public sealed record RunModeCancelled() : IDomainEvent;
