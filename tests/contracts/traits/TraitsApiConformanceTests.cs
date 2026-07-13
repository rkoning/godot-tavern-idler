using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;
using static TavernIdler.Tests.Contracts.Traits.Catalog;
using static TavernIdler.Tests.Contracts.Traits.Presence;

namespace TavernIdler.Tests.Contracts.Traits;

/// <summary>
/// CON-011 (Traits API v1.1) abstract conformance suite. Covers every bullet of the contract's
/// "Conformance tests" section: episode lifecycle (open/close/re-entry), reach (same-room /
/// tavern-wide / broadcaster widening), the REQ-040 guest-participation gate, Binary vs
/// CountScaling stacking with caps, v1.1 episode churn (count and membership), v1.1 EndNight
/// closure, seeded behavior rolls (once per episode), discovery + codex round-trip + prestige
/// persistence, and effect ordering within a tick result.
///
/// This class is ABSTRACT — xUnit never instantiates it, so nothing runs until the Traits domain
/// ticket (TKT-013) supplies a concrete subclass implementing <see cref="CreateHarness"/> against
/// the real rule engine. TKT-005 only defines the suite and the frozen port types it targets.
/// </summary>
public abstract class TraitsApiConformanceTests
{
    /// Build a harness over the real rule engine loaded from <paramref name="catalogJson"/>
    /// (CON-011 schema) with behavior rolls drawn from <paramref name="random"/>'s "traits" stream.
    /// The harness starts in the pre-night state; the suite calls <c>BeginNight</c> before ticking.
    protected abstract ITraitsTestHarness CreateHarness(string catalogJson, IRandomSource random);

    private ITraitsTestHarness Harness(string catalogJson, params double[] traitsDraws) =>
        CreateHarness(catalogJson, ScriptedRandomSource.ForTraits(traitsDraws));

    // ── effect / event accessors ────────────────────────────────
    private static T Single<T>(TraitsTickResult r) where T : EmittedEffect => r.Effects.OfType<T>().Single();
    private static bool Has<T>(TraitsTickResult r) where T : EmittedEffect => r.Effects.OfType<T>().Any();
    private static IReadOnlyList<int> Ids(IReadOnlyList<GuestId> ids) => ids.Select(g => g.Value).OrderBy(v => v).ToArray();

    private static readonly RoomId R1 = new(1);
    private static readonly RoomId R2 = new(2);
    private static readonly RoomId R3 = new(3);

