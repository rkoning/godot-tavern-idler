# CON-009: Staffing API v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + domain events
> Provider: DOM-005 Staffing
> Consumers: staffing UI adapter, guests bridge (CON-006 `IRoomServiceState`), traits presence bridge (CON-012), app orchestrator (wage bill, refusal routing, room-removal routing), persistence adapter
> Conformance tests: `tests/contracts/staffing/`

## Purpose

Hiring, dismissal, assignment, room staff-state evaluation, wage bill. Traces: REQ-056–065, REQ-108–109.

## Interface definition

```csharp
namespace TavernIdler.Domains.Staffing;
using TavernIdler.Kernel;

public enum RoomStaffState { Open, Degraded, Closed }   // REQ-058

public enum StaffingError
{
    WrongPhase,             // hire/dismiss/assign outside Prep (REQ-059/062/108)
    UnknownRole,
    UnknownNamedHire,
    NamedHireLocked,        // REQ-063: perk not owned
    NamedHireAlreadyEmployed,
    UnknownEmployee,
    UnknownRoom,
    RoomAtStaffingMax,      // REQ-065 via room maxima
    RoleNotAcceptedByRoom,  // employee's role not in the room's requirements
    NotAssigned             // Unassign on pool employee
}

public interface IStaffingCommands
{
    Outcome<StaffingError> Hire(RoleId role);                       // REQ-062
    Outcome<StaffingError> HireNamed(NamedHireId hire);             // REQ-063
    Outcome<StaffingError> Dismiss(EmployeeId employee);            // REQ-108
    Outcome<StaffingError> Assign(EmployeeId employee, RoomId room);// REQ-056/059
    Outcome<StaffingError> Unassign(EmployeeId employee);
    IReadOnlyList<IDomainEvent> EvaluateRoomStates();               // at ServiceBegan (REQ-058)
    IReadOnlyList<IDomainEvent> OnRoomRemoved(RoomId room);         // REQ-109 (demolish/move)
    IReadOnlyList<IDomainEvent> OnRoomDeactivated(RoomId room);     // REQ-098 interaction
    IReadOnlyList<IDomainEvent> SetRefusals(IReadOnlyList<EmployeeId> employees, bool refusing); // REQ-028
    IReadOnlyList<IDomainEvent> ResetAll();                         // prestige (REQ-037)
    StaffingSnapshot Capture();
    void Restore(StaffingSnapshot snapshot);
}

public interface IStaffingQueries
{
    IReadOnlyList<EmployeeInfo> Roster { get; }
    IReadOnlyList<WageLineView> WageBill();                         // REQ-019/064 input to CON-007
    RoomStaffState State(RoomId room);                              // rooms with requirements only
    double SpeedFactor(RoomId room);                                // REQ-061; see semantics
    IReadOnlyList<StaffPresenceEntry> CurrentPresence();            // for CON-012 bridge
    IReadOnlyList<HireableView> HireCatalog();                      // roles + unlocked named hires
    IReadOnlyDictionary<RoleId, int> StaffedRoleCounts();           // composition input (CON-006)
}

public sealed record EmployeeInfo(
    EmployeeId Id, RoleId Role, NamedHireId? NamedHire, string DisplayName,
    Money Wage, RoomId? AssignedRoom, bool Refusing,
    IReadOnlyList<TraitId> Traits);

public sealed record WageLineView(EmployeeId Employee, RoleId Role, Money Wage);
public sealed record StaffPresenceEntry(EmployeeId Id, RoomId Room, IReadOnlyList<TraitId> Traits); // assigned only
public sealed record HireableView(RoleId Role, NamedHireId? NamedHire, string DisplayName, Money Wage);

public sealed record StaffingSnapshot(int SchemaVersion /*1*/, string JsonPayload);   // schema in CON-017

// ── Events ──────────────────────────────────────────────────
public sealed record EmployeeHired(EmployeeId Id, RoleId Role, NamedHireId? NamedHire) : IDomainEvent;
public sealed record EmployeeDismissed(EmployeeId Id) : IDomainEvent;
public sealed record EmployeeAssigned(EmployeeId Id, RoomId Room) : IDomainEvent;
public sealed record EmployeeUnassigned(EmployeeId Id) : IDomainEvent;
public sealed record EmployeeOrphaned(EmployeeId Id, RoomId FormerRoom) : IDomainEvent;   // REQ-109
public sealed record RoomStaffStateChanged(RoomId Room, RoomStaffState State) : IDomainEvent;
public sealed record StaffReset() : IDomainEvent;
```

