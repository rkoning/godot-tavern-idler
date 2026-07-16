namespace TavernIdler.Domains.Guests;
using TavernIdler.Kernel;

// ── DOM-003: the guest-sheet catalog model (CON-005 §"Guest sheet JSON schema") ─────────────
// Plain, immutable data holders that mirror the CON-005 content schema. Validation is NOT a
// construction invariant: the REQ-092 patience band and the structural / crowding / wallet-range /
// vip-kind rules are enforced only on the content-load path (GuestCatalogJson.Parse, which needs the
// service-phase length to check the band). Building a catalog programmatically (e.g. the CON-005
// behavioral conformance scenarios, whose patience values are deliberately out of band) is therefore
// unvalidated by design — those scenarios never travel through the JSON loader.

/// The parsed guest catalog: every guest type the game can spawn (REQ-052).
public sealed record GuestCatalog(IReadOnlyList<GuestTypeSheet> Types)
{
    /// Parse + validate a <c>content/guests.json</c> document (CON-005 schema) against the given
    /// service-phase length (needed for the REQ-092 patience band). The content adapter (TKT-020)
    /// reads the file and delegates here rather than reimplementing the schema rules.
    public static GuestCatalog FromJson(string json, int serviceDurationTicks) =>
        GuestCatalogJson.Parse(json, serviceDurationTicks);
}

/// One guest type's authored sheet (CON-005). Patience values are raw ticks; wallet is a
/// [<see cref="WalletMin"/>, <see cref="WalletMax"/>] inclusive band drawn uniformly at arrival (G8).
public sealed record GuestTypeSheet(
    GuestTypeId Id, string DisplayName, bool IsVip, string SpriteId, int BaseWeight,
    IReadOnlyList<GuestAttractor> Attractors, CrowdingSpec Crowding,
    int QueuePatienceTicks, int BlockedWaitTicks,
    IReadOnlyList<GuestAgendaItem> Agenda, Money WalletMin, Money WalletMax,
    IReadOnlyList<TraitId> Traits, VipSpec? Vip);

/// An attraction bonus: <c>Kind</c> ∈ { menuItem, roomType }; adds <c>Weight</c> to the type's
/// effective arrival weight when the referenced item is stocked / room type is active (REQ-024/102).
public sealed record GuestAttractor(string Kind, string Id, int Weight);

/// Crowd tolerance: <c>Preference</c> ∈ { loves, neutral, hates }; <c>Magnitude</c> ∈ [0,1] (REQ-009).
public sealed record CrowdingSpec(string Preference, double Magnitude);

/// One agenda want (REQ-048): pursue service <c>ServiceId</c>, buying <c>MenuItem</c> if set.
public sealed record GuestAgendaItem(string ServiceId, MenuItemId? MenuItem);

/// VIP visit rule (REQ-050): per-night visit chance gated on a closed set of conditions.
public sealed record VipSpec(double VisitChancePerNight, IReadOnlyList<VipCondition> Conditions);

/// A VIP condition. <c>Kind</c> ∈ { hasRoomType, menuHasItem, venueIs, lifetimeAcclaimAtLeast };
/// <c>Id</c>/<c>Value</c> are populated per kind.
public sealed record VipCondition(string Kind, string? Id, long? Value);

/// Thrown when a <c>content/guests.json</c> document violates the CON-005 schema. The message names
/// the offending field/value (mirror of <c>TraitsCatalogException</c>).
public sealed class GuestCatalogException : Exception
{
    public GuestCatalogException(string message) : base(message) { }
    public GuestCatalogException(string message, Exception inner) : base(message, inner) { }
}
