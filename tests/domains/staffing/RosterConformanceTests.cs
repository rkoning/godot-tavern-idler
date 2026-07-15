using TavernIdler.Tests.Contracts.Staffing;

namespace TavernIdler.Tests.Domains.Staffing;

/// <summary>
/// Runs the CON-009 (Staffing API) abstract conformance suite against the real <see cref="Roster"/>
/// aggregate, wired to the in-memory CON-010 driven ports and the CON-002 phase gate.
/// </summary>
public sealed class RosterConformanceTests : StaffingApiConformanceTests
{
    protected override IStaffingTestHarness CreateSut(StaffCatalog catalog) => new StaffingHarness(catalog);
}
