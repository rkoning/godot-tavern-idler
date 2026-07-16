using System.Linq;
using TavernIdler.Tests.Contracts.Guests;   // test-support catalog model
using Domain = TavernIdler.Domains.Guests;   // real domain catalog model

namespace TavernIdler.Tests.Domains.Guests;

/// <summary>
/// Projects between the CON-005 test-support catalog model (owned by the abstract suites, TKT-006)
/// and the real domain catalog model (owned by this ticket, TKT-014). The two are structurally
/// identical; the domain cannot reference the test project, so the fixtures translate at the seam:
/// <see cref="ToSupport"/> for the catalog-conformance assertions, <see cref="ToDomain"/> to feed
/// the real <c>GuestPopulation</c> a behavioral scenario's catalog.
/// </summary>
internal static class GuestCatalogMapping
{
    public static GuestCatalog ToSupport(Domain.GuestCatalog catalog) =>
        new(catalog.Types.Select(ToSupport).ToArray());

    private static GuestTypeSheet ToSupport(Domain.GuestTypeSheet t) =>
        new(t.Id, t.DisplayName, t.IsVip, t.SpriteId, t.BaseWeight,
            t.Attractors.Select(a => new GuestAttractor(a.Kind, a.Id, a.Weight)).ToArray(),
            new CrowdingSpec(t.Crowding.Preference, t.Crowding.Magnitude),
            t.QueuePatienceTicks, t.BlockedWaitTicks,
            t.Agenda.Select(g => new GuestAgendaItem(g.ServiceId, g.MenuItem)).ToArray(),
            t.WalletMin, t.WalletMax, t.Traits,
            t.Vip is null ? null
                : new VipSpec(t.Vip.VisitChancePerNight,
                    t.Vip.Conditions.Select(c => new VipCondition(c.Kind, c.Id, c.Value)).ToArray()));

    public static Domain.GuestCatalog ToDomain(GuestCatalog catalog) =>
        new(catalog.Types.Select(ToDomain).ToArray());

    private static Domain.GuestTypeSheet ToDomain(GuestTypeSheet t) =>
        new(t.Id, t.DisplayName, t.IsVip, t.SpriteId, t.BaseWeight,
            t.Attractors.Select(a => new Domain.GuestAttractor(a.Kind, a.Id, a.Weight)).ToArray(),
            new Domain.CrowdingSpec(t.Crowding.Preference, t.Crowding.Magnitude),
            t.QueuePatienceTicks, t.BlockedWaitTicks,
            t.Agenda.Select(g => new Domain.GuestAgendaItem(g.ServiceId, g.MenuItem)).ToArray(),
            t.WalletMin, t.WalletMax, t.Traits,
            t.Vip is null ? null
                : new Domain.VipSpec(t.Vip.VisitChancePerNight,
                    t.Vip.Conditions.Select(c => new Domain.VipCondition(c.Kind, c.Id, c.Value)).ToArray()));
}