### Staff content JSON schema (content file `content/staff.json`)

```json
{ "roles": [
    { "id": "bartender", "displayName": "Bartender", "wage": 8,
      "traits": [ "sturdy" ], "paidService": null },
    { "id": "masseuse",  "displayName": "Masseuse",  "wage": 12,
      "traits": [ "soothing" ],
      "paidService": { "serviceId": "massage", "price": 15 } } ],
  "namedHires": [
    { "id": "old-tom", "displayName": "Old Tom", "role": "bartender", "wage": 20,
      "traits": [ "legendary", "sturdy" ], "unlockPerk": "perk-old-tom" } ] }
```

## Semantics

- **Phase gates:** `Hire*`/`Dismiss`/`Assign`/`Unassign` are Prep-only via injected `ICycleQueries` → `WrongPhase` (REQ-059/062/108). `OnRoomRemoved`/`OnRoomDeactivated`/`SetRefusals` are event reactions, legal any phase.
- **Assignment invariants (REQ-056/065):** at most one room per employee; `Assign` to a full room (per CON-010 maxima for the room's current tier) → `RoomAtStaffingMax`; role must appear in the room's requirements → `RoleNotAcceptedByRoom`. Re-`Assign` of an assigned employee implicitly unassigns first (one event pair).
- **State evaluation (REQ-058):** `Open` = every required role count ≥ min; `Degraded` = every required role ≥ 1 but some below min; `Closed` = some required role has 0 **counting only non-refusing employees** (REQ-028). Computed by `EvaluateRoomStates` at service start; re-computed for a single room by `OnRoomDeactivated`/`OnRoomRemoved`. `RoomStaffStateChanged` emitted only on change.
- **Speed (REQ-061):** per room, `SpeedFactor = clamp(staffedTotal / requiredMinTotal, 0.5, 1.5)` counting non-refusing assignees; `Degraded` rooms are additionally capped at 0.8. `Closed` rooms return 0.
- **REQ-109:** `OnRoomRemoved`/moved-with-broken-assignment → assignees emit `EmployeeOrphaned`, become unassigned, keep wages.
- **Wages:** `WageBill()` = every employed employee (assigned or not, refusing or not — refusal doesn't stop wage accrual; REQ-028's refusal is about work, arrears handling lives in CON-007).
- **Dismiss (REQ-108):** removes from roster immediately; any arrears for that employee remain payable via CON-007 `PayBackPay` (dismissal doesn't erase debt) — wage accrual stops.
- `Capture` Prep/Settlement only. Single-threaded per CON-016.

## Conformance tests

`tests/contracts/staffing/`:

- Error matrix: every `StaffingError` variant provoked; failures mutate nothing.
- State table (REQ-058): 1-bartender+0-barmaid → Closed; 1+1 (min 1–3… below a 2-min role) → Degraded; mins met → Open; refusing employee excluded → flips Closed.
- Speed: min-staffed = 1.0; over-staffed 1.5 cap; degraded cap 0.8; closed 0.
- REQ-109 flow: assign → OnRoomRemoved → `EmployeeOrphaned`, employee unassigned, still in `WageBill()`.
- Named hires: locked → `NamedHireLocked`; unlocked (stub CON-010) → hireable once; second `HireNamed` → `NamedHireAlreadyEmployed`.
- Wage bill contents: assigned + unassigned + refusing all present; dismissed absent.
- Assignment invariants: maxima enforcement, role acceptance, implicit reassign event pair.
- Snapshot round-trip: roster/assignments/refusals preserved; content golden-file load + validation rules.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
