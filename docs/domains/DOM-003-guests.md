# DOM-003: Guests

> Status: APPROVED (Gate 3 PASSED 2026-07-13)
> Parents: [PDD](../design/PDD.md), [SYS-003 Guest Simulation](../systems/SYS-003-guest-simulation.md)
> Contracts: [CON-005](../contracts/CON-005-guests-api.md), [CON-006](../contracts/CON-006-guests-driven-ports.md), [CON-015](../contracts/CON-015-random-port.md)
> Tickets: — (added in stage 5)

## Bounded context

Models guests as simulated agents: who shows up, how they queue, move, pursue agendas, spend, react to crowds, and leave. Authority on every guest's state and on nightly guest statistics; consumer of everything else (structure, prices, room states, effects).

Ubiquitous language:

- **Guest** — an agent instance of a `GuestType`; **VIP** — unique named guest with visit conditions and bespoke rules (REQ-049/050).
- **Guest sheet (`GuestType`)** — identity/sprite ref (id only — no engine types), attraction weights, crowding preference, queue patience, agenda, wallet range, trait list, VIP visit conditions (REQ-052).
- **Agenda / Want** — ordered wants-list; a want targets a service (room/menu/employee-service); blocked wants wait (patience-bounded), penalize, then skip (REQ-048/053).
- **Wallet** — finite gold carried; zero → immediate departure (REQ-051/054).
- **Attraction context** — composition + lifetime Acclaim + venue pool modifiers; drives both type weights and arrival rate (REQ-024/102).
- **Trickle** — arrivals distributed across the service phase; overflow queues (REQ-102).
- **Queue** — outside line with per-guest patience; visible portion + "+N waiting" (REQ-010/018).
- **Crowding** — per-room occupancy-vs-capacity ratio a guest's preference reacts to (REQ-009/103).
- **Satisfaction** — per-guest scalar from fulfilled/unfulfilled wants and effects; modifies transaction gold only (REQ-023).
- **Lodger** — guest who bought lodging; persists through settlement/prep, leaves at next service start (REQ-107).

Boundary: does not own rooms or the graph (reads DOM-001), prices or ledgers (requests transactions from DOM-004), staffing states (reads DOM-005 outputs), rule evaluation (DOM-006 pushes effects in), or milestones (emits feat statistics DOM-007 consumes).

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|
| REQ-002 | SYS-003 | Guests are individually simulated agents; positions exposed via the view port |
| REQ-008 | SYS-003 | Admission capped by structural capacity read from DOM-001 |
| REQ-009 | SYS-003 | Crowding preference applied to satisfaction and payment modifier |
| REQ-010 | SYS-003 | Queue model with visible-agents + overflow count |
| REQ-018 | SYS-003 | Queue admission as capacity frees; per-guest patience expiry |
| REQ-023 | SYS-003 | Satisfaction passed as payment modifier on each transaction request |
| REQ-024 | SYS-003 | Weighted draw from attraction context (no player invitation control) |
| REQ-048 | SYS-003 | Agenda execution engine |
| REQ-049 | SYS-003 | Ordinary anonymous instances vs unique VIP entities |
| REQ-050 | SYS-003 | VIP condition check + per-night visit roll |
| REQ-051 | SYS-003 | Wallet enforcement on every spend |
| REQ-052 | SYS-003 | Owns the guest sheet schema |
| REQ-053 | SYS-003 | Blocked-want wait/penalty/skip behavior |
| REQ-054 | SYS-003 | Zero-wallet immediate departure |
| REQ-055 | SYS-003 | VIP revisit semantics; no milestone consumption on unsatisfied exit |
| REQ-092 | SYS-003 | Patience values validated against the 10–30%-of-night band (config-time check) |
| REQ-093 | SYS-003 | Content scope marker (~10 types + ~5 VIPs); schema must support both kinds |
| REQ-102 | SYS-003 | Arrival-rate function from the attraction context |
| REQ-103 | SYS-003 | Per-room crowding evaluation |
| REQ-104 | SYS-003 | Service occupancy = base duration (from room sheet) × staffing/trait/perk modifiers |
| REQ-107 | SYS-003 | Lodger persistence across settlement/prep |

## Domain model

Pure C# — no engine types.

