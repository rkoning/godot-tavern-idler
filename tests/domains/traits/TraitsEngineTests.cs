using System;
using System.Linq;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;
using TavernIdler.Tests.Contracts.Traits;
using static TavernIdler.Tests.Contracts.Traits.Catalog;
using static TavernIdler.Tests.Contracts.Traits.Presence;

namespace TavernIdler.Tests.Domains.Traits;

/// <summary>
/// Unit tests for the DOM-006 rule engine covering behavior the CON-011 abstract suite leaves to the
/// implementation: carrier pairing edge cases, per-episode event bookkeeping, night-lifecycle guards,
/// and codex merge semantics.
/// </summary>
public class TraitsEngineTests
{
    private static readonly RoomId R1 = new(1);
    private static readonly RoomId R2 = new(2);

    private static TraitsEngineHarness Harness(string doc, params double[] draws) =>
        new(doc, ScriptedRandomSource.ForTraits(draws));

    private static string SpendRule(string reach, string stacking = "Binary", double factor = 1.5) =>
        Doc(TraitDefs("alpha", "beta"),
            Rule("a-x-b", "alpha", "beta", reach, stacking, SpendingBinary(factor)));

    private static bool Began(TraitsTickResult r) => r.Effects.OfType<EmittedEffect.SpendingMultiplierBegan>().Any();

    // ── Pairing ─────────────────────────────────────────────────

