namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

// ── DOM-006: the immutable rule catalog (CON-011 v1.1) ──────────────────────
// Trait registry + trait×trait rules with their reach, stacking mode and effect specs.
// Authoring errors are rejected fail-fast at construction with a diagnostic naming the
// offending field/value (CON-011 "Schema validation").

/// A trait as authored in the registry (REQ-095): always player-visible on its carrier.
public sealed record TraitDefinition(TraitId Id, string DisplayName, string Description);

/// What a rule emits while its episode is open. The param set is fixed by the rule's
/// <see cref="StackingMode"/> (CON-011 v1.1): flat for <c>Binary</c>, per-pair + cap for
/// <c>CountScaling</c>. Behavior events author a chance + outcome under either mode.
public abstract record EffectSpec
{
    public sealed record SpendingBinary(double Factor) : EffectSpec;
    public sealed record SpendingScaling(double FactorPerPair, double MaxFactor) : EffectSpec;
    public sealed record SatisfactionBinary(double RatePerTick) : EffectSpec;
    public sealed record SatisfactionScaling(double RatePerTickPerPair, double MaxRate) : EffectSpec;
    public sealed record Behavior(double Chance, BehaviorOutcome Outcome) : EffectSpec;

    private EffectSpec() { }

    public EffectClassKind Class => this switch
    {
        SpendingBinary or SpendingScaling => EffectClassKind.SpendingMultiplier,
        SatisfactionBinary or SatisfactionScaling => EffectClassKind.SatisfactionModifier,
        _ => EffectClassKind.BehaviorEvent,
    };
}

public sealed record RuleDefinition(
    RuleId Id,
    TraitId TraitA,
    TraitId TraitB,
    string Description,
    RuleReach Reach,
    StackingMode Stacking,
    IReadOnlyList<EffectSpec> Effects);

/// Thrown when a catalog violates the CON-011 schema. The message names the offending field/value.
public sealed class TraitsCatalogException : Exception
{
    public TraitsCatalogException(string message) : base(message) { }
    public TraitsCatalogException(string message, Exception inner) : base(message, inner) { }
}

public sealed class RuleBook
{
    public RuleBook(IReadOnlyList<TraitDefinition> traits, IReadOnlyList<RuleDefinition> rules)
    {
        Validate(traits, rules);
        Traits = traits;
        Rules = rules;
    }

    public IReadOnlyList<TraitDefinition> Traits { get; }
    public IReadOnlyList<RuleDefinition> Rules { get; }

    /// Parse + validate a <c>content/traits.json</c> document (CON-011 schema).
    public static RuleBook FromJson(string json) => TraitsCatalogJson.Parse(json);

    private static void Validate(IReadOnlyList<TraitDefinition> traits, IReadOnlyList<RuleDefinition> rules)
    {
        var known = new HashSet<TraitId>();
        foreach (var trait in traits)
        {
            if (string.IsNullOrEmpty(trait.Id.Value))
                throw new TraitsCatalogException("traits[].id: trait id must be non-empty");
            if (!known.Add(trait.Id))
                throw new TraitsCatalogException($"traits[].id: duplicate trait id '{trait.Id.Value}'");
        }

        var ruleIds = new HashSet<RuleId>();
        foreach (var rule in rules)
        {
            var where = $"rules[{rule.Id.Value}]";
            if (string.IsNullOrEmpty(rule.Id.Value))
                throw new TraitsCatalogException("rules[].id: rule id must be non-empty");
            if (!ruleIds.Add(rule.Id))
                throw new TraitsCatalogException($"rules[].id: duplicate rule id '{rule.Id.Value}'");

            foreach (var (endpoint, trait) in new[] { ("traitA", rule.TraitA), ("traitB", rule.TraitB) })
                if (!known.Contains(trait))
                    throw new TraitsCatalogException($"{where}.{endpoint}: unknown trait id '{trait.Value}'");

            var classes = new HashSet<EffectClassKind>();
            foreach (var effect in rule.Effects)
            {
                if (!classes.Add(effect.Class))
                    throw new TraitsCatalogException(
                        $"{where}.effects: more than one effect of class '{effect.Class}'");

                ValidateEffect(where, rule.Stacking, effect);
            }
        }
    }

    private static void ValidateEffect(string where, StackingMode stacking, EffectSpec effect)
    {
        switch (effect)
        {
            case EffectSpec.Behavior behavior when behavior.Chance <= 0.0 || behavior.Chance > 1.0:
                throw new TraitsCatalogException(
                    $"{where}.effects[BehaviorEvent].chance: must be in (0,1]; got {behavior.Chance}");

            // Binary/CountScaling param symmetry (CON-011 v1.1): the param set must match the mode.
            case EffectSpec.SpendingBinary when stacking != StackingMode.Binary:
                throw new TraitsCatalogException(
                    $"{where}.effects[SpendingMultiplier]: binary param 'factor' requires stacking Binary; " +
                    "a CountScaling rule must author 'factorPerPair' + 'maxFactor'");
            case EffectSpec.SatisfactionBinary when stacking != StackingMode.Binary:
                throw new TraitsCatalogException(
                    $"{where}.effects[SatisfactionModifier]: binary param 'ratePerTick' requires stacking Binary; " +
                    "a CountScaling rule must author 'ratePerTickPerPair' + 'maxRate'");
            case EffectSpec.SpendingScaling when stacking != StackingMode.CountScaling:
                throw new TraitsCatalogException(
                    $"{where}.effects[SpendingMultiplier]: scaling params 'factorPerPair'/'maxFactor' require " +
                    "stacking CountScaling; a Binary rule must author a flat 'factor'");
            case EffectSpec.SatisfactionScaling when stacking != StackingMode.CountScaling:
                throw new TraitsCatalogException(
                    $"{where}.effects[SatisfactionModifier]: scaling params 'ratePerTickPerPair'/'maxRate' require " +
                    "stacking CountScaling; a Binary rule must author a flat 'ratePerTick'");
        }
    }
}
