using System;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Kernel;

// CON-001 conformance: Money arithmetic, comparison, and MultiplyRounded rounding table.
public class MoneyConformanceTests
{
    [Fact]
    public void Zero_is_amount_zero()
    {
        Assert.Equal(new Money(0), Money.Zero);
    }

    [Fact]
    public void Add_and_subtract()
    {
        Assert.Equal(new Money(30), new Money(10) + new Money(20));
        Assert.Equal(new Money(-10), new Money(10) - new Money(20));
    }

    [Fact]
    public void Add_overflow_throws()
    {
        Assert.Throws<OverflowException>(() => new Money(long.MaxValue) + new Money(1));
    }

    [Fact]
    public void Subtract_overflow_throws()
    {
        Assert.Throws<OverflowException>(() => new Money(long.MinValue) - new Money(1));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(-5, -3)]
    [InlineData(0, 0)]
    public void Comparison_operators(long a, long b)
    {
        var ma = new Money(a);
        var mb = new Money(b);
        Assert.Equal(a > b, ma > mb);
        Assert.Equal(a < b, ma < mb);
        Assert.Equal(a >= b, ma >= mb);
        Assert.Equal(a <= b, ma <= mb);
        Assert.Equal(a.CompareTo(b), ma.CompareTo(mb));
    }

    [Fact]
    public void MultiplyRounded_identity()
    {
        Assert.Equal(new Money(7), new Money(7).MultiplyRounded(1.0));
        Assert.Equal(new Money(-7), new Money(-7).MultiplyRounded(1.0));
    }

    // Half-away-from-zero: odd amounts × 0.5 land on a .5 boundary and round outward.
    [Theory]
    [InlineData(7, 0.5, 4)]     // 3.5 -> 4
    [InlineData(-7, 0.5, -4)]   // -3.5 -> -4
    [InlineData(5, 0.5, 3)]     // 2.5 -> 3
    [InlineData(-5, 0.5, -3)]   // -2.5 -> -3
    [InlineData(10, 0.5, 5)]    // exact
    [InlineData(7, -0.5, -4)]   // negative factor: -3.5 -> -4
    [InlineData(-7, -0.5, 4)]   // negative factor: 3.5 -> 4
    [InlineData(100, 1.5, 150)]
    public void MultiplyRounded_rounding_table(long amount, double factor, long expected)
    {
        Assert.Equal(new Money(expected), new Money(amount).MultiplyRounded(factor));
    }

    [Fact]
    public void MultiplyRounded_overflow_throws()
    {
        Assert.Throws<OverflowException>(() => new Money(long.MaxValue).MultiplyRounded(2.0));
    }
}
