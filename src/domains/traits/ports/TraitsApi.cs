namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

// ── CON-011: Traits API v1.1 (FROZEN 2026-07-13; amended v1.1 2026-07-13) ────
// The trait×trait rule engine surface: night lifecycle, per-tick evaluation, emitted
// effects, discovery codex, and the trait/rule catalog schema. Port interfaces +
// value/effect/event types only — no domain behavior lives here (the CountScaling
// formulas, episode churn, and effect ordering are asserted by the conformance suite
// and implemented by DOM-006 / TKT-013).
// Traces: REQ-040–047, REQ-094–096, REQ-110–111, REQ-044.

public interface ITraitsCommands
{
    IReadOnlyList<IDomainEvent> BeginNight();     // clears episode state
    TraitsTickResult Tick();                      // pulls presence via IPresenceSource (CON-012)
    IReadOnlyList<IDomainEvent> EndNight();       // closes open episodes internally; emits no effects, returns empty (v1.1)
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
