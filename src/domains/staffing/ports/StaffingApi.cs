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