    // ════════════════════════════════════════════════════════════
    //  Episode lifecycle
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Copresence_opens_episode_emitting_began_effects_to_guest_targets()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "TavernWide", "Binary",
                 SpendingBinary(1.1) + "," + SatisfactionBinary(0.002)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));
        var r = h.Commands.Tick();

        var spend = Single<EmittedEffect.SpendingMultiplierBegan>(r);
        Assert.Equal(1.1, spend.Factor, precision: 6);
        Assert.Equal(new[] { 1, 2 }, Ids(spend.Targets));
        var sat = Single<EmittedEffect.SatisfactionModifierBegan>(r);
        Assert.Equal(0.002, sat.SatisfactionRatePerTick, precision: 6);
        Assert.Equal(new[] { 1, 2 }, Ids(sat.Targets));
        Assert.Contains(r.Events, e => e is RuleActivated ra && ra.Rule == new RuleId("bard-x-rowdy"));
    }

    [Fact]
    public void Separation_closes_episode_emitting_ended_with_same_id()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "TavernWide", "Binary", SpendingBinary(1.1)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));
        var open = h.Commands.Tick();
        var episode = Single<EmittedEffect.SpendingMultiplierBegan>(open).EpisodeId;

        h.SetPresence(Snapshot(Guest(1, R1, "bard")));   // rowdy carrier gone
        var close = h.Commands.Tick();

        var ended = Single<EmittedEffect.SpendingMultiplierEnded>(close);
        Assert.Equal(episode, ended.EpisodeId);
        Assert.False(Has<EmittedEffect.SpendingMultiplierBegan>(close));
    }

    [Fact]
    public void Full_closure_then_reentry_opens_new_episode_and_rerolls_behavior()
    {
        var doc = Doc(TraitDefs("outlaw", "lawful"),
            Rule("outlaw-x-lawful", "outlaw", "lawful", "SameRoom", "Binary", BehaviorGuestsLeave(0.5)));
        var h = Harness(doc, traitsDraws: new[] { 0.1, 0.9 });   // 1st open succeeds, 2nd fails
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "outlaw"), Guest(2, R1, "lawful")));
        var open1 = h.Commands.Tick();
        var e1 = open1.Events.OfType<RuleActivated>().Single().EpisodeId;
        Assert.True(Has<EmittedEffect.BehaviorEventTriggered>(open1));

        h.SetPresence(Snapshot(Guest(1, R1, "outlaw")));         // fully separated → episode closes
        h.Commands.Tick();

        h.SetPresence(Snapshot(Guest(1, R1, "outlaw"), Guest(2, R1, "lawful")));
        var open2 = h.Commands.Tick();
        var e2 = open2.Events.OfType<RuleActivated>().Single().EpisodeId;

        Assert.NotEqual(e1, e2);                                 // fresh episode id
        Assert.False(Has<EmittedEffect.BehaviorEventTriggered>(open2));   // re-rolled → this time a failure
    }

    // ════════════════════════════════════════════════════════════
    //  Reach (REQ-046 / REQ-047)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Same_room_rule_ignores_cross_room_pair_then_activates_when_colocated()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "SameRoom", "Binary", SpendingBinary(1.2)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R2, "rowdy")));   // different rooms
        var apart = h.Commands.Tick();
        Assert.False(Has<EmittedEffect.SpendingMultiplierBegan>(apart));
        Assert.DoesNotContain(apart.Events, e => e is RuleActivated);

        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));   // co-located
        var together = h.Commands.Tick();
        Assert.True(Has<EmittedEffect.SpendingMultiplierBegan>(together));
    }

    [Fact]
    public void Tavern_wide_rule_qualifies_across_rooms()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "TavernWide", "Binary", SpendingBinary(1.1)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R2, "rowdy")));   // different rooms
        var r = h.Commands.Tick();
        Assert.True(Has<EmittedEffect.SpendingMultiplierBegan>(r));
    }

    [Fact]
    public void Broadcaster_widens_a_same_room_rule_and_leaving_it_closes_the_episode()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "SameRoom", "Binary", SpendingBinary(1.2)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        // bard broadcasts → the same-room rule reaches the rowdy guest in another room (REQ-047).
        h.SetPresence(Snapshot(GuestInBroadcaster(1, R1, "bard"), Guest(2, R2, "rowdy")));
        var open = h.Commands.Tick();
        var episode = Single<EmittedEffect.SpendingMultiplierBegan>(open).EpisodeId;

        // bard leaves the broadcaster room (now a plain room, still apart) → widening gone → closes.
        h.SetPresence(Snapshot(Guest(1, R3, "bard"), Guest(2, R2, "rowdy")));
        var close = h.Commands.Tick();
        Assert.Equal(episode, Single<EmittedEffect.SpendingMultiplierEnded>(close).EpisodeId);
    }

    // ════════════════════════════════════════════════════════════
    //  REQ-040 — guest participation gate
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Staff_staff_pair_never_activates()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("alpha-x-beta", "alpha", "beta", "SameRoom", "Binary", SpendingBinary(1.1)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Staff(1, R1, "alpha"), Staff(2, R1, "beta")));
        var r = h.Commands.Tick();
        Assert.False(Has<EmittedEffect.SpendingMultiplierBegan>(r));
        Assert.DoesNotContain(r.Events, e => e is RuleActivated);
    }

    [Fact]
    public void Room_item_pair_with_no_guest_participant_never_activates()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("alpha-x-beta", "alpha", "beta", "SameRoom", "Binary", SpendingBinary(1.1)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        // Room carries alpha, a consumed item carries beta; the consuming guest has no rule trait.
        h.SetPresence(Snapshot(
            RoomCarrier(1, "alpha"),
            ConsumedItem("ale", consumedBy: 5, R1, "beta"),
            Guest(5, R1)));
        var r = h.Commands.Tick();
        Assert.False(Has<EmittedEffect.SpendingMultiplierBegan>(r));
        Assert.DoesNotContain(r.Events, e => e is RuleActivated);
    }

    [Fact]
    public void Guest_staff_pair_activates_and_targets_only_the_guest()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("alpha-x-beta", "alpha", "beta", "SameRoom", "Binary", SpendingBinary(1.3)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Staff(2, R1, "beta")));
        var r = h.Commands.Tick();
        var began = Single<EmittedEffect.SpendingMultiplierBegan>(r);
        Assert.Equal(new[] { 1 }, Ids(began.Targets));   // staff is a participant but never a target
    }

    // ════════════════════════════════════════════════════════════
    //  Stacking (REQ-045)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Binary_stacking_is_independent_of_pair_count()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "TavernWide", "Binary", SpendingBinary(1.1)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(
            Guest(1, R1, "bard"), Guest(2, R1, "bard"),
            Guest(3, R1, "rowdy"), Guest(4, R1, "rowdy")));   // 4 qualifying pairs
        var r = h.Commands.Tick();
        Assert.Equal(1.1, Single<EmittedEffect.SpendingMultiplierBegan>(r).Factor, precision: 6);
    }

    [Fact]
    public void CountScaling_grows_with_pairs()
    {
        var doc = Doc(TraitDefs("rowdy"),
            Rule("rowdy-x-rowdy", "rowdy", "rowdy", "SameRoom", "CountScaling",
                 SpendingScaling(1.05, 1.5) + "," + SatisfactionScaling(0.001, 0.01)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        // 3 co-present rowdy guests ⇒ C(3,2) = 3 unordered distinct-carrier pairs.
        h.SetPresence(Snapshot(Guest(1, R1, "rowdy"), Guest(2, R1, "rowdy"), Guest(3, R1, "rowdy")));
        var r = h.Commands.Tick();

        Assert.Equal(System.Math.Pow(1.05, 3), Single<EmittedEffect.SpendingMultiplierBegan>(r).Factor, precision: 6);
        Assert.Equal(0.003, Single<EmittedEffect.SatisfactionModifierBegan>(r).SatisfactionRatePerTick, precision: 6);
    }

    [Fact]
    public void CountScaling_caps_at_max_factor_and_max_rate()
    {
        var doc = Doc(TraitDefs("rowdy"),
            Rule("rowdy-x-rowdy", "rowdy", "rowdy", "SameRoom", "CountScaling",
                 SpendingScaling(1.05, 1.5) + "," + SatisfactionScaling(0.001, 0.01)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        // 5 co-present rowdy guests ⇒ C(5,2) = 10 pairs ⇒ both effects hit their caps.
        h.SetPresence(Snapshot(
            Guest(1, R1, "rowdy"), Guest(2, R1, "rowdy"), Guest(3, R1, "rowdy"),
            Guest(4, R1, "rowdy"), Guest(5, R1, "rowdy")));
        var r = h.Commands.Tick();

        Assert.Equal(1.5, Single<EmittedEffect.SpendingMultiplierBegan>(r).Factor, precision: 6);
        Assert.Equal(0.01, Single<EmittedEffect.SatisfactionModifierBegan>(r).SatisfactionRatePerTick, precision: 6);
    }

    [Fact]
    public void Pair_count_change_closes_and_reopens_without_behavior_reroll()
    {
        var doc = Doc(TraitDefs("rowdy"),
            Rule("rowdy-x-rowdy", "rowdy", "rowdy", "SameRoom", "CountScaling",
                 SpendingScaling(1.05, 1.5) + "," + BehaviorGuestsLeave(0.4)));
        var h = Harness(doc, traitsDraws: new[] { 0.1 });   // exactly one scripted draw
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "rowdy"), Guest(2, R1, "rowdy")));   // 1 pair
        var open = h.Commands.Tick();
        var e1 = Single<EmittedEffect.SpendingMultiplierBegan>(open).EpisodeId;
        Assert.True(Has<EmittedEffect.BehaviorEventTriggered>(open));            // consumed the 0.1 draw

        h.SetPresence(Snapshot(Guest(1, R1, "rowdy"), Guest(2, R1, "rowdy"), Guest(3, R1, "rowdy")));  // 3 pairs
        var churn = h.Commands.Tick();

        var e2 = Single<EmittedEffect.SpendingMultiplierBegan>(churn).EpisodeId;
        Assert.Equal(e1, Single<EmittedEffect.SpendingMultiplierEnded>(churn).EpisodeId);
        Assert.NotEqual(e1, e2);
        // No re-roll: the script is drained, so a re-roll would draw 0.0 (< 0.4) and re-trigger.
        Assert.False(Has<EmittedEffect.BehaviorEventTriggered>(churn));
    }

    // ════════════════════════════════════════════════════════════
    //  Episode churn (v1.1) — count-preserving membership swap
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Membership_swap_reopens_with_new_id_and_current_targets_without_reroll()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("alpha-x-beta", "alpha", "beta", "TavernWide", "Binary",
                 SpendingBinary(1.1) + "," + BehaviorGuestsLeave(0.4)));
        var h = Harness(doc, traitsDraws: new[] { 0.1 });   // one scripted draw
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta")));
        var open = h.Commands.Tick();
        var e1 = Single<EmittedEffect.SpendingMultiplierBegan>(open).EpisodeId;
        Assert.Equal(new[] { 1, 2 }, Ids(Single<EmittedEffect.SpendingMultiplierBegan>(open).Targets));
        Assert.True(Has<EmittedEffect.BehaviorEventTriggered>(open));

        // Count-preserving swap: guest 2 (beta) leaves as guest 3 (beta) arrives — still exactly 1 pair.
        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(3, R1, "beta")));
        var swap = h.Commands.Tick();

        var began = Single<EmittedEffect.SpendingMultiplierBegan>(swap);
        Assert.Equal(e1, Single<EmittedEffect.SpendingMultiplierEnded>(swap).EpisodeId);
        Assert.NotEqual(e1, began.EpisodeId);
        Assert.Equal(new[] { 1, 3 }, Ids(began.Targets));                       // targets follow membership
        Assert.False(Has<EmittedEffect.BehaviorEventTriggered>(swap));          // churn, not full closure ⇒ no re-roll
    }

    // ════════════════════════════════════════════════════════════
    //  EndNight (v1.1)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void EndNight_closes_open_episodes_and_emits_nothing()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "TavernWide", "Binary", SpendingBinary(1.1)));
        var h = Harness(doc);

        h.Commands.BeginNight();
        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));
        var e1 = Single<EmittedEffect.SpendingMultiplierBegan>(h.Commands.Tick()).EpisodeId;

        Assert.Empty(h.Commands.EndNight());   // no effects/events — closure is internal (v1.1)

        // The next night reopens the qualifying pair with a fresh episode id.
        h.Commands.BeginNight();
        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));
        var e2 = Single<EmittedEffect.SpendingMultiplierBegan>(h.Commands.Tick()).EpisodeId;
        Assert.NotEqual(e1, e2);
    }

    // ════════════════════════════════════════════════════════════
    //  Behavior roll (seeded, once per episode)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Behavior_roll_well_below_chance_triggers_with_outcome()
    {
        var doc = Doc(TraitDefs("outlaw", "lawful"),
            Rule("outlaw-x-lawful", "outlaw", "lawful", "SameRoom", "Binary", BehaviorGuestsLeave(0.4, "brawl")));
        var h = Harness(doc, traitsDraws: new[] { 0.1 });
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "outlaw"), Guest(2, R1, "lawful")));
        var r = h.Commands.Tick();

        var bet = Single<EmittedEffect.BehaviorEventTriggered>(r);
        var outcome = Assert.IsType<BehaviorOutcome.GuestsLeave>(bet.Outcome);
        Assert.Equal("brawl", outcome.FlavorId);
        Assert.Equal(R1, bet.Room);
    }

    [Fact]
    public void Behavior_roll_well_above_chance_does_not_trigger()
    {
        var doc = Doc(TraitDefs("outlaw", "lawful"),
            Rule("outlaw-x-lawful", "outlaw", "lawful", "SameRoom", "Binary", BehaviorGuestsLeave(0.4)));
        var h = Harness(doc, traitsDraws: new[] { 0.9 });
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "outlaw"), Guest(2, R1, "lawful")));
        var r = h.Commands.Tick();
        Assert.False(Has<EmittedEffect.BehaviorEventTriggered>(r));
    }

    [Fact]
    public void Behavior_rolls_at_most_once_per_episode()
    {
        var doc = Doc(TraitDefs("outlaw", "lawful"),
            Rule("outlaw-x-lawful", "outlaw", "lawful", "SameRoom", "Binary", BehaviorGuestsLeave(0.4)));
        var h = Harness(doc, traitsDraws: new[] { 0.1 });   // one draw only
        h.Commands.BeginNight();

        var present = Snapshot(Guest(1, R1, "outlaw"), Guest(2, R1, "lawful"));
        h.SetPresence(present);
        Assert.True(Has<EmittedEffect.BehaviorEventTriggered>(h.Commands.Tick()));   // rolled at open

        h.SetPresence(present);                                                       // same episode persists
        Assert.False(Has<EmittedEffect.BehaviorEventTriggered>(h.Commands.Tick()));   // no second roll
    }

    // ════════════════════════════════════════════════════════════
    //  Discovery + codex (REQ-043 / REQ-044 / REQ-111)
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void First_activation_discovers_the_rule_exactly_once_and_records_codex_entry()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "TavernWide", "Binary", SpendingBinary(1.1),
                 description: "A bard turns a rowdy house into a chorus."));
        var h = Harness(doc);
        h.Commands.BeginNight();
        Assert.Equal(1, h.Queries.TotalRuleCount);
        Assert.Empty(h.Queries.Codex);

        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));
        var first = h.Commands.Tick();
        Assert.Single(first.Events.OfType<RuleDiscovered>().Where(d => d.Rule == new RuleId("bard-x-rowdy")));

        var entry = Assert.Single(h.Queries.Codex);
        Assert.Equal(new RuleId("bard-x-rowdy"), entry.Rule);
        Assert.Equal(new TraitId("bard"), entry.TraitA);
        Assert.Equal(new TraitId("rowdy"), entry.TraitB);
        Assert.Equal("A bard turns a rowdy house into a chorus.", entry.Description);
        Assert.Equal(RuleReach.TavernWide, entry.Reach);
        Assert.Contains(EffectClassKind.SpendingMultiplier, entry.EffectClasses);

        // Re-activation never re-discovers.
        h.SetPresence(Snapshot(Guest(1, R1, "bard")));
        h.Commands.Tick();
        h.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));
        var second = h.Commands.Tick();
        Assert.Empty(second.Events.OfType<RuleDiscovered>());
    }

    [Fact]
    public void Codex_snapshot_round_trips_and_persists_across_prestige()
    {
        var doc = Doc(TraitDefs("bard", "rowdy"),
            Rule("bard-x-rowdy", "bard", "rowdy", "TavernWide", "Binary", SpendingBinary(1.1)));
        var run1 = Harness(doc);

        // Capture is legal before any night and reflects an empty codex.
        Assert.Empty(run1.Commands.Capture().Discovered);

        run1.Commands.BeginNight();
        run1.SetPresence(Snapshot(Guest(1, R1, "bard"), Guest(2, R1, "rowdy")));
        run1.Commands.Tick();

        var snap = run1.Commands.Capture();
        Assert.Equal(1, snap.SchemaVersion);
        Assert.Contains(new RuleId("bard-x-rowdy"), snap.Discovered);

        // A fresh run (prestige) restores the codex without any activation.
        var run2 = Harness(doc);
        run2.Commands.Restore(snap);
        Assert.Contains(run2.Queries.Codex, e => e.Rule == new RuleId("bard-x-rowdy"));
    }

    // ════════════════════════════════════════════════════════════
    //  Effect ordering within a tick result
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Effects_are_ordered_all_ended_then_all_began_then_behavior()
    {
        var doc = Doc(TraitDefs("alpha", "beta", "gamma", "delta"),
            Rule("a-x-b", "alpha", "beta", "TavernWide", "Binary", SpendingBinary(1.1)),
            Rule("g-x-d", "gamma", "delta", "TavernWide", "Binary",
                 SpendingBinary(1.2) + "," + BehaviorGuestsLeave(0.4)));
        var h = Harness(doc, traitsDraws: new[] { 0.1 });
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta")));
        h.Commands.Tick();   // opens a-x-b

        // Next tick: a-x-b closes (Ended) while g-x-d opens (Began) and its behavior triggers.
        h.SetPresence(Snapshot(Guest(3, R1, "gamma"), Guest(4, R1, "delta")));
        var r = h.Commands.Tick();

        int lastEnded = -1, firstBegan = int.MaxValue, firstBehavior = int.MaxValue;
        for (var i = 0; i < r.Effects.Count; i++)
        {
            switch (r.Effects[i])
            {
                case EmittedEffect.SpendingMultiplierEnded:
                case EmittedEffect.SatisfactionModifierEnded:
                    lastEnded = i; break;
                case EmittedEffect.SpendingMultiplierBegan:
                case EmittedEffect.SatisfactionModifierBegan:
                    firstBegan = System.Math.Min(firstBegan, i); break;
                case EmittedEffect.BehaviorEventTriggered:
                    firstBehavior = System.Math.Min(firstBehavior, i); break;
            }
        }

        Assert.True(lastEnded >= 0 && firstBegan != int.MaxValue && firstBehavior != int.MaxValue,
            "expected an Ended, a Began, and a Behavior effect in the same tick");
        Assert.True(lastEnded < firstBegan, "all Ended effects must precede Began effects");
        Assert.True(firstBegan < firstBehavior, "Began effects must precede BehaviorEventTriggered");
    }
}
