# CON-011: Traits API v1.0

> Status: FROZEN (Gate 4 PASSED 2026-07-13)
> Kind: port interface + domain events + data schema
> Provider: DOM-006 Traits
> Consumers: app orchestrator (tick + effect routing), DOM-003 (`ApplyEffects` payloads), codex UI adapter, DOM-007 feat router, rule content adapter, persistence adapter
> Conformance tests: `tests/contracts/traits/`

## Purpose

The trait×trait rule engine: night lifecycle, per-tick evaluation, emitted effects, discovery codex, and the trait/rule catalog schema. Traces: REQ-040–047, REQ-094–096, REQ-110–111, REQ-044.

## Interface definition

```csharp
namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

public interface ITraitsCommands
{
    IReadOnlyList<IDomainEvent> BeginNight();     // clears episode state
    TraitsTickResult Tick();                      // pulls presence via IPresenceSource (CON-012)
    IReadOnlyList<IDomainEvent> EndNight();       // closes all episodes → ModifierEnded/…Ended effects
    CodexSnapshot Capture();                      // lifetime scope (REQ-044)
    void Restore(CodexSnapshot snapshot);
}

public sealed record TraitsTickResult(
    IReadOnlyList<EmittedEffect> Effects,         // routed to DOM-003 (and carried into CON-006 requests)
    IReadOnlyList<IDomainEvent> Events);          // RuleActivated / RuleDiscovered

public interface ITraitsQueries
{
    IReadOnlyList<CodexEntry> Codex { get; }                  // discovered rules (REQ-043)
    IReadOnlyList<TraitDef> TraitRegistry { get; }            // REQ-095; for hover UI
    int TotalRuleCount { get; }                               // codex progress display (count only; rules stay hidden)
}

public sealed record CodexEntry(RuleId Rule, TraitId TraitA, TraitId TraitB, string Description, RuleReach Reach, IReadOnlyList<EffectClassKind> EffectClasses);
public sealed record TraitDef(TraitId Id, string DisplayName, string Description);
public enum RuleReach { SameRoom, TavernWide }                // REQ-046
public enum EffectClassKind { SatisfactionModifier, BehaviorEvent, SpendingMultiplier }  // REQ-042
public enum StackingMode { Binary, CountScaling }             // REQ-045

public sealed record CodexSnapshot(int SchemaVersion /*1*/, IReadOnlyList<RuleId> Discovered);

// ── Emitted effects (executed by DOM-003 / carried into CON-006 requests) ──
public abstract record EmittedEffect
{
    /// Continuous while the episode lasts (REQ-110). SatisfactionRatePerTick applied per tick per target.
    public sealed record SatisfactionModifierBegan(RuleId Rule, long EpisodeId, IReadOnlyList<GuestId> Targets, double SatisfactionRatePerTick) : EmittedEffect;
    public sealed record SatisfactionModifierEnded(RuleId Rule, long EpisodeId) : EmittedEffect;

    /// Multiplies targets' TransactionRequest.SpendingMultiplier while active.
    public sealed record SpendingMultiplierBegan(RuleId Rule, long EpisodeId, IReadOnlyList<GuestId> Targets, double Factor) : EmittedEffect;
    public sealed record SpendingMultiplierEnded(RuleId Rule, long EpisodeId) : EmittedEffect;

    /// Rolled once per episode (REQ-110); fired only when the roll succeeds.
    public sealed record BehaviorEventTriggered(RuleId Rule, long EpisodeId, BehaviorOutcome Outcome, IReadOnlyList<GuestId> Targets, RoomId? Room) : EmittedEffect;

    private EmittedEffect() { }
}

/// Closed set of mechanical consequences (REQ-042(b)); flavor/animation is content data.
public abstract record BehaviorOutcome
{
    public sealed record GuestsLeave(string FlavorId) : BehaviorOutcome;                          // e.g. brawl
    public sealed record SpendingBurst(string FlavorId, double Factor, int DurationTicks) : BehaviorOutcome; // e.g. sing-along
    public sealed record SatisfactionShock(string FlavorId, double Delta) : BehaviorOutcome;
    private BehaviorOutcome() { }
}

// ── Events ──────────────────────────────────────────────────
public sealed record RuleActivated(RuleId Rule, long EpisodeId, RoomId? Room) : IDomainEvent;   // feat signal (REQ-029)
public sealed record RuleDiscovered(RuleId Rule) : IDomainEvent;                                 // REQ-111
```

### Trait & rule catalog JSON schema (content file `content/traits.json`)

