# CON-016: Orchestration & Adapter Binding Conventions v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: adapter binding
> Provider: app layer (`src/app/`)
> Consumers: every domain ticket (assumes these guarantees), every adapter ticket (must follow these conventions)
> Conformance tests: `tests/contracts/app/`

## Purpose

The glue rules: tick order, event routing, phase-gating responsibilities, threading, the Godot binding, and the prestige/settlement sequences. This is the contract that makes the per-domain contracts composable. Traces: Decisions A/B/D (user 2026-07-13); REQ-005–007, REQ-021, REQ-037, REQ-113.

## Interface definition

```csharp
namespace TavernIdler.App;
using TavernIdler.Kernel;

/// The engine-free composition root + per-tick driver. The ONLY caller of domain
/// command ports during simulation; UI adapters call domain commands only for
/// player-initiated prep/mid-service actions listed below.
public interface IGameLoop
{
    /// Advance the simulation by whole ticks (Decision B). Called by the engine adapter.
    void Advance(int ticks);
    /// Player intents (UI adapters call these; the loop forwards to domain ports
    /// and runs the follow-up routing below).
    void StartNight();
    void CloseEarly();
    void SetRunMode(bool enabled);
    void DismissReport();
    void PrestigeTo(VenueId venue);
}

public sealed record GameConfig(
    int ServiceDurationTicks,      // REQ-091
    int TicksPerSecond,            // fixed timestep rate (Decision B), e.g. 15
    int MaxTicksPerFrame,          // spiral-of-death cap, e.g. 5
    double ArrivalRateFactor,      // REQ-102 (CON-006)
    int GuestTicksPerCell,         // DOM003-Q1: movement speed tuning constant
    Money StartingGold,
    long RngSeed);
```

## Semantics

### Tick pipeline (Decision A — normative order, one sim tick during Service)

1. `ICycleCommands.Tick(1)` — route events (may emit `DrainBegan`).
2. `IGuestSimCommands.Tick(1)` — inside it, guests call CON-006 driven ports synchronously.
3. `IProgressionCommands.TickAbilities(1)`.
4. `ITraitsCommands.Tick()` — pulls presence (CON-012) reflecting step 2's state.
5. Route `TraitsTickResult.Effects` → `IGuestSimCommands.ApplyEffects(...)`.
6. Route all collected domain events (routing table below).

Outside Service, only step 1 runs (clock still advances). Steps execute synchronously, in order, single-threaded. **No domain may call another domain's driving port directly** — only bridges implementing driven ports (reads) and this loop (commands).

### Event routing table (exhaustive; unlisted events are UI/log-only)

| Event (contract) | Routed to |
|---|---|
| `DrainBegan` (CON-002) | `IGuestSimCommands.BeginDrain()` |
| `ServiceBegan` (CON-002) | `IStaffingCommands.EvaluateRoomStates()`, `IGuestSimCommands.BeginService()`, `ITraitsCommands.BeginNight()`, `IProgressionCommands.BeginNightReset()` |
| `AllGuestsGone` (CON-005) | `ICycleCommands.NotifyAllGuestsGone()` → triggers settlement sequence |
| `NightStatsFinal` (CON-005) | `IProgressionCommands.RecordFeat(NightStats)`; stats held for settlement |
| `SettlementTriggered` (CON-002) | settlement sequence (below) |
| `RoomDemolished`/`RoomMoved` (CON-003) | `IStaffingCommands.OnRoomRemoved(room)` |
| `RoomDeactivated` (CON-003) | `IStaffingCommands.OnRoomDeactivated(room)` |
| `StructureChanged` (CON-003) | `IProgressionCommands.RecordFeat(StructureState(metrics))` |
| `VipSatisfied` (CON-005) | `IProgressionCommands.RecordFeat(VipSatisfied)` |
| `RuleActivated` (CON-011) | `IProgressionCommands.RecordFeat(RuleActivated)` |
| `InsolvencyDeclared` (CON-007) | `IStaffingCommands.SetRefusals(unpaid, true)` |
| `BackPayCleared` (CON-007) | `IStaffingCommands.SetRefusals(cleared, false)` |
| `AbilityUsed` (CON-013) | apply `AbilityEffect` (gold cost via CON-007 charge; effects via `ApplyEffects`/arrival injection) |
| `PrestigeExecuted` (CON-013) | prestige sequence (below) |
| `ReportDismissed` (CON-002) | autosave (CON-017), then if run mode: `StartNight()` |

Routing within one tick is depth-first in emission order; events produced by routed calls are appended and routed in the same tick.

### Settlement sequence (on `SettlementTriggered`)

`awards = IProgressionCommands.CommitSettlementAwards()` → `IEconomyCommands.RunSettlement(new SettlementInput(structure.NightlyUpkeepBill, staffing.WageBill(), awards, heldNightStats))` → route its events → `IGuestSimCommands.EndNight()` → `ITraitsCommands.EndNight()` → `ICycleCommands.NotifySettlementComputed()` → autosave. Run mode active ⇒ `DismissReport()` is auto-issued next frame (REQ-007; deeper edge semantics deferred, Q-048).

### Prestige sequence (on `PrestigeExecuted`)

`IGuestSimCommands.ClearAll()` → `IStaffingCommands.ResetAll()` → `IStructureCommands.ResetAll()` → `IEconomyCommands.ResetGold()` → rebuild venue-dependent bridges from `IVenueData.Current` → `ICycleSnapshot.Restore(fresh Prep, night 0 state)` → codex NOT touched (REQ-044) → autosave. Legal any phase (REQ-113); mid-service, no settlement runs first.

### Phase gating (division of duty)

Domains own their prep/service gates (each command contract lists them) using injected `ICycleQueries`. UI adapters additionally disable ineligible controls, but domain gates are authoritative.

### Godot binding (Decision D)

- One autoload `GameLoopNode` (the only C# Godot node holding the app reference): in `_Process(delta)`, accumulate `delta × TicksPerSecond`, call `Advance(wholeTicks)` capped at `MaxTicksPerFrame` (excess time discarded — slow hardware slows the game, never spirals).
- Presentation adapters read view ports (`IGuestView`, `IStructureQueries`, `IEconomyQueries`, …) in `_Process` **after** `Advance` and reconcile scene nodes; they hold no game state beyond node bookkeeping and interpolate movement via `MoveProgress`.
- UI adapters translate input to `IGameLoop` intents / domain prep commands; they never mutate domain state otherwise.
- All domain and app access on the main thread; adapters must not use threads, `Task.Run`, or Godot signals that re-enter domain code.
- `src/domains/**` and `src/app/**` must not reference Godot assemblies (enforced by an architecture test compiling them against the plain .NET SDK).

### Composition root order

Kernel/config → content adapters load (fail-fast) → domains constructed (Cycle first; others receive `ICycleQueries`, driven-port bridges, `IRandomSource`) → bridges wired → save loaded if present (CON-017) else `StartRun(starterVenue)` → `GameLoopNode` starts.

## Conformance tests

`tests/contracts/app/`:

- Tick order: instrumented fake domains record call order 1–6 exactly; no re-entrancy.
- Routing table: each listed event emitted by a fake ⇒ exactly the specified calls (full table coverage).
- Settlement sequence order + `heldNightStats` plumbed; run-mode auto-dismiss.
- Prestige sequence order; codex untouched; works mid-service without settlement.
- Frame accumulation: fractional deltas accumulate to whole ticks; `MaxTicksPerFrame` cap discards excess.
- Architecture test: domain + app assemblies build with no Godot reference (grep + compile).

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
