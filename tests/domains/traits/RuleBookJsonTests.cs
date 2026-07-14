using System;
using System.Linq;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;
using static TavernIdler.Tests.Contracts.Traits.Catalog;

namespace TavernIdler.Tests.Domains.Traits;

/// <summary>
/// Parsing/validation of the CON-011 <c>content/traits.json</c> schema, beyond the rules the abstract
/// catalog conformance suite pins: the remaining behavior outcomes, missing required fields, and the
/// boundaries of the <c>chance</c> range.
/// </summary>
public class RuleBookJsonTests
{
    private static void AssertRejected(string json, string offendingToken)
    {
        var ex = Assert.ThrowsAny<Exception>(() => RuleBook.FromJson(json));
        Assert.Contains(offendingToken, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string BehaviorRule(string outcome) =>
        Doc(TraitDefs("alpha", "beta"),
            Rule("a-x-b", "alpha", "beta", "SameRoom", "Binary",
                 "{ \"class\": \"BehaviorEvent\", \"chance\": 0.5, \"outcome\": " + outcome + " }"));

    [Fact]
    public void Parses_a_spending_burst_outcome()
    {
        var book = RuleBook.FromJson(BehaviorRule(
            "{ \"kind\": \"SpendingBurst\", \"flavorId\": \"sing-along\", \"factor\": 1.4, \"durationTicks\": 30 }"));

        var behavior = book.Rules.Single().Effects.OfType<EffectSpec.Behavior>().Single();
        var burst = Assert.IsType<BehaviorOutcome.SpendingBurst>(behavior.Outcome);
        Assert.Equal("sing-along", burst.FlavorId);
        Assert.Equal(1.4, burst.Factor, precision: 6);
        Assert.Equal(30, burst.DurationTicks);
    }

    [Fact]
    public void Parses_a_satisfaction_shock_outcome()
    {
        var book = RuleBook.FromJson(BehaviorRule(
            "{ \"kind\": \"SatisfactionShock\", \"flavorId\": \"awe\", \"delta\": -0.25 }"));

        var behavior = book.Rules.Single().Effects.OfType<EffectSpec.Behavior>().Single();
        var shock = Assert.IsType<BehaviorOutcome.SatisfactionShock>(behavior.Outcome);
        Assert.Equal("awe", shock.FlavorId);
        Assert.Equal(-0.25, shock.Delta, precision: 6);
    }

    [Fact]
    public void Parses_the_reach_and_stacking_of_every_rule()
    {
        var book = RuleBook.FromJson(Doc(TraitDefs("alpha", "beta", "rowdy"),
            Rule("a-x-b", "alpha", "beta", "TavernWide", "Binary", SatisfactionBinary(0.002)),
            Rule("r-x-r", "rowdy", "rowdy", "SameRoom", "CountScaling", SatisfactionScaling(0.001, 0.01))));

        var binary = book.Rules.Single(r => r.Id == new RuleId("a-x-b"));
        Assert.Equal(RuleReach.TavernWide, binary.Reach);
        Assert.Equal(StackingMode.Binary, binary.Stacking);
        Assert.Equal(0.002, binary.Effects.OfType<EffectSpec.SatisfactionBinary>().Single().RatePerTick, precision: 6);

        var scaling = book.Rules.Single(r => r.Id == new RuleId("r-x-r"));
        Assert.Equal(RuleReach.SameRoom, scaling.Reach);
        Assert.Equal(StackingMode.CountScaling, scaling.Stacking);
        var sat = scaling.Effects.OfType<EffectSpec.SatisfactionScaling>().Single();
        Assert.Equal(0.001, sat.RatePerTickPerPair, precision: 6);
        Assert.Equal(0.01, sat.MaxRate, precision: 6);
    }

    [Fact]
    public void Behavior_events_are_legal_under_count_scaling()
    {
        var book = RuleBook.FromJson(Doc(TraitDefs("rowdy"),
            Rule("r-x-r", "rowdy", "rowdy", "SameRoom", "CountScaling",
                 SpendingScaling(1.05, 1.5) + "," + BehaviorGuestsLeave(0.4))));

        Assert.Single(book.Rules.Single().Effects.OfType<EffectSpec.Behavior>());
    }

    [Fact]
    public void Accepts_a_chance_of_exactly_one_and_rejects_zero()
    {
        var certain = RuleBook.FromJson(Doc(TraitDefs("alpha", "beta"),
            Rule("a-x-b", "alpha", "beta", "SameRoom", "Binary", BehaviorGuestsLeave(1.0))));
        Assert.Equal(1.0, certain.Rules.Single().Effects.OfType<EffectSpec.Behavior>().Single().Chance, precision: 6);

        AssertRejected(Doc(TraitDefs("alpha", "beta"),
            Rule("a-x-b", "alpha", "beta", "SameRoom", "Binary", BehaviorGuestsLeave(0.0))), "chance");
    }

    [Fact]
    public void Rejects_a_count_scaling_effect_missing_its_cap()
    {
        var doc = Doc(TraitDefs("rowdy"),
            Rule("r-x-r", "rowdy", "rowdy", "SameRoom", "CountScaling",
                 "{ \"class\": \"SpendingMultiplier\", \"factorPerPair\": 1.05 }"));
        AssertRejected(doc, "maxFactor");
    }

    [Fact]
    public void Rejects_an_unknown_behavior_outcome_kind()
    {
        AssertRejected(BehaviorRule("{ \"kind\": \"Teleport\", \"flavorId\": \"poof\" }"), "Teleport");
    }

    [Fact]
    public void Rejects_a_rule_missing_its_reach()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            "{ \"id\": \"a-x-b\", \"traitA\": \"alpha\", \"traitB\": \"beta\", \"description\": \"d\", " +
            "\"stacking\": \"Binary\", \"effects\": [ " + SpendingBinary(1.1) + " ] }");
        AssertRejected(doc, "reach");
    }

    [Fact]
    public void Rejects_an_unknown_reach_value()
    {
        AssertRejected(
            Doc(TraitDefs("alpha", "beta"),
                Rule("a-x-b", "alpha", "beta", "NextDoor", "Binary", SpendingBinary(1.1))),
            "NextDoor");
    }

    [Fact]
    public void Rejects_a_malformed_document()
    {
        Assert.ThrowsAny<Exception>(() => RuleBook.FromJson("{ \"traits\": "));
    }
}
