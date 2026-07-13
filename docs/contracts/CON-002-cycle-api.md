# CON-002: Cycle API v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + domain events
> Provider: DOM-002 Cycle
> Consumers: DOM-001/003/004/005/007 (phase gating via `ICycleQueries`), app orchestrator, cycle UI adapter, persistence adapter
> Conformance tests: `tests/contracts/cycle/`

## Purpose

The night-cycle FSM: phase authority, night clock, run mode, early close, report dismissal. Traces: REQ-003, REQ-005, REQ-006, REQ-007, REQ-016, REQ-017, REQ-091, REQ-101.

## Interface definition

```csharp
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
```

## Semantics

- **FSM legality** (anything else → `WrongPhase`): `StartService` in Prep; `CloseEarly` in Service while not draining; `NotifyAllGuestsGone` in Service while draining; `NotifySettlementComputed` in Settlement (exactly once per night); `DismissReport` in Settlement with report pending; `EnableRunMode`/`CancelRunMode` in Prep or Service.
- **Tick:** legal in every phase (`Now` always advances). During Service, `ElapsedServiceTicks` accrues; when it reaches `ServiceDurationTicks`, the same `Tick` call emits `DrainBegan(Expired)` (REQ-016). Tick never crosses a phase boundary by itself beyond entering drain — Settlement entry requires `NotifyAllGuestsGone`.
- **StartService** emits `NightStarted` + `ServiceBegan` and increments `NightNumber`. `NotifyAllGuestsGone` emits `SettlementTriggered` and enters Settlement. `DismissReport` emits `ReportDismissed` + `PrepBegan` and enters Prep.
- **Run mode (REQ-007):** while active, the orchestrator — not this domain — auto-issues `DismissReport` then `StartService` (sequence in CON-016). This domain only stores the flag and emits its events. `CancelRunMode` during Service lets the current night finish into a normal prep. Edge semantics beyond this are deferred (Q-048) and MUST NOT be assumed by implementers.
- **Mid-service prestige (REQ-113):** handled by full domain re-construction/`Restore` per CON-016 prestige sequence; no cycle command models it.
- **Snapshot:** `Capture` outside Prep/Settlement throws `InvalidOperationException` (decision C: no mid-service saves). `Restore` accepts only `SchemaVersion == 1`, else `NotSupportedException`.
- **Ranges:** `ticks ≥ 1` (else `ArgumentOutOfRangeException`); `ServiceDurationTicks > 0`. Single-threaded per CON-016.

## Conformance tests

`tests/contracts/cycle/`:

- Full legal-transition walk: Prep→Service→drain(expiry)→Settlement→Prep with exact event sequences asserted.
- Every command × every illegal phase → `Failure(WrongPhase)` (exhaustive matrix), state unchanged.
- Expiry: `Tick` that reaches `ServiceDurationTicks` emits `DrainBegan(Expired)` exactly once; further ticks emit nothing new; `RemainingServiceTicks == 0`.
- Early close emits `DrainBegan(ClosedEarly)`; expiry after early close emits nothing.
- `DismissReport` before `NotifySettlementComputed` → `ReportNotPending` (REQ-101).
- `NightNumber` increments only on `StartService`; `Now` monotonic across phases.
- Snapshot round-trip in Prep and in Settlement preserves all queryable state; `Capture` during Service throws.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
