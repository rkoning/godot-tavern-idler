# DOM-002: Cycle

> Status: APPROVED (Gate 3 PASSED 2026-07-13)
> Parents: [PDD](../design/PDD.md), [SYS-002 Night Cycle](../systems/SYS-002-night-cycle.md)
> Contracts: [CON-002](../contracts/CON-002-cycle-api.md)
> Tickets: — (added in stage 5)

## Bounded context

Models the passage of play time: the night cycle as a finite state machine (prep → service → settlement → prep …) and the simulation clock. This domain is the authority on *which phase it is* and *how many ticks remain*; it never knows what other domains do inside a phase.

Ubiquitous language:

- **Night** — one full prep→service→settlement pass, numbered per run.
- **Phase** — Prep | Service | Settlement. Exactly one is active.
- **Tick** — the atomic simulation time unit (fixed timestep, Decision B). All domain timing (service duration, patience, service durations) is expressed in ticks.
- **Service duration** — global tuning constant in ticks (REQ-091, ≈2–3 min real time).
- **Run mode** — "run to next night": settlement + prep are skipped-through automatically at normal speed until cancelled (REQ-007).
- **Early close** — player-triggered stop of new entries; service ends when guests are gone (REQ-017).
- **Drain** — the tail of service after expiry/early close: no new entries, remaining guests finish and leave (REQ-016).

Boundary: this domain owns phase legality and timing only. It does not compute settlement contents (DOM-004), spawn guests (DOM-003), or enforce per-domain prep-only rules (each domain checks the phase itself via `CycleQueries`).

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|
| REQ-003 | SYS-002 | Night cycle FSM with defined start and settlement |
| REQ-005 | SYS-002 | Phase order prep→service→settlement enforced by the FSM |
| REQ-006 | SYS-002 | `StartService` is the only entry to Service, except active run mode |
| REQ-007 | SYS-002 | Run-mode flag drives auto-chaining transitions at normal tick rate; cancellable |
| REQ-016 | SYS-002 | Service expiry starts the Drain sub-state; Settlement entered when guests are gone |
| REQ-017 | SYS-002 | `CloseEarly` enters Drain immediately |
| REQ-091 | SYS-002 | `ServiceDurationTicks` config constant, single source of truth |
| REQ-101 | SYS-002 | Settlement holds until `DismissReport`; only then does Prep begin |

## Domain model

Pure C# — no engine types.

- **Aggregate: `NightCycle`** — root. State: current `Phase`, `NightNumber`, elapsed service ticks, drain flag, run-mode flag, report-pending flag.
- **Value objects:** `Phase` (enum-like), `Tick` (shared kernel), `CycleConfig` (`ServiceDurationTicks`).
- **Domain events (returned from commands/ticks):** `NightStarted`, `ServiceBegan`, `DrainBegan(reason: Expired|ClosedEarly)`, `SettlementTriggered`, `ReportDismissed`, `PrepBegan`, `RunModeEnabled`, `RunModeCancelled`.

FSM transitions (all others rejected): Prep→Service (`StartService` or run mode), Service→Drain (`expiry` or `CloseEarly`), Drain→Settlement (`NotifyAllGuestsGone`), Settlement→Prep (`DismissReport`, or automatically in run mode — exact run-mode report semantics deferred per Q-048).

## Architecture decisions

Global decisions for the whole architecture are recorded here (this domain owns the loop); other DOM docs reference this table.

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| Domain map | 7 domains (Venues→Progression) vs 8 (1:1) vs other merge | 7 domains | Venue behavior already lives in Progression; standalone Venues domain would be a single data port | user (2026-07-13) |
| A. Orchestration | Ticked orchestrator + queued domain events vs pure event bus vs direct calls | Ticked orchestrator + events | Deterministic fixed-order stepping via driving ports; cross-cutting notifications as events dispatched at tick end | user (2026-07-13) |
| B. Simulation time | Fixed-timestep ticks vs variable frame dt | Fixed-timestep ticks | Engine accumulates delta, steps N whole ticks; rendering interpolates. Deterministic, xUnit-testable, exact REQ-091 duration | user (2026-07-13) |
| C. Save model | Snapshot at phase boundaries vs snapshot anytime vs event sourcing | Snapshot at phase boundaries | Saves written during prep + at settlement; quitting mid-service discards the night (consistent with REQ-113 abandonment) | user (2026-07-13) |
| D. Presentation binding | Pull view-model vs push event mirror | Pull view-model | Adapters read domain view snapshots each frame through read ports and reconcile; cheap at capped guest scale | user (2026-07-13) |
| E. Steamworks library (Q-010) | Defer vs Steamworks.NET vs Facepunch vs GodotSteam | Deferred to post-prototype | Not prototype-critical; Steam Auto-Cloud syncs save files with zero code. Library chosen when achievements/overlay work starts | user (2026-07-13) |
| Shared kernel | None vs minimal value types | Minimal | ID value types, `Money`, `Tick` only — no behavior | user (2026-07-13, part of map approval) |

Composition note (Decision A): a thin engine-free **application orchestrator** (`src/app/`) owns tick order — Cycle → Guests → Traits → effect application — and routes domain events between domains. A minimal Godot node adapter accumulates frame delta and calls it. The orchestrator holds no game rules.

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| `CycleCommandsPort` | driving | `StartService`, `CloseEarly`, `EnableRunMode`, `CancelRunMode`, `DismissReport`, `NotifyAllGuestsGone`, `NotifySettlementComputed`, `Tick(n)`; each returns resulting domain events | CON-002 |
| `CycleQueriesPort` | driving | Current phase, night number, elapsed/remaining service ticks, run-mode state (read by all domains' prep-gates and by UI) | CON-002 |
| `CycleSnapshotPort` | driving | Serialize/restore cycle state at phase boundaries (Decision C) | CON-002 |

No driven ports: the FSM needs nothing from outside; time and completion signals are pushed in by the orchestrator.

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| `GameLoopNode` | calls orchestrator → `CycleCommandsPort.Tick` | Godot `_Process` delta accumulation | TKT-### (stage 5) |
| Cycle HUD/controls | calls `CycleCommandsPort`/`CycleQueriesPort` | Godot UI (start night, early close, run mode, report dismissal) | TKT-### (stage 5) |
| Persistence adapter | `CycleSnapshotPort` (+ all domains' snapshot ports) | JSON on disk; Steam Auto-Cloud syncs the save directory | TKT-### (stage 5) |

## Source layout

```
src/domains/cycle/         pure domain code + ports
src/app/                   engine-free orchestrator (composition root logic)
src/adapters/cycle/        adapter implementations
tests/domains/cycle/       unit tests
tests/contracts/cycle/     contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
| Q-048 | Run-mode edge semantics (report while chaining, early close, interrupts) | DEFERRED (user) → post-prototype |
