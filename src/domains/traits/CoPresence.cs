namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

// ── DOM-006: co-presence evaluation (REQ-040, REQ-045, REQ-046, REQ-047) ────
// Turns one CON-012 PresenceSnapshot into a rule's qualifying pair set: unordered
// distinct-carrier pairs holding the rule's two traits, with ≥1 guest participant, whose
// carriers satisfy the rule's reach. Runs for every rule every tick, so the hot path avoids
// LINQ and keeps its scratch buffers alive between calls.

/// One qualifying pair, identified by its carriers (not by snapshot position) so the pair set
/// can be compared across ticks — membership changes, not just count changes, churn an episode
/// (CON-011 v1.1). Carriers are stored in a canonical order so {a,b} and {b,a} are one pair.
public readonly record struct CarrierPair(CarrierRef First, CarrierRef Second)
{
    public static CarrierPair Of(CarrierRef a, CarrierRef b) =>
        Compare(a, b) <= 0 ? new CarrierPair(a, b) : new CarrierPair(b, a);

    private static int Compare(CarrierRef a, CarrierRef b)
    {
        var (rankA, numberA, textA) = Order(a);
        var (rankB, numberB, textB) = Order(b);
        if (rankA != rankB) return rankA.CompareTo(rankB);
        if (numberA != numberB) return numberA.CompareTo(numberB);
        return string.CompareOrdinal(textA, textB);
    }

    private static (int Rank, int Number, string Text) Order(CarrierRef carrier) => carrier switch
    {
        CarrierRef.Guest g => (0, g.Id.Value, ""),
        CarrierRef.Employee e => (1, e.Id.Value, ""),
        CarrierRef.Room r => (2, r.Id.Value, ""),
        CarrierRef.ConsumedItem i => (3, i.ConsumedBy.Value, i.Item.Value),
        _ => (4, 0, ""),
    };
}

public sealed class CoPresenceEvaluator
{
    private readonly List<int> _candidates = new();

    /// The rule's qualifying pairs in the current snapshot, plus the guest targets and the room the
    /// episode sits in (null when it spans rooms — a tavern-wide or broadcaster-widened episode).
    public QualifyingPairs Evaluate(RuleDefinition rule, PresenceSnapshot snapshot)
    {
        var carriers = snapshot.Carriers;

        _candidates.Clear();
        for (var i = 0; i < carriers.Count; i++)
            if (Holds(carriers[i], rule.TraitA) || Holds(carriers[i], rule.TraitB))
                _candidates.Add(i);

        var pairs = new HashSet<CarrierPair>();
        var targets = new SortedSet<int>();
        RoomId? room = null;
        var uniformRoom = true;
        var first = true;

        for (var a = 0; a < _candidates.Count; a++)
        for (var b = a + 1; b < _candidates.Count; b++)
        {
            var left = carriers[_candidates[a]];
            var right = carriers[_candidates[b]];

            if (!EndpointsMatch(rule, left, right)) continue;
            if (!left.IsGuest && !right.IsGuest) continue;          // REQ-040: ≥1 guest participant
            if (!Reaches(rule, left, right)) continue;              // REQ-046 / REQ-047

            pairs.Add(CarrierPair.Of(left.Ref, right.Ref));

            if (left.Ref is CarrierRef.Guest lg) targets.Add(lg.Id.Value);
            if (right.Ref is CarrierRef.Guest rg) targets.Add(rg.Id.Value);

            var shared = left.Room is { } lr && right.Room is { } rr && lr == rr ? lr : (RoomId?)null;
            if (first) { room = shared; first = false; }
            else if (!Nullable.Equals(room, shared)) uniformRoom = false;
        }

        var guests = new GuestId[targets.Count];
        var next = 0;
        foreach (var id in targets) guests[next++] = new GuestId(id);

        return new QualifyingPairs(pairs, guests, uniformRoom ? room : null);
    }

    /// A rule's two endpoints are trait ids (REQ-096); a pair qualifies when the carriers cover them
    /// in either direction. A carrier holding both traits still needs a partner: pairs are
    /// distinct-carrier (CON-011 v1.1).
    private static bool EndpointsMatch(RuleDefinition rule, PresentCarrier left, PresentCarrier right) =>
        (Holds(left, rule.TraitA) && Holds(right, rule.TraitB)) ||
        (Holds(left, rule.TraitB) && Holds(right, rule.TraitA));

    /// Reach (REQ-046): tavern-wide rules reach anywhere; same-room rules need a shared room, unless a
    /// carrier stands in a broadcaster room, which widens its same-room effects tavern-wide (REQ-047).
    /// A carrier in circulation (Room == null) is in no room, so it satisfies no same-room rule.
    private static bool Reaches(RuleDefinition rule, PresentCarrier left, PresentCarrier right)
    {
        if (rule.Reach == RuleReach.TavernWide) return true;
        if (left.InBroadcaster || right.InBroadcaster) return true;
        return left.Room is { } lr && right.Room is { } rr && lr == rr;
    }

    private static bool Holds(PresentCarrier carrier, TraitId trait)
    {
        var traits = carrier.Traits;
        for (var i = 0; i < traits.Count; i++)
            if (traits[i] == trait)
                return true;
        return false;
    }
}

/// The evaluation of one rule against one snapshot.
public sealed record QualifyingPairs(
    HashSet<CarrierPair> Pairs,
    IReadOnlyList<GuestId> Targets,   // guest participants only (REQ-042): staff/rooms/items never targeted
    RoomId? Room)
{
    public int Count => Pairs.Count;
    public bool Any => Pairs.Count > 0;
}
