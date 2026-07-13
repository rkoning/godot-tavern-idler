using System;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Structure.Driven;

/// <summary>
/// CON-004 abstract conformance suite for <see cref="IBuildLedger"/>. Runs against any
/// implementer — the real economy bridge (over CON-007, TKT-019) or a reference stub —
/// supplied by a concrete subclass. Abstract ⇒ nothing runs until then.
/// </summary>
public abstract class BuildLedgerConformanceTests
{
    /// A ledger seeded with <paramref name="startingGold"/> that applies
    /// <paramref name="buildMultiplier"/> (REQ-087) to every base cost.
    protected abstract IBuildLedger CreateLedger(Money startingGold, double buildMultiplier);

    /// Gold currently held by <paramref name="ledger"/> (the port itself exposes no balance).
    protected abstract Money BalanceOf(IBuildLedger ledger);

    [Fact]
    public void TryCharge_applies_multiplier_and_debits_rounded_amount()
    {
        var ledger = CreateLedger(new Money(1000), buildMultiplier: 1.5);
        var result = ledger.TryCharge(new Money(101), BuildCostKind.Room);   // 101 × 1.5 = 151.5 → 152
        var charged = Assert.IsType<ChargeResult.Charged>(result);
        Assert.Equal(new Money(152), charged.AmountCharged);
        Assert.Equal(new Money(848), BalanceOf(ledger));
    }

    [Fact]
    public void TryCharge_uses_MultiplyRounded_half_away_from_zero()
    {
        var ledger = CreateLedger(new Money(1000), buildMultiplier: 0.5);
        var charged = Assert.IsType<ChargeResult.Charged>(ledger.TryCharge(new Money(7), BuildCostKind.Upgrade));
        Assert.Equal(new Money(4), charged.AmountCharged);                    // 3.5 → 4
    }

    [Fact]
    public void TryCharge_insufficient_funds_leaves_balance_unchanged()
    {
        var ledger = CreateLedger(new Money(100), buildMultiplier: 2.0);
        var result = ledger.TryCharge(new Money(100), BuildCostKind.Room);    // needs 200
        var insufficient = Assert.IsType<ChargeResult.InsufficientGold>(result);
        Assert.Equal(new Money(200), insufficient.Required);
        Assert.Equal(new Money(100), insufficient.Available);
        Assert.Equal(new Money(100), BalanceOf(ledger));                      // untouched
    }

    [Fact]
    public void Refund_credits_exactly()
    {
        var ledger = CreateLedger(new Money(100), buildMultiplier: 1.0);
        ledger.Refund(new Money(250));
        Assert.Equal(new Money(350), BalanceOf(ledger));
    }

    [Fact]
    public void Refund_of_negative_amount_throws()
    {
        var ledger = CreateLedger(new Money(100), buildMultiplier: 1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Refund(new Money(-1)));
    }
}
