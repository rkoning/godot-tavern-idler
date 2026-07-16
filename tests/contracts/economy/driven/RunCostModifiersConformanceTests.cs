using TavernIdler.Domains.Economy;

namespace TavernIdler.Tests.Contracts.Economy.Driven;

/// <summary>
/// CON-008 abstract conformance suite for <see cref="IRunCostModifiers"/>: the build/restock cost
/// multipliers are the current venue's values and stay constant across a run. The bridge ticket
/// (TKT-019, over CON-013 venue state) supplies the sealed subclass. Abstract ⇒ nothing runs until then.
///
/// CON-008's "constant across a run (stub prestige changes it)" temporal property is split: the
/// within-run invariant (repeated reads return the same values) is asserted here at the port level;
/// the "only a prestige/venue-change swaps them" property is a live bridge behaviour the implementer
/// asserts against its CON-013 backing (CON-013 is not yet frozen and venue swapping is bridge-specific)
/// — mirroring how the CON-010 RoomRequirements suite defers its temporal bullets to the implementer.
/// </summary>
public abstract class RunCostModifiersConformanceTests
{
    /// A modifiers view carrying the current venue's cost multipliers (both > 0, REQ-087/090).
    protected abstract IRunCostModifiers CreateModifiers(double buildMultiplier, double restockMultiplier);

    [Fact]
    public void Exposes_the_configured_multipliers()
    {
        var m = CreateModifiers(2.0, 1.5);
        Assert.Equal(2.0, m.BuildCostMultiplier, precision: 6);
        Assert.Equal(1.5, m.RestockCostMultiplier, precision: 6);
    }

    [Fact]
    public void Multipliers_are_stable_across_repeated_reads_within_a_run()
    {
        var m = CreateModifiers(2.0, 1.5);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(2.0, m.BuildCostMultiplier, precision: 6);
            Assert.Equal(1.5, m.RestockCostMultiplier, precision: 6);
        }
    }

    [Fact]
    public void A_new_run_can_carry_different_multipliers()
    {
        var run1 = CreateModifiers(1.0, 1.0);
        var run2 = CreateModifiers(3.0, 0.5);
        Assert.NotEqual(run1.BuildCostMultiplier, run2.BuildCostMultiplier);
        Assert.Equal(3.0, run2.BuildCostMultiplier, precision: 6);
        Assert.Equal(0.5, run2.RestockCostMultiplier, precision: 6);
    }
}
