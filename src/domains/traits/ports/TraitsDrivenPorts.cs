namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

// ── CON-012: Traits Driven Ports v1.0 (FROZEN 2026-07-13) ───────────────────
// The presence snapshot the rule engine evaluates each tick. Resolves DOM006-Q1
// (user 2026-07-13): menu items participate while being consumed, in the consuming
// guest's room. The interface is owned by DOM-006; the implementer is the presence
// bridge (composing CON-005 IGuestPresence, CON-009 IStaffingQueries.CurrentPresence,
// CON-003 IStructureQueries, CON-008 menu traits) — see CON-012 / TKT-019.
// Traces: REQ-040–041, REQ-047, REQ-095.

public interface IPresenceSource
{
    /// Fresh snapshot for the current tick. Called exactly once per ITraitsCommands.Tick().
    PresenceSnapshot Current();
}

public sealed record PresenceSnapshot(IReadOnlyList<PresentCarrier> Carriers);

public sealed record PresentCarrier(
    CarrierRef Ref,
    RoomId? Room,                       // null = walking circulation/exterior (participates in NO same-room rules)
    bool IsGuest,                       // REQ-040 gate
    IReadOnlyList<TraitId> Traits,
    bool InBroadcaster);                // REQ-047: current room has Broadcaster flag

public abstract record CarrierRef
{
    public sealed record Guest(GuestId Id) : CarrierRef;
    public sealed record Employee(EmployeeId Id) : CarrierRef;
    public sealed record Room(RoomId Id) : CarrierRef;                              // the room itself, Room == Id
    public sealed record ConsumedItem(MenuItemId Item, GuestId ConsumedBy) : CarrierRef; // DOM006-Q1
    private CarrierRef() { }
}
