# CON-005: Guests API v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + domain events + data schema
> Provider: DOM-003 Guests
> Consumers: app orchestrator, guest render adapter, DOM-006 presence bridge (CON-012), DOM-007 feat router, DOM-004 (NightGuestStats in settlement), persistence adapter, guest content adapter
> Conformance tests: `tests/contracts/guests/`

## Purpose

Driving surface of the guest simulation: night lifecycle, per-tick agent advancement, effect application, view model, presence, guest-sheet schema. Traces: REQ-002, REQ-008–010, REQ-018, REQ-023–024, REQ-048–055, REQ-092–093, REQ-102–104, REQ-107.

## Interface definition

```csharp
namespace TavernIdler.Domains.Guests;
using TavernIdler.Kernel;
using TavernIdler.Domains.Traits;   // EmittedEffect (CON-011)

public interface IGuestSimCommands
{
    IReadOnlyList<IDomainEvent> BeginService();   // ServiceBegan: lodgers depart (REQ-107), arrivals start
    IReadOnlyList<IDomainEvent> Tick(int ticks);  // arrivals, queue admission, movement, agendas, departures
    IReadOnlyList<IDomainEvent> BeginDrain();     // REQ-016/017: stop entries; queue disbands
    IReadOnlyList<IDomainEvent> EndNight();       // after settlement: finalize stats; only lodgers remain
    IReadOnlyList<IDomainEvent> ApplyEffects(IReadOnlyList<EmittedEffect> effects); // from Traits via orchestrator
    IReadOnlyList<IDomainEvent> ClearAll();       // prestige: everyone (incl. lodgers) removed
    GuestsSnapshot Capture();                     // Prep/Settlement only
    void Restore(GuestsSnapshot snapshot);
}

public sealed record GuestsSnapshot(
    int SchemaVersion,                            // 1
    IReadOnlyList<LodgerRecord> Lodgers,          // reduced form (DOM003-Q2 resolution)
    IReadOnlyList<VipState> VipStates,
    int NextGuestIdValue);

public sealed record LodgerRecord(GuestId Id, GuestTypeId Type, RoomId LodgingRoom, Money WalletRemaining, double Satisfaction);
public sealed record VipState(GuestTypeId Vip, bool VisitedThisNight);

// ── View (pull model, Decision D) ───────────────────────────
public interface IGuestView
{
    IReadOnlyList<GuestAgentView> Agents { get; }
    QueueView Queue { get; }
    IReadOnlyDictionary<RoomId, RoomOccupancy> Occupancy { get; }   // REQ-103 inputs
}

public sealed record GuestAgentView(
    GuestId Id, GuestTypeId Type, bool IsVip,
    CellCoord Cell, CellCoord? NextCell, double MoveProgress,        // [0,1) toward NextCell; renderer interpolates
    GuestActivity Activity, double Satisfaction,                     // [-1, +1]
    IReadOnlyList<TraitId> Traits);                                  // REQ-043: always visible

public enum GuestActivity { Entering, Walking, WaitingBlocked, BeingServed, Leaving, Lodging }

public sealed record QueueView(IReadOnlyList<QueuedGuestView> VisibleLine, int OverflowCount); // REQ-010
public sealed record QueuedGuestView(GuestId Id, GuestTypeId Type, int PatienceRemainingTicks);

public sealed record RoomOccupancy(int Occupants, int Capacity);     // crowd ratio = Occupants / Capacity

// ── Presence (for CON-012 bridge) ───────────────────────────
public interface IGuestPresence
{
    IReadOnlyList<GuestPresenceEntry> CurrentPresence();
}
public sealed record GuestPresenceEntry(
    GuestId Id, GuestTypeId Type, RoomId? Room,       // null = walking circulation/exterior
    IReadOnlyList<TraitId> Traits,
    MenuItemId? Consuming);                           // non-null during MenuConsumption service (DOM006-Q1)

// ── Events ──────────────────────────────────────────────────
public sealed record GuestAdmitted(GuestId Id, GuestTypeId Type) : IDomainEvent;
public sealed record GuestQueued(GuestId Id, GuestTypeId Type) : IDomainEvent;
public sealed record GuestLeftQueue(GuestId Id, QueueLeaveReason Reason) : IDomainEvent;
public sealed record GuestLeft(GuestId Id, GuestTypeId Type, LeaveReason Reason, double FinalSatisfaction) : IDomainEvent;
public sealed record WantFulfilled(GuestId Id, string ServiceId, RoomId Room) : IDomainEvent;
public sealed record WantBlocked(GuestId Id, string ServiceId, BlockReason Reason) : IDomainEvent;
public sealed record VipVisited(GuestTypeId Vip) : IDomainEvent;
public sealed record VipSatisfied(GuestTypeId Vip, double FinalSatisfaction) : IDomainEvent;   // feat (REQ-029)
public sealed record AllGuestsGone() : IDomainEvent;                                            // → Cycle
public sealed record NightStatsFinal(NightGuestStats Stats) : IDomainEvent;                     // at AllGuestsGone

public enum QueueLeaveReason { Admitted, PatienceExpired, Disbanded }
public enum LeaveReason { AgendaComplete, WalletEmpty, PatienceGaveUp, BehaviorEvent, Drain, LodgingCheckout }
public enum BlockReason { RoomClosed, RoomInactive, RoomFull, SoldOut, NoSuchService }

public sealed record NightGuestStats(
    int TotalAdmitted, int TotalTurnedAwayQueue,
    IReadOnlyDictionary<GuestTypeId, int> AdmittedByType,
    double MeanSatisfaction,
    int MaxConcurrentGuests,
    IReadOnlyList<string> NotableEvents);   // human-readable lines for REQ-022 report
```

