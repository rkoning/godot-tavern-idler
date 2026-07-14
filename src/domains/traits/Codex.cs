namespace TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

// ── DOM-006: the discovery codex (REQ-043 / REQ-044 / REQ-111) ──────────────
// Lifetime-scoped: a rule discovered on its first-ever activation stays discovered, and the
// prestige sequence never clears it (CON-016). Restore merges rather than replaces.

public sealed class Codex
{
    private readonly HashSet<RuleId> _discovered = new();

    /// True when this is the rule's first-ever activation, i.e. the caller should emit RuleDiscovered.
    public bool Discover(RuleId rule) => _discovered.Add(rule);

    public bool Knows(RuleId rule) => _discovered.Contains(rule);

    public IReadOnlyCollection<RuleId> Discovered => _discovered;

    public void Merge(IEnumerable<RuleId> rules)
    {
        foreach (var rule in rules)
            _discovered.Add(rule);
    }
}
