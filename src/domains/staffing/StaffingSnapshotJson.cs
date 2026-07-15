namespace TavernIdler.Domains.Staffing;

using System.Text.Json;
using System.Text.Json.Serialization;

// ── CON-009 StaffingSnapshot payload (schema owned by DOM-005, envelope by CON-017) ──────────
// camelCase, enums as strings, strict on unknown fields — the CON-017 serialization rules.
// Only mutable roster state is persisted; display name / wage / traits are re-derived from the
// catalog on restore (the catalog is authoritative), and room-state is a derived cache (not saved).

internal static class StaffingSnapshotJson
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Serialize(IEnumerable<StaffRoster.Employee> employees, int nextEmployeeId)
    {
        var payload = new Payload(
            nextEmployeeId,
            employees
                .Select(e => new EmployeeRecord(
                    e.Id.Value,
                    e.Role.Value,
                    e.NamedHire?.Value,
                    e.AssignedRoom?.Value,
                    e.Refusing))
                .OrderBy(e => e.Id)
                .ToList());
        return JsonSerializer.Serialize(payload, Options);
    }

    public static RestoredState Deserialize(StaffingSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != SchemaVersion)
            throw new InvalidOperationException(
                $"Unsupported staffing snapshot schemaVersion {snapshot.SchemaVersion} (expected {SchemaVersion}).");

        var payload = JsonSerializer.Deserialize<Payload>(snapshot.JsonPayload, Options)
            ?? throw new InvalidOperationException("Staffing snapshot payload is empty.");

        return new RestoredState(payload.Employees, payload.NextEmployeeId);
    }

    internal sealed record RestoredState(IReadOnlyList<EmployeeRecord> Employees, int NextEmployeeId);

    private sealed record Payload(int NextEmployeeId, IReadOnlyList<EmployeeRecord> Employees);

    internal sealed record EmployeeRecord(int Id, string Role, string? NamedHire, int? AssignedRoom, bool Refusing);
}
