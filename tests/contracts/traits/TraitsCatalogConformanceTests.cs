using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;
using static TavernIdler.Tests.Contracts.Traits.Catalog;

namespace TavernIdler.Tests.Contracts.Traits;

/// <summary>
/// CON-011 abstract conformance suite for the trait/rule catalog JSON schema (<c>content/traits.json</c>):
/// the golden-file load and every schema-validation rule, including the v1.1 binary-vs-scaling param
/// symmetry. Fail-fast rejection must carry a diagnostic naming the offending field. The content/loader
/// ticket supplies the concrete <see cref="LoadCatalog"/>; abstract ⇒ nothing runs until then. The golden
/// document lives beside this file as <c>traits.sample.json</c>.
/// </summary>
public abstract class TraitsCatalogConformanceTests
{
    /// Parse + validate a traits.json document, returning the queryable registry it defines
    /// (with an as-yet-empty codex). MUST throw fail-fast on any schema violation, with an exception
    /// message naming the offending field/value (per CON-011 "Schema validation").
    protected abstract ITraitsQueries LoadCatalog(string catalogJson);

    protected static string GoldenTraitsJson([CallerFilePath] string? thisFile = null) =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(thisFile!)!, "traits.sample.json"));

    // ── Golden-file load ────────────────────────────────────────
    [Fact]
    public void Golden_file_loads_expected_traits_and_rule_count()
    {
        var q = LoadCatalog(GoldenTraitsJson());

        Assert.Equal(3, q.TotalRuleCount);
        Assert.Empty(q.Codex);                       // nothing discovered at load time

        var rowdy = q.TraitRegistry.Single(t => t.Id == new TraitId("rowdy"));
        Assert.Equal("Rowdy", rowdy.DisplayName);
        Assert.Equal("Loud and boisterous.", rowdy.Description);
        Assert.Equal(
            new[] { "bard", "lawful", "outlaw", "rowdy" },
            q.TraitRegistry.Select(t => t.Id.Value).OrderBy(v => v).ToArray());
    }

    // ── Schema-validation rules (each rejects, naming the offending field/value) ──
    private void AssertRejected(string badJson, string offendingToken)
    {
        var ex = Assert.ThrowsAny<Exception>(() => LoadCatalog(badJson));
        Assert.Contains(offendingToken, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_duplicate_trait_ids()
    {
        var doc = Doc(TraitDefs("dupTrait", "dupTrait"));
        AssertRejected(doc, "dupTrait");
    }

    [Fact]
    public void Rejects_duplicate_rule_ids()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("dupRule", "alpha", "beta", "SameRoom", "Binary", SpendingBinary(1.1)),
            Rule("dupRule", "alpha", "beta", "TavernWide", "Binary", SpendingBinary(1.2)));
        AssertRejected(doc, "dupRule");
    }

    [Fact]
    public void Rejects_rule_referencing_unknown_trait()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("r", "alpha", "ghostTrait", "SameRoom", "Binary", SpendingBinary(1.1)));
        AssertRejected(doc, "ghostTrait");
    }

    [Fact]
    public void Rejects_more_than_one_effect_of_a_class_per_rule()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("r", "alpha", "beta", "TavernWide", "Binary",
                 SpendingBinary(1.1) + "," + SpendingBinary(1.2)));
        AssertRejected(doc, "SpendingMultiplier");
    }

    [Fact]
    public void Rejects_behavior_chance_outside_zero_to_one()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("r", "alpha", "beta", "SameRoom", "Binary", BehaviorGuestsLeave(1.5)));
        AssertRejected(doc, "chance");
    }

    [Fact]
    public void Rejects_binary_param_on_a_count_scaling_rule()
    {
        // "factor" is a Binary param; a CountScaling rule must author factorPerPair/maxFactor.
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("r", "alpha", "beta", "SameRoom", "CountScaling", SpendingBinary(1.1)));
        AssertRejected(doc, "factor");
    }

    [Fact]
    public void Rejects_scaling_param_on_a_binary_rule()
    {
        // "factorPerPair" is a CountScaling param; a Binary rule must author a flat "factor".
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("r", "alpha", "beta", "TavernWide", "Binary", SpendingScaling(1.05, 1.5)));
        AssertRejected(doc, "factorPerPair");
    }

    [Fact]
    public void Rejects_unknown_json_field()
    {
        var doc =
            "{ \"traits\": [ { \"id\": \"alpha\", \"displayName\": \"A\", \"description\": \"d\", \"wizardry\": true } ], " +
            "\"rules\": [] }";
        AssertRejected(doc, "wizardry");
    }
}
