# CON-013: Progression API v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + domain events
> Provider: DOM-007 Progression
> Consumers: progression UI adapter, app orchestrator (feat router, settlement, prestige sequence), venue bridge (CON-004 `ILotConstraints`), attraction bridge (CON-006), venue-modifier bridge (CON-008), hire-unlock bridge (CON-010), persistence adapter
> Conformance tests: `tests/contracts/progression/`

## Purpose

Milestones, Acclaim, the shop, abilities, prestige, and venue data. Traces: REQ-029, REQ-031–039, REQ-076–090, REQ-112–113.

## Interface definition

```csharp
namespace TavernIdler.Domains.Progression;
using TavernIdler.Kernel;
using TavernIdler.Domains.Guests;      // NightGuestStats (CON-005)
using TavernIdler.Domains.Structure;   // StructureMetrics (CON-003), TerrainFeature (CON-004)
using TavernIdler.Domains.Economy;     // MilestoneAward (CON-007)

public enum ShopError { WrongPhase, UnknownItem, AlreadyOwned, PrerequisitesMissing, InsufficientAcclaim }
public enum AbilityError { UnknownAbility, NotOwned, OnCooldown, NoUsesLeft, CannotPayCost, NotInService }

public interface IProgressionCommands
{
    /// Feat intake; legal during Service. Conditions matched incrementally; met milestones become PENDING.
    IReadOnlyList<IDomainEvent> RecordFeat(Feat feat);
    /// Settlement only: pending → earned; returns awards for CON-007 SettlementInput (REQ-021).
    IReadOnlyList<MilestoneAward> CommitSettlementAwards();
    Outcome<ShopError> Purchase(ShopItemId item);                 // Prep only (REQ-039)
    Outcome<AbilityError> UseAbility(AbilityId ability);          // Service only (REQ-080)
    IReadOnlyList<IDomainEvent> TickAbilities(int ticks);         // cooldown decay
    /// Any phase (REQ-113). Discards pending awards; refunds all spending (REQ-033/079).
    IReadOnlyList<IDomainEvent> Prestige(VenueId nextVenue);
    IReadOnlyList<IDomainEvent> BeginNightReset();                // per-night ability uses reset
    ProgressionSnapshot Capture();
    void Restore(ProgressionSnapshot snapshot);
}

public abstract record Feat                                       // REQ-029 condition inputs
{
    public sealed record NightStats(NightGuestStats Stats) : Feat;
    public sealed record VipSatisfied(GuestTypeId Vip) : Feat;
    public sealed record RuleActivated(RuleId Rule) : Feat;
    public sealed record StructureState(StructureMetrics Metrics) : Feat;
    private Feat() { }
}

public interface IProgressionQueries
{
    IReadOnlyList<MilestoneView> Milestones { get; }              // REQ-112 visibility applied
    long LifetimeAcclaim { get; }                                 // never decreases (REQ-032)
    long SpentAcclaim { get; }
    long AvailableAcclaim { get; }                                // Lifetime − Spent
    IReadOnlyList<ShopItemView> ShopCatalog { get; }
    IReadOnlyList<AbilityView> Abilities { get; }
    UnlockState Unlocks { get; }                                  // REQ-035
}

public sealed record MilestoneView(MilestoneId Id, string DisplayName, string ConditionText, long Acclaim,
    VenueId? VenueBound, MilestoneStatus Status, bool Secret);    // Secret+unearned ⇒ masked fields, see semantics
public enum MilestoneStatus { Unearned, PendingSettlement, Earned }

public sealed record ShopItemView(ShopItemId Id, string DisplayName, ShopItemKind Kind, long Cost,
    IReadOnlyList<ShopItemId> Prerequisites, bool Owned, bool Purchasable);  // REQ-076/079
public enum ShopItemKind { Perk, SpecialRoom, NamedEmployee }

public sealed record AbilityView(AbilityId Id, string DisplayName, bool Owned,
    int CooldownRemainingTicks, int UsesLeftTonight);             // REQ-080

public sealed record UnlockState(
    IReadOnlyList<RoomTypeId> Rooms,
    IReadOnlyList<NamedHireId> NamedHires,
    IReadOnlyList<GuestTypeId> GuestTypes,
    IReadOnlyList<MenuItemId> MenuItems,
    IReadOnlyList<VenueId> Venues,                                // REQ-038 choice set
    bool EntranceFeeEnabled, Money EntranceFeeAmount);            // REQ-015

public interface IVenueData                                       // REQ-081–088
{
    VenueSheet Current { get; }                                   // fixed per run (REQ-090)
    IReadOnlyList<VenueSheet> UnlockedVenues { get; }             // for the prestige choice UI
}

public sealed record VenueSheet(
    VenueId Id, string DisplayName,
    GridRect Lot,                                                 // REQ-082
    CellCoord Entrance,                                           // REQ-084
    IReadOnlyList<TerrainFeature> Terrain,                        // REQ-083 (CON-004 type)
    IReadOnlyDictionary<GuestTypeId, double> GuestWeightMultipliers,  // REQ-085
    IReadOnlyList<GuestTypeId> GuestExclusions,                   // REQ-085
    ExclusiveContent Exclusives,                                  // REQ-086
    double BuildCostMultiplier, double RestockCostMultiplier,     // REQ-087
    IReadOnlyList<MilestoneId> VenueMilestones);                  // REQ-088

public sealed record ExclusiveContent(
    IReadOnlyList<GuestTypeId> GuestTypes, IReadOnlyList<RoomTypeId> RoomTypes,
    IReadOnlyList<MenuItemId> MenuItems, IReadOnlyList<GuestTypeId> Vips);

public sealed record ProgressionSnapshot(int SchemaVersion /*1*/, string LifetimeJson, string RunJson); // CON-017

// ── Events ──────────────────────────────────────────────────
public sealed record MilestonePending(MilestoneId Id) : IDomainEvent;         // condition met mid-service
public sealed record MilestoneEarnedEvent(MilestoneId Id, long Acclaim) : IDomainEvent; // at commit
public sealed record PurchaseMade(ShopItemId Item, long Cost) : IDomainEvent;
public sealed record UnlockGranted(ShopItemId Source) : IDomainEvent;
public sealed record AbilityUsed(AbilityId Id) : IDomainEvent;
public sealed record PrestigeExecuted(VenueId NextVenue, long RefundedAcclaim) : IDomainEvent; // → CON-016 reset sequence
```

