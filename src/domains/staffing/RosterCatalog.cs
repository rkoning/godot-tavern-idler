namespace TavernIdler.Domains.Staffing;

using TavernIdler.Kernel;

// ── DOM-005 staff content catalog (domain type, NOT a contract type) ──────────────────
// The static staff catalog the Roster aggregate is built over: hireable roles and named hires
// with their display name, wage and traits. The content adapter (TKT-020, CON-009 loader) produces
// the equivalent data from content/staff.json; a bridge/composition root maps it onto this type.
// Named-hire *unlock* state is NOT here — it is a runtime driven port (CON-010 IHireUnlocks).

/// <summary>An ordinary hireable role. REQ-062/064/095.</summary>
public sealed record StaffRoleDef(RoleId Id, string DisplayName, Money Wage, IReadOnlyList<TraitId> Traits);

/// <summary>A unique named hire, gated by an unlock perk (checked via CON-010). REQ-063.</summary>
public sealed record NamedHireDef(NamedHireId Id, string DisplayName, RoleId Role, Money Wage, IReadOnlyList<TraitId> Traits);

/// <summary>The full staff catalog the roster hires from.</summary>
public sealed record RosterCatalog(IReadOnlyList<StaffRoleDef> Roles, IReadOnlyList<NamedHireDef> NamedHires);
