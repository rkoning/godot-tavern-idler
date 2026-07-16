using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TavernIdler.Domains.Economy;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Economy.Driven;

/// <summary>
/// CON-008 abstract conformance suite for <see cref="IMenuContent"/> and the menu JSON schema: the
/// golden-file load, every schema-validation rule (fail-fast with a diagnostic naming the offending
/// field, incl. the cross-file unknown-trait check against the CON-011 trait registry), and unlock/
/// venue filtering. The content adapter ticket (TKT-020) supplies the concrete loader/adapter via the
/// abstract hooks below. Abstract ⇒ nothing runs until then. The golden document lives beside this
/// file as <c>menu.sample.json</c>. Each invalid case violates exactly ONE rule against an
/// otherwise-valid baseline, so a loader cannot pass by implementing only a subset of the rules.
/// </summary>
public abstract class MenuContentConformanceTests
{
    /// Parse + validate a menu.json document into sheets against the known trait ids (the adapter
    /// supplies the loaded CON-011 traits). MUST throw fail-fast on any schema violation, with an
    /// exception message naming the offending field (per CON-008).
    protected abstract IReadOnlyList<MenuItemSheet> LoadCatalog(string menuJson, IReadOnlyCollection<TraitId> knownTraits);

    /// Build an <see cref="IMenuContent"/> composing the golden catalog with unlock/venue state:
    /// base items are always available; ids in <paramref name="unlockedItems"/> additionally appear.
    protected abstract IMenuContent CreateContent(
        string menuJson, IReadOnlyCollection<TraitId> knownTraits, IReadOnlyCollection<MenuItemId> unlockedItems);

    /// A menu id present in the golden catalog but NOT in the base (always-available) set — it surfaces
    /// only when its unlock/venue-exclusive is owned. For <c>menu.sample.json</c> TKT-020 picks this.
    protected abstract MenuItemId LockedItemInGolden { get; }

    private static string GoldenMenuJson([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "menu.sample.json"));

    private static readonly IReadOnlyCollection<TraitId> GoldenTraits =
        new[] { new TraitId("humble"), new TraitId("alcoholic"), new TraitId("refined") };

    // ── Golden-file load ────────────────────────────────────────
    [Fact]
    public void Golden_file_loads_expected_ale_sheet()
    {
        var ale = LoadCatalog(GoldenMenuJson(), GoldenTraits).Single(s => s.Id == new MenuItemId("ale"));
        Assert.Equal("Ale", ale.DisplayName);
        Assert.Equal(MenuCategory.Drink, ale.Category);
        Assert.Equal(new Money(4), ale.SalePrice);
        Assert.Equal(new Money(1), ale.RestockCost);
        Assert.Equal(new[] { new TraitId("humble"), new TraitId("alcoholic") }, ale.Traits);
    }

    [Fact]
    public void Golden_file_loads_the_food_item_with_its_category_and_prices()
    {
        var pheasant = LoadCatalog(GoldenMenuJson(), GoldenTraits).Single(s => s.Id == new MenuItemId("roast-pheasant"));
        Assert.Equal(MenuCategory.Food, pheasant.Category);
        Assert.Equal(new Money(25), pheasant.SalePrice);
        Assert.Equal(new Money(10), pheasant.RestockCost);
        Assert.Equal(new[] { new TraitId("refined") }, pheasant.Traits);
    }

    // ── Schema-validation rules (each rejects, naming the offending field) ──
    private static string Item(
        string id = "ale", string display = "Ale", string category = "Drink",
        int salePrice = 4, int restockCost = 1, string traits = "[\"humble\"]", string extra = "")
        => $"{{\"id\":\"{id}\",\"displayName\":\"{display}\",\"category\":\"{category}\","
         + $"\"salePrice\":{salePrice},\"restockCost\":{restockCost},\"traits\":{traits}{extra}}}";

    private static string Cat(params string[] items) => "{\"menuItems\":[" + string.Join(",", items) + "]}";

    private static readonly IReadOnlyCollection<TraitId> KnownTraits = new[] { new TraitId("humble") };

    public static IEnumerable<object[]> InvalidMenus()
    {
        yield return new object[] { "duplicate id", Cat(Item(id: "ale"), Item(id: "ale")), "ale" };
        yield return new object[] { "empty id", Cat(Item(id: "")), "id" };
        yield return new object[] { "negative salePrice", Cat(Item(salePrice: -1)), "salePrice" };
        yield return new object[] { "negative restockCost", Cat(Item(restockCost: -1)), "restockCost" };
        yield return new object[] { "unknown category", Cat(Item(category: "Dessert")), "Dessert" };
        yield return new object[] { "trait not in registry", Cat(Item(traits: "[\"nonexistent\"]")), "nonexistent" };
        yield return new object[] { "unknown JSON field", Cat(Item(extra: ",\"bogus\":true")), "bogus" };
    }

    [Theory]
    [MemberData(nameof(InvalidMenus))]
    public void Invalid_menu_is_rejected_naming_the_offending_field(string reason, string json, string token)
    {
        _ = reason;   // shown in the test name
        var ex = Assert.ThrowsAny<Exception>(() => LoadCatalog(json, KnownTraits));
        Assert.Contains(token, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Unlock/venue filtering (stubbed CON-013 state) ──────────
    [Fact]
    public void Locked_menu_item_is_excluded_until_unlocked_while_base_items_are_always_available()
    {
        var locked = LockedItemInGolden;

        var without = CreateContent(GoldenMenuJson(), GoldenTraits, Array.Empty<MenuItemId>())
            .AvailableItems().Select(s => s.Id).ToList();
        Assert.DoesNotContain(locked, without);
        Assert.Contains(new MenuItemId("ale"), without);   // base item present regardless of unlocks

        var with = CreateContent(GoldenMenuJson(), GoldenTraits, new[] { locked })
            .AvailableItems().Select(s => s.Id).ToList();
        Assert.Contains(locked, with);
    }
}