## Semantics

- **Milestones (REQ-029/032):** conditions are typed condition classes parameterized from content JSON (decision 7; schema in CON-014). Each matches against accumulated feat state; one-time — `Earned` never re-pends. Mid-service condition satisfaction ⇒ `PendingSettlement` + `MilestonePending`; `CommitSettlementAwards` converts pendings to `Earned`, adds Acclaim to `LifetimeAcclaim`, emits `MilestoneEarnedEvent`s, returns the award list for the night report. **Prestige with pendings discards them entirely** (REQ-113/021) — conditions may be re-met in a later run only if the milestone is still unearned.
- **Secret milestones (REQ-112):** `Secret && Status == Unearned` ⇒ `DisplayName = "???"`, `ConditionText = "???"`, real `Acclaim` shown as 0; unmasked once earned.
- **Shop (REQ-034/076/077/079):** `Purchasable = !Owned && prerequisites all owned && AvailableAcclaim ≥ Cost`. `Purchase` failures per `ShopError` in that check order. Success: `SpentAcclaim += Cost`, `PurchaseMade` + `UnlockGranted` (what it unlocks is content data: room types, named hires, menu items, guest types, abilities, entrance fee, venue-independent flags).
- **Prestige (REQ-033/037–039/113):** legal any phase. `nextVenue` must be in `Unlocks.Venues` (else `ArgumentException` — UI only offers valid choices). Effects inside this domain: `SpentAcclaim = 0` (full refund), all purchases un-owned, ability state cleared, `CurrentRun` ← next venue, run counter +1, pendings discarded, codex untouched (CON-011), `LifetimeAcclaim` and `Earned` milestones and `Unlocks` retained (REQ-035). Emits `PrestigeExecuted`; the cross-domain reset sequence is CON-016's.
- **Abilities (REQ-080):** `UseAbility` checks owned → cooldown → uses → resource cost (gold costs settle via a CON-007 charge issued by the orchestrator on `AbilityUsed`; insufficient gold surfaces as `CannotPayCost` via pre-check query). `BeginNightReset` restores per-night uses at each `ServiceBegan`.
- **Feat state:** `NightStats`/`StructureState` replace prior samples; `VipSatisfied`/`RuleActivated` accumulate as sets. Volume conditions ("1,000 guests in one night") evaluate against the current night's sample only.
- **Venue data:** `Current` constant per run; `Lot`, terrain, entrance immutable. Starter venue is content-flagged (CON-014); fresh saves start there (REQ-089).
- `Capture` Prep/Settlement only. Single-threaded per CON-016.

## Conformance tests

`tests/contracts/progression/`:

- Milestone lifecycle: feat → `MilestonePending` → commit → `Earned` + award list + lifetime increase; one-time (re-feat never re-pends); pending discarded by prestige and re-earnable later.
- Secret masking before/after earn.
- Shop: each `ShopError` in defined order; purchase updates balances and `Purchasable` of dependents (tree gating REQ-076).
- Acclaim invariants: `Lifetime` monotone; `Available = Lifetime − Spent` always; prestige zeroes `Spent`, keeps `Lifetime`, un-owns purchases, keeps `Unlocks` and earned milestones.
- Abilities: cooldown/uses/cost check order; night reset restores uses; `NotInService` outside Service.
- Venue: `Current` stable within run; changes only via prestige; sheet fields round-trip content golden file.
- Snapshot: lifetime vs run scope separation — restoring lifetime alone (fresh run) keeps milestones/unlocks, drops run state.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
