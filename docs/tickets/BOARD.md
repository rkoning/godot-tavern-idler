# Ticket Board

| ID | Title | Type | Domain | Status | Blocked by | Owner session |
|---|---|---|---|---|---|---|
| TKT-001 | Repo scaffold + Kernel & Random contracts | contract-definition | shared kernel (cross-domain) | DONE | — | /implement TKT-001 (2026-07-13) |
| TKT-002 | Cycle contracts (CON-002) | contract-definition | DOM-002 | DONE | TKT-001 | /implement TKT-002 (2026-07-13) |
| TKT-003 | Structure contracts (CON-003, CON-004) | contract-definition | DOM-001 | DONE | TKT-001 | /implement TKT-003 (2026-07-13) |
| TKT-004 | Staffing contracts (CON-009, CON-010) | contract-definition | DOM-005 | DONE | TKT-003 | /implement TKT-004 (2026-07-13) |
| TKT-005 | Traits contracts (CON-011, CON-012) | contract-definition | DOM-006 | DONE | TKT-001 | /implement TKT-005 (2026-07-13) |
| TKT-006 | Guests contracts (CON-005, CON-006) | contract-definition | DOM-003 | TODO | TKT-003, TKT-004, TKT-005 | — |
| TKT-007 | Economy contracts (CON-007, CON-008) | contract-definition | DOM-004 | TODO | TKT-003, TKT-006 | — |
| TKT-008 | Progression contracts (CON-013, CON-014) | contract-definition | DOM-007 | TODO | TKT-003, TKT-006, TKT-007 | — |
| TKT-009 | App & Save contracts (CON-016, CON-017) | contract-definition | app layer (cross-domain) | TODO | TKT-002, TKT-003, TKT-004, TKT-005, TKT-006, TKT-007, TKT-008 | — |
| TKT-010 | Cycle domain implementation (NightCycle FSM) | implementation | DOM-002 | TODO | TKT-002 | — |
| TKT-011 | Structure domain implementation (Tavern aggregate) | implementation | DOM-001 | TODO | TKT-002, TKT-003 | — |
| TKT-012 | Staffing domain implementation (Roster) | implementation | DOM-005 | TODO | TKT-002, TKT-004 | — |
| TKT-013 | Traits domain implementation (rule engine) | implementation | DOM-006 | TODO | TKT-002, TKT-005 | — |
| TKT-014 | Guests domain implementation (agent simulation) | implementation | DOM-003 | TODO | TKT-002, TKT-006 | — |
| TKT-015 | Economy domain implementation (ledger + settlement) | implementation | DOM-004 | TODO | TKT-002, TKT-007 | — |
| TKT-016 | Progression domain implementation (milestones, shop, prestige, venues) | implementation | DOM-007 | TODO | TKT-002, TKT-008 | — |
| TKT-017 | RNG adapter (CON-015 implementation) | implementation | adapters (shared) | DONE | TKT-001 | /implement TKT-017 (2026-07-13) |
| TKT-018 | App orchestrator (IGameLoop, routing, sequences) | implementation | app layer (cross-domain) | TODO | TKT-009 | — |
| TKT-019 | In-process bridges (all driven-port implementations) | implementation | adapters (cross-domain) | TODO | TKT-010, TKT-011, TKT-012, TKT-013, TKT-014, TKT-015, TKT-016, TKT-027 | — |
| TKT-020 | Content adapters + starter-venue content | implementation | adapters (content) | TODO | TKT-003, TKT-004, TKT-005, TKT-006, TKT-007, TKT-008, TKT-027, TKT-028 | — |
| TKT-021 | Persistence adapter (CON-017 ISaveStore) | implementation | adapters (persistence) | TODO | TKT-009, TKT-027 | — |
| TKT-022 | Godot bootstrap (project, GameLoopNode, composition root) | integration | adapters (Godot) | TODO | TKT-017, TKT-018, TKT-019, TKT-020, TKT-021 | — |
| TKT-023 | Godot render adapters (structure + guests) | integration | adapters (Godot) | TODO | TKT-022 | — |
| TKT-024 | Godot prep/management UI (build, stock, hire/assign) | integration | adapters (Godot) | TODO | TKT-022 | — |
| TKT-025 | Godot HUD, night report, progression & codex UI | integration | adapters (Godot) | TODO | TKT-022 | — |
| TKT-026 | Headless end-to-end integration + save round-trip + architecture tests | integration | cross-domain | TODO | TKT-017, TKT-018, TKT-019, TKT-020, TKT-021 | — |
| TKT-027 | Adapter project auto-discovery (build config) | build-config | build / adapters | DONE | TKT-017 | /implement TKT-027 (2026-07-13) |
| TKT-028 | CON-009 v1.1 staff-content validation (contract-change) | contract-change | DOM-005 | TODO | TKT-004 | — |

## Parallelization waves

Derived from the dependency DAG + file-ownership audit (2026-07-13, /tickets). Every ticket in a wave has disjoint file ownership from every other ticket it could run alongside (all tickets in its own and other unfinished waves that it neither blocks nor is blocked by, transitively). Waves run in order; within a wave, tickets are parallel-safe.

| Wave | Tickets |
|---|---|
| 1 | TKT-001 |
| 2 | TKT-002  TKT-003  TKT-005  TKT-017 |
| 3 | TKT-004  TKT-010  TKT-011  TKT-013  TKT-027 |
| 4 | TKT-006  TKT-012  TKT-028 |
| 5 | TKT-007  TKT-014 |
| 6 | TKT-008  TKT-015 |
| 7 | TKT-009  TKT-016  TKT-020 |
| 8 | TKT-018  TKT-019  TKT-021 |
| 9 | TKT-022  TKT-026 |
| 10 | TKT-023  TKT-024  TKT-025 |

Rules reminder: one session = one ticket; update your row on start (Status, Owner session) and finish; contract-generated files (`src/domains/*/ports/`, `src/app/ports/`, `tests/contracts/`) are read-only for implementation tickets. Adapter tickets own their own `src/adapters/<x>/<x>.csproj`; `TavernIdler.sln` and `tests/TavernIdler.Tests.csproj` are owned solely by TKT-027 (glob auto-discovery) — no other ticket edits them.