- **Aggregate: `GuestPopulation`** — root for a night: active guests, the queue, nightly statistics; plus persistent lodgers between nights.
- **Entities:** `Guest` (id, type ref, wallet, satisfaction, agenda cursor, position/path state, patience timers), `VipRegistry` entry (per-VIP visit state).
- **Value objects:** `GuestTypeSheet`, `AgendaItem`, `Wallet`, `Satisfaction`, `CrowdingPreference`, `ArrivalPlan`, `NightGuestStats` (served counts, per-type breakdown, satisfaction summary, notable events — feeds the REQ-022 report and feat detection).
- **Domain events:** `GuestAdmitted`, `GuestQueued`, `QueueOverflowChanged`, `GuestLeft(reason)`, `WantFulfilled`, `WantBlocked`, `VipVisited`, `VipSatisfied`, `AllGuestsGone` (drives DOM-002 drain completion), `GuestFeatSample` (stat deltas for milestones).

Movement: guests path over the DOM-001 `TraversalGraph`; travel time per cell is a tuning constant (DOM003-Q1).

## Architecture decisions

Global decisions (orchestration, time, save, presentation, Steamworks, shared kernel) are recorded in [DOM-002](DOM-002-cycle.md) — user-chosen 2026-07-13. Consequences here: guests advance only in `Tick`; effects from Traits arrive as commands between ticks; saves never occur mid-service, so in-flight agent state (except lodgers) is never serialized.

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| Lodger persistence form | Serialize lodger `Guest` entities in prep snapshots vs reduce to a `Lodger` record (type + room + wallet + return state) | Reduced `LodgerRecord` | Only lodging-relevant state survives the night; full agent state never serialized (Decision C) | user (2026-07-13, contract batch approval — CON-005) |

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| `GuestSimCommandsPort` | driving | `BeginService(attraction context, seed)`, `Tick(n)`, `BeginDrain`, `EndNight`, `ApplyEffect(satisfaction/behavior event → targets)` (from Traits via orchestrator), `Snapshot/Restore` (lodgers + VIP registry + stats only) | CON-005 |
| `GuestViewPort` | driving | Pull view-model (Decision D): agent positions/sprites ids, queue visible line + "+N", per-room occupancy, satisfaction/trait hover data | CON-005 |
| `PresenceQueryPort` | driving | Who (guests) is in which room + trait lists — read by the Traits bridge | CON-005 |
| `StructureAccessPort` | driven | Traversal graph, entrance, room capacity/services/base durations, active states (over DOM-001) | CON-006 |
| `RoomServiceStatePort` | driven | Open/degraded/closed per room + service-speed modifiers (over DOM-005) | CON-006 |
| `TransactionPort` | driven | Request a purchase (item/lodging/fee/service) with satisfaction + spending multipliers; returns priced outcome incl. sell-out (over DOM-004) | CON-006 |
| `AttractionContextPort` | driven | Lifetime Acclaim, venue weight multipliers/exclusions/exclusives, composition inputs (assembled over DOM-007/001/004) | CON-006 |
| `RandomPort` | driven | Seeded deterministic RNG (draws, VIP rolls, patience jitter) | CON-015 |

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| Guest render adapter | reads `GuestViewPort` | Godot sprites/AnimatedSprite2D, interpolating between tick positions | TKT-### (stage 5) |
| Structure bridge | `StructureAccessPort` | in-process call into DOM-001 queries | TKT-### (stage 5) |
| Staffing bridge | `RoomServiceStatePort` | in-process call into DOM-005 queries | TKT-### (stage 5) |
| Economy bridge | `TransactionPort` | in-process call into DOM-004 commands | TKT-### (stage 5) |
| Attraction bridge | `AttractionContextPort` | in-process composition over DOM-007/001/004 queries | TKT-### (stage 5) |
| RNG adapter | `RandomPort` | seeded `System.Random`-based implementation (still engine-free; lives in adapters for seed management) | TKT-### (stage 5) |
| Guest content adapter | guest sheet catalog into `BeginService`/init | data files | TKT-### (stage 5) |

## Source layout

```
src/domains/guests/        pure domain code + ports
src/adapters/guests/       adapter implementations
tests/domains/guests/      unit tests
tests/contracts/guests/    contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
| DOM003-Q1 | Guest movement speed | RESOLVED 2026-07-13 → `GuestTicksPerCell` config constant in CON-016 `GameConfig`; value set in playtesting |
| DOM003-Q2 | Lodger snapshot form | RESOLVED 2026-07-13 → reduced `LodgerRecord` (CON-005); keeps Decision C's no-mid-flight-serialization clean |
