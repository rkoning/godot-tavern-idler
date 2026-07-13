using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Staffing;

/// <summary>
/// CON-009 staff content catalog conformance. Abstract — the content adapter (TKT-020) subclasses
/// with its real loader. NOTE (clarification, no contract edit): CON-009 shows the staff JSON
/// schema but — unlike CON-011 — does not enumerate validation rules. This suite therefore asserts
/// only the structurally-implied invariants (unique ids, named-hire → existing role, non-empty
/// ids). If tighter rules are wanted, that is a CON-009 clarification via /requirement.
/// </summary>
public abstract class StaffCatalogConformanceTests
{
    /// Parse + validate staff content JSON. MUST throw on the invalid inputs below.
    protected abstract StaffCatalog LoadCatalog(string json);

    private static string GoldenJson([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "staff.sample.json"));

    [Fact]
    public void Golden_catalog_loads_expected_ids()
    {
        var cat = LoadCatalog(GoldenJson());
        Assert.Contains(cat.Roles, r => r.Id == new RoleId("bartender"));
        Assert.Contains(cat.Roles, r => r.Id == new RoleId("masseuse") && r.PaidServiceId == "massage");
        Assert.Contains(cat.NamedHires, n => n.Id == new NamedHireId("old-tom") && n.Role == new RoleId("bartender"));
    }

    [Theory]
    [InlineData("{ \"roles\": [ {\"id\":\"a\",\"displayName\":\"A\",\"wage\":1,\"traits\":[],\"paidService\":null}, {\"id\":\"a\",\"displayName\":\"A2\",\"wage\":1,\"traits\":[],\"paidService\":null} ], \"namedHires\": [] }")] // duplicate role id
    [InlineData("{ \"roles\": [ {\"id\":\"\",\"displayName\":\"A\",\"wage\":1,\"traits\":[],\"paidService\":null} ], \"namedHires\": [] }")] // empty role id
    [InlineData("{ \"roles\": [ {\"id\":\"a\",\"displayName\":\"A\",\"wage\":1,\"traits\":[],\"paidService\":null} ], \"namedHires\": [ {\"id\":\"n\",\"displayName\":\"N\",\"role\":\"ghost\",\"wage\":1,\"traits\":[],\"unlockPerk\":\"p\"} ] }")] // named hire → unknown role
    [InlineData("{ \"roles\": [ {\"id\":\"a\",\"displayName\":\"A\",\"wage\":1,\"traits\":[],\"paidService\":null} ], \"namedHires\": [ {\"id\":\"n\",\"displayName\":\"N\",\"role\":\"a\",\"wage\":1,\"traits\":[],\"unlockPerk\":\"p\"}, {\"id\":\"n\",\"displayName\":\"N2\",\"role\":\"a\",\"wage\":1,\"traits\":[],\"unlockPerk\":\"p\"} ] }")] // duplicate named-hire id
    public void Invalid_catalog_is_rejected(string json)
        => Assert.ThrowsAny<System.Exception>(() => LoadCatalog(json));
}
