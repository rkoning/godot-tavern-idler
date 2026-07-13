using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Kernel;

// CON-001 conformance: Tick arithmetic and comparison.
public class TickConformanceTests
{
    [Fact]
    public void Add_ticks_advances_value()
    {
        Assert.Equal(new Tick(105), new Tick(100) + 5);
    }

    [Fact]
    public void Add_zero_is_identity()
    {
        Assert.Equal(new Tick(42), new Tick(42) + 0);
    }

    [Theory]
    [InlineData(3, 5, -1)]
    [InlineData(5, 5, 0)]
    [InlineData(9, 5, 1)]
    public void CompareTo_orders_by_value(long a, long b, int sign)
    {
        Assert.Equal(sign, System.Math.Sign(new Tick(a).CompareTo(new Tick(b))));
    }

    [Fact]
    public void Equality_by_value()
    {
        Assert.Equal(new Tick(7), new Tick(7));
        Assert.NotEqual(new Tick(7), new Tick(8));
    }
}
