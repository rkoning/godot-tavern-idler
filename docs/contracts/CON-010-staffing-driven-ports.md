# CON-010: Staffing Driven Ports v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface
> Provider: DOM-005 Staffing (interface owner); implementers: structure bridge (over CON-003), progression bridge (over CON-013)
> Consumers: DOM-005 domain code (caller); bridge tickets (implementers)
> Conformance tests: `tests/contracts/staffing/driven/`

## Purpose

What Staffing needs from outside: per-room staffing requirements/maxima and named-hire unlock state. Traces: REQ-057, REQ-063, REQ-065.

## Interface definition

```csharp
namespace TavernIdler.Domains.Staffing;
using TavernIdler.Kernel;
using TavernIdler.Domains.Structure;   // StaffRequirements (CON-003)

public interface IRoomRequirements
{
    /// Requirements for the room's CURRENT tier (tier overrides applied, CON-004).
    /// Rooms without staffing requirements return an empty Roles list.
    StaffRequirements Get(RoomId room);          // KeyNotFoundException if room absent
    IReadOnlyList<RoomId> RoomsWithRequirements();
}

public interface IHireUnlocks
{
    /// Named hires currently purchasable/hireable (their unlock perk is owned). REQ-063.
    IReadOnlyList<NamedHireId> UnlockedNamedHires();
}
```

## Semantics

- **`IRoomRequirements`:** values change only on structure mutations (build/upgrade/demolish, all Prep) — stable during Service. The bridge reflects CON-003 `RoomInfo.Staffing` with tier overrides already applied; DOM-005 never sees tier mechanics.
- **`IHireUnlocks`:** monotone within a run except at prestige — REQ-035 keeps unlocks across prestiges, and REQ-079 refunds re-lock purchased perks until re-bought; so the list may shrink only at prestige. Stable within a phase.
- No re-entrancy: implementations must not call `IStaffingCommands`.

## Conformance tests

`tests/contracts/staffing/driven/`:

- Bridge equivalence: `Get(room)` equals CON-003 sheet + tier override for scripted rooms across an upgrade.
- Rooms without requirements return empty list, appear only in `RoomsWithRequirements()` when non-empty.
- `IHireUnlocks` reflects stubbed CON-013 perk ownership; shrinks only across a stubbed prestige.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