### Guest sheet JSON schema (content file `content/guests.json`) — REQ-052

```json
{ "guestTypes": [ {
  "id": "dwarf", "displayName": "Dwarf", "isVip": false,
  "spriteId": "guest_dwarf",
  "baseWeight": 10,
  "attractors": [ { "kind": "menuItem", "id": "ale", "weight": 5 },
                  { "kind": "roomType", "id": "taproom", "weight": 3 } ],
  "crowding": { "preference": "loves", "magnitude": 0.3 },
  "queuePatienceTicks": 300,
  "blockedWaitTicks": 200,
  "agenda": [ { "serviceId": "drink", "menuItem": "ale" }, { "serviceId": "meal", "menuItem": null } ],
  "walletMin": 20, "walletMax": 60,
  "traits": [ "rowdy", "hearty" ],
  "vip": null
}, {
  "id": "food-critic", "displayName": "Aldous the Critic", "isVip": true,
  "spriteId": "vip_critic", "baseWeight": 0,
  "attractors": [], "crowding": { "preference": "hates", "magnitude": 0.5 },
  "queuePatienceTicks": 250, "blockedWaitTicks": 150,
  "agenda": [ { "serviceId": "meal", "menuItem": "roast-pheasant" } ],
  "walletMin": 100, "walletMax": 100,
  "traits": [ "refined" ],
  "vip": { "visitChancePerNight": 0.15,
           "conditions": [ { "kind": "menuHasItem", "id": "roast-pheasant" },
                           { "kind": "lifetimeAcclaimAtLeast", "value": 50 } ] }
} ] }
```

VIP condition kinds (closed set, REQ-050): `hasRoomType`, `menuHasItem`, `venueIs`, `lifetimeAcclaimAtLeast`.
Crowding `preference` ∈ { `loves`, `neutral`, `hates` }; `magnitude` ∈ [0, 1].

## Semantics