```json
{ "traits": [
    { "id": "rowdy",   "displayName": "Rowdy",   "description": "Loud and boisterous." },
    { "id": "lawful",  "displayName": "Lawful",  "description": "Upholds the law." },
    { "id": "outlaw",  "displayName": "Outlaw",  "description": "Wanted by the law." } ],
  "rules": [
    { "id": "outlaw-x-lawful", "traitA": "outlaw", "traitB": "lawful",
      "description": "Outlaws and the lawful come to blows.",
      "reach": "SameRoom", "stacking": "Binary",
      "effects": [ { "class": "BehaviorEvent", "chance": 0.4,
                     "outcome": { "kind": "GuestsLeave", "flavorId": "brawl" } } ] },
    { "id": "rowdy-x-rowdy", "traitA": "rowdy", "traitB": "rowdy",
      "description": "Rowdy crowds egg each other on.",
      "reach": "SameRoom", "stacking": "CountScaling",
      "effects": [ { "class": "SpendingMultiplier", "factorPerPair": 1.05, "maxFactor": 1.5 },
                   { "class": "SatisfactionModifier", "ratePerTickPerPair": 0.001, "maxRate": 0.01 } ] }
] }
```

## Semantics

- **Episode (REQ-110):** a rule's episode opens when ≥1 qualifying pair (one carrier holding `traitA`, another holding `traitB`, at least one a guest — REQ-040) satisfies the rule's reach: same room, or anywhere if reach is `TavernWide` or either carrier `InBroadcaster` (REQ-047). Episode closes when no qualifying pair remains. `EpisodeId` is unique per run.
- **Effects timing:** continuous classes emit `…Began` when the episode opens and `…Ended` when it closes (also en masse at `EndNight`); behavior events roll once per episode at open (`IRandom` stream `"traits"`, probability `chance`) — success ⇒ `BehaviorEventTriggered`, failure ⇒ nothing for that episode. Re-entry (new episode) re-rolls.
- **Stacking (REQ-045):** `Binary` — factors/rates as authored, independent of pair count. `CountScaling` — `factor = min(maxFactor, factorPerPair^pairs)`, `rate = min(maxRate, ratePerTickPerPair × pairs)`; pair-count changes update the active effect via `…Ended` + `…Began` with the same `EpisodeId`? **No — normative:** pair-count changes close and reopen with a NEW `EpisodeId` and do not re-roll behavior events for the same underlying rule while any pair persists (behavior re-roll requires full episode closure).
- **Targets:** guest participants of qualifying pairs at emission time. `Targets` is a snapshot; DOM-003 applies to those ids only. Employees/rooms/items are participants but never targets (effects land on guests, REQ-042).
- **Discovery (REQ-111/043):** first `RuleActivated` for a rule ever (lifetime) also emits `RuleDiscovered` and permanently adds the codex entry. Codex survives prestige (REQ-044): `Restore` merges — the prestige sequence never clears it (CON-016).
- **Guest-participation rule (REQ-040):** enforced at evaluation; a rule between two non-guest carriers never activates.
- **Ordering:** effects within one `TraitsTickResult` are ordered: all `…Ended` first, then `…Began`, then `BehaviorEventTriggered`.
- **Schema validation:** trait ids unique; rule ids unique; `traitA`/`traitB` exist; ≤1 effect per class per rule; `chance` ∈ (0,1]; scaling params require `CountScaling`; unknown fields fail-fast. REQ-094 density (~20–30 rules) is content guidance, not validated.
- `Capture` legal any phase (codex is tiny and lifetime-scoped); `Tick` legal only during Service.

## Conformance tests

`tests/contracts/traits/`:

- Episode lifecycle: co-presence open → effects `Began`; separation → `Ended`; re-entry → new `EpisodeId`, behavior re-rolled (seeded).
- Reach: same-room only; tavern-wide rule; broadcaster room widening (REQ-047) incl. leaving broadcaster closes episode.
- REQ-040: staff×staff and room×item pairs never activate; guest×staff does.
- Stacking: binary invariance vs count-scaling growth with caps; pair-count change ⇒ close/reopen without behavior re-roll.
- Behavior roll: seeded chance table — success/failure deterministic; once per episode.
- Discovery: first activation emits `RuleDiscovered` exactly once ever; codex snapshot round-trip; persists across simulated prestige.
- Effect ordering within a tick result.
- Golden-file catalog load + every validation rule.

## Change history

| Version | Date | Change | Approved by | Affected tickets |
|---|---|---|---|---|
| 1.0 | 2026-07-13 | initial | user | — |
