# Contract Registry

Canonical index of every contract. Check here before defining a new contract — no duplicates, no overlaps.

| ID | Name | Version | Kind | Provider | Consumers | Status |
|---|---|---|---|---|---|---|
| CON-001 | Shared Kernel Types | 1.0 | shared type | shared kernel | DOM-001..007, app, all adapters | FROZEN |
| CON-002 | Cycle API | 1.0 | port interface + events | DOM-002 | DOM-001/003/004/005/007 (phase gates), app orchestrator, cycle UI, persistence | FROZEN |
| CON-003 | Structure API | 1.0 | port interface + events | DOM-001 | build UI, render adapter, DOM-003/005/006 bridges, DOM-007 feat router, app, persistence | FROZEN |
| CON-004 | Structure Driven Ports | 1.0 | port interface + data schema | DOM-001 (owner) | economy bridge, venue bridge, room content adapter | FROZEN |
| CON-005 | Guests API | 1.0 | port interface + events + data schema | DOM-003 | app, guest render adapter, DOM-006 presence bridge, DOM-007 feat router, DOM-004 (stats), guest content adapter, persistence | FROZEN |
| CON-006 | Guests Driven Ports | 1.0 | port interface | DOM-003 (owner) | structure/staffing/economy/attraction bridges | FROZEN |
| CON-007 | Economy API | 1.0 | port interface + events | DOM-004 | guests bridge, structure bridge, economy UI, app (settlement), DOM-005 (refusals), persistence | FROZEN |
| CON-008 | Economy Driven Ports | 1.0 | port interface + data schema | DOM-004 (owner) | menu content adapter, venue-modifier bridge | FROZEN |
| CON-009 | Staffing API | 1.0 | port interface + events | DOM-005 | staffing UI, guests bridge, traits presence bridge, app, persistence | FROZEN |
| CON-010 | Staffing Driven Ports | 1.0 | port interface | DOM-005 (owner) | structure bridge, progression bridge | FROZEN |
| CON-011 | Traits API | 1.1 | port interface + events + data schema | DOM-006 | app (effect routing), DOM-003 (effect payloads), codex UI, DOM-007 feat router, rule content adapter, persistence | FROZEN |
| CON-012 | Traits Driven Ports | 1.0 | port interface | DOM-006 (owner) | presence bridge (over DOM-003/005/001/004) | FROZEN |
| CON-013 | Progression API | 1.0 | port interface + events | DOM-007 | progression UI, app (feats/settlement/prestige), venue/attraction/cost/hire-unlock bridges, persistence | FROZEN |
| CON-014 | Progression Content Schema | 1.0 | port interface + data schema | DOM-007 (owner) | progression content adapter | FROZEN |
| CON-015 | Random Port | 1.0 | port interface | shared (kernel) | DOM-003, DOM-006; RNG adapter (implementer) | FROZEN |
| CON-016 | Orchestration & Adapter Binding Conventions | 1.0 | adapter binding | app layer | all domain tickets, all adapter tickets | FROZEN |
| CON-017 | Save File Schema | 1.0 | data schema | persistence adapter (envelope); domains own payloads | all snapshot ports, app composition root | FROZEN |
