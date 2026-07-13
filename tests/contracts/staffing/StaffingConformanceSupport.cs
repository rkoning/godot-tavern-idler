using System.Collections.Generic;
using TavernIdler.Kernel;
using TavernIdler.Domains.Staffing;
using TavernIdler.Domains.Structure;   // StaffRequirements / RoleRequirement (CON-003)

namespace TavernIdler.Tests.Contracts.Staffing;

// ── Test-support catalog (NOT a port type) ───────────────────────────────
// Lets the abstract suite express roles + named hires without depending on the content
// adapter's loader (TKT-020). The domain-impl ticket (TKT-012) maps this onto its catalog.
public sealed record RoleSpec(
    RoleId Id, string DisplayName, Money Wage, IReadOnlyList<TraitId> Traits,
    string? PaidServiceId, Money PaidServicePrice);

public sealed record NamedHireSpec(
    NamedHireId Id, string DisplayName, RoleId Role, Money Wage,
    IReadOnlyList<TraitId> Traits, string UnlockPerk);

public sealed record StaffCatalog(IReadOnlyList<RoleSpec> Roles, IReadOnlyList<NamedHireSpec> NamedHires);

/// <summary>
/// The seam the CON-009 conformance suite drives. TKT-012 (staffing domain) provides a sealed
/// subclass of <see cref="StaffingApiConformanceTests"/> whose <c>CreateSut</c> returns a harness
/// that builds the real staffing aggregate over <paramref name="catalog"/> and wires fakes for the
/// injected driven ports (ICycleQueries phase, CON-010 IRoomRequirements / IHireUnlocks).
/// </summary>
public interface IStaffingTestHarness
{
    IStaffingCommands Commands { get; }
    IStaffingQueries Queries { get; }

    /// Drives the injected ICycleQueries: true ⇒ Prep (mutations allowed), false ⇒ Service.
    void SetInPrep(bool inPrep);

    /// Drives CON-010 IRoomRequirements for the given room (current-tier requirements).
    void SetRoomRequirements(RoomId room, StaffRequirements requirements);
    void ClearRoomRequirements(RoomId room);

    /// Drives CON-010 IHireUnlocks: the named hires whose unlock perk is currently owned.
    void SetUnlockedNamedHires(params NamedHireId[] hires);
}

// Small shared builders so the suite reads cleanly.
internal static class StaffScenario
{
    public static StaffRequirements Req(params (string role, int min, int max)[] roles) =>
        new(new List<RoleRequirement>(System.Linq.Enumerable.Select(
            roles, r => new RoleRequirement(new RoleId(r.role), r.min, r.max))));

    public static RoleSpec Role(string id, long wage, params string[] traits) =>
        new(new RoleId(id), id, new Money(wage),
            System.Array.ConvertAll(traits, t => new TraitId(t)), null, Money.Zero);
}
