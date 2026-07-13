# DOM-007: Progression

> Status: APPROVED (Gate 3 PASSED 2026-07-13)
> Parents: [PDD](../design/PDD.md), [SYS-007 Acclaim & Prestige](../systems/SYS-007-acclaim-prestige.md), [SYS-008 Venues](../systems/SYS-008-venues.md)
> Contracts: [CON-013](../contracts/CON-013-progression-api.md), [CON-014](../contracts/CON-014-progression-content-schema.md)
> Tickets: — (added in stage 5)

## Bounded context

Models long-term progression across prestiges: milestones and their detection, the Acclaim account, the shop (perk tree, special rooms, named employees), active abilities, the prestige operation, and venues. Venues live here (merge decided by user 2026-07-13): venue *behavior* — unlock via milestones, choice at prestige, venue-bound milestone gating — is progression behavior, and what remains is a static sheet other domains read through ports.

Ubiquitous language:

- **Milestone** — one-time Acclaim-earning condition over feats; may be venue-bound (REQ-029/032/036/088); visible from start unless flagged secret (REQ-112).
- **Feat** — a fact pattern observed from other domains (guest volume, VIP satisfaction, synergy activation, build/venue state) that milestone conditions match against.
- **Acclaim account** — lifetime earned total (never decreases), spent amount, available balance; refund-at-prestige (REQ-033/039/077).
- **Shop** — the single Acclaim spend surface: perks (prerequisite tree, REQ-076), special rooms, named employees (REQ-034/079).
- **Perk effect** — unrestricted kind (REQ-078); active abilities carry cooldown, uses-per-night, optional resource costs (REQ-080).
- **Unlock** — content availability flag that persists across prestiges (REQ-035).
- **Prestige** — reset of tavern/staff/gold + full Acclaim redistribution + venue choice (REQ-033/037/038); any time, mid-service abandons the night unsettled (REQ-113).
- **Venue / venue sheet** — lot rectangle, entrance cell, terrain features, guest-pool modifiers, exclusive content, economy multipliers, venue milestones (REQ-081–088); **starter venue** fixed per fresh save (REQ-089); **run** — one venue, locked until prestige (REQ-090).

Boundary: does not award Acclaim mid-night (DOM-004 executes awards at settlement per REQ-021), implement unlocked content's behavior (lives in its domain; this domain flips availability), execute the physical reset (emits `PrestigeExecuted`; DOM-001/004/005 reset via their commands), or build within lots (DOM-001).

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|
| REQ-029, REQ-032 | SYS-007 | Milestone book: one-time conditions over feats; lifetime accumulation |
| REQ-031 | SYS-007 | No fail-state logic anywhere; progression is strictly additive |
| REQ-033, REQ-037 | SYS-007 | Prestige computes refunded pool; emits reset directives |
| REQ-034, REQ-079 | SYS-007 | Single shop, single pool, three purchase kinds |
| REQ-035 | SYS-007 | Unlock registry persists in lifetime save scope |
| REQ-036, REQ-088 | SYS-007/008 | Venue-bound milestone conditions |
| REQ-038, REQ-090 | SYS-007/008 | Venue choice at prestige from unlocked set; venue locked per run |
| REQ-039 | SYS-007 | Spend prep-gated; refund only via prestige |
| REQ-076, REQ-077 | SYS-007 | Perk prerequisite tree; Acclaim as pure points |
| REQ-078 | SYS-007 | Perk effect kinds unrestricted; sub-system perks gated here, designed at content design (Q-044) |
| REQ-080 | SYS-007 | Active-ability spec: cooldown, uses/night, resource costs |
| REQ-081–REQ-087 | SYS-008 | Owns the venue sheet schema; serves lot/terrain/entrance to DOM-001, pool modifiers/exclusives to DOM-003, cost multipliers to DOM-004 |
| REQ-089 | SYS-008 | Fresh save starts at the fixed starter venue (prototype: only venue) |
| REQ-112 | SYS-007 | Milestone list visibility + secret flag |
| REQ-113 | SYS-007 | Prestige command legal in any phase; mid-service path skips settlement |

