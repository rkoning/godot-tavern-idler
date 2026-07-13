# Project Design Document: Tavern Idler (working title)

> Status: BROKEN DOWN (Gate 2 PASSED 2026-07-13) — all REQs partitioned into SYS-001..008
> Last session: 2026-07-13 (/breakdown: REQ-097..113 added; REQ-016 exception via REQ-107; REQ-039/056/068/074 amended; Q-048 deferred)
> Children: [SYS-001 Construction](../systems/SYS-001-construction.md) · [SYS-002 Night Cycle](../systems/SYS-002-night-cycle.md) · [SYS-003 Guest Simulation](../systems/SYS-003-guest-simulation.md) · [SYS-004 Economy & Transactions](../systems/SYS-004-economy.md) · [SYS-005 Staffing](../systems/SYS-005-staffing.md) · [SYS-006 Traits & Synergy](../systems/SYS-006-traits-synergy.md) · [SYS-007 Acclaim & Prestige](../systems/SYS-007-acclaim-prestige.md) · [SYS-008 Venues](../systems/SYS-008-venues.md)

## 1. Vision

`DECIDED (user)` — A tavern idle game where the player builds a tavern out of rooms in a flat 2D stacked layout (in the vein of SimTower), hires employees, unlocks perks through various currencies, and sells different food and drink. Combinations of room types, employees, and items attract and satisfy different guest types. Guests generate gold (the primary resource, used to improve the tavern, pay employees, and afford better food and drink) and Acclaim (a secondary resource used to prestige). Certain guest types synergize or conflict with each other and with tavern staff (in the vein of UFO 50's Party House), so the player plays off these synergies. Vibe: cozy + big numbers. It targets a unique spot between a settlement/business management game and an idler.

## 2. Goals & non-goals

| ID | Goal | Status |
|---|---|---|
| G-001 | Commercial release, shipped on Steam | DECIDED (user) |
| G-004 | Commercial targets: 90% Steam review score; 3,000 wishlists at launch | DECIDED (user) |
| G-002 | Occupy a unique hybrid niche between settlement/business management games and idlers | DECIDED (user) |
| G-003 | Deliver a "cozy + big numbers" vibe | DECIDED (user) |

Non-goals (explicitly out of scope) — equally important, equally user-confirmed.

| ID | Non-goal | Status |
|---|---|---|
| NG-001 | No multiplayer | DECIDED (user) |
| NG-002 | No mobile release for now — but decisions must not make a future mobile port totally out of the question | DECIDED (user) |
| NG-003 | No monetization other than buying the game upfront | DECIDED (user) |

## 3. What it does (problems & outcomes)

Requirement seeds from the vision (high zoom; each becomes REQ rows in section 6):

- **Tavern building:** player builds a tavern out of rooms in a flat 2D stacked layout (SimTower-style).
- **Employees:** player hires employees; employees are paid in gold.
- **Perks:** unlockable via various currencies.
- **Food & drink:** different food and drink for sale; better food/drink costs gold to afford.
- **Guests:** types of rooms + employees + items attract and satisfy different guest types.
- **Economy:** guests generate gold (primary resource) used to improve the tavern, pay employees, and buy better food+drink.
- **Prestige:** guests generate Acclaim (secondary resource) used to prestige.
- **Synergies:** certain guest types synergize or conflict with each other and with tavern staff; playing off these synergies is core strategy.
- **Idle model:** progression happens only while the game is running — no offline/closed-app progress. `DECIDED (user)`
- **Theatre seed (ideation-level, details deferred):** one Acclaim progression tree (and/or venue) enables building a theatre with a playwright employee who writes plays over several nights and then presents them — creating big showcase days that draw large crowds. Existence `DECIDED (user)`; all mechanics OPEN → Q-046. `DO NOT DETAIL until user revisits.`
- **Strategic core:** the "sauce" is breaking through to the next Acclaim level, not passively watching numbers rise. Deliberately making *no* modifications between nights to save up for big upgrades is an intended, viable strategy. Managerial pacing over pure idle. `DECIDED (user)`
- **Crowding as a dial:** popularity has costs — crowds can sour picky guests while pleasing crowd-lovers; the queue outside is both a pressure valve and a status symbol. `DECIDED (user)`
- **Setting:** classic medieval fantasy — dwarves, bards, nobles, monarchs, spas with masseuses. `DECIDED (user)`
- **Build sequencing:** the first development milestone is a single-venue prototype using only the starter venue (REQ-089); multi-venue content (Q-047) is designed after the prototype validates the core loop. `DECIDED (user)`
- **Prestige (gnorp-apologue-style):** no fail state, infinite nights with any tavern. Acclaim comes from one-time milestones that often demand specific builds or venues. Prestiging resets the tavern and refunds all Acclaim spending, letting the player redistribute lifetime Acclaim into a new build (perks, special rooms/employees) aimed at the next milestone. The game is a series of deliberate build-crafting runs, not one endless escalator. `DECIDED (user)`

## 4. Where it lives

| Aspect | Decision | Status |
|---|---|---|
| Platform | Desktop PC (mobile port kept viable per NG-002/C-001..003) | DECIDED (user) |
| Distribution | Steam | DECIDED (user) |
| Hosting (cloud/local/hybrid) | Fully local game; saves on disk + Steam Cloud sync; no custom backend | DECIDED (user) |
| Target hardware | Floor: low-end laptops (integrated GPU, ~8GB RAM) and Steam Deck at stable framerate | DECIDED (user) |
| OS support | Windows, macOS, Linux + Steam Deck | DECIDED (user) |

## 5. What it uses

| Aspect | Decision | Status |
|---|---|---|
| Language(s) | C# (domain logic engine-free; unit-testable outside Godot) | DECIDED (user) |
| Engine/framework (+ version) | Godot 4.7-stable (.NET edition) | DECIDED (user) |
| Key libraries | xUnit (domain unit tests); Steamworks integration library TBD at architecture stage (needed for Steam Cloud) | DECIDED (user) / Steamworks lib OPEN → Q-010 |
| Tooling / CI | GitHub Actions: run domain tests on push; Godot export builds added later | DECIDED (user) |

## 6. Requirements

| ID | Requirement | Priority | Source goal | Status | System (stage 2) |
|---|---|---|---|---|---|
| REQ-001 | The tavern is a free 2D grid of cells; the player places rooms as rectangles occupying contiguous grid cells (floors are emergent, not a placement primitive) | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-002 | Guests are individually simulated agents that visibly enter the tavern, move between rooms, perform transactions, and leave | MUST | G-002, G-003 | DECIDED (user) | SYS-003 |
| REQ-003 | Play is structured in discrete day/night cycles; each cycle has a defined start, a cohort of arriving guests, and an end-of-cycle settlement | MUST | G-002 | DECIDED (user) | SYS-002 |
| REQ-004 | Gold is generated only by guest transactions; each transaction transfers a defined gold amount at the moment it occurs | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-005 | Each cycle runs three phases in order: prep (no guests; player builds/hires/stocks), service (guests arrive and transact), settlement (end-of-cycle report) | MUST | G-002 | DECIDED (user) | SYS-002 |
| REQ-006 | The service phase begins only when the player explicitly starts it; cycles never auto-chain without player input, except while run-to-next-night mode (REQ-007) is active — which the player explicitly enabled | MUST | G-002 | DECIDED (user) — amended 2026-07-12 for REQ-007 rework | SYS-002 |
| REQ-007 | During prep, the player can enable "run to next night" mode: cycles chain automatically at normal simulation speed — settlement completes, the prep phase is skipped (no build/hire/stock changes), and the next service phase starts immediately. There is no accelerated/fast-forward simulation. The mode persists until the player cancels it; on cancel, the current service phase completes normally and is followed by a normal prep phase | MUST | G-002 | DECIDED (user) — reworked 2026-07-12, supersedes "accelerated" wording | SYS-002 |
| REQ-008 | Maximum concurrent guests inside the tavern is determined by the number and capacity of rooms (structural soft cap) | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-009 | Each guest type has a crowding preference; a guest's satisfaction and payment are modified by the current crowd level according to that preference | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-010 | Guests beyond tavern capacity form a queue rendered as a line of guest sprites extending off screen; queued guests beyond the visible portion are represented by a "+N waiting" label, not individual agents | MUST | G-002, G-003 | DECIDED (user) | SYS-003 |
| REQ-011 | At game start, the available transaction types are food purchases and drink purchases | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-012 | Lodging rooms can be built; once built, guests can pay for overnight stays | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-013 | A room type may define a per-guest entry fee; guests pay it on entering such a room (example: spa) | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-014 | An employee type may offer a paid service that guests purchase (examples: masseuse, dancer) | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-015 | Entering the tavern main area is free by default; specific perks or employees, once obtained, charge guests an entrance fee | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-016 | The service phase has a fixed duration; when it expires, no new guests enter, remaining guests finish their current activity and leave, then settlement begins | MUST | G-002 | DECIDED (user) | SYS-002 |
| REQ-017 | The player can close the tavern early during the service phase: new entries stop immediately and the night ends once remaining guests leave | MUST | G-002 | DECIDED (user) | SYS-002 |
| REQ-018 | Queued guests enter the tavern as capacity frees during the service phase; each queued guest has a patience value and permanently leaves the queue for that night when it expires | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-019 | Employee wages are deducted from gold at settlement, once per cycle | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-020 | Food/drink supplies consumed during the night are tallied at settlement, including restock costs | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-021 | Acclaim earned during a cycle is computed and awarded at settlement, not during the service phase | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-022 | Settlement displays a night report containing: gold earned, guest breakdown by type, satisfaction summary, and notable events | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-023 | Guest satisfaction modifies the gold amount of that guest's transactions; satisfaction has no other systemic effect (does not affect attraction, stay duration, or Acclaim) | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-024 | Nightly guest arrivals are drawn from a weighted random pool; weights derive from tavern composition (rooms, employees, menu items) and lifetime Acclaim earned; the player has no direct guest-invitation control | MUST | G-002 | DECIDED (user) — "Acclaim level" reworded per REQ-077 | SYS-003 |
| REQ-025 | Food and drink stock is purchased during prep; each menu item has a finite stock count for the night | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-026 | When a menu item sells out mid-night, a guest who wanted it receives a satisfaction penalty | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-027 | Settlement tallies leftover stock per item | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-028 | If gold cannot cover wages at settlement, unpaid employees refuse to work subsequent nights until their back pay is paid in full | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-029 | All Acclaim is earned via one-time milestones; milestone conditions span guest-volume feats, notable-guest satisfaction, synergy feats, and build/venue feats (no repeatable Acclaim income) | MUST | G-002 | DECIDED (user) — supersedes earlier four-source wording, conflict resolved via Q-026 | SYS-007 |
| REQ-030 | Leftover stock carries over between nights | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-031 | There is no fail state; any tavern can continue for an unlimited number of nights | MUST | G-003 | DECIDED (user) | SYS-007 |
| REQ-032 | Acclaim is earned by hitting one-time milestones (examples: building a very tall tavern, 1,000 guests served in one night, satisfying a famous food critic, hosting the monarch and entourage); each milestone can be earned at most once per save, and earned Acclaim accumulates into a lifetime total | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-033 | Prestiging resets the tavern; the player's full Acclaim pool (lifetime total including refunded prior spending plus this run's earnings) becomes available to redistribute | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-034 | Acclaim is spent on perks and special rooms/employees that enable specific builds | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-035 | Content once unlocked (guest types, room types, recipes) remains available across prestiges | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-036 | Some milestones require specific builds or a tavern at a specific venue/location | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-037 | On prestige, the entire tavern resets: all rooms and staff are removed and gold returns to the starting amount | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-038 | At prestige, the player chooses a venue for the next run from those unlocked by lifetime milestones achieved | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-039 | Unspent Acclaim may be spent during any prep phase (not mid-service); refund and full redistribution of spent Acclaim happens only at prestige | MUST | G-002 | DECIDED (user) — amended 2026-07-13, spend window = prep | SYS-007 |
| REQ-040 | Every synergy/conflict rule is defined between exactly two traits (example: Outlaw × Lawful → fight behavior event); a rule activates only for participant pairs in which at least one participant is a guest | MUST | G-002 | DECIDED (user) — reworked 2026-07-12 to trait-based model | SYS-006 |
| REQ-041 | Synergy/conflict effects apply between entities in the same room by default; specific designated entities have tavern-wide effects | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-042 | An active synergy/conflict produces one or more of exactly three effect classes: (a) satisfaction modifier on involved guests, (b) a behavior event with mechanical consequences acted out visibly by agents (conflict example: brawl causing guests to leave; synergy example: sing-along causing a spending burst), (c) a spending multiplier on involved guests' transactions | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-043 | Traits are always visible when selecting/hovering their carrier; the rules between traits are hidden until first observed in play. A discovered rule is permanently recorded and revealed at trait level — it applies visibly to every carrier combination sharing those traits, not per type-pair | MUST | G-002 | DECIDED (user) — reworked 2026-07-12 to trait-based model | SYS-006 |
| REQ-044 | Discovered synergy/conflict knowledge persists across prestiges | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-045 | Each synergy/conflict rule individually defines whether its effect is binary (active/inactive) or scales with participant count | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-046 | Each synergy/conflict rule individually defines its reach: same-room or tavern-wide | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-047 | Certain room types are broadcasters: while an entity is inside one, its same-room synergy/conflict effects apply tavern-wide (candidate examples: stage, balcony) | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-048 | Guests are agenda-driven: each guest type defines an ordered wants-list; a guest attempts to fulfill its agenda in order and leaves when it is complete or blocked; satisfaction reflects fulfilled vs. unfulfilled wants | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-049 | Ordinary guests are anonymous instances of their type; notable/VIP guests are unique named characters with bespoke rules | MUST | G-002, G-003 | DECIDED (user) | SYS-003 |
| REQ-050 | Each named VIP defines visit conditions (drawn from: build, menu, venue, lifetime Acclaim earned); while conditions are met the VIP has a per-night random chance to visit; while unmet the VIP never visits | MUST | G-002 | DECIDED (user) — "Acclaim level" reworded per REQ-077 | SYS-003 |
| REQ-051 | Each guest carries a finite, type-dependent gold wallet and cannot spend more than it holds | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-052 | Consolidated guest-type definition: a guest type consists of display identity/sprite, attraction weights, crowding preference, queue patience, agenda, wallet range, trait list, and (VIPs only) visit conditions | MUST | G-002 | DECIDED (user) — "participating rules" → "trait list" per REQ-095, 2026-07-12 | SYS-003 |
| REQ-053 | When an agenda item is blocked, the guest waits (bounded by patience); if still blocked when patience expires, the guest takes a satisfaction penalty and proceeds to the next agenda item | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-054 | A guest whose wallet reaches zero leaves the tavern immediately | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-055 | A VIP leaving unsatisfied does not consume or lock the milestone: while visit conditions remain met, the VIP may visit again on later nights | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-056 | Every hired employee is assigned to at most one room; an unassigned employee performs no work but still draws wages | MUST | G-002 | DECIDED (user) — amended 2026-07-13 per REQ-109 | SYS-005 |
| REQ-057 | A room type may define staffing requirements as roles with min–max counts (example: tap room requires exactly 1 bartender and 1–3 barmaids) | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-058 | If any required role in a room has zero assigned employees, the room is closed for the night: guests cannot enter, and agenda items targeting it are blocked per REQ-053 (example: no bartender → tap room closed). If every required role has ≥1 assignee but counts are below minimums, the room operates with degraded service: longer waits and a satisfaction penalty | MUST | G-002 | DECIDED (user) — amended 2026-07-11 | SYS-005 |
| REQ-059 | Employee room assignments may be changed only during prep; they are locked for the duration of the service phase | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-060 | Ordinary employees are interchangeable within their role (defined by role + wage); in addition to Acclaim-bought special employees (REQ-034), rare unique named hires exist | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-061 | Staffing a room above its role minimum improves service speed and adds synergy participants; it does not increase room capacity and grants no direct satisfaction bonus | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-062 | Any ordinary employee role can be hired during any prep phase at its wage; there is no candidate scarcity or rotating pool | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-063 | Rare named hires are unlocked through perks; once the unlocking perk is owned, the named hire can be hired during prep | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-064 | Wages are flat per role (every hire of a role costs the same per cycle); each named/special hire has its own fixed wage | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-065 | There is no separate employee cap; total staff is implicitly capped by the staffing maxima of built rooms | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-066 | A room type is defined by: footprint size range, guest capacity, build cost, per-night upkeep cost, services offered (agenda wants it fulfills), staffing requirements, trait list, and broadcaster flag | MUST | G-002 | DECIDED (user) — "synergy participation" → "trait list" per REQ-095, 2026-07-12 | SYS-001 |
| REQ-067 | Structural support: every room cell must be supported by ground or by room cells directly beneath it (no floating rooms) | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-068 | Connectivity: every room must be reachable from the tavern entrance via a path of traversable cells; rooms and circulation are both traversable (guests may pass through rooms); guests physically walk to rooms | MUST | G-002 | DECIDED (user) — amended 2026-07-12 (rooms traversable; ground-gap rule in REQ-097) | SYS-001 |
| REQ-069 | Rooms are built at variable sizes within their type's range; per-capacity service efficiency decreases beyond a type-defined optimum size (a 20-table taproom is less efficient than two 10-table taprooms), while each separate room instance incurs its own base build cost | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-070 | Rooms can be demolished (refund policy: Q-041) | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-071 | Rooms have upgrade tiers (example: tap room → grand taproom) improving stats and staffing maxima | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-072 | Rooms can be moved/swapped after building, but only onto cells that are already part of the built structure | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-073 | Demolishing a room refunds its full build cost | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-074 | Corridors (horizontal) and stairs (vertical) are player-built circulation elements, traversable by guests like rooms; circulation is not the sole connectivity carrier (REQ-068 paths may pass through rooms) | MUST | G-002 | DECIDED (user) — amended 2026-07-12, supersedes circulation-only connectivity | SYS-001 |
| REQ-075 | Each venue has exactly one fixed ground-level entrance; the guest queue (REQ-010) forms outside it | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-076 | Perks are nodes in a prerequisite tree; a perk cannot be purchased until all its prerequisite perks are owned | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-077 | Acclaim is a pure point currency; there are no separate "Acclaim levels" — progression gating comes from perk-tree prerequisites (and lifetime Acclaim totals where REQs reference them) | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-078 | Perk effects are unrestricted in kind — passive modifiers, content unlocks, rule changers, active abilities, and entire sub-systems; variety is a design goal (flagship example: a hedge-wizard perk unlocking a unique character with his own perk sub-tree of game-changing spells, a tower built with gold during a run, and a personal mana resource) | MUST | G-002, G-003 | DECIDED (user) | SYS-007 |
| REQ-079 | Perks, special rooms, and named employees are all purchased from a single Acclaim shop drawing on one Acclaim pool; prestige redistribution (REQ-033/039) covers all of it | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-080 | Every active ability defines: a configurable cooldown, a uses-per-night limit (one or more), and optionally a cost in one or more defined game resources (examples: gold, mana) | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-081 | A venue is defined by: buildable grid dimensions, entrance position, terrain features, guest-pool modifiers, venue-exclusive content, economy multipliers, and venue-specific milestones (per REQ-082..088) | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-082 | Each venue defines its buildable lot as a full rectangle of venue-specific width and maximum height; all REQ-001 placement occurs within it (no blocked/unbuildable cells inside the rectangle) | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-083 | A venue may define terrain feature cells at fixed positions; each feature defines its effect as one of: (a) enabling construction of a room type buildable only on that feature, or (b) modifying the stats of a room whose footprint covers it (examples: hot spring → spa, cellar → brewery) | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-084 | The single fixed ground-level entrance (REQ-075) is located at a venue-defined ground cell | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-085 | Each venue defines guest-pool modifiers applied to the REQ-024 attraction weights: per-guest-type weight multipliers, and optionally full exclusions — an excluded guest type never appears at that venue | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-086 | A venue may define venue-exclusive content: guest types, room types, menu items, and VIPs available only while playing that venue | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-087 | A venue may define economy multipliers on exactly two costs: room build/upgrade costs and stock restock costs; wages and starting gold never vary by venue | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-088 | Every venue has at least one Acclaim milestone achievable only at that venue (specializes REQ-036) | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-089 | A fresh save begins at a fixed starter venue, identical for every new save; all other venues unlock via lifetime milestones (REQ-038) | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-090 | The venue is fixed for the entire run; the only venue switch point is the prestige venue choice (REQ-038) — no mid-run relocation | MUST | G-002 | DECIDED (user) | SYS-008 |
| REQ-091 | The service phase's fixed duration (REQ-016) is a single global tuning constant targeted at 2–3 minutes of real time at normal speed; the exact value is a config parameter settled in playtesting | MUST | G-003 | DECIDED (user) | SYS-002 |
| REQ-092 | Per-type patience values (queue patience per REQ-018/052 and blocked-agenda waits per REQ-053) fall within 10–30% of the service-phase duration | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-093 | Launch content scope: approximately 10 ordinary guest types and 5 named VIPs (exact roster defined at content design) | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-094 | Launch target: ≈20–30 distinct trait-based rules; each ordinary guest type participates in 2–3 rules via its traits (rules are shared across types through common traits, not authored per type); VIP bespoke rules (REQ-049) are additional | MUST | G-002 | DECIDED (user) — reworked 2026-07-12 to trait-based model | SYS-006 |
| REQ-095 | Traits are named attributes carried by guest types (including VIPs), employee types, room types, and menu items; a carrier may hold any number of traits, and multiple carriers may share a trait (example: Soldiers and Bandits are both Rowdy; only Bandits are Outlaw). Type-specific rules are expressed via single-member traits | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-096 | Rule endpoints reference traits only, never carrier types directly | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-097 | At ground level, empty cells between built structures are traversable exterior ground; ground-level rooms separated by empty ground cells count as connected for REQ-068 | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-098 | Demolishing or moving a room that breaks support or connectivity for other rooms is permitted; affected rooms become inactive (closed to guests; agenda items targeting them are blocked per REQ-053) until support/connectivity is restored | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-099 | Corridor and stair cells have a per-cell build cost, refund fully on demolish (per REQ-073), and provide structural support (per REQ-067) | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-100 | Room upgrades apply in place with unchanged footprint; demolishing an upgraded room refunds the base build cost plus all upgrade costs paid (extends REQ-073) | MUST | G-002 | DECIDED (user) | SYS-001 |
| REQ-101 | Settlement presents the night report (REQ-022) as a screen the player dismisses; the next prep phase begins only after dismissal | MUST | G-002 | DECIDED (user) | SYS-002 |
| REQ-102 | Guests arrive as a trickle across the service phase; the arrival rate derives from the same composition + lifetime-Acclaim attraction model as REQ-024 (no fixed nightly cohort size) | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-103 | Crowd level for REQ-009 is per-room: a guest's crowding preference reacts to the occupancy-vs-capacity of the room it is currently in | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-104 | Each service defines a base fulfillment duration (field on the REQ-066 room sheet); effective duration is subject to modifiers, including staffing speed (REQ-061), traits (example: a Lingerer guest stays longer), and perks | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-105 | A menu item is defined by: fixed sale price, restock cost, per-night stock count (REQ-025), and trait list (REQ-095); the player chooses what to stock, never sets prices | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-106 | At settlement, room upkeep is deducted before wages; insolvency (REQ-028) applies to the wage shortfall only | MUST | G-002 | DECIDED (user) | SYS-004 |
| REQ-107 | A guest who buys lodging persists through settlement and the next prep phase, occupying its lodging room, and leaves when the next service phase starts (explicit exception to the REQ-016 guest drain) | MUST | G-002 | DECIDED (user) | SYS-003 |
| REQ-108 | The player may dismiss any employee during prep; dismissal is immediate, carries no severance, and the employee incurs no further wages | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-109 | When a room with assigned employees is demolished or moved such that assignments break, its employees become unassigned (REQ-056): still employed and paid, idle until reassigned in a prep phase or dismissed | MUST | G-002 | DECIDED (user) | SYS-005 |
| REQ-110 | Rule timing by effect class (REQ-042): satisfaction modifiers and spending multipliers apply continuously while participants are co-present (per the rule's reach); behavior events roll their chance once per co-presence episode | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-111 | A rule is discovered (REQ-043) at its first activation, regardless of which carriers triggered it; discovery is immediate and permanent | MUST | G-002 | DECIDED (user) | SYS-006 |
| REQ-112 | The milestone list, including conditions and Acclaim values, is visible to the player from the start; individual milestones may be flagged secret and stay hidden until earned | MUST | G-002 | DECIDED (user) | SYS-007 |
| REQ-113 | Prestige can be triggered at any time, including mid-service; a mid-service prestige abandons the current night — no settlement occurs, so no Acclaim is awarded for it (per REQ-021) | MUST | G-002 | DECIDED (user) | SYS-007 |

Requirement quality bar: testable, unambiguous, no "etc.", no implied behavior.

## 7. Constraints & risks

Mobile-port viability constraints (from NG-002) — `DECIDED (user)`:

- **C-001 Touch-friendly input:** no hover-dependent or precision-mouse-only interactions; UI targets sized for fingers.
- **C-002 Engine/tech portability:** stack and libraries must be able to export to iOS/Android.
- **C-003 Performance budget:** simulation must stay cheap enough for mid-range phones.

Risks:

- **R-001:** Godot C# export to Android/iOS is still marked experimental (as of Godot 4.2+, per godotengine.org). Affects NG-002 mobile-port viability, not the Steam launch. Monitor per Godot release.
- **R-002:** Godot 4.7-stable is <2 weeks old at pin time; week-one regressions possible. Mitigation: adopt 4.7.x patch releases promptly.

## 8. Open questions

| ID | Question | Raised | Status |
|---|---|---|---|
| Q-001 | What is the project's name/working title? | 2026-07-11 | ANSWERED → "Tavern Idler" (working title) |
| Q-002 | What is the game/product, in one paragraph (vision)? | 2026-07-11 | ANSWERED → §1 |
| Q-003 | Who is it for, and why should it exist? | 2026-07-11 | ANSWERED → G-001, G-002 |
| Q-004 | What does success look like? | 2026-07-11 | ANSWERED → G-001 (shipped on Steam) |
| Q-005 | What is explicitly out of scope? | 2026-07-11 | ANSWERED → NG-001..003 |
| Q-006 | Does "idle" include offline/closed-app progression, or only progression while the game runs? | 2026-07-11 | ANSWERED → running only (see §3, decision log) |
| Q-007 | What concretely must be preserved to keep a mobile port viable (input model, UI scale, performance, save portability)? | 2026-07-11 | ANSWERED → touch-friendly input, engine/tech portability, performance budget (see §7) |
| Q-008 | Beyond "shipped on Steam", are there commercial targets (sales, reviews, wishlists)? | 2026-07-11 | ANSWERED → G-004 (90% review score, 3k wishlists at launch) |
| Q-009 | Does NG-003 (upfront purchase only) also exclude paid DLC/expansions? | 2026-07-11 | ANSWERED → paid DLC/expansions remain possible |
| Q-010 | Which Steamworks integration library (e.g., GodotSteam vs Steamworks.NET)? | 2026-07-11 | DEFERRED (user) 2026-07-13 → post-prototype; Steam Auto-Cloud covers save sync with no library (decision recorded in DOM-002) |
| Q-011 | Is there a cap on simultaneous visible guests? | 2026-07-11 | ANSWERED → REQ-008..010 (room-based soft cap + off-screen queue) |
| Q-012 | Anatomy of a cycle? | 2026-07-11 | ANSWERED → REQ-005 (prep/service/settle); duration & settlement contents still open → Q-016, Q-017 |
| Q-013 | What exactly can guests transact on? | 2026-07-11 | ANSWERED → REQ-011..015 |
| Q-014 | Where does "idle" live — do cycles auto-run? | 2026-07-11 | ANSWERED → REQ-006, REQ-007 (manual start + fast-forward) |
| Q-015 | Queue behavior | 2026-07-11 | ANSWERED → REQ-018 |
| Q-016 | How does a night end? | 2026-07-11 | ANSWERED → REQ-016, REQ-017 |
| Q-017 | What does settlement include? | 2026-07-11 | ANSWERED → REQ-019..022 |
| Q-018 | What does satisfaction affect? | 2026-07-11 | ANSWERED → REQ-023 (payment only) |
| Q-019 | Supply/stock model | 2026-07-11 | ANSWERED → REQ-025..027 |
| Q-020 | Attraction model | 2026-07-11 | ANSWERED → REQ-024 |
| Q-021 | Insolvency at settlement | 2026-07-11 | ANSWERED → REQ-028 |
| Q-022 | Numeric tuning (night duration, patience values) — zoom 3 material | 2026-07-11 | ANSWERED → REQ-091..092 (bands set; exact values are config tuning) |
| Q-023 | What earns Acclaim? | 2026-07-11 | ANSWERED → REQ-029 |
| Q-024 | Does leftover stock carry over between nights, or perish? | 2026-07-11 | ANSWERED → REQ-030 |
| Q-025 | Prestige structure? | 2026-07-11 | ANSWERED → REQ-031..036 |
| Q-026 | REQ-029 vs REQ-032 conflict | 2026-07-11 | ANSWERED → all Acclaim via one-time milestones (REQ-029 rewritten) |
| Q-027 | What resets at prestige? | 2026-07-11 | ANSWERED → REQ-037 (everything; keep Acclaim + unlocks) |
| Q-028 | Venue mechanics: what differs between venues (guest pools, size, milestones), how many at launch? | 2026-07-11 | ANSWERED → REQ-081..090; launch count deferred → Q-047 |
| Q-029 | Acclaim spend timing | 2026-07-11 | ANSWERED → REQ-039 (spend anytime, refund at prestige) |
| Q-030 | Does discovered synergy knowledge persist across prestiges? | 2026-07-11 | ANSWERED → REQ-044 |
| Q-031 | Synergy content density (how many rules, per-type counts) — zoom 3 / content design | 2026-07-11 | ANSWERED → REQ-094 |
| Q-034 | Blocked agenda item behavior | 2026-07-11 | ANSWERED → REQ-053 |
| Q-035 | Guest-type count at launch — content scope, zoom 3 | 2026-07-11 | ANSWERED → REQ-093 (~10 ordinary + ~5 VIPs) |
| Q-036 | Empty wallet behavior | 2026-07-11 | ANSWERED → REQ-054 |
| Q-037 | VIP milestone retry | 2026-07-11 | ANSWERED → REQ-055 |
| Q-038 | Hiring pool | 2026-07-11 | ANSWERED → REQ-062 (always available), REQ-063 (named via perks) |
| Q-039 | Wage structure | 2026-07-11 | ANSWERED → REQ-064 (flat per role) |
| Q-040 | Staff cap | 2026-07-11 | ANSWERED → REQ-065 (room slots implicit) |
| Q-041 | Demolish refund | 2026-07-11 | ANSWERED → REQ-073 (full) |
| Q-042 | Circulation | 2026-07-11 | ANSWERED → REQ-074 (buildable corridors/stairs) |
| Q-043 | Entrance | 2026-07-11 | ANSWERED → REQ-075 (fixed, ground level) |
| Q-044 | Sub-system perk details (hedge wizard: spell sub-tree currency, mana mechanics, tower) — zoom 3 / content design | 2026-07-11 | DEFERRED (user) → content design |
| Q-045 | Active ability limits | 2026-07-11 | ANSWERED → REQ-080 |
| Q-046 | Theatre/playwright system (multi-night play writing, showcase days): full mechanics | 2026-07-11 | OPEN — user will revisit; seed recorded in §3 |
| Q-047 | Venue roster: launch count, per-venue content (terrain features, exclusives, milestone lists) — zoom 3 / content design | 2026-07-11 | DEFERRED (user) → content design; first build milestone is a single-venue prototype (starter venue only) |
| Q-048 | Run-to-next-night edge semantics: report handling while chaining, early-close interaction, event/insolvency interrupts | 2026-07-13 | DEFERRED (user) → not critical to the single-venue prototype |
| Q-032 | Stacking? | 2026-07-11 | ANSWERED → REQ-045 (per-rule) |
| Q-033 | For rules that reach beyond one room: is reach a property of the character type, of the individual rule, or created by rooms? | 2026-07-11 | ANSWERED → REQ-046, REQ-047 (per-rule reach + broadcaster rooms) |

## 9. Decision log

| Date | Decision | Options considered | Chosen by |
|---|---|---|---|
| 2026-07-11 | Vision (§1): tavern idle game, 2D stacked rooms, employees, perks, food+drink, guest types, gold + Acclaim/prestige, guest synergies, cozy + big numbers | — | user |
| 2026-07-11 | Commercial premium release on Steam; success = shipped on Steam | — | user |
| 2026-07-11 | Out of scope: multiplayer; mobile (for now, keep port viable); any monetization beyond upfront purchase | — | user |
| 2026-07-11 | Idle model: progression only while the game runs; no offline progress | offline progress / running only | user |
| 2026-07-11 | Mobile-port viability: preserve touch-friendly input, engine portability, phone-class performance budget (C-001..003) | specific constraints / "just don't block it" | user |
| 2026-07-11 | Paid DLC/expansions remain possible; base game is one-time purchase (clarifies NG-003) | DLC allowed / no paid DLC | user |
| 2026-07-11 | Working title: "Tavern Idler" | — | user |
| 2026-07-11 | Commercial targets: 90% review score, 3k wishlists at launch (G-004) | — | user |
| 2026-07-11 | OS support: Windows, macOS, Linux + Steam Deck | Win only / +Deck / +macOS | user |
| 2026-07-11 | Engine: Godot 4.x (exact version pin still OPEN) | Godot / Unity / other | user |
| 2026-07-11 | Language: C# for game code; domain logic engine-free | C# / GDScript / mixed | user |
| 2026-07-11 | Engine version pin: Godot 4.7-stable (.NET) | 4.6.x / 4.7-stable | user |
| 2026-07-11 | Saves: local disk + Steam Cloud sync; no custom backend | local only / +Steam Cloud / own backend | user |
| 2026-07-11 | Hardware floor: low-end laptop (iGPU, ~8GB RAM) + Steam Deck | laptop+Deck / Deck-only floor / defer | user |
| 2026-07-11 | Test framework: xUnit | xUnit / NUnit / defer | user |
| 2026-07-11 | CI: GitHub Actions from day one (domain tests on push) | GH Actions / none / other | user |
| 2026-07-11 | Steamworks library choice deferred to architecture stage (Q-010) | pick now / later | user |
| 2026-07-11 | Zoom 0 and zoom 1 confirmed settled; descending to zoom 2 (requirements) | — | user |
| 2026-07-11 | Building model: free 2D grid, rooms as rectangles (REQ-001) | floors+widths / fixed slots / free grid | user |
| 2026-07-11 | Guests are visible simulated agents (REQ-002) | visible agents / abstract+dressing / fully abstract | user |
| 2026-07-11 | Time: discrete day/night cycles with settlement (REQ-003) | continuous / cycles / hybrid | user |
| 2026-07-11 | Gold: per-transaction (REQ-004) | per transaction / rate per guest / settle at exit | user |
| 2026-07-11 | Cycle anatomy: prep → service → settle (REQ-005); manual night start, managerial pacing (REQ-006); fast-forward to next night (REQ-007) | prep/service/settle vs always-open; auto-chain vs manual | user |
| 2026-07-11 | Guest cap structural via rooms; crowding preferences per guest type; off-screen queue with "+N waiting" (REQ-008..010) | unbounded / capped | user |
| 2026-07-11 | Transactions: food+drink at start; lodging via rooms; entry-fee rooms; paid employee services; free main-area entry until perks/employees charge (REQ-011..015) | — | user |
| 2026-07-11 | Night: fixed duration + early close (REQ-016..017); queue: patience + refill (REQ-018) | fixed / cohort / early close; patience options | user |
| 2026-07-11 | Settlement includes wages, supply costs, Acclaim award, night report (REQ-019..022) | multiselect | user |
| 2026-07-11 | Satisfaction affects payment size only (REQ-023) | payment / Acclaim / attraction / stay duration | user |
| 2026-07-11 | Attraction: weighted random pool from tavern composition + Acclaim level; no player invitations (REQ-024) | composition / Acclaim / randomness / invitations | user |
| 2026-07-11 | Stock: finite, bought in prep, sell-outs dissatisfy (REQ-025..027) | finite / infinite per-sale cost / auto-restock | user |
| 2026-07-11 | Insolvency: unpaid staff refuse to work until back pay covered (REQ-028) | debt / walkout / can't happen | user |
| 2026-07-11 | Acclaim from synergies, volume, notable guests, milestones (REQ-029) | multiselect (all chosen) | user |
| 2026-07-11 | Leftover stock carries over (REQ-030) | carry / perish / per-item | user |
| 2026-07-11 | Prestige: gnorp-style — no fail state, one-time milestones, full-refund Acclaim redistribution, unlocked content persists, venue-specific milestones (REQ-031..036) | spend-to-level / classic reset / new venue | user |
| 2026-07-11 | All Acclaim via one-time milestones; REQ-029 rewritten (resolves Q-026) | all milestones / + repeatables | user |
| 2026-07-11 | Prestige resets rooms/staff/gold; venue picked from milestone-unlocked set (REQ-037..038) | multiselect | user |
| 2026-07-11 | Acclaim: spend anytime, refund only at prestige (REQ-039) | only at prestige / anytime+refund / fully liquid | user |
| 2026-07-11 | Synergies: all four pairing classes; same-room scope with tavern-wide radiators; effects = satisfaction/behavior events/spending multipliers; discover-by-play with hover codex (REQ-040..043) | see Q&A options | user |
| 2026-07-11 | Codex persists across prestige (REQ-044); stacking per-rule (REQ-045); reach per-rule + broadcaster rooms (REQ-046..047) | type-fixed / per-rule / room-created | user |
| 2026-07-11 | Guests: agenda-driven (REQ-048); anonymous types + named VIPs (REQ-049); VIPs condition-gated with random visit chance (REQ-050); finite wallets (REQ-051); classic fantasy setting | see Q&A options | user |
| 2026-07-11 | REQ-052 attribute sheet confirmed; blocked wants = wait-then-skip (REQ-053); empty wallet = leave (REQ-054); VIP milestones retryable (REQ-055) | see Q&A options | user |
| 2026-07-11 | Employees assigned to exactly one room; rooms define role-based staffing requirements with min–max counts (REQ-056..057) | — | user |
| 2026-07-11 | Understaffed = degraded service (REQ-058); assignment prep-only (REQ-059); roles + rare named hires (REQ-060); extra staff → speed + synergy presence (REQ-061) | see Q&A options | user |
| 2026-07-11 | Hiring always available per role; named hires via perks; flat wages; staff capped by room slots (REQ-062..065) | see Q&A options | user |
| 2026-07-11 | AMENDMENT to REQ-058: zero staff in a required role closes the room; degraded service only applies when all roles present but below min counts | closed / degraded | user |
| 2026-07-11 | REQ-058 edge confirmed (zero barmaids = closed); room sheet all-four (REQ-066); support + connectivity + soft size limits (REQ-067..069); demolish/upgrade/move-on-structure (REQ-070..072) | see Q&A options | user |
| 2026-07-11 | Full demolish refund (REQ-073); buildable corridors/stairs circulation (REQ-074); single fixed ground entrance (REQ-075) | see Q&A options | user |
| 2026-07-11 | Perks: prerequisite tree (REQ-076); Acclaim = points only, no levels (REQ-077, REQ-024/050 reworded); perk effects unrestricted incl. sub-systems, hedge wizard flagship (REQ-078); one shop one pool (REQ-079) | see Q&A options | user |
| 2026-07-11 | Active abilities: cooldown + uses-per-night + optional resource costs (REQ-080) | — | user |
| 2026-07-11 | Theatre/playwright seed added at ideation level; mechanics deferred (Q-046) | — | user |
| 2026-07-11 | Venues differ on all four axes: buildable lot, guest pool, exclusive content, economy modifiers (REQ-081) | any subset of four axes | user |
| 2026-07-11 | Lot variation = grid dimensions + terrain features + entrance position; blocked/irregular cells rejected (REQ-082..084) | + blocked cells | user |
| 2026-07-11 | Guest pool per venue: weight multipliers + full exclusions + venue-exclusive types (REQ-085..086) | reweight only / exclusions / exclusives | user |
| 2026-07-11 | Venue economy knobs limited to build costs and stock prices; wages and starting gold universal (REQ-087) | + wages / + starting gold | user |
| 2026-07-11 | Every venue has ≥1 venue-only milestone (REQ-088) | every venue / some venues | user |
| 2026-07-11 | Fixed starter venue for all new saves (REQ-089); venue locked per run, switch only at prestige (REQ-090) | starter set choice / mid-run moves | user |
| 2026-07-11 | Venue launch count deferred to zoom 3 (Q-047) | 3–4 / 5–6 / 7+ / defer | user |
| 2026-07-12 | Q-044 (hedge-wizard sub-system details) DEFERRED to content design | resolve now / defer | user |
| 2026-07-12 | Service phase ≈2–3 min real time, global tuning constant (REQ-091) | 2–3 / 5 / 8–10 min / scaling | user |
| 2026-07-12 | REQ-007 reworked: "run to next night" = no fast-forward; prep-skip chaining at normal speed until cancelled; REQ-006 amended to reference it | selectable speeds / fixed multiplier / instant resolve / prep-skip | user |
| 2026-07-12 | Patience band: 10–30% of night for queue + blocked-agenda waits (REQ-092) | 10–30% / 30–60% / full night / defer | user |
| 2026-07-12 | Launch roster: ~10 ordinary guest types, ~5 VIPs (REQ-093) | 10 / 15–20 / 25+; 5 / 10 / 15+ | user |
| 2026-07-12 | Synergy density: 2–3 rules per ordinary type, ≈20–30 total (REQ-094) | 2–3 per type / 4–6 per type / sparse+deep | user |
| 2026-07-12 | Synergy model reworked to traits: rules are trait×trait only (REQ-096); traits carried by guests, employees, rooms, menu items (REQ-095); REQ-040/043/052/066/094 amended | traits only / mixed trait-or-type / traits as authoring shorthand | user |
| 2026-07-12 | Traits always visible; rules hidden until discovered; discovery reveals at trait level (REQ-043 rework) | traits visible / both hidden / both visible; trait-level / per-pair reveal | user |
| 2026-07-12 | Rules require ≥1 guest participant — no staff×staff or room×item activation (REQ-040) | guest required / any carrier pair | user |
| 2026-07-12 | Q-047 (venue roster) DEFERRED; first milestone = single-venue prototype on the starter venue, before further requirements work | settle roster now / defer | user |
| 2026-07-12 | Breakdown: 8-system partition approved (SYS-001..008); every REQ assigned to exactly one system | 8 systems / split stock / split perks / merge cycle+economy | user |
| 2026-07-12 | Connectivity: rooms AND circulation both traversable — guests may pass through rooms (REQ-068/074 reworked, resolves their conflict) | circulation only / rooms+circulation / ground-adjacency hybrid | user |
| 2026-07-12 | Ground-level gap rule: empty ground cells traversable; gapped ground rooms count as connected (REQ-097) | — (user-added rider) | user |
| 2026-07-12 | Breaking demolish/move allowed; affected rooms inactive until restored (REQ-098) | block / allow+inactive / cascade demolish | user |
| 2026-07-12 | Circulation cells: per-cell cost, full refund, provide support (REQ-099) | like rooms / cost no support / free | user |
| 2026-07-12 | Upgrades in place, footprint unchanged; refund includes upgrade spend (REQ-100) | incl. tiers / base only / footprint growth | user |
| 2026-07-13 | Settlement is a player-dismissed report screen; next prep only after dismissal (REQ-101) | dismiss / auto-continue | user |
| 2026-07-13 | REQ-039 clarified: Acclaim spend window is prep phases only — "anytime" meant any prep, not mid-service | — | user |
| 2026-07-13 | Run-mode edge semantics DEFERRED (Q-048); mid-service player actions remain early close (REQ-017) + active abilities (REQ-080) | resolve now / defer | user |
| 2026-07-13 | Guest volume: attraction-driven trickle across the phase, no cohort size (REQ-102) | trickle / sized cohort / doors-open burst | user |
| 2026-07-13 | Crowding is per-room occupancy (REQ-103) | tavern-wide / per-room / split | user |
| 2026-07-13 | Service durations per-service on room sheet, open modifier system: staffing, traits, perks (REQ-104) | per-service / per-guest-type / global constant | user |
| 2026-07-13 | Menu prices fixed per item; no player pricing (REQ-105) | fixed / player-set / defer | user |
| 2026-07-13 | Upkeep is priority debt, deducted before wages; REQ-028 covers wage shortfall only (REQ-106) | wage-like debt / floor at 0 / priority | user |
| 2026-07-13 | Lodgers pay and persist to next service start (REQ-107) | persist / pay-and-leave flavor / defer | user |
| 2026-07-13 | Firing allowed freely in prep, no severance (REQ-108) | free / back-pay gated / no firing | user |
| 2026-07-13 | Demolish orphans staff into paid unassigned pool; REQ-056 relaxed to "at most one room" (REQ-109) | pool / auto-fire / block demolish | user |
| 2026-07-13 | Rule timing split by effect class: continuous modifiers, once-per-episode events (REQ-110) | by class / continuous aura / periodic ticks | user |
| 2026-07-13 | Discovery = first activation (REQ-111) | first activation / witnessed only | user |
| 2026-07-13 | Milestones visible with a hidden few (REQ-112) | all visible / hidden few / all hidden | user |
| 2026-07-13 | Prestige any time incl. mid-service; abandoned night unsettled (REQ-113) | prep only / prep+gated / any time | user |
| 2026-07-13 | `/requirement` — CON-011 → v1.1 (clarification-only, no new REQ): traits `EndNight` emits nothing; `Binary` continuous params named `factor`/`ratePerTick` (symmetric validation); episode churn keys on qualifying pair set (count *or* membership) per REQ-110. Serves REQ-110/045/042 more correctly; folded into TKT-005 | emit nothing / reorder+carry effects · churn on set / per-tick re-target | user |
