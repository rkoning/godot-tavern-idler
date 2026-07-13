# Pipeline State

> Sessions: read this file first. Update it when a stage's status changes. Only the user may set a gate to PASSED.

**Current stage:** 6 — Implementation

| # | Stage | Command | Gate status | Gate passed on |
|---|---|---|---|---|
| 1 | Ideation | `/ideate` | PASSED | 2026-07-12 |
| 2 | Breakdown | `/breakdown` | PASSED | 2026-07-13 |
| 3 | Architecture | `/architect` | PASSED | 2026-07-13 |
| 4 | Contracts | `/contracts` | PASSED | 2026-07-13 |
| 5 | Tickets | `/tickets` | PASSED | 2026-07-13 |
| 6 | Implementation | `/implement` | NOT STARTED | — |

Gate statuses: `NOT STARTED` → `IN PROGRESS` → `READY FOR REVIEW` → `PASSED` (user only) | `BLOCKED` (prior gate not passed)

## Gate criteria

- **Gate 1 (Ideation → Breakdown):** PDD exists; zero `OPEN` items in PDD scope/platform/stack sections; user states the PDD is complete enough to break down. Open questions may remain only if explicitly deferred by the user with a `DEFERRED` tag.
- **Gate 2 (Breakdown → Architecture):** every PDD requirement is assigned to exactly one system doc; user approves the system list.
- **Gate 3 (Architecture → Contracts):** every system maps to ≥1 domain; all flagged architecture decisions resolved by the user; user approves domain map.
- **Gate 4 (Contracts → Tickets):** every port and cross-domain interaction has a contract; all contracts user-approved and `FROZEN`; REGISTRY.md complete.
- **Gate 5 (Tickets → Implementation):** every requirement covered by ≥1 ticket; file-ownership audit shows no overlap between parallelizable tickets; dependency graph acyclic; user approves board.

## Log

