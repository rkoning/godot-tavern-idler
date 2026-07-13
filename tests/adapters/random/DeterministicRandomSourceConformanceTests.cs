using TavernIdler.Adapters.Random;
using TavernIdler.Kernel;
using TavernIdler.Tests.Contracts.Random;

namespace TavernIdler.Tests.Adapters.Random;

/// <summary>
/// CON-015 conformance fixture (TKT-017): runs the frozen abstract suite in
/// <c>tests/contracts/random/</c> against the real <see cref="DeterministicRandomSource"/>.
/// </summary>
public sealed class DeterministicRandomSourceConformanceTests : RandomSourceConformanceTests
{
    protected override IRandomSource CreateSut(long seed, int nightNumber)
        => new DeterministicRandomSource(seed, nightNumber);
}
