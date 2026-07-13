# CON-014: Progression Content Schema v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + data schema
> Provider: DOM-007 Progression (interface owner); implementer: progression content adapter
> Consumers: DOM-007 domain code (caller); content adapter ticket (implementer)
> Conformance tests: `tests/contracts/progression/content/`

## Purpose

Content definitions for milestones, perks/shop items, abilities, and venues. Milestone conditions use typed condition kinds + JSON parameters (DOM007-Q1 resolution, user 2026-07-13). Traces: REQ-029, REQ-032, REQ-036, REQ-076–083, REQ-085–089, REQ-112.

## Interface definition

```csharp
namespace TavernIdler.Domains.Progression;
using TavernIdler.Kernel;

public interface IProgressionContent
{
    ProgressionContent Load();   // once at startup; fail-fast on invalid content
}

public sealed record ProgressionContent(
    IReadOnlyList<MilestoneDef> Milestones,
    IReadOnlyList<ShopItemDef> ShopItems,
    IReadOnlyList<AbilityDef> Abilities,
    IReadOnlyList<VenueSheet> Venues,          // CON-013 type
    VenueId StarterVenue);                     // REQ-089

public sealed record MilestoneDef(
    MilestoneId Id, string DisplayName, string ConditionText,
    long Acclaim,                              // > 0
    VenueId? VenueBound,                       // REQ-036/088
    bool Secret,                               // REQ-112
    IMilestoneCondition Condition);            // ALL-of if composite

/// Closed set of condition kinds (decision 7). New kinds = contract change.
public interface IMilestoneCondition { bool IsMet(FeatState state); }

public sealed record GuestVolumeCondition(int GuestsInOneNight) : IMilestoneCondition;      // e.g. 1000 served
public sealed record VipSatisfiedCondition(GuestTypeId Vip) : IMilestoneCondition;
public sealed record RuleActivatedCondition(RuleId Rule) : IMilestoneCondition;
public sealed record StructureHeightCondition(int MinHeightCells) : IMilestoneCondition;
public sealed record RoomCountCondition(RoomTypeId? Type, int MinCount) : IMilestoneCondition; // null = any rooms
public sealed record AllOfCondition(IReadOnlyList<IMilestoneCondition> Conditions) : IMilestoneCondition;

/// Accumulated feat state the conditions read (built from CON-013 Feat intake).
public sealed record FeatState(
    NightGuestStats? CurrentNightStats,
    StructureMetrics? CurrentStructure,
    IReadOnlySet<GuestTypeId> VipsSatisfied,       // lifetime
    IReadOnlySet<RuleId> RulesActivated,           // lifetime
    VenueId CurrentVenue);

public sealed record ShopItemDef(
    ShopItemId Id, string DisplayName, ShopItemKind Kind, long Cost,      // Cost > 0
    IReadOnlyList<ShopItemId> Prerequisites,                              // REQ-076; perk-tree edges
    ShopEffect Effect);

/// Closed set for the prototype (REQ-078's open-endedness lands post-prototype via contract change).
public abstract record ShopEffect
{
    public sealed record UnlockRoomType(RoomTypeId Room) : ShopEffect;
    public sealed record UnlockNamedHire(NamedHireId Hire) : ShopEffect;
    public sealed record UnlockMenuItem(MenuItemId Item) : ShopEffect;
    public sealed record UnlockGuestType(GuestTypeId Guest) : ShopEffect;
    public sealed record GrantAbility(AbilityId Ability) : ShopEffect;
    public sealed record EnableEntranceFee(Money Amount) : ShopEffect;    // REQ-015
    private ShopEffect() { }
}

public sealed record AbilityDef(                    // REQ-080
    AbilityId Id, string DisplayName,
    int CooldownTicks,                              // ≥ 0
    int UsesPerNight,                               // ≥ 1
    Money GoldCost,                                 // Money.Zero = free
    AbilityEffect Effect);

/// Closed prototype set.
public abstract record AbilityEffect
{
    public sealed record SpendingBurstAll(double Factor, int DurationTicks) : AbilityEffect;
    public sealed record SatisfactionBoostAll(double Delta) : AbilityEffect;
    public sealed record SummonArrivals(int Count) : AbilityEffect;
    private AbilityEffect() { }
}
```

### JSON schema (content file `content/progression.json`)

```json
{ "starterVenue": "crossroads-inn",
  "milestones": [
    { "id": "first-full-house", "displayName": "Full House",
      "conditionText": "Reach 50 guests in a single night", "acclaim": 10,
      "venueBound": null, "secret": false,
      "condition": { "kind": "guestVolume", "guestsInOneNight": 50 } },
    { "id": "critics-choice", "displayName": "Critic's Choice",
      "conditionText": "Satisfy Aldous the Critic", "acclaim": 25,
      "venueBound": null, "secret": false,
      "condition": { "kind": "vipSatisfied", "vip": "food-critic" } } ],
  "shopItems": [
    { "id": "perk-old-tom", "displayName": "An Old Friend", "kind": "NamedEmployee",
      "cost": 15, "prerequisites": [],
      "effect": { "kind": "unlockNamedHire", "hire": "old-tom" } } ],
  "abilities": [
    { "id": "last-call", "displayName": "Last Call", "cooldownTicks": 600, "usesPerNight": 1,
      "goldCost": 0, "effect": { "kind": "spendingBurstAll", "factor": 1.5, "durationTicks": 100 } } ],
  "venues": [
    { "id": "crossroads-inn", "displayName": "Crossroads Inn",
      "lot": { "x": 0, "y": 0, "width": 24, "height": 10 },
      "entrance": { "x": 11, "y": 0 },
      "terrain": [ { "cell": { "x": 3, "y": 0 }, "effect": { "kind": "enablesRoomType", "room": "brewery" } } ],
      "guestWeightMultipliers": {}, "guestExclusions": [],
      "exclusives": { "guestTypes": [], "roomTypes": [], "menuItems": [], "vips": [] },
      "buildCostMultiplier": 1.0, "restockCostMultiplier": 1.0,
      "venueMilestones": [ "crossroads-landmark" ] } ] }
```

Condition `kind` discriminators: `guestVolume`, `vipSatisfied`, `ruleActivated`, `structureHeight`, `roomCount`, `allOf`. Effect discriminators mirror the record names in camelCase.

## Semantics

- **Validation (adapter's duty, fail-fast with field context):** unique ids per collection; `starterVenue` exists; every `venueBound`/`venueMilestones` id resolves; every venue has ≥1 entry in some milestone's `venueBound` or its `venueMilestones` list (REQ-088); prerequisite graph is acyclic and ids resolve (REQ-076); condition/effect discriminators known; cross-file id checks (rooms/hires/items/guests/rules) against the other content catalogs; entrance inside lot ground row; terrain cells inside lot; multipliers > 0; acclaim/cost > 0.
- **`IsMet` purity:** conditions are pure functions of `FeatState`; no side effects, no clock access.
- **Load-once:** content is immutable after `Load()`; hot-reload is out of contract.

## Conformance tests

`tests/contracts/progression/content/`:

- Golden-file load of the sample above into exact records.
- Each validation rule rejects a crafted bad file (incl. cyclic prerequisites, REQ-088 violation, unresolved cross-file ids, unknown discriminators).
- Condition semantics: each condition kind's `IsMet` truth table against constructed `FeatState`s; `AllOf` conjunction.
- Discriminator round-trip: every condition/effect kind serializes and parses.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
