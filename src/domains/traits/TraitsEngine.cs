namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

// ── DOM-006: the trait×trait rule engine (implements CON-011 v1.1) ──────────
// Per tick: pull a fresh presence snapshot (CON-012), diff each rule's qualifying pair set against
// the episode ledger, and emit the resulting effects — Ended, then Began, then BehaviorEventTriggered
// (CON-011 "Ordering"). The engine never applies a consequence itself: effects are data routed by the
// orchestrator to Guests/Economy (CON-016). Behavior rolls draw from the "traits" RNG stream (CON-015).

public sealed class TraitsEngine : ITraitsCommands, ITraitsQueries
{
    private const int CodexSchemaVersion = 1;
    private const string RandomStream = "traits";

    private readonly RuleBook _book;
    private readonly IPresenceSource _presence;
    private readonly IRandom _random;
    private readonly CoPresenceEvaluator _evaluator = new();
    private readonly EpisodeLedger _episodes = new();
    private readonly Codex _codex = new();

    private bool _nightOpen;

    public TraitsEngine(RuleBook book, IPresenceSource presence, IRandomSource random)
    {
        _book = book;
        _presence = presence;
        _random = random.GetStream(RandomStream);
    }

    // ── Commands (CON-011) ──────────────────────────────────────

    /// Clears stale episode state; emits nothing.
    public IReadOnlyList<IDomainEvent> BeginNight()
    {
        _episodes.Clear();
        _nightOpen = true;
        return System.Array.Empty<IDomainEvent>();
    }

    /// Closes any still-open episode internally and emits no effects (CON-011 v1.1): settlement runs
    /// this after the guests have left, so there is nobody left to receive an …Ended. Closure is
    /// observable in that the next BeginNight reopens qualifying pairs with fresh EpisodeIds.
    public IReadOnlyList<IDomainEvent> EndNight()
    {
        _episodes.Clear();
        _nightOpen = false;
        return System.Array.Empty<IDomainEvent>();
    }

    public TraitsTickResult Tick()
    {
        if (!_nightOpen)
            throw new InvalidOperationException(
                "ITraitsCommands.Tick is legal only during Service, between BeginNight and EndNight (CON-011).");

        var snapshot = _presence.Current();
        var ended = new List<EmittedEffect>();
        var began = new List<EmittedEffect>();
        var triggered = new List<EmittedEffect>();
        var events = new List<IDomainEvent>();

        foreach (var rule in _book.Rules)
        {
            var qualifying = _evaluator.Evaluate(rule, snapshot);
            var open = _episodes.Find(rule.Id);

            if (!qualifying.Any)
            {
                if (open is not null)
                {
                    EmitEnded(rule, open, ended);
                    _episodes.Close(rule.Id);
                }
                continue;
            }

            // An unchanged pair set means the same episode continues: continuous effects stay applied
            // from their …Began, so nothing is re-emitted.
            if (open is not null && open.Pairs.SetEquals(qualifying.Pairs)) continue;

            var behaviorRolled = false;
            if (open is not null)
            {
                EmitEnded(rule, open, ended);            // churn: close, then reopen with the new pair set
                behaviorRolled = open.BehaviorRolled;    // …without re-arming the once-per-span roll
            }

            var episode = _episodes.Open(rule.Id, qualifying.Pairs, behaviorRolled);
            EmitBegan(rule, episode, qualifying, began);

            events.Add(new RuleActivated(rule.Id, episode.Id, qualifying.Room));
            if (_codex.Discover(rule.Id)) events.Add(new RuleDiscovered(rule.Id));   // REQ-111: first time ever

            RollBehavior(rule, episode, qualifying, triggered);
        }

        ended.AddRange(began);
        ended.AddRange(triggered);
        return new TraitsTickResult(ended, events);
    }

    public CodexSnapshot Capture() =>
        new(CodexSchemaVersion, DiscoveredInCatalogOrder());

    /// Merges into the codex (REQ-044: it survives prestige, so a restore never narrows it). Ids the
    /// current catalog no longer defines are dropped — they name no rule to reveal.
    public void Restore(CodexSnapshot snapshot) =>
        _codex.Merge(snapshot.Discovered.Where(KnownRule));

