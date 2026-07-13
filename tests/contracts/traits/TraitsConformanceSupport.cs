using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Traits;

/// <summary>
/// The seam the CON-011 abstract conformance suite drives. The Traits domain ticket (TKT-013)
/// provides a concrete <see cref="ITraitsTestHarness"/> wrapping the real rule engine wired to a
/// settable stub <see cref="IPresenceSource"/> (CON-012) and the injected <see cref="IRandomSource"/>
/// (CON-015). TKT-005 only defines the suite and the frozen port types it targets — no domain
/// behavior lives here.
///
/// Presence is abstracted through <see cref="SetPresence"/> so the suite scripts what the engine
/// observes on the next <see cref="ITraitsCommands.Tick"/> without referencing the real presence
/// bridge (its own contract, CON-012, exercised by <c>Driven.PresenceSourceConformanceTests</c>).
/// </summary>
public interface ITraitsTestHarness
{
    ITraitsCommands Commands { get; }
    ITraitsQueries Queries { get; }

    /// The snapshot the injected <see cref="IPresenceSource"/> returns on the NEXT
    /// <see cref="ITraitsCommands.Tick"/>. Persists until replaced.
    void SetPresence(PresenceSnapshot snapshot);
}

/// <summary>Compact builders for <see cref="PresentCarrier"/> lists used by the CON-011 suite.</summary>
public static class Presence
{
    public static RoomId Room(int n) => new(n);

    public static IReadOnlyList<TraitId> T(params string[] ids) =>
        ids.Select(s => new TraitId(s)).ToArray();

    public static PresentCarrier Guest(int id, RoomId? room, params string[] traits) =>
        new(new CarrierRef.Guest(new GuestId(id)), room, IsGuest: true, T(traits), InBroadcaster: false);

    public static PresentCarrier GuestInBroadcaster(int id, RoomId? room, params string[] traits) =>
        new(new CarrierRef.Guest(new GuestId(id)), room, IsGuest: true, T(traits), InBroadcaster: true);

    public static PresentCarrier Staff(int id, RoomId? room, params string[] traits) =>
        new(new CarrierRef.Employee(new EmployeeId(id)), room, IsGuest: false, T(traits), InBroadcaster: false);

    public static PresentCarrier RoomCarrier(int room, params string[] traits) =>
        new(new CarrierRef.Room(new RoomId(room)), new RoomId(room), IsGuest: false, T(traits), InBroadcaster: false);

    public static PresentCarrier ConsumedItem(string item, int consumedBy, RoomId? room, params string[] traits) =>
        new(new CarrierRef.ConsumedItem(new MenuItemId(item), new GuestId(consumedBy)), room, IsGuest: false, T(traits), InBroadcaster: false);

    public static PresenceSnapshot Snapshot(params PresentCarrier[] carriers) => new(carriers);
}

/// <summary>
/// Fluent-ish assembly of <c>content/traits.json</c> documents (CON-011 schema) so a test states
/// only the traits/rules/effects it exercises. Emits invariant-culture numbers.
/// </summary>
public static class Catalog
{
    private static string Num(double d) => d.ToString("0.###############", CultureInfo.InvariantCulture);

    public static string TraitDefs(params string[] ids) =>
        string.Join(",", ids.Select(id =>
            $"{{ \"id\": \"{id}\", \"displayName\": \"{id}\", \"description\": \"the {id} trait\" }}"));

    public static string Rule(
        string id, string traitA, string traitB, string reach, string stacking, string effects, string description = "a rule") =>
        $"{{ \"id\": \"{id}\", \"traitA\": \"{traitA}\", \"traitB\": \"{traitB}\", " +
        $"\"description\": \"{description}\", \"reach\": \"{reach}\", \"stacking\": \"{stacking}\", " +
        $"\"effects\": [ {effects} ] }}";

    public static string Doc(string traits, params string[] rules) =>
        $"{{ \"traits\": [ {traits} ], \"rules\": [ {string.Join(",", rules)} ] }}";

    // ── effect fragments ────────────────────────────────────────
    public static string BehaviorGuestsLeave(double chance, string flavorId = "brawl") =>
        $"{{ \"class\": \"BehaviorEvent\", \"chance\": {Num(chance)}, " +
        $"\"outcome\": {{ \"kind\": \"GuestsLeave\", \"flavorId\": \"{flavorId}\" }} }}";

    public static string SpendingBinary(double factor) =>
        $"{{ \"class\": \"SpendingMultiplier\", \"factor\": {Num(factor)} }}";

    public static string SatisfactionBinary(double ratePerTick) =>
        $"{{ \"class\": \"SatisfactionModifier\", \"ratePerTick\": {Num(ratePerTick)} }}";

    public static string SpendingScaling(double factorPerPair, double maxFactor) =>
        $"{{ \"class\": \"SpendingMultiplier\", \"factorPerPair\": {Num(factorPerPair)}, \"maxFactor\": {Num(maxFactor)} }}";

    public static string SatisfactionScaling(double ratePerTickPerPair, double maxRate) =>
        $"{{ \"class\": \"SatisfactionModifier\", \"ratePerTickPerPair\": {Num(ratePerTickPerPair)}, \"maxRate\": {Num(maxRate)} }}";
}

/// <summary>
/// A deterministic <see cref="IRandomSource"/> whose named streams replay a scripted queue of
/// doubles (behavior rolls consume <c>NextDouble</c> from the <c>"traits"</c> stream, CON-011).
/// Draining a stream past its script yields 0.0. Lets the CON-011 suite fix roll outcomes.
/// </summary>
public sealed class ScriptedRandomSource : IRandomSource
{
    private readonly Dictionary<string, IReadOnlyList<double>> _scripts;
    private readonly Dictionary<string, ScriptedRandom> _streams = new();

    public ScriptedRandomSource(long seed, IReadOnlyDictionary<string, IReadOnlyList<double>> scripts)
    {
        Seed = seed;
        _scripts = scripts.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public long Seed { get; }

    public IRandom GetStream(string name)
    {
        if (!_streams.TryGetValue(name, out var stream))
        {
            var script = _scripts.TryGetValue(name, out var s) ? s : new List<double>();
            stream = new ScriptedRandom(script);
            _streams[name] = stream;
        }
        return stream;
    }

    /// Convenience: a source whose <c>"traits"</c> stream replays <paramref name="traitsDraws"/>.
    public static ScriptedRandomSource ForTraits(params double[] traitsDraws) =>
        new(seed: 1, new Dictionary<string, IReadOnlyList<double>> { ["traits"] = traitsDraws });

    private sealed class ScriptedRandom : IRandom
    {
        private readonly Queue<double> _doubles;
        public ScriptedRandom(IReadOnlyList<double> doubles) => _doubles = new Queue<double>(doubles);
        public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0.0;
        public int NextInt(int maxExclusive) => 0;
    }
}