- **Arrivals (REQ-024/102):** per tick, expected arrivals = `ArrivalRateFactor × Σ effectiveWeight(type)` (config constant; from CON-006 `IAttractionContext`). `effectiveWeight = (baseWeight + Σ matched attractor weights) × venueMultiplier`, 0 if excluded or unavailable. Type of each arrival drawn proportional to effective weights via `IRandom` stream `"guests"`. VIPs: independent per-night roll at `BeginService` when conditions met (REQ-050); a rolled VIP arrives at a uniformly random tick in the first half of the phase.
- **Admission/queue (REQ-008/010/018):** admitted while `Agents.Count < TotalGuestCapacity` (CON-006 structure read); else queued FIFO. Queue admission re-checked each tick; patience decrements per tick; expiry → `GuestLeftQueue(PatienceExpired)`, gone for the night. `BeginDrain` disbands the queue.
- **Agenda (REQ-048/053/054):** wants pursued in order; guest paths to a room offering the want's `serviceId` (nearest by path length; ties → lowest `RoomId`). Blocked (closed/inactive/full/sold-out) → wait up to `blockedWaitTicks`, then satisfaction penalty `-0.2` and skip. Wallet-empty → immediate `GuestLeft(WalletEmpty)`. Agenda done → `GuestLeft(AgendaComplete)`.
- **Service occupancy (REQ-104):** duration = `ceil(BaseDurationTicks / (roomSpeedFactor × efficiencyFactor × traitPerkModifiers))`; occupant counts toward `RoomOccupancy` for that span.
- **Crowding (REQ-009/103):** at service completion, crowd ratio `r = Occupants/Capacity` of the current room maps to a satisfaction delta: `loves`: `+magnitude × r`; `hates`: `-magnitude × r`; `neutral`: 0. The same delta signs the payment modifier (below).
- **Satisfaction → payment (REQ-023):** each transaction request carries `SatisfactionModifier = clamp(1 + 0.5 × satisfaction, 0.5, 1.5)`. Satisfaction ∈ [-1, +1], starts 0, has no effect other than this modifier.
- **Effects in (`ApplyEffects`, REQ-042/110):** `SatisfactionModifierBegan/Ended` add/remove a per-tick satisfaction drift on targets; `SpendingMultiplierBegan/Ended` set the multiplier carried on targets' transaction requests (stacking multiplicatively); `BehaviorEventTriggered` executes the mechanical outcome (CON-011 `BehaviorOutcome`): `GuestsLeave` → targets `GuestLeft(BehaviorEvent)`; `SpendingBurst` → one-shot multiplier for its duration; `SatisfactionShock` → immediate delta.
- **Lodging (REQ-107):** guest completing a `Lodging` service stays as `Lodging` activity through Settlement/Prep, occupies its room, departs (`LodgingCheckout`) at next `BeginService`. Snapshots persist lodgers in reduced `LodgerRecord` form; all other agent state is never serialized (Decision C).
- **AllGuestsGone:** emitted by the `Tick` (or `BeginDrain`) whose processing leaves zero non-lodging agents while draining; followed immediately by `NightStatsFinal`.
- **Determinism:** identical command sequence + identical driven-port reads + same RNG seed ⇒ identical events and view states. `Capture` outside Prep/Settlement throws `InvalidOperationException`.
- **Patience config validation (REQ-092):** at content load, every type's patience values must lie within 10–30% of `ServiceDurationTicks`; violation fails startup with the offending type named.

## Conformance tests

`tests/contracts/guests/`:

- Determinism: two runs, same seed + scripted driven-port stubs ⇒ identical event streams.
- Queue: capacity fill → queue growth → admission on capacity free (FIFO) → patience expiry → drain disband; `OverflowCount` vs `VisibleLine` split.
- Agenda walk: fulfill → blocked-wait → penalty+skip → complete; each `BlockReason` provoked; wallet-empty exit.
- Crowding table: loves/neutral/hates × empty/half/full room satisfaction deltas.
- Payment modifier bounds: satisfaction −1/0/+1 → 0.5/1.0/1.5.
- Effects: each `EmittedEffect` kind applied and (where applicable) ended; behavior outcomes act as specified.
- Lodger cycle: buy lodging → persists through EndNight/snapshot round-trip → departs at next BeginService.
- VIP: conditions unmet ⇒ never arrives (1000-night stub run); met ⇒ arrival frequency ≈ `visitChancePerNight`; `VipSatisfied` only when satisfaction > 0 at departure; REQ-055 revisit after unsatisfied exit.
- `AllGuestsGone` emitted exactly once per night, followed by `NightStatsFinal`; stats fields consistent with scripted scenario.
- Guest content golden-file load + every schema validation rule (incl. REQ-092 band check).

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
