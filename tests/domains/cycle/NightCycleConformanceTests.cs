using TavernIdler.Domains.Cycle;
using TavernIdler.Tests.Contracts.Cycle;

namespace TavernIdler.Tests.Domains.Cycle;

/// <summary>
/// Runs the CON-002 abstract conformance suite against the real <see cref="NightCycle"/> aggregate.
/// </summary>
public sealed class NightCycleConformanceTests : CycleConformanceTests
{
    protected override CycleSut CreateSut(CycleConfig config)
    {
        var cycle = new NightCycle(config);
        return new CycleSut(cycle, cycle, cycle);
    }
}