<!-- Append one line per session: date, command run, what changed -->
- 2026-07-11 — `/ideate` — created docs/design/PDD.md from template; stage 1 set to IN PROGRESS; zoom 0 questions opened (Q-001–Q-005).
- 2026-07-11 — `/ideate` (cont.) — zoom 0 and zoom 1 settled by user: vision, goals G-001..004, non-goals NG-001..003, platform/stack tables fully decided (Godot 4.7-stable .NET, C#, xUnit, GH Actions, Steam Cloud, laptop+Deck floor); Q-010 user-DEFERRED; zoom 2 begun.
- 2026-07-11 — `/ideate` (cont.) — zoom 2: core loop + prestige specified, REQ-001..039 (grid building, agent guests, prep/service/settle cycle, queue, stock, wages, insolvency, gnorp-style milestone prestige, venue unlocks, Acclaim spend rules); one REQ conflict raised and resolved (Q-026). OPEN: Q-022 (tuning), Q-028 (venues).
- 2026-07-11 — `/ideate` (cont.) — zoom 2 continued: synergies (REQ-040..047), guest types (REQ-048..055), employees (REQ-056..065, incl. REQ-058 amendment: unstaffed role closes room), rooms (REQ-066..075), perks/Acclaim shop (REQ-076..080; REQ-024/050 reworded to lifetime Acclaim per REQ-077). Theatre/playwright seed added at ideation level (Q-046, deferred). OPEN: Q-022, Q-028, Q-031, Q-035, Q-044, Q-046.
- 2026-07-11 — `/ideate` (cont.) — venues (Q-028 → ANSWERED): REQ-081..090 — venue sheet, rectangular lots with per-venue dimensions, terrain features, entrance position, guest-pool weights/exclusions/exclusives, build+stock cost multipliers only, ≥1 venue-only milestone each, fixed starter venue, venue locked per run. Launch count deferred → Q-047. OPEN: Q-022, Q-031, Q-035, Q-044, Q-046, Q-047.
- 2026-07-12 — `/ideate` (cont.) — Q-044 user-DEFERRED. Q-022/031/035 → ANSWERED: night ≈2–3 min (REQ-091), patience 10–30% of night (REQ-092), ~10 guest types + ~5 VIPs (REQ-093), 2–3 synergy rules per type (REQ-094). REQ-007 reworked (run mode = prep-skip chaining at normal speed, no fast-forward); REQ-006 amended. OPEN: Q-047 (deferred: Q-010, Q-044, Q-046).
- 2026-07-12 — `/ideate` (cont.) — synergy model reworked to traits (user): rules are trait×trait only, ≥1 guest participant; traits on guests/employees/rooms/items, always visible; rules hidden, trait-level discovery. New REQ-095..096; REQ-040/043/052/066/094 amended. OPEN: Q-047 (deferred: Q-010, Q-044, Q-046).
- 2026-07-12 — `/ideate` — Q-047 user-DEFERRED; single-venue-prototype-first milestone recorded in PDD §3. Gate 1 check passed; **user confirmed Gate 1 PASSED**. PDD → READY FOR BREAKDOWN; stage 2 unblocked.
- 2026-07-12 — `/breakdown` — stage 2 IN PROGRESS. 8-system partition approved by user (Construction, Night Cycle, Guest Sim, Economy, Staffing, Traits & Synergy, Acclaim & Prestige, Venues); SYS-001..008 docs created (DRAFT); all 96 REQs assigned exactly once; PDD System column + Children header filled. Next: per-system mini-ideation (boundaries/interactions are PROPOSED drafts).
- 2026-07-13 — `/breakdown` (cont.) — mini-ideation completed for all 8 systems: REQ-097..113 added (traversal, inactive rooms, circulation costs, upgrades, settlement report, arrival trickle, per-room crowding, service durations, menu pricing, upkeep priority, lodging persistence, firing, orphaned staff, rule timing, discovery, milestone visibility, prestige timing); REQ-039/056/068/074 amended; Q-048 (run-mode edges) user-DEFERRED. Audit: 113 REQs each in exactly one system; all boundaries + interactions user-confirmed. **User confirmed Gate 2 PASSED.** SYS docs → APPROVED; stage 3 unblocked.
- 2026-07-13 — `/architect` — stage 3: 7-domain map approved by user (Venues merged into Progression); global decisions user-chosen (ticked orchestrator + domain events; fixed-timestep ticks; snapshots at phase boundaries; pull view-model presentation; Steamworks lib deferred → Q-010 updated; minimal shared kernel: IDs/Money/Tick). DOM-001..007 created (DRAFT, global decision table in DOM-002); SYS-001..008 Children links set. Gate 3 audit presented (113/113 REQs reachable; no engine types in ports; all decisions user-answered). New OPEN: DOM001-Q1, DOM003-Q1/Q2, DOM006-Q1, DOM007-Q1 (contracts-stage material). **User confirmed Gate 3 PASSED.** DOM docs → APPROVED; stage 4 unblocked.
- 2026-07-13 — `/contracts` — stage 4: 17-contract inventory approved by user; contract-wide decisions user-chosen (Result types + exceptions; commands return events; Money = long + round-half-away; JSON saves; JSON content; item traits participate while consumed → DOM006-Q1; typed milestone conditions + JSON params → DOM007-Q1; upkeep shortfall forgiven at 0 → SYS004-Q1). CON-001..017 drafted (DRAFT); REGISTRY populated; DOM port tables + Contracts headers linked; DOM001-Q1, DOM003-Q1/Q2 resolved via contract details. Audit clean (all ports covered, no TBDs, registry matches). **User confirmed Gate 4 PASSED.** CON-001..017 → FROZEN (registry updated); change protocol now in force; stage 5 unblocked.
- 2026-07-13 — `/implement TKT-003` — stage 6: began TKT-003 (Structure contracts CON-003, CON-004). Status → IN PROGRESS; BOARD row updated. Defining port/type/event/error definitions and abstract conformance suites; no domain behavior.
- 2026-07-13 — `/implement TKT-003` (done) — CON-003/CON-004 port surface + abstract conformance suites written; `TraversalGraph.Neighbors` TDD'd (9 tests). Golden `rooms.sample.json` + validation-rule suite included. Full test suite 46 passed / 0 failed / 0 skipped. contract-compliance: COMPLIANT. Status → DONE; BOARD updated. Unblocks TKT-004, TKT-006, TKT-007, TKT-008, TKT-009, TKT-011, TKT-020. **Clarification recorded (no contract edit):** DOM-001 gets the full static room-type catalog at construction so it can distinguish `RoomTypeLocked` from `UnknownRoomType` (user-decided) — see TKT-003 session log; TKT-011 must honor this.
- 2026-07-13 — `/tickets` — stage 5: 26-ticket slicing approved by user (9 contract-definition, 12 implementation, 5 integration; Guests kept unsplit so conformance-DONE rule holds). TKT-001..026 written; BOARD.md populated with 10 parallelization waves. Audits: dependency DAG acyclic ✓; concurrent-ticket file ownership disjoint ✓ (ports/ vs non-ports split; per-ticket Godot scene dirs); REQ coverage 113/113 ✓; every contract port implemented by exactly one ticket ✓. **User confirmed Gate 5 PASSED.** Stage 6 unblocked — implementation begins with `/implement TKT-001` (wave 1), then waves per BOARD.md.
