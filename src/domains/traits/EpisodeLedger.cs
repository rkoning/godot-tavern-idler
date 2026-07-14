namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

// ── DOM-006: the episode ledger (REQ-110, CON-011 v1.1) ─────────────────────
// One open episode per rule, keyed on the rule's qualifying pair SET. A change to that set —
// count or membership — closes the episode and reopens a new one (new EpisodeId, continuous
// effects re-emitted against the current targets). A behavior event rolls once per activation
// span: a churn reopen does NOT re-roll, only a full closure (no pairs left, or the night ends)
// arms the next roll.

public sealed class Episode
{
    internal Episode(long id, HashSet<CarrierPair> pairs, bool behaviorRolled)
    {
        Id = id;
        Pairs = pairs;
        BehaviorRolled = behaviorRolled;
    }

    public long Id { get; }
    public HashSet<CarrierPair> Pairs { get; }
    public bool BehaviorRolled { get; internal set; }
}

public sealed class EpisodeLedger
{
    private readonly Dictionary<RuleId, Episode> _open = new();
    private long _lastEpisodeId;   // unique per run: never rewound, not even by BeginNight

    public Episode? Find(RuleId rule) => _open.TryGetValue(rule, out var episode) ? episode : null;

    /// Opens a fresh episode for the rule. <paramref name="behaviorRolled"/> carries the roll flag
    /// over a churn reopen (the pair set changed but the rule never fully deactivated).
    public Episode Open(RuleId rule, HashSet<CarrierPair> pairs, bool behaviorRolled)
    {
        var episode = new Episode(++_lastEpisodeId, pairs, behaviorRolled);
        _open[rule] = episode;
        return episode;
    }

    public void Close(RuleId rule) => _open.Remove(rule);

    /// Drops every open episode without emitting: BeginNight clears stale state, EndNight closes
    /// internally (CON-011 v1.1 — no effects, no events).
    public void Clear() => _open.Clear();
}
