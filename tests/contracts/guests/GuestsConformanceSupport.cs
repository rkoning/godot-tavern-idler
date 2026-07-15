using System.Collections.Generic;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Guests;

// ── Test-support guest catalog (NOT port types) ──────────────────────────
// Mirrors the CON-005 guest-sheet JSON schema so the abstract suites can express content without
// depending on the content adapter's loader (TKT-020). The domain-impl ticket (TKT-014) maps its
// real catalog onto these for the conformance fixtures.
public sealed record GuestTypeSheet(
    GuestTypeId Id, string DisplayName, bool IsVip, string SpriteId, int BaseWeight,
    IReadOnlyList<GuestAttractor> Attractors, CrowdingSpec Crowding,
    int QueuePatienceTicks, int BlockedWaitTicks,
    IReadOnlyList<GuestAgendaItem> Agenda, Money WalletMin, Money WalletMax,
    IReadOnlyList<TraitId> Traits, VipSpec? Vip);

public sealed record GuestAttractor(string Kind, string Id, int Weight);   // kind: menuItem | roomType
public sealed record CrowdingSpec(string Preference, double Magnitude);     // preference: loves | neutral | hates
public sealed record GuestAgendaItem(string ServiceId, MenuItemId? MenuItem);
public sealed record VipSpec(double VisitChancePerNight, IReadOnlyList<VipCondition> Conditions);
public sealed record VipCondition(string Kind, string? Id, long? Value);    // kind: hasRoomType|menuHasItem|venueIs|lifetimeAcclaimAtLeast
public sealed record GuestCatalog(IReadOnlyList<GuestTypeSheet> Types);