    // ── Queries (CON-011) ───────────────────────────────────────

    public IReadOnlyList<CodexEntry> Codex =>
        _book.Rules
            .Where(rule => _codex.Knows(rule.Id))
            .Select(rule => new CodexEntry(
                rule.Id, rule.TraitA, rule.TraitB, rule.Description, rule.Reach,
                rule.Effects.Select(e => e.Class).Distinct().ToArray()))
            .ToArray();

    public IReadOnlyList<TraitDef> TraitRegistry =>
        _book.Traits.Select(t => new TraitDef(t.Id, t.DisplayName, t.Description)).ToArray();

    public int TotalRuleCount => _book.Rules.Count;

    // ── Emission ────────────────────────────────────────────────

    private static void EmitEnded(RuleDefinition rule, Episode episode, List<EmittedEffect> into)
    {
        foreach (var effect in rule.Effects)
        {
            switch (effect)
            {
                case EffectSpec.SpendingBinary or EffectSpec.SpendingScaling:
                    into.Add(new EmittedEffect.SpendingMultiplierEnded(rule.Id, episode.Id));
                    break;
                case EffectSpec.SatisfactionBinary or EffectSpec.SatisfactionScaling:
                    into.Add(new EmittedEffect.SatisfactionModifierEnded(rule.Id, episode.Id));
                    break;
            }
        }
    }

    /// Continuous effects at their current strength (REQ-045): Binary is flat, CountScaling grows with
    /// the unordered distinct-carrier pair count and saturates at the authored cap.
    private static void EmitBegan(
        RuleDefinition rule, Episode episode, QualifyingPairs qualifying, List<EmittedEffect> into)
    {
        var pairs = qualifying.Count;
        foreach (var effect in rule.Effects)
        {
            switch (effect)
            {
                case EffectSpec.SpendingBinary spending:
                    into.Add(new EmittedEffect.SpendingMultiplierBegan(
                        rule.Id, episode.Id, qualifying.Targets, spending.Factor));
                    break;

                case EffectSpec.SpendingScaling spending:
                    into.Add(new EmittedEffect.SpendingMultiplierBegan(
                        rule.Id, episode.Id, qualifying.Targets,
                        Math.Min(spending.MaxFactor, Math.Pow(spending.FactorPerPair, pairs))));
                    break;

                case EffectSpec.SatisfactionBinary satisfaction:
                    into.Add(new EmittedEffect.SatisfactionModifierBegan(
                        rule.Id, episode.Id, qualifying.Targets, satisfaction.RatePerTick));
                    break;

                case EffectSpec.SatisfactionScaling satisfaction:
                    into.Add(new EmittedEffect.SatisfactionModifierBegan(
                        rule.Id, episode.Id, qualifying.Targets,
                        Math.Min(satisfaction.MaxRate, satisfaction.RatePerTickPerPair * pairs)));
                    break;
            }
        }
    }

    /// Once per activation span (REQ-110): rolled when the rule first qualifies, never again while any
    /// pair persists. A failed roll consumes the span's roll — the episode simply produces no event.
    private void RollBehavior(
        RuleDefinition rule, Episode episode, QualifyingPairs qualifying, List<EmittedEffect> into)
    {
        if (episode.BehaviorRolled) return;
        if (rule.Effects.FirstOrDefault(e => e is EffectSpec.Behavior) is not EffectSpec.Behavior behavior) return;

        episode.BehaviorRolled = true;
        if (_random.NextDouble() < behavior.Chance)
            into.Add(new EmittedEffect.BehaviorEventTriggered(
                rule.Id, episode.Id, behavior.Outcome, qualifying.Targets, qualifying.Room));
    }

    private bool KnownRule(RuleId rule) => _book.Rules.Any(r => r.Id == rule);

    private IReadOnlyList<RuleId> DiscoveredInCatalogOrder() =>
        _book.Rules.Where(rule => _codex.Knows(rule.Id)).Select(rule => rule.Id).ToArray();
}
