namespace TavernIdler.Domains.Structure;

using System.Text.Json;
using System.Text.Json.Serialization;
using TavernIdler.Kernel;

// ── CON-003 StructureSnapshot payload (schema owned by DOM-001, envelope by CON-017) ─────────
// camelCase, enums as strings, strict on unknown fields — the CON-017 serialization rules.
// Active flags are NOT persisted: they are derived from the layout on restore (REQ-098).

internal static class StructureSnapshotJson
{
    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Serialize(
        IEnumerable<Tavern.RoomState> rooms,
        IReadOnlyDictionary<CellCoord, Tavern.CirculationState> circulation,
        int nextRoomId,
        int graphVersion)
    {
        var payload = new Payload(
            nextRoomId,
            graphVersion,
            rooms.Select(r => new RoomDto(
                    r.Id.Value, r.Type.Value, r.Tier,
                    r.Footprint.X, r.Footprint.Y, r.Footprint.Width, r.Footprint.Height,
                    r.PaidTotal.Amount))
                .OrderBy(r => r.Id)
                .ToList(),
            circulation
                .Select(kv => new CirculationDto(kv.Key.X, kv.Key.Y, kv.Value.Kind, kv.Value.Paid.Amount))
                .OrderBy(c => c.X).ThenBy(c => c.Y)
                .ToList());
        return JsonSerializer.Serialize(payload, Options);
    }

    public static RestoredState Deserialize(StructureSnapshot snapshot)
    {
        if (snapshot.SchemaVersion != SchemaVersion)
            throw new InvalidOperationException(
                $"Unsupported structure snapshot schemaVersion {snapshot.SchemaVersion} (expected {SchemaVersion}).");

        var payload = JsonSerializer.Deserialize<Payload>(snapshot.JsonPayload, Options)
            ?? throw new InvalidOperationException("Structure snapshot payload is empty.");

        var rooms = payload.Rooms
            .Select(r => new Tavern.RoomState(
                new RoomId(r.Id), new RoomTypeId(r.Type), r.Tier,
                new GridRect(r.X, r.Y, r.Width, r.Height),
                active: true,                                   // recomputed from the layout
                new Money(r.PaidTotal)))
            .ToList();

        var circulation = payload.Circulation.ToDictionary(
            c => new CellCoord(c.X, c.Y),
            c => new Tavern.CirculationState(c.Kind, new Money(c.Paid)));

        return new RestoredState(rooms, circulation, payload.NextRoomId, payload.GraphVersion);
    }

    internal sealed record RestoredState(
        IReadOnlyList<Tavern.RoomState> Rooms,
        IReadOnlyDictionary<CellCoord, Tavern.CirculationState> Circulation,
        int NextRoomId,
        int GraphVersion);

    private sealed record Payload(
        int NextRoomId,
        int GraphVersion,
        IReadOnlyList<RoomDto> Rooms,
        IReadOnlyList<CirculationDto> Circulation);

    private sealed record RoomDto(
        int Id, string Type, int Tier, int X, int Y, int Width, int Height, long PaidTotal);

    private sealed record CirculationDto(int X, int Y, CirculationKind Kind, long Paid);
}
