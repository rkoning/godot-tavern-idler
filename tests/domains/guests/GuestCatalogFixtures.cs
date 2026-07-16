using TavernIdler.Tests.Contracts.Guests;
using Domain = TavernIdler.Domains.Guests;

namespace TavernIdler.Tests.Domains.Guests;

/// <summary>
/// Runs the CON-005 guest-catalog schema conformance suite (TKT-006) against the real domain-side
/// loader/validator <see cref="Domain.GuestCatalog.FromJson"/>. The content adapter (TKT-020) reads
/// the file and delegates here rather than reimplementing the schema rules (G1). The suite's
/// <see cref="GuestCatalog"/> is the test-support model; <see cref="GuestCatalogMapping"/> projects
/// the domain model onto it so the loader stays the single source of the parse/validation rules.
/// </summary>
public sealed class GuestCatalogConformance : GuestCatalogConformanceTests
{
    protected override GuestCatalog LoadCatalog(string json, int serviceDurationTicks) =>
        GuestCatalogMapping.ToSupport(Domain.GuestCatalog.FromJson(json, serviceDurationTicks));
}
