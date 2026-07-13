# CON-012: Traits Driven Ports v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface
> Provider: DOM-006 Traits (interface owner); implementer: presence bridge (composing CON-005 `IGuestPresence`, CON-009 `IStaffingQueries.CurrentPresence`, CON-003 `IStructureQueries`, CON-008 menu traits)
> Consumers: DOM-006 domain code (caller); presence bridge ticket (implementer)
> Conformance tests: `tests/contracts/traits/driven/`

## Purpose

The presence snapshot the rule engine evaluates each tick. Resolves DOM006-Q1 (user 2026-07-13): menu items participate **while being consumed**, in the consuming guest's room. Traces: REQ-040–041, REQ-047, REQ-095.

## Interface definition

```csharp
namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

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
```

## Semantics

- **Composition rules for the bridge:**
  - *Guests:* every CON-005 `GuestPresenceEntry` → `Guest` carrier (`IsGuest = true`).
  - *Employees:* every CON-009 `StaffPresenceEntry` (assigned employees only) → `Employee` carrier in their assigned room.
  - *Rooms:* every **active** room with a non-empty trait list → `Room` carrier located in itself.
  - *Consumed items:* every guest with `Consuming != null` contributes a `ConsumedItem` carrier in that guest's room with the item's traits (CON-008 sheet). Effect targeting still lands on guests only (CON-011) — for item×trait rules the qualifying guest participant is `ConsumedBy` or any other guest satisfying reach.
  - `InBroadcaster` = carrier's `Room` is non-null and that room's CON-003 `Broadcaster` flag is true **and the room is active**.
- **Traits:** carrier trait lists are the content-sheet lists verbatim (REQ-095); the bridge performs no filtering or deduplication beyond the sheet.
- **Freshness:** reflects end-of-current-Guests-tick state (CON-016 order: Guests tick before Traits tick). The bridge must not cache across ticks.
- **Determinism:** carrier order must be deterministic: guests by `GuestId`, then employees by `EmployeeId`, then rooms by `RoomId`, then consumed items by consuming `GuestId`.
- No re-entrancy: the bridge must not call `ITraitsCommands`.

## Conformance tests

`tests/contracts/traits/driven/` (against the real bridge with stubbed sources):

- Composition: scripted guests/staff/rooms/consumptions produce the exact expected carrier list, ordering asserted.
- Inactive rooms excluded (as `Room` carriers and for `InBroadcaster`).
- Walking guests have `Room == null`; `InBroadcaster` false.
- Consumed item appears only during the consumption span and carries sheet traits.
- Two consecutive calls without state change return equal snapshots (no hidden mutation), and reflect changes after one.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
