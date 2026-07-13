# DOM-005: Staffing

> Status: APPROVED (Gate 3 PASSED 2026-07-13)
> Parents: [PDD](../design/PDD.md), [SYS-005 Staffing](../systems/SYS-005-staffing.md)
> Contracts: [CON-009](../contracts/CON-009-staffing-api.md), [CON-010](../contracts/CON-010-staffing-driven-ports.md)
> Tickets: — (added in stage 5)

## Bounded context

Models the workforce: employees, roles, wages, room assignments, and the staffing evaluation that decides whether each room is open, degraded, or closed for the night. Authority on who is employed, where they're assigned, and what the wage bill is.

Ubiquitous language:

- **Employee** — a hired worker; ordinary (interchangeable within role) or **named hire** (unique, perk-unlocked) (REQ-060/063).
- **Role** — employee kind with a flat per-cycle wage (REQ-064).
- **Assignment** — employee ↔ at most one room; changes prep-only, locked during service (REQ-056/059).
- **Unassigned pool** — employed, idle, still paid (REQ-056/109); uncapped (REQ-065).
- **Room staff state** — Open | Degraded (all required roles present but below min) | Closed (a required role has zero) (REQ-058).
- **Refusal** — insolvency state from DOM-004: unpaid employees don't work until back pay clears (REQ-028).

Boundary: does not define which roles a room needs (DOM-001 room sheets), pay wages (DOM-004 deducts; this domain supplies the bill), price services (DOM-004), evaluate traits (DOM-006 reads employee traits/presence), or unlock named hires (DOM-007).

## Requirements served

| REQ ID | Via system | How this domain serves it |
|---|---|---|
| REQ-056 | SYS-005 | At-most-one-room assignment invariant; paid idle pool |
| REQ-057 | SYS-005 | Evaluates role min–max requirements from DOM-001 sheets |
| REQ-058 | SYS-005 | Room state evaluation at service start (closed/degraded/open) |
| REQ-059 | SYS-005 | Assignment commands prep-gated; locked in service |
| REQ-060 | SYS-005 | Ordinary-role instances vs unique named entities |
| REQ-061 | SYS-005 | Above-min staffing → service-speed modifier + synergy presence; no capacity/satisfaction bonus |
| REQ-062 | SYS-005 | Hire-any-role-any-prep; no scarcity model |
| REQ-063 | SYS-005 | Named hires hireable once their perk is owned (checked via driven port) |
| REQ-064 | SYS-005 | Flat wage per role; fixed wage per named hire; bill exposed to DOM-004 |
| REQ-065 | SYS-005 | No explicit cap; assignment maxima come from room sheets |
| REQ-108 | SYS-005 | Free prep dismissal; wages stop immediately |
| REQ-109 | SYS-005 | Reacts to `RoomDemolished`/`RoomMoved` by orphaning assignees into the pool |

## Domain model

Pure C# — no engine types.

- **Aggregate: `Roster`** — root: all employees, assignments, per-room staff-state cache, refusal flags.
- **Entities:** `Employee` (id, role or named-hire ref, wage, assignment, refusal flag).
- **Value objects:** `Role`, `Wage` (`Money`), `RoomStaffState`, `WageBill` (per-employee lines for settlement + back-pay tracking), `SpeedModifier`.
- **Domain events:** `EmployeeHired`, `EmployeeDismissed`, `EmployeeAssigned`, `EmployeeOrphaned`, `RoomStaffStateChanged`, `StaffReset` (prestige).

Room-state evaluation runs at service start (per SYS-005) and re-runs if a room deactivates mid-night (REQ-098 interaction); refusal (REQ-028) excludes an employee from counting toward requirements.

## Architecture decisions

Global decisions (orchestration, time, save, presentation, Steamworks, shared kernel) are recorded in [DOM-002](DOM-002-cycle.md) — user-chosen 2026-07-13. Consequences here: orphaning is event-driven (reacts to Structure events routed by the orchestrator); states are pushed once at service start, then read by Guests via bridge (pull).

| Decision | Options considered | Chosen | Rationale | Chosen by |
|---|---|---|---|---|
| — (no domain-local consequential decisions) | | | | |

## Ports (owned by this domain)

| Port | Direction | Purpose | Contract |
|---|---|---|---|
| `StaffingCommandsPort` | driving | `Hire`, `Dismiss`, `Assign`, `Unassign` (all prep-gated), `EvaluateRoomStates`, `OnRoomRemoved(roomId)`, `SetRefusals(employees)`, `ResetAll` (prestige), `Snapshot/Restore` | CON-009 |
| `StaffingQueriesPort` | driving | Roster, wage bill, room staff states + speed modifiers, employee traits + room presence (for Traits bridge), hireable catalog | CON-009 |
| `RoomRequirementsPort` | driven | Staffing requirements + maxima per room/tier (over DOM-001) | CON-010 |
| `HireUnlockPort` | driven | Which named hires are unlocked (over DOM-007) | CON-010 |

## Adapters required

| Adapter | Implements port | Binds to | Owned by ticket |
|---|---|---|---|
| Staffing UI adapter | calls `StaffingCommandsPort`, reads queries | Godot UI (hire screen, assignment drag/drop per C-001 touch targets) | TKT-### (stage 5) |
| Structure bridge | `RoomRequirementsPort` | in-process call into DOM-001 queries | TKT-### (stage 5) |
| Progression bridge | `HireUnlockPort` | in-process call into DOM-007 queries | TKT-### (stage 5) |
| Staff content adapter | role/named-hire catalog | data files | TKT-### (stage 5) |

## Source layout

```
src/domains/staffing/        pure domain code + ports
src/adapters/staffing/       adapter implementations
tests/domains/staffing/      unit tests
tests/contracts/staffing/    contract conformance tests
```

## Open questions

| ID | Question | Status |
|---|---|---|
