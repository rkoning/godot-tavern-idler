using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Staffing;

/// <summary>
/// CON-009 staff content catalog conformance (v1.1). Abstract — the content adapter (TKT-020)
/// subclasses with its real loader. CON-009 v1.1 enumerates the validation rules; this suite pins
/// each of them. The cross-file trait-existence rule needs the trait registry, so <see cref="LoadCatalog"/>
/// takes the set of known trait ids (the adapter supplies the loaded CON-011 traits).
/// Each invalid case below violates exactly ONE rule against an otherwise-valid baseline, so a loader
/// cannot pass the suite by implementing only a subset of the rules.
/// </summary>
public abstract class StaffCatalogConformanceTests
{
    /// Parse + validate staff content JSON against the known trait ids. MUST throw on any invalid input below.
    protected abstract StaffCatalog LoadCatalog(string json, IReadOnlyCollection<TraitId> knownTraits);

    private static readonly IReadOnlyCollection<TraitId> KnownTraits = new[] { new TraitId("t1") };

    private static string GoldenJson([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "staff.sample.json"));

    [Fact]
    public void Golden_catalog_loads_expected_ids()
    {
        // Golden references sturdy / soothing / legendary — supply them as the known trait set.
        var known = new[] { new TraitId("sturdy"), new TraitId("soothing"), new TraitId("legendary") };
        var cat = LoadCatalog(GoldenJson(), known);
        Assert.Contains(cat.Roles, r => r.Id == new RoleId("bartender"));
        Assert.Contains(cat.Roles, r => r.Id == new RoleId("masseuse") && r.PaidServiceId == "massage");
        Assert.Contains(cat.NamedHires, n => n.Id == new NamedHireId("old-tom") && n.Role == new RoleId("bartender"));
    }

    // ── Builders: a valid baseline, then one field mutated per case ──
    private static string Role(string id = "a", string display = "A", int wage = 1,
                               string traits = "[\"t1\"]", string paid = "null", string extra = "")
        => $"{{\"id\":\"{id}\",\"displayName\":\"{display}\",\"wage\":{wage},\"traits\":{traits},\"paidService\":{paid}{extra}}}";

    private static string Hire(string id = "n", string role = "a", string display = "N", int wage = 1,
                               string traits = "[\"t1\"]", string perk = "p")
        => $"{{\"id\":\"{id}\",\"displayName\":\"{display}\",\"role\":\"{role}\",\"wage\":{wage},\"traits\":{traits},\"unlockPerk\":\"{perk}\"}}";

    private static string Cat(string roles, string hires = "") => $"{{\"roles\":[{roles}],\"namedHires\":[{hires}]}}";

    public static IEnumerable<object[]> InvalidCatalogs()
    {
        // structural (v1.0)
        yield return new object[] { "duplicate role id", Cat($"{Role()},{Role()}") };
        yield return new object[] { "empty role id", Cat(Role(id: "")) };
        yield return new object[] { "dangling namedHire.role", Cat(Role(), Hire(role: "ghost")) };
        yield return new object[] { "duplicate named-hire id", Cat(Role(), $"{Hire()},{Hire()}") };
        // v1.1 additions
        yield return new object[] { "negative role wage", Cat(Role(wage: -1)) };
        yield return new object[] { "negative named-hire wage", Cat(Role(), Hire(wage: -1)) };
        yield return new object[] { "zero-trait role", Cat(Role(traits: "[]")) };
        yield return new object[] { "zero-trait named hire", Cat(Role(), Hire(traits: "[]")) };
        yield return new object[] { "negative paidService price", Cat(Role(paid: "{\"serviceId\":\"s\",\"price\":-1}")) };
        yield return new object[] { "empty paidService serviceId", Cat(Role(paid: "{\"serviceId\":\"\",\"price\":1}")) };
        yield return new object[] { "trait not in registry", Cat(Role(traits: "[\"not-a-known-trait\"]")) };
        yield return new object[] { "unknown JSON field", Cat(Role(extra: ",\"bogus\":true")) };
        yield return new object[] { "empty displayName", Cat(Role(display: "")) };
        yield return new object[] { "empty unlockPerk", Cat(Role(), Hire(perk: "")) };
    }

    [Theory]
    [MemberData(nameof(InvalidCatalogs))]
    public void Invalid_catalog_is_rejected(string reason, string json)
    {
        _ = reason; // shown in the test name
        Assert.ThrowsAny<Exception>(() => LoadCatalog(json, KnownTraits));
    }
}
