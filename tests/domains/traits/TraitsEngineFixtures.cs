using System.Collections.Generic;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;
using TavernIdler.Tests.Contracts.Traits;

namespace TavernIdler.Tests.Domains.Traits;

/// <summary>
/// Wires the real rule engine (TKT-013) to the seam the CON-011 abstract suites drive:
/// a settable stub <see cref="IPresenceSource"/> (CON-012, exercised for real by TKT-019) and the
/// scripted <see cref="IRandomSource"/> (CON-015) the suite uses to fix behavior rolls.
/// </summary>
public sealed class TraitsEngineHarness : ITraitsTestHarness, IPresenceSource
{
    private PresenceSnapshot _presence = new(new List<PresentCarrier>());
    private readonly TraitsEngine _engine;

    public TraitsEngineHarness(string catalogJson, IRandomSource random) =>
        _engine = new TraitsEngine(RuleBook.FromJson(catalogJson), this, random);

    public ITraitsCommands Commands => _engine;
    public ITraitsQueries Queries => _engine;

    public void SetPresence(PresenceSnapshot snapshot) => _presence = snapshot;
    public PresenceSnapshot Current() => _presence;
}

/// <summary>Runs the CON-011 API conformance suite against the real <see cref="TraitsEngine"/>.</summary>
public sealed class TraitsEngineApiConformanceTests : TraitsApiConformanceTests
{
    protected override ITraitsTestHarness CreateHarness(string catalogJson, IRandomSource random) =>
        new TraitsEngineHarness(catalogJson, random);
}

/// <summary>
/// Runs the CON-011 catalog-schema conformance suite against <see cref="RuleBook.FromJson"/>, the
/// domain-side parser/validator of the traits.json schema. The content adapter (TKT-020) reads the
/// file and delegates here rather than reimplementing the schema rules; it supplies its own fixture.
/// </summary>
public sealed class TraitsEngineCatalogConformanceTests : TraitsCatalogConformanceTests
{
    protected override ITraitsQueries LoadCatalog(string catalogJson) =>
        new TraitsEngineHarness(catalogJson, ScriptedRandomSource.ForTraits()).Queries;
}