## Domain model

Pure C# — no engine types.

- **Aggregates:** `MilestoneBook` (definitions + earned set + pending-award set), `AcclaimAccount`, `Shop` (catalog, ownership, perk tree state), `AbilityTracker` (cooldowns, uses-per-night), `VenueRegistry` (sheets + unlock state), `CurrentRun` (chosen venue, run number).
- **Value objects:** `MilestoneDef` (condition expr over feats, Acclaim value, venue binding, secret flag), `Feat` (typed samples: guest stats, VIP satisfied, rule activated, structure metrics), `VenueSheet`, `PerkDef`, `AbilitySpec`, `PurchaseReceipt`.
- **Domain events:** `MilestoneEarned` (pending until settlement), `AcclaimAwarded`, `PurchaseMade`, `UnlockGranted`, `AbilityUsed`, `PrestigeExecuted(chosenVenue)` (directs DOM-001/004/005 resets + DOM-003 clear; codex explicitly exempt).

Milestone flow (REQ-021/113): feats accumulate during service → conditions met mark `MilestoneEarned (pending)` → `CommitSettlementAwards` at settlement finalizes and hands award amounts to DOM-004 → mid-service prestige discards pendings.

## Architecture decisions

Global decisions (orchestration, time, save, presentation, Steamworks, shared kernel) are recorded in [DOM-002](DOM-002-cycle.md) — user-chosen 2026-07-13. Consequences here: feats arrive as routed domain events (`RecordFeat`); Acclaim/unlock/codex state lives in the lifetime save scope, run state in the run scope.

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| Venues merged into Progression | 7-domain map vs 8-domain 1:1 vs other merge | Merged | Venue lifecycle is progression behavior; standalone domain would own a single data port | user (2026-07-13) |

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| `ProgressionCommandsPort` | driving | `RecordFeat(feat)`, `CommitSettlementAwards`, `Purchase(shopItem)` (prep-gated), `UseAbility(id)`, `Prestige(venueChoice)`, `StartRun`, `Snapshot/Restore` (lifetime + run scopes) | CON-013 |
| `ProgressionQueriesPort` | driving | Milestone list (visibility-filtered) + progress, Acclaim balances, shop catalog + affordability + perk tree, unlock state (rooms/employees/guest types/recipes/venues), ability states | CON-013 |
| `VenueDataPort` | driving | Current run's venue sheet: lot/terrain/entrance (→ DOM-001 bridge), pool modifiers/exclusions/exclusives (→ DOM-003 bridge), cost multipliers (→ DOM-004 bridge); unlocked-venue catalog for prestige UI | CON-013 |
| `ProgressionContentPort` | driven | Milestone, perk, ability, and venue definitions (content data) | CON-014 |

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| Progression UI adapter | reads `ProgressionQueriesPort`, calls commands | Godot UI (milestone list, shop/perk tree, prestige flow, venue choice) | TKT-### (stage 5) |
| Content adapter | `ProgressionContentPort` | data files | TKT-### (stage 5) |
| Feat router | orchestrator routing into `RecordFeat` | domain events from DOM-003/006/001/004 | TKT-### (stage 5) |

(DOM-001's `LotConstraintPort`, DOM-003's `AttractionContextPort`, DOM-004's `RunCostModifierPort`, DOM-005's `HireUnlockPort` bridges all bind to `VenueDataPort`/`ProgressionQueriesPort`.)

## Source layout

```
src/domains/progression/        pure domain code + ports
src/adapters/progression/       adapter implementations
tests/domains/progression/      unit tests
tests/contracts/progression/    contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
| Q-044 | Hedge-wizard sub-system perk details | DEFERRED (user) → content design |
| Q-046 | Theatre/playwright system | OPEN — user will revisit; no architectural provision beyond REQ-078's open-ended perk effects |
| Q-047 | Venue roster beyond the starter venue | DEFERRED (user) → content design (prototype = starter venue only) |
| DOM007-Q1 | Milestone condition format | RESOLVED (user 2026-07-13) → typed C# condition kinds + JSON parameters (CON-014 `IMilestoneCondition`) |