    [Fact]
    public void Walking_guest_with_no_room_never_satisfies_a_same_room_rule()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, null, "alpha"), Guest(2, null, "beta")));   // both in circulation
        Assert.False(Began(h.Commands.Tick()));

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, null, "beta")));      // one still walking
        Assert.False(Began(h.Commands.Tick()));
    }

    [Fact]
    public void Tavern_wide_rule_reaches_guests_in_circulation()
    {
        var h = Harness(SpendRule("TavernWide"));
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, null, "alpha"), Guest(2, R1, "beta")));
        Assert.True(Began(h.Commands.Tick()));
    }

    [Fact]
    public void A_single_carrier_holding_both_traits_does_not_pair_with_itself()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha", "beta")));   // pairs are distinct-carrier (CON-011)
        var r = h.Commands.Tick();
        Assert.False(Began(r));
        Assert.DoesNotContain(r.Events, e => e is RuleActivated);
    }

    [Fact]
    public void A_carrier_holding_both_traits_still_pairs_with_a_single_trait_carrier()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha", "beta"), Guest(2, R1, "beta")));
        var began = h.Commands.Tick().Effects.OfType<EmittedEffect.SpendingMultiplierBegan>().Single();
        Assert.Equal(new[] { 1, 2 }, began.Targets.Select(g => g.Value).OrderBy(v => v).ToArray());
    }

    [Fact]
    public void Consumed_item_pairs_with_a_guest_and_targets_only_guests()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.Commands.BeginNight();

        // The consuming guest carries alpha; the ale it consumes carries beta (DOM006-Q1).
        h.SetPresence(Snapshot(Guest(5, R1, "alpha"), ConsumedItem("ale", consumedBy: 5, R1, "beta")));
        var began = h.Commands.Tick().Effects.OfType<EmittedEffect.SpendingMultiplierBegan>().Single();
        Assert.Equal(new[] { 5 }, began.Targets.Select(g => g.Value).ToArray());
    }

    // ── Episode bookkeeping ─────────────────────────────────────

    [Fact]
    public void A_stable_episode_re_emits_nothing_on_later_ticks()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.Commands.BeginNight();

        var present = Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta"));
        h.SetPresence(present);
        Assert.True(Began(h.Commands.Tick()));

        h.SetPresence(present);
        var steady = h.Commands.Tick();
        Assert.Empty(steady.Effects);
        Assert.Empty(steady.Events);
    }

    [Fact]
    public void Each_episode_open_including_a_churn_reopen_raises_exactly_one_rule_activated()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta")));
        var open = h.Commands.Tick();
        var first = open.Events.OfType<RuleActivated>().Single();

        // Membership swap → churn: the old episode closes and a new one opens.
        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(3, R1, "beta")));
        var churn = h.Commands.Tick();

        var second = churn.Events.OfType<RuleActivated>().Single();
        Assert.NotEqual(first.EpisodeId, second.EpisodeId);
        Assert.Equal(R1, second.Room);
    }

    [Fact]
    public void Rule_activated_room_is_null_when_the_episode_spans_rooms()
    {
        var h = Harness(SpendRule("TavernWide"));
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, R2, "beta")));
        var activated = h.Commands.Tick().Events.OfType<RuleActivated>().Single();
        Assert.Null(activated.Room);
    }

    // ── Night lifecycle ─────────────────────────────────────────

    [Fact]
    public void Begin_night_clears_episode_state_and_emits_nothing()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.Commands.BeginNight();

        var present = Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta"));
        h.SetPresence(present);
        h.Commands.Tick();

        Assert.Empty(h.Commands.BeginNight());   // stale episodes dropped, no effects/events

        h.SetPresence(present);
        var reopened = h.Commands.Tick();
        Assert.True(Began(reopened));                                                   // reopened from scratch...
        Assert.Empty(reopened.Effects.OfType<EmittedEffect.SpendingMultiplierEnded>()); // ...without closing the stale one
    }

    [Fact]
    public void Ticking_outside_an_open_night_is_rejected()
    {
        var h = Harness(SpendRule("SameRoom"));
        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta")));

        Assert.Throws<InvalidOperationException>(() => h.Commands.Tick());   // CON-011: Tick is Service-only

        h.Commands.BeginNight();
        h.Commands.Tick();
        h.Commands.EndNight();
        Assert.Throws<InvalidOperationException>(() => h.Commands.Tick());
    }

    [Fact]
    public void Behavior_rerolls_after_end_night_closes_the_episode()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("a-x-b", "alpha", "beta", "SameRoom", "Binary", BehaviorGuestsLeave(0.4)));
        var h = Harness(doc, 0.1, 0.9);   // night 1 triggers, night 2 does not
        var present = Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta"));

        h.Commands.BeginNight();
        h.SetPresence(present);
        Assert.Single(h.Commands.Tick().Effects.OfType<EmittedEffect.BehaviorEventTriggered>());
        h.Commands.EndNight();

        h.Commands.BeginNight();
        h.SetPresence(present);
        Assert.Empty(h.Commands.Tick().Effects.OfType<EmittedEffect.BehaviorEventTriggered>());
    }

    // ── Codex (REQ-043 / REQ-044 / REQ-111) ─────────────────────

    [Fact]
    public void Restore_merges_into_the_existing_codex_rather_than_replacing_it()
    {
        var doc = Doc(TraitDefs("alpha", "beta", "gamma", "delta"),
            Rule("a-x-b", "alpha", "beta", "TavernWide", "Binary", SpendingBinary(1.1)),
            Rule("g-x-d", "gamma", "delta", "TavernWide", "Binary", SpendingBinary(1.2)));
        var h = Harness(doc);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta")));
        h.Commands.Tick();   // discovers a-x-b

        h.Commands.Restore(new CodexSnapshot(1, new[] { new RuleId("g-x-d") }));

        Assert.Equal(
            new[] { "a-x-b", "g-x-d" },
            h.Queries.Codex.Select(e => e.Rule.Value).OrderBy(v => v, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Restore_ignores_rule_ids_absent_from_the_catalog()
    {
        var h = Harness(SpendRule("TavernWide"));

        h.Commands.Restore(new CodexSnapshot(1, new[] { new RuleId("a-x-b"), new RuleId("retired-rule") }));

        var entry = Assert.Single(h.Queries.Codex);
        Assert.Equal(new RuleId("a-x-b"), entry.Rule);
    }

    [Fact]
    public void Codex_entry_lists_every_effect_class_of_the_rule()
    {
        var doc = Doc(TraitDefs("alpha", "beta"),
            Rule("a-x-b", "alpha", "beta", "SameRoom", "Binary",
                 SpendingBinary(1.1) + "," + SatisfactionBinary(0.002) + "," + BehaviorGuestsLeave(0.4)));
        var h = Harness(doc, 0.9);
        h.Commands.BeginNight();

        h.SetPresence(Snapshot(Guest(1, R1, "alpha"), Guest(2, R1, "beta")));
        h.Commands.Tick();

        var entry = Assert.Single(h.Queries.Codex);
        Assert.Equal(
            new[] { EffectClassKind.BehaviorEvent, EffectClassKind.SatisfactionModifier, EffectClassKind.SpendingMultiplier },
            entry.EffectClasses.OrderBy(c => c.ToString(), StringComparer.Ordinal).ToArray());
    }
}
