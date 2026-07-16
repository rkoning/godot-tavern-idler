namespace TavernIdler.Domains.Guests;
using System.Text.Json;
using TavernIdler.Kernel;

// ── CON-005: content/guests.json schema (parse + fail-fast validation) ──────
// Pure text→model translation over the BCL JSON reader; no file or engine I/O (the content
// adapter, TKT-020, reads the file and hands the text here). Every rejection names the offending
// field/value. Structural rules, enum spellings, the REQ-092 patience band (needs the service-phase
// length), crowding-magnitude bounds, wallet ordering and duplicate/empty ids are all enforced here.

internal static class GuestCatalogJson
{
    private static readonly string[] CrowdingPreferences = { "loves", "neutral", "hates" };
    private static readonly string[] VipConditionKinds =
        { "hasRoomType", "menuHasItem", "venueIs", "lifetimeAcclaimAtLeast" };

    public static GuestCatalog Parse(string json, int serviceDurationTicks)
    {
        if (serviceDurationTicks <= 0)
            throw new GuestCatalogException(
                $"serviceDurationTicks: must be > 0 to check the REQ-092 patience band; got {serviceDurationTicks}");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new GuestCatalogException($"guests.json is not valid JSON: {ex.Message}", ex);
        }

        using (doc)
        {
            var root = Object(doc.RootElement, "guests.json");
            RejectUnknownFields(root, "guests.json", "guestTypes");

            var types = Array(root, "guestTypes", "guests.json")
                .Select((e, i) => ParseType(e, i, serviceDurationTicks))
                .ToArray();

            var seen = new HashSet<GuestTypeId>();
            foreach (var t in types)
                if (!seen.Add(t.Id))
                    throw new GuestCatalogException($"guestTypes[].id: duplicate guest type id '{t.Id.Value}'");

            return new GuestCatalog(types);
        }
    }

    private static GuestTypeSheet ParseType(JsonElement element, int index, int serviceDurationTicks)
    {
        var where = $"guestTypes[{index}]";
        var obj = Object(element, where);
        RejectUnknownFields(obj, where,
            "id", "displayName", "isVip", "spriteId", "baseWeight", "attractors", "crowding",
            "queuePatienceTicks", "blockedWaitTicks", "agenda", "walletMin", "walletMax", "traits", "vip");

        var id = String(obj, "id", where);
        where = $"guestTypes[{id}]";   // name subsequent diagnostics by id once we have one

        var patience = Int(obj, "queuePatienceTicks", where);
        var blockedWait = Int(obj, "blockedWaitTicks", where);
        RejectOutsidePatienceBand(where, "queuePatienceTicks", patience, serviceDurationTicks);
        RejectOutsidePatienceBand(where, "blockedWaitTicks", blockedWait, serviceDurationTicks);

        var walletMin = Long(obj, "walletMin", where);
        var walletMax = Long(obj, "walletMax", where);
        if (walletMin > walletMax)
            throw new GuestCatalogException(
                $"{where}: walletMin ({walletMin}) must be ≤ walletMax ({walletMax})");

        return new GuestTypeSheet(
            new GuestTypeId(id),
            String(obj, "displayName", where),
            Bool(obj, "isVip", where),
            String(obj, "spriteId", where),
            Int(obj, "baseWeight", where),
            Array(obj, "attractors", where).Select((e, i) => ParseAttractor(e, $"{where}.attractors[{i}]")).ToArray(),
            ParseCrowding(Property(obj, "crowding", where), $"{where}.crowding"),
            patience, blockedWait,
            Array(obj, "agenda", where).Select((e, i) => ParseAgenda(e, $"{where}.agenda[{i}]")).ToArray(),
            new Money(walletMin), new Money(walletMax),
            Array(obj, "traits", where).Select((e, i) => new TraitId(RequireString(e, $"{where}.traits[{i}]"))).ToArray(),
            ParseVip(Property(obj, "vip", where), $"{where}.vip"));
    }

    private static GuestAttractor ParseAttractor(JsonElement element, string where)
    {
        var obj = Object(element, where);
        RejectUnknownFields(obj, where, "kind", "id", "weight");
        var kind = String(obj, "kind", where);
        if (kind is not ("menuItem" or "roomType"))
            throw new GuestCatalogException($"{where}.kind: unknown attractor kind '{kind}' (expected menuItem or roomType)");
        return new GuestAttractor(kind, String(obj, "id", where), Int(obj, "weight", where));
    }

    private static CrowdingSpec ParseCrowding(JsonElement element, string where)
    {
        var obj = Object(element, where);
        RejectUnknownFields(obj, where, "preference", "magnitude");
        var preference = String(obj, "preference", where);
        if (!CrowdingPreferences.Contains(preference))
            throw new GuestCatalogException(
                $"{where}.preference: unknown crowding preference '{preference}' (expected {string.Join(", ", CrowdingPreferences)})");
        var magnitude = Number(obj, "magnitude", where);
        if (magnitude < 0.0 || magnitude > 1.0)
            throw new GuestCatalogException($"{where}.magnitude: must be in [0,1]; got {magnitude}");
        return new CrowdingSpec(preference, magnitude);
    }

    private static GuestAgendaItem ParseAgenda(JsonElement element, string where)
    {
        var obj = Object(element, where);
        RejectUnknownFields(obj, where, "serviceId", "menuItem");
        var menuItem = NullableString(obj, "menuItem", where);
        return new GuestAgendaItem(String(obj, "serviceId", where),
            menuItem is null ? null : new MenuItemId(menuItem));
    }

    private static VipSpec? ParseVip(JsonElement element, string where)
    {
        if (element.ValueKind == JsonValueKind.Null) return null;
        var obj = Object(element, where);
        RejectUnknownFields(obj, where, "visitChancePerNight", "conditions");
        var chance = Number(obj, "visitChancePerNight", where);
        if (chance < 0.0 || chance > 1.0)
            throw new GuestCatalogException($"{where}.visitChancePerNight: must be in [0,1]; got {chance}");
        var conditions = Array(obj, "conditions", where)
            .Select((e, i) => ParseVipCondition(e, $"{where}.conditions[{i}]"))
            .ToArray();
        return new VipSpec(chance, conditions);
    }

    private static VipCondition ParseVipCondition(JsonElement element, string where)
    {
        var obj = Object(element, where);
        RejectUnknownFields(obj, where, "kind", "id", "value");
        var kind = String(obj, "kind", where);
        if (!VipConditionKinds.Contains(kind))
            throw new GuestCatalogException(
                $"{where}.kind: unknown VIP condition kind '{kind}' (expected {string.Join(", ", VipConditionKinds)})");
        return new VipCondition(kind, NullableString(obj, "id", where), NullableLong(obj, "value", where));
    }

    private static void RejectOutsidePatienceBand(string where, string field, int value, int serviceDurationTicks)
    {
        // REQ-092: patience values must lie within 10–30% of the service-phase length (inclusive).
        var lo = 0.1 * serviceDurationTicks;
        var hi = 0.3 * serviceDurationTicks;
        if (value < lo || value > hi)
            throw new GuestCatalogException(
                $"{where}.{field}: {value} is outside the REQ-092 band [{lo:0.###}, {hi:0.###}] " +
                $"(10–30% of ServiceDurationTicks={serviceDurationTicks})");
    }

    // ── JSON access helpers: every failure names the field ───────
    private static void RejectUnknownFields(JsonElement obj, string where, params string[] allowed)
    {
        foreach (var property in obj.EnumerateObject())
            if (!allowed.Contains(property.Name))
                throw new GuestCatalogException(
                    $"{where}.{property.Name}: unknown field (allowed: {string.Join(", ", allowed)})");
    }

    private static JsonElement Object(JsonElement element, string where) =>
        element.ValueKind == JsonValueKind.Object
            ? element
            : throw new GuestCatalogException($"{where}: expected an object, got {element.ValueKind}");

    private static JsonElement Property(JsonElement obj, string field, string where) =>
        obj.TryGetProperty(field, out var value)
            ? value
            : throw new GuestCatalogException($"{where}.{field}: missing required field");

    private static IEnumerable<JsonElement> Array(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.Array)
            throw new GuestCatalogException($"{where}.{field}: expected an array, got {value.ValueKind}");
        return value.EnumerateArray();
    }

    private static string String(JsonElement obj, string field, string where) =>
        RequireString(Property(obj, field, where), $"{where}.{field}");

    private static string RequireString(JsonElement value, string where)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
            throw new GuestCatalogException($"{where}: expected a non-empty string, got {value}");
        return value.GetString()!;
    }

    /// A field that may be absent or an explicit JSON null (→ C# null), else a non-empty string.
    private static string? NullableString(JsonElement obj, string field, string where)
    {
        if (!obj.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return RequireString(value, $"{where}.{field}");
    }

    private static double Number(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
            throw new GuestCatalogException($"{where}.{field}: expected a number, got {value}");
        return number;
    }

    private static int Int(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
            throw new GuestCatalogException($"{where}.{field}: expected an integer, got {value}");
        return number;
    }

    private static long Long(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
            throw new GuestCatalogException($"{where}.{field}: expected an integer, got {value}");
        return number;
    }

    private static long? NullableLong(JsonElement obj, string field, string where)
    {
        if (!obj.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
            throw new GuestCatalogException($"{where}.{field}: expected an integer or null, got {value}");
        return number;
    }

    private static bool Bool(JsonElement obj, string field, string where)
    {
        var value = Property(obj, field, where);
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new GuestCatalogException($"{where}.{field}: expected a boolean, got {value}");
        return value.GetBoolean();
    }
}
