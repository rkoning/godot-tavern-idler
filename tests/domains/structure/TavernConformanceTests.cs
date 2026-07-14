using TavernIdler.Domains.Cycle;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;
using TavernIdler.Tests.Contracts.Structure;
using TavernIdler.Tests.Contracts.Structure.Driven;

namespace TavernIdler.Tests.Domains.Structure;

/// <summary>
/// Runs the CON-003 abstract conformance suite against the real <see cref="Tavern"/> aggregate,
/// wired to the in-memory CON-004 driven ports.
/// </summary>
public sealed class TavernConformanceTests : StructureApiConformanceTests
{
    protected override IStructureTestHarness CreateHarness(StructureScenario scenario) =>
        new TavernHarness(scenario);
}

/// <summary>
/// The CON-004 <see cref="IBuildLedger"/> conformance suite run against the reference stub the
/// Structure tests use (the contract asks for "the real economy bridge + a reference stub"; the
/// bridge subclass arrives with TKT-019).
/// </summary>
public sealed class InMemoryBuildLedgerConformanceTests : BuildLedgerConformanceTests
{
    protected override IBuildLedger CreateLedger(Money startingGold, double buildMultiplier) =>
        new InMemoryBuildLedger(startingGold, buildMultiplier);

    protected override Money BalanceOf(IBuildLedger ledger) => ((InMemoryBuildLedger)ledger).Balance;
}

/// <summary>Real <see cref="Tavern"/> + in-memory driven ports, driven by a <see cref="StructureScenario"/>.</summary>
public sealed class TavernHarness : IStructureTestHarness
{
    /// Circulation is priced per cell (REQ-099); the scenario does not specify the tuning values,
    /// so the harness picks fixed ones (content supplies the real numbers in TKT-020).
    public static readonly CirculationCosts Costs = new(Corridor: new Money(25), Stair: new Money(50));

    private readonly FakeCycleQueries _cycle = new();
    private readonly InMemoryBuildLedger _ledger;
    private readonly Tavern _tavern;

    public TavernHarness(StructureScenario scenario)
    {
        _ledger = new InMemoryBuildLedger(scenario.StartingGold, scenario.BuildCostMultiplier);
        _tavern = new Tavern(
            _cycle,
            new FixedLot(scenario.Lot, scenario.Entrance, scenario.Terrain),
            new FakeRoomContent(scenario.FullCatalog, scenario.Available),
            _ledger,
            scenario.FullCatalog,
            Costs);
    }

    public IStructureCommands Commands => _tavern;
    public IStructureQueries Queries => _tavern;
    public IStructureSnapshot Snapshot => _tavern;
    public Money LedgerBalance => _ledger.Balance;

    public void SetPrep(bool isPrep) => _cycle.Phase = isPrep ? Phase.Prep : Phase.Service;
}
