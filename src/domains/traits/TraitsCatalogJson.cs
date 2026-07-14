namespace TavernIdler.Domains.Traits;
using System.Text.Json;
using TavernIdler.Kernel;

// ── CON-011: content/traits.json schema (parse + fail-fast validation) ──────
// Pure text→model translation over the BCL JSON reader; no file or engine I/O (that is the
// content adapter's job, TKT-020, which reads the file and hands the text here). Every rejection
// names the offending field/value. Structural rules (required/unknown fields, enum spellings,
// param sets) are enforced here; cross-entity rules (unique ids, trait existence, one effect per
// class, chance range, binary↔scaling symmetry) are enforced by RuleBook's constructor, so a
// programmatically-built catalog is held to the same schema.

internal static class TraitsCatalogJson
{
    private const string ClassSpending = "SpendingMultiplier";
    private const string ClassSatisfaction = "SatisfactionModifier";
    private const string ClassBehavior = "BehaviorEvent";

    public static RuleBook Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new TraitsCatalogException($"traits.json is not valid JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var root = Object(doc.RootElement, "traits.json");
            RejectUnknownFields(root, "traits.json", "traits", "rules");

            var traits = Array(root, "traits", "traits.json").Select(ParseTrait).ToArray();
            var rules = Array(root, "rules", "traits.json").Select(ParseRule).ToArray();
            return new RuleBook(traits, rules);
        }
    }

    private static TraitDefinition ParseTrait(JsonElement element, int index)
    {
        var where = $"traits[{index}]";
        var obj = Object(element, where);
        RejectUnknownFields(obj, where, "id", "displayName", "description");

        return new TraitDefinition(
            new TraitId(String(obj, "id", where)),
            String(obj, "displayName", where),
            String(obj, "description", where));
    }

    private static RuleDefinition ParseRule(JsonElement element, int index)
    {
        var obj = Object(element, $"rules[{index}]");
        var id = String(obj, "id", $"rules[{index}]");
        var where = $"rules[{id}]";
        RejectUnknownFields(obj, where, "id", "traitA", "traitB", "description", "reach", "stacking", "effects");

        var reach = Enum<RuleReach>(obj, "reach", where);
        var stacking = Enum<StackingMode>(obj, "stacking", where);
        var effects = Array(obj, "effects", where)
            .Select((e, i) => ParseEffect(e, $"{where}.effects[{i}]", stacking))
            .ToArray();

        return new RuleDefinition(
            new RuleId(id),
            new TraitId(String(obj, "traitA", where)),
            new TraitId(String(obj, "traitB", where)),
            String(obj, "description", where),
            reach,
            stacking,
            effects);
    }

    /// The authored param set identifies the spec: a rule whose params contradict its stacking mode
    /// still parses (as the spec its params name) and is rejected by RuleBook's symmetry check, so the
    /// diagnostic points at the mismatch rather than at a "missing" field of the other mode.
    private static EffectSpec ParseEffect(JsonElement element, string where, StackingMode stacking)
    {
        var obj = Object(element, where);
        var kind = String(obj, "class", where);

        switch (kind)
        {
            case ClassSpending when Scaling(obj, stacking, "factorPerPair"):
                RejectUnknownFields(obj, where, "class", "factorPerPair", "maxFactor");
                return new EffectSpec.SpendingScaling(Number(obj, "factorPerPair", where), Number(obj, "maxFactor", where));

            case ClassSpending:
                RejectUnknownFields(obj, where, "class", "factor");
                return new EffectSpec.SpendingBinary(Number(obj, "factor", where));

            case ClassSatisfaction when Scaling(obj, stacking, "ratePerTickPerPair"):
                RejectUnknownFields(obj, where, "class", "ratePerTickPerPair", "maxRate");
                return new EffectSpec.SatisfactionScaling(Number(obj, "ratePerTickPerPair", where), Number(obj, "maxRate", where));

            case ClassSatisfaction:
                RejectUnknownFields(obj, where, "class", "ratePerTick");
                return new EffectSpec.SatisfactionBinary(Number(obj, "ratePerTick", where));

            case ClassBehavior:
                RejectUnknownFields(obj, where, "class", "chance", "outcome");
                return new EffectSpec.Behavior(Number(obj, "chance", where), ParseOutcome(obj, where));

            default:
                throw new TraitsCatalogException(
                    $"{where}.class: unknown effect class '{kind}' " +
                    $"(expected {ClassSatisfaction}, {ClassBehavior} or {ClassSpending})");
        }
    }

    /// True when the effect should be read as the CountScaling shape: it authors a per-pair param, or
    /// its rule is CountScaling and it authors no binary param (so the missing per-pair param is what
    /// gets reported).
    private static bool Scaling(JsonElement effect, StackingMode stacking, string perPairField) =>
        effect.TryGetProperty(perPairField, out _) || stacking == StackingMode.CountScaling;

    private static BehaviorOutcome ParseOutcome(JsonElement effect, string effectWhere)
    {
        var where = $"{effectWhere}.outcome";
        var obj = Object(Property(effect, "outcome", effectWhere), where);
        var kind = String(obj, "kind", where);

        switch (kind)
        {
            case nameof(BehaviorOutcome.GuestsLeave):
                RejectUnknownFields(obj, where, "kind", "flavorId");
                return new BehaviorOutcome.GuestsLeave(String(obj, "flavorId", where));

            case nameof(BehaviorOutcome.SpendingBurst):
                RejectUnknownFields(obj, where, "kind", "flavorId", "factor", "durationTicks");
                return new BehaviorOutcome.SpendingBurst(
                    String(obj, "flavorId", where),
                    Number(obj, "factor", where),
                    Int(obj, "durationTicks", where));

            case nameof(BehaviorOutcome.SatisfactionShock):
                RejectUnknownFields(obj, where, "kind", "flavorId", "delta");
                return new BehaviorOutcome.SatisfactionShock(String(obj, "flavorId", where), Number(obj, "delta", where));

            default:
                throw new TraitsCatalogException(
                    $"{where}.kind: unknown behavior outcome '{kind}' (expected " +
                    $"{nameof(BehaviorOutcome.GuestsLeave)}, {nameof(BehaviorOutcome.SpendingBurst)} or " +
                    $"{nameof(BehaviorOutcome.SatisfactionShock)})");
        }
    }

    // ── JSON access helpers: every failure names the field ───────
    private static void RejectUnknownFields(JsonElement obj, string where, params string[] allowed)
    {
        foreach (var property in obj.EnumerateObject())
            if (!allowed.Contains(property.Name))
                throw new TraitsCatalogException(
                    $"{where}.{property.Name}: unknown field (allowed: {string.Join(", ", allowed)})");
    }

    private static JsonElement Object(JsonElement element, string where) =>
        element.ValueKind == JsonValueKind.Object
            ? element
            : throw new TraitsCatalogException($"{where}: expected an object, got {element.ValueKind}");

    private static JsonElement Property(JsonElement obj, string field, string where) =>
        obj.TryGetProperty(field, out var value)
            ? value
            : throw new TraitsCatalogException($"{where}.{field}: missing required field");

    private static IEnumerable<JsonElement> Array(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.Array)
            throw new TraitsCatalogException($"{where}.{field}: expected an array, got {value.ValueKind}");
        return value.EnumerateArray();
    }

    private static string String(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
            throw new TraitsCatalogException($"{where}.{field}: expected a non-empty string, got {value}");
        return value.GetString()!;
    }

    private static double Number(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
            throw new TraitsCatalogException($"{where}.{field}: expected a number, got {value}");
        return number;
    }

    private static int Int(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new TraitsCatalogException($"{where}.{field}: expected an integer, got {value}");
        return number;
    }

    private static TEnum Enum<TEnum>(JsonElement obj, string field, string where) where TEnum : struct
    {
        var text = String(obj, field, where);
        if (!System.Enum.TryParse<TEnum>(text, ignoreCase: false, out var parsed) ||
            !System.Enum.IsDefined(typeof(TEnum), parsed))
            throw new TraitsCatalogException(
                $"{where}.{field}: unknown {typeof(TEnum).Name} '{text}' " +
                $"(expected {string.Join(" or ", System.Enum.GetNames(typeof(TEnum)))})");
        return parsed;
    }
}
