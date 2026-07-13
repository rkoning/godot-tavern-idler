# DOM-006: Traits

> Status: APPROVED (Gate 3 PASSED 2026-07-13)
> Parents: [PDD](../design/PDD.md), [SYS-006 Traits & Synergy](../systems/SYS-006-traits-synergy.md)
> Contracts: [CON-011](../contracts/CON-011-traits-api.md), [CON-012](../contracts/CON-012-traits-driven-ports.md), [CON-015](../contracts/CON-015-random-port.md)
> Tickets: — (added in stage 5)

## Bounded context

Models the trait system and its rule engine: trait definitions, trait×trait rules, co-presence tracking, effect emission, and the discovery codex. Authority on which rules exist, when they activate, what effects they emit, and what the player has discovered. It never applies consequences itself — effects are emitted for Guests (satisfaction, behavior events) and Economy (spending multipliers) to execute.

Ubiquitous language:

- **Trait** — named attribute carried by guest types, employee types, room types, menu items; always player-visible on the carrier (REQ-095/043).
- **Carrier** — anything holding traits; carrier *presence* is supplied from outside per tick.
- **Rule** — trait×trait pair with: effect class(es), reach (same-room | tavern-wide), stacking (binary | count-scaling), activation requiring ≥1 guest participant (REQ-040/042/045/046).
- **Effect class** — satisfaction modifier | behavior event | spending multiplier (REQ-042).
- **Co-presence episode** — the span two carriers satisfy a rule's reach; modifiers are continuous over it, behavior events roll once at its start; re-entry starts a new episode (REQ-110).
- **Broadcaster** — room making occupants' same-room effects tavern-wide (REQ-047).
- **Discovery / Codex** — first activation permanently reveals the rule at trait level; codex survives prestige (REQ-111/043/044).

Boundary: does not define carriers (their sheets live in DOM-001/003/004/005 and include trait-list fields), apply effects (DOM-003/004 execute), or detect synergy-feat milestones (emits activation events DOM-007 consumes).

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|
| REQ-040 | SYS-006 | Rule shape: trait×trait, ≥1 guest participant enforced at activation |
| REQ-041 | SYS-006 | Same-room default reach; designated tavern-wide entities |
| REQ-042 | SYS-006 | Three effect classes emitted as typed effects |
| REQ-043 | SYS-006 | Trait visibility is carrier-sheet data; rule hiding + trait-level reveal via codex |
| REQ-044 | SYS-006 | Codex in lifetime save scope, exempt from prestige reset |
| REQ-045 | SYS-006 | Per-rule stacking mode |
| REQ-046 | SYS-006 | Per-rule reach |
| REQ-047 | SYS-006 | Broadcaster flag (from DOM-001 sheet) widens occupants' reach |
| REQ-094 | SYS-006 | Content density target; rule catalog is data-driven |
| REQ-095 | SYS-006 | Trait registry; any carrier, any count, shared traits |
| REQ-096 | SYS-006 | Rule endpoints are trait ids only — enforced by the schema |
| REQ-110 | SYS-006 | Episode tracker: continuous modifiers, once-per-episode event rolls |
| REQ-111 | SYS-006 | Discovery on first activation, immediate + permanent |

## Domain model

Pure C# — no engine types.

- **Aggregates:** `RuleBook` (immutable rule catalog + trait registry), `Codex` (discovered rule ids; lifetime scope), `EpisodeLedger` (active co-presence episodes for the current night).
- **Value objects:** `TraitId`, `Rule` (traitA, traitB, effect specs, reach, stacking), `EffectSpec`/`EmittedEffect` (typed: `SatisfactionModifier`, `BehaviorEvent`, `SpendingMultiplier` — each with targets and scaling), `PresenceSnapshot` (room → carriers + traits, broadcaster flags).
- **Domain events:** `RuleActivated` (feeds feat detection), `EffectEmitted`, `EffectEnded` (modifier episodes), `RuleDiscovered`.

Evaluation per tick: diff `PresenceSnapshot` against `EpisodeLedger` → open/close episodes → roll behavior events on open (via `RandomPort`) → emit continuous-modifier begin/end and multiplier state.

## Architecture decisions

Global decisions (orchestration, time, save, presentation, Steamworks, shared kernel) are recorded in [DOM-002](DOM-002-cycle.md) — user-chosen 2026-07-13. Consequences here: Traits ticks after Guests each tick; emitted effects are routed by the orchestrator to DOM-003/004 in the same tick; codex snapshots with the lifetime save.

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| Presence acquisition | Pull `PresenceSnapshot` via driven port each tick vs subscribe to entry/exit events | Pull per tick | Matches Decision A ordering; diffing is trivial at capped guest scale; no event-ordering hazards | follows Decision A (user 2026-07-13) |

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| `TraitsCommandsPort` | driving | `BeginNight`, `Tick` (evaluate against fresh presence), `EndNight`, `Snapshot/Restore` (codex) | CON-011 |
| `TraitsQueriesPort` | driving | Codex contents (for UI), trait registry, active effects (debug/UI), rule catalog metadata | CON-011 |
| `PresencePort` | driven | `PresenceSnapshot` per tick: guests (DOM-003), assigned employees (DOM-005), room traits/broadcasters (DOM-001), in-play menu items (DOM-004/003 — semantics pending DOM006-Q1) | CON-012 |
| `RandomPort` | driven | Behavior-event rolls (seeded, shared adapter with DOM-003) | CON-015 |

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| Presence bridge | `PresencePort` | in-process composition over DOM-003/005/001/004 queries | TKT-### (stage 5) |
| Codex UI adapter | reads `TraitsQueriesPort` | Godot UI (codex screen, trait hover panels) | TKT-### (stage 5) |
| Rule content adapter | rule catalog into `RuleBook` init | data files | TKT-### (stage 5) |
| RNG adapter | `RandomPort` | shared seeded RNG implementation | TKT-### (stage 5) |

## Source layout

```
src/domains/traits/        pure domain code + ports
src/adapters/traits/       adapter implementations
tests/domains/traits/      unit tests
tests/contracts/traits/    contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
| DOM006-Q1 | Menu-item trait presence | RESOLVED (user 2026-07-13) → item traits participate while being consumed, in the consuming guest's room (CON-012 `ConsumedItem` carrier) |
