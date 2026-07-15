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
