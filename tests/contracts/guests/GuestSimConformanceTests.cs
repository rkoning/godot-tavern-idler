using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Guests;
using TavernIdler.Domains.Structure;
using TavernIdler.Domains.Staffing;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;
using static TavernIdler.Tests.Contracts.Guests.Scenario;

namespace TavernIdler.Tests.Contracts.Guests;

/// <summary>
/// CON-005 (Guests API v1.0) abstract behavioral conformance suite. Covers every bullet of the
/// contract's "Conformance tests" section: determinism, queue/capacity, agenda walk + each
/// <see cref="BlockReason"/>, the crowding table, payment-modifier bounds, effect application +
/// behavior outcomes, the lodger cycle, VIP arrival statistics, and the
/// <c>AllGuestsGone → NightStatsFinal</c> terminal sequence.
///
/// ABSTRACT — xUnit never instantiates it, so nothing runs until the Guests domain ticket (TKT-014)
/// supplies a concrete subclass implementing <see cref="CreateSut"/> over the real simulation. This
/// ticket (TKT-006) only defines the suite + the frozen port types it targets.
///
/// ⚠ Satisfiability: arrival discretization is under-specified by CON-005 (expected rate → discrete
/// per-tick arrivals), so this suite asserts queue/arrival behavior via <b>invariants</b> (admitted
/// while <c>Agents.Count &lt; TotalGuestCapacity</c>, else queued FIFO) and <b>determinism</b>, never
/// exact per-tick counts. Where a precise value is needed (crowding deltas, payment modifiers) the
/// scenario pins it with <c>TotalGuestCapacity == 1</c> (⇒ a single, alone agent) and clamped
/// effects, so every assertion is reachable by any conforming implementation.
/// </summary>
public abstract class GuestSimConformanceTests
{
    /// Build the SUT over the real guest simulation for <paramref name="world"/>. Called once per
    /// run; determinism tests call it twice with equal worlds (⇒ identical "guests" RNG streams).
    protected abstract IGuestSimTestHarness CreateSut(GuestWorld world);

    // ════════════════════════════════════════════════════════════
    //  Night drivers + helpers
    // ════════════════════════════════════════════════════════════

    /// BeginService → <paramref name="serviceTicks"/> single ticks → BeginDrain → drain to
    /// AllGuestsGone (capped). Returns the full ordered event stream across every command.
    private static List<IDomainEvent> DriveNight(IGuestSimTestHarness h, int serviceTicks, int drainCap = 4000)
    {
        var ev = new List<IDomainEvent>();
        ev.AddRange(h.Commands.BeginService());
        for (var i = 0; i < serviceTicks; i++) ev.AddRange(h.Commands.Tick(1));
        ev.AddRange(h.Commands.BeginDrain());
        for (var i = 0; i < drainCap && !ev.OfType<AllGuestsGone>().Any(); i++)
            ev.AddRange(h.Commands.Tick(1));
        return ev;
    }

    /// Tick after BeginService until at least one agent is present; returns its id (or null).
    private static GuestId? AdvanceToFirstAgent(IGuestSimTestHarness h, int cap = 400)
    {
        for (var i = 0; i < cap && h.View.Agents.Count == 0; i++) h.Commands.Tick(1);
        return h.View.Agents.Count > 0 ? h.View.Agents[0].Id : null;
    }

    /// A stable textual key for an event: captures its type + scalar fields (collection members
    /// render as their type name, identical across deterministic runs) for order-sensitive compare.
    private static IReadOnlyList<string> Keys(IEnumerable<IDomainEvent> events) =>
        events.Select(e => e.GetType().Name + ":" + e).ToArray();

    // A minimal, effect-free tavern: one non-VIP type wanting a single "drink", one MenuConsumption
    // taproom one cell from the entrance. Neutral crowding + no traits ⇒ satisfaction stays 0 unless
    // a test drives it. Fakes live on the returned world so the caller can read them back.
    private static GuestWorld CleanWorld(
        long seed = 20260715,
        int totalCapacity = 1,
        int roomCapacity = 4,
        string crowdPref = "neutral",
        double crowdMag = 0.0,
        long wallet = 1000,
        Money? alePrice = null,
        int? aleStock = null,
        int serviceTicks = 300,
        int baseDuration = 4,
        IEnumerable<GuestAgendaItem>? agenda = null)
    {
        var room = Room(1, cellX: 1, capacity: roomCapacity,
            Service("drink", ServiceKind.MenuConsumption, baseDuration));
        var txns = new FakeTransactions(alePrice ?? new Money(10));
        if (aleStock is int s) txns.WithStock("ale", s);
        var catalog = Catalog(Type("dwarf",
            agenda: agenda ?? new[] { Want("drink", "ale") },
            crowdPref: crowdPref, crowdMag: crowdMag, wallet: wallet));
        return new GuestWorld(
            seed, serviceTicks, catalog,
            Scenario.Structure(totalCapacity, room),
            FakeRoomServiceState.AllOpen(new[] { new RoomId(1) }),
            txns,
            new FakeAttractionContext(Attract(new[] { "dwarf" }, arrivalRateFactor: 1.0)));
    }

    // ════════════════════════════════════════════════════════════
    //  Determinism (REQ-002 / CON-005 "Determinism")
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Same_seed_and_world_produce_identical_event_streams()
    {
        GuestWorld Build() => CleanWorld(seed: 987654321, totalCapacity: 3, roomCapacity: 4);

        var run1 = DriveNight(CreateSut(Build()), serviceTicks: 250);
        var run2 = DriveNight(CreateSut(Build()), serviceTicks: 250);

        Assert.Equal(Keys(run1), Keys(run2));
    }

    [Fact]
    public void Same_seed_and_world_produce_identical_view_states_each_tick()
    {
        GuestWorld Build() => CleanWorld(seed: 424242, totalCapacity: 2, roomCapacity: 3);
        var a = CreateSut(Build());
        var b = CreateSut(Build());

        a.Commands.BeginService();
        b.Commands.BeginService();
        for (var i = 0; i < 120; i++)
        {
            a.Commands.Tick(1);
            b.Commands.Tick(1);
            Assert.Equal(ViewKey(a.View), ViewKey(b.View));
        }
    }

    private static string ViewKey(IGuestView v)
    {
        var agents = string.Join("|", v.Agents
            .OrderBy(g => g.Id.Value)
            .Select(g => $"{g.Id.Value},{g.Type.Value},{g.Cell.X}:{g.Cell.Y},{g.Activity},{g.Satisfaction:F4}"));
        var queue = string.Join("|", v.Queue.VisibleLine.Select(q => $"{q.Id.Value},{q.PatienceRemainingTicks}"));
        var occ = string.Join("|", v.Occupancy.OrderBy(o => o.Key.Value).Select(o => $"{o.Key.Value}:{o.Value.Occupants}/{o.Value.Capacity}"));
        return $"A[{agents}] Q[{queue};{v.Queue.OverflowCount}] O[{occ}]";
    }

    // ════════════════════════════════════════════════════════════
    //  AllGuestsGone → NightStatsFinal (CON-005 "AllGuestsGone")
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void AllGuestsGone_fires_exactly_once_and_is_immediately_followed_by_NightStatsFinal()
    {
        var world = CleanWorld(totalCapacity: 3, roomCapacity: 4, serviceTicks: 200);
        var ev = DriveNight(CreateSut(world), serviceTicks: 200);

        var goneIndices = ev.Select((e, i) => (e, i)).Where(t => t.e is AllGuestsGone).Select(t => t.i).ToArray();
        var gone = Assert.Single(goneIndices);
        Assert.True(gone + 1 < ev.Count && ev[gone + 1] is NightStatsFinal,
            "NightStatsFinal must immediately follow AllGuestsGone");
    }

    [Fact]
    public void Night_stats_are_internally_consistent_with_the_event_stream()
    {
        var world = CleanWorld(totalCapacity: 3, roomCapacity: 4, serviceTicks: 200);
        var ev = DriveNight(CreateSut(world), serviceTicks: 200);

        var stats = ev.OfType<NightStatsFinal>().Single().Stats;
        var admitted = ev.OfType<GuestAdmitted>().ToArray();

        Assert.Equal(admitted.Length, stats.TotalAdmitted);
        Assert.Equal(stats.TotalAdmitted, stats.AdmittedByType.Values.Sum());
        Assert.True(stats.MaxConcurrentGuests >= 0 && stats.MaxConcurrentGuests <= stats.TotalAdmitted);
        Assert.InRange(stats.MeanSatisfaction, -1.0, 1.0);
        Assert.True(stats.TotalTurnedAwayQueue >= 0);
        foreach (var a in admitted)
            Assert.True(stats.AdmittedByType.ContainsKey(a.Type), "every admitted type appears in AdmittedByType");
    }

    // ════════════════════════════════════════════════════════════
    //  Snapshot guard (CON-005 "Capture outside Prep/Settlement throws")
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Capture_is_legal_in_prep_but_throws_during_service()
    {
        var h = CreateSut(CleanWorld());

        // Prep (before BeginService): legal.
        var prep = h.Commands.Capture();
        Assert.Equal(1, prep.SchemaVersion);

        // Mid-service: illegal.
        h.Commands.BeginService();
        h.Commands.Tick(1);
        Assert.Throws<InvalidOperationException>(() => h.Commands.Capture());
    }

    // ════════════════════════════════════════════════════════════
    //  Queue + capacity (REQ-008 / REQ-010 / REQ-018)
    // ════════════════════════════════════════════════════════════

    // A flooding tavern: one slot, ~50 arrivals/tick ⇒ a persistent FIFO queue behind a single
    // admitted guest. Huge patience so admission is (until drained) the only queue exit.
    private GuestWorld FloodWorld(long seed = 555, int queuePatience = 100000, int serviceTicks = 200) =>
        CleanWorld(seed: seed, totalCapacity: 1, roomCapacity: 4, serviceTicks: serviceTicks)
            with
        {
            Catalog = Catalog(Type("dwarf",
                agenda: new[] { Want("drink", "ale") }, queuePatience: queuePatience, wallet: 1000)),
        };

    [Fact]
    public void Capacity_is_never_exceeded_and_the_queue_only_holds_the_overflow()
    {
        var world = FloodWorld();
        var cap = world.Structure.TotalGuestCapacity;
        var h = CreateSut(world);

        int queued = 0, leftQueue = 0;
        h.Commands.BeginService();
        for (var i = 0; i < world.ServiceDurationTicks; i++)
        {
            var ev = h.Commands.Tick(1);
            queued += ev.OfType<GuestQueued>().Count();
            leftQueue += ev.OfType<GuestLeftQueue>().Count();

            // Never over capacity.
            Assert.True(h.View.Agents.Count <= cap, $"agents {h.View.Agents.Count} exceeded capacity {cap}");

            // The queue holds exactly the guests that entered it and have not yet left (conservation),
            // split across the visible line and the overflow count. (Intra-tick admission ordering is
            // not pinned by CON-005, so the "no queue while a slot is free" direction is deliberately
            // NOT asserted per-tick — only these order-independent invariants are.)
            var live = h.View.Queue.VisibleLine.Count + h.View.Queue.OverflowCount;
            Assert.Equal(queued - leftQueue, live);
        }
    }

    [Fact]
    public void Queued_guests_are_admitted_in_fifo_order()
    {
        var world = FloodWorld(seed: 909, serviceTicks: 200);
        var h = CreateSut(world);

        var queuedOrder = new List<GuestId>();
        var admittedFromQueue = new List<GuestId>();

        h.Commands.BeginService();
        for (var i = 0; i < world.ServiceDurationTicks; i++)
        {
            var ev = h.Commands.Tick(1);
            queuedOrder.AddRange(ev.OfType<GuestQueued>().Select(e => e.Id));
            admittedFromQueue.AddRange(ev.OfType<GuestLeftQueue>()
                .Where(e => e.Reason == QueueLeaveReason.Admitted).Select(e => e.Id));
        }

        // With no expiry, the queue drains strictly from its front: the guests admitted from the
        // queue are the first-queued ones, in queue-entry order.
        Assert.True(admittedFromQueue.Count > 0, "expected some queued guests to be admitted");
        Assert.Equal(queuedOrder.Take(admittedFromQueue.Count), admittedFromQueue);
    }

    [Fact]
    public void Visible_queue_patience_decrements_while_a_guest_waits()
    {
        var world = FloodWorld(seed: 313, queuePatience: 100000, serviceTicks: 60);
        var h = CreateSut(world);

        h.Commands.BeginService();
        var prev = h.View.Queue.VisibleLine.ToDictionary(q => q.Id, q => q.PatienceRemainingTicks);
        for (var i = 0; i < world.ServiceDurationTicks; i++)
        {
            h.Commands.Tick(1);
            foreach (var q in h.View.Queue.VisibleLine)
            {
                Assert.True(q.PatienceRemainingTicks >= 0);
                if (prev.TryGetValue(q.Id, out var before))
                    Assert.True(q.PatienceRemainingTicks < before,
                        $"guest {q.Id.Value} patience did not decrement ({before} → {q.PatienceRemainingTicks})");
            }
            prev = h.View.Queue.VisibleLine.ToDictionary(q => q.Id, q => q.PatienceRemainingTicks);
        }
    }

    [Fact]
    public void Impatient_queuers_expire_and_leave_for_the_night()
    {
        // Short patience behind a single slow slot ⇒ back-of-queue guests time out.
        var world = FloodWorld(seed: 77, queuePatience: 15, serviceTicks: 200);
        var h = CreateSut(world);

        var expired = new List<GuestId>();
        h.Commands.BeginService();
        for (var i = 0; i < world.ServiceDurationTicks; i++)
            expired.AddRange(h.Commands.Tick(1).OfType<GuestLeftQueue>()
                .Where(e => e.Reason == QueueLeaveReason.PatienceExpired).Select(e => e.Id));

        Assert.True(expired.Count > 0, "short patience behind a single slot should expire some queuers");
        // Any guest recorded as patience-expired never appears among the still-queued.
        var stillQueued = h.View.Queue.VisibleLine.Select(q => q.Id).ToHashSet();
        Assert.All(expired, id => Assert.DoesNotContain(id, stillQueued));
    }

    [Fact]
    public void Begin_drain_disbands_the_whole_queue_and_admits_no_one_after()
    {
        var world = FloodWorld(seed: 4242, serviceTicks: 40);
        var h = CreateSut(world);

        h.Commands.BeginService();
        for (var i = 0; i < world.ServiceDurationTicks; i++) h.Commands.Tick(1);

        var hadQueue = h.View.Queue.VisibleLine.Count + h.View.Queue.OverflowCount > 0;
        var drain = h.Commands.BeginDrain();

        // Queue is empty immediately after drain…
        Assert.Empty(h.View.Queue.VisibleLine);
        Assert.Equal(0, h.View.Queue.OverflowCount);
        // …with a disband event for the guests that were waiting.
        if (hadQueue)
            Assert.Contains(drain.OfType<GuestLeftQueue>(), e => e.Reason == QueueLeaveReason.Disbanded);

        // No further admissions once draining.
        for (var i = 0; i < 200; i++)
            Assert.Empty(h.Commands.Tick(1).OfType<GuestAdmitted>());
    }

    // ════════════════════════════════════════════════════════════
    //  Agenda walk (REQ-048 / REQ-053 / REQ-054) + block reasons
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Guest_fulfills_its_want_then_leaves_agenda_complete()
    {
        var world = CleanWorld(totalCapacity: 2, roomCapacity: 4, wallet: 1000, serviceTicks: 250);
        var ev = DriveNight(CreateSut(world), serviceTicks: 250);

        var fulfilled = ev.OfType<WantFulfilled>().ToArray();
        Assert.True(fulfilled.Length > 0, "a clean, affordable, open room should let guests be served");
        Assert.All(fulfilled, f =>
        {
            Assert.Equal("drink", f.ServiceId);
            Assert.Equal(new RoomId(1), f.Room);
        });
        Assert.Contains(ev, e => e is GuestLeft { Reason: LeaveReason.AgendaComplete });
    }

    [Fact]
    public void Guest_that_cannot_afford_its_next_want_leaves_wallet_empty()
    {
        // wallet 10, ale 10, two ales: first purchase drains the wallet to 0, the second is unaffordable.
        var world = CleanWorld(totalCapacity: 2, roomCapacity: 4, wallet: 10, alePrice: new Money(10),
            serviceTicks: 250, agenda: new[] { Want("drink", "ale"), Want("drink", "ale") });
        var ev = DriveNight(CreateSut(world), serviceTicks: 250);

        Assert.Contains(ev, e => e is WantFulfilled);   // the first, affordable want
        Assert.Contains(ev, e => e is GuestLeft { Reason: LeaveReason.WalletEmpty });
    }

    // For each block cause the want is unfulfillable, so the strong invariant is "never fulfilled";
    // the reason attribution is asserted conditionally (Assert.All) so a differing-but-valid
    // discretization can never make the test unsatisfiable.
    private void AssertBlocked(GuestWorld world, string blockedService, BlockReason expected, int serviceTicks = 250)
    {
        var ev = DriveNight(CreateSut(world), serviceTicks);
        Assert.DoesNotContain(ev, e => e is WantFulfilled f && f.ServiceId == blockedService);
        Assert.All(ev.OfType<WantBlocked>().Where(b => b.ServiceId == blockedService),
            b => Assert.Equal(expected, b.Reason));
        Assert.Contains(ev, e => e is AllGuestsGone);   // the tavern still drains to empty
    }

    [Fact]
    public void Block_reason_room_closed()
    {
        var room = Room(1, cellX: 1, capacity: 4, Service("drink", ServiceKind.MenuConsumption, 4));
        var world = new GuestWorld(101, 250,
            Catalog(Type("dwarf", agenda: new[] { Want("drink", "ale") })),
            Scenario.Structure(2, room),
            new FakeRoomServiceState(new Dictionary<RoomId, (RoomStaffState, double)>
                { [new RoomId(1)] = (RoomStaffState.Closed, 0.0) }),
            new FakeTransactions(new Money(10)),
            new FakeAttractionContext(Attract(new[] { "dwarf" })));
        AssertBlocked(world, "drink", BlockReason.RoomClosed);
    }

    [Fact]
    public void Block_reason_sold_out()
    {
        var world = CleanWorld(totalCapacity: 2, roomCapacity: 4, aleStock: 0, serviceTicks: 250);
        AssertBlocked(world, "drink", BlockReason.SoldOut);
    }

    [Fact]
    public void Block_reason_no_such_service()
    {
        // The agenda wants "spa" but the only room offers "drink" — no provider anywhere.
        var world = CleanWorld(totalCapacity: 2, roomCapacity: 4, serviceTicks: 250,
            agenda: new[] { Want("spa", null) });
        AssertBlocked(world, "spa", BlockReason.NoSuchService);
    }

    [Fact]
    public void Block_reason_room_full()
    {
        // One 1-capacity room, long service, several guests ⇒ everyone but the occupant is turned away.
        var room = Room(1, cellX: 1, capacity: 1, Service("drink", ServiceKind.MenuConsumption, baseDuration: 40));
        var world = new GuestWorld(202, 250,
            Catalog(Type("dwarf", agenda: new[] { Want("drink", "ale") }, blockedWait: 10)),
            Scenario.Structure(4, room),
            FakeRoomServiceState.AllOpen(new[] { new RoomId(1) }),
            new FakeTransactions(new Money(10)),
            new FakeAttractionContext(Attract(new[] { "dwarf" })));

        var ev = DriveNight(CreateSut(world), serviceTicks: 250);
        var blocked = ev.OfType<WantBlocked>().Where(b => b.ServiceId == "drink").ToArray();
        Assert.True(blocked.Length > 0, "a single-capacity room under contention should turn guests away");
        Assert.All(blocked, b => Assert.Equal(BlockReason.RoomFull, b.Reason));
    }

    [Fact]
    public void Block_reason_room_inactive_after_mid_night_deactivation()
    {
        var room = Room(1, cellX: 1, capacity: 4, Service("drink", ServiceKind.MenuConsumption, 4));
        var structure = Scenario.Structure(2, room);
        var world = new GuestWorld(303, 400,
            Catalog(Type("dwarf", agenda: new[] { Want("drink", "ale") }, blockedWait: 500)),
            structure,
            FakeRoomServiceState.AllOpen(new[] { new RoomId(1) }),
            new FakeTransactions(new Money(10)),
            new FakeAttractionContext(Attract(new[] { "dwarf" })));

        var h = CreateSut(world);
        h.Commands.BeginService();
        for (var i = 0; i < 20; i++) h.Commands.Tick(1);

        structure.Deactivate(new RoomId(1));   // REQ-098: the only drink room goes inactive

        var after = new List<IDomainEvent>();
        for (var i = 0; i < 380; i++) after.AddRange(h.Commands.Tick(1));

        // After deactivation the want can no longer be fulfilled (firm invariant), and any block for
        // it reports a "room gone" reason. CON-005 does not pin whether a mid-night-deactivated room
        // is reported as RoomInactive or as NoSuchService (no active provider remains), so either is
        // accepted — but never a stale RoomClosed/RoomFull/SoldOut.
        Assert.DoesNotContain(after, e => e is WantFulfilled f && f.ServiceId == "drink");
        Assert.All(after.OfType<WantBlocked>().Where(b => b.ServiceId == "drink"),
            b => Assert.Contains(b.Reason, new[] { BlockReason.RoomInactive, BlockReason.NoSuchService }));
    }

    // ════════════════════════════════════════════════════════════
    //  Crowding table (REQ-009 / REQ-103)
    // ════════════════════════════════════════════════════════════

    // TotalGuestCapacity == 1 ⇒ the served guest is alone in the tavern, so the crowd ratio at
    // completion is exactly Occupants/Capacity = 1/roomCapacity, and the resulting satisfaction delta
    // is fully determined. Neutral scenarios add nothing, so FinalSatisfaction == the crowding delta.
    public static IEnumerable<object[]> CrowdingCases()
    {
        const double mag = 0.4;
        foreach (var roomCap in new[] { 1, 2, 8 })   // r = 1.0, 0.5, 0.125 (full / half / near-empty)
        {
            var r = 1.0 / roomCap;
            yield return new object[] { "loves", mag, roomCap, +mag * r };
            yield return new object[] { "neutral", mag, roomCap, 0.0 };
            yield return new object[] { "hates", mag, roomCap, -mag * r };
        }
    }

    [Theory]
    [MemberData(nameof(CrowdingCases))]
    public void Crowding_delta_signs_final_satisfaction(string pref, double mag, int roomCap, double expectedDelta)
    {
        var world = CleanWorld(totalCapacity: 1, roomCapacity: roomCap, crowdPref: pref, crowdMag: mag,
            wallet: 1000, serviceTicks: 300);
        var ev = DriveNight(CreateSut(world), serviceTicks: 300);

        var completed = ev.OfType<GuestLeft>().Where(g => g.Reason == LeaveReason.AgendaComplete).ToArray();
        Assert.True(completed.Length > 0, "at least one alone guest should complete its agenda");
        Assert.All(completed, g => Assert.Equal(expectedDelta, g.FinalSatisfaction, precision: 6));
    }

    // ════════════════════════════════════════════════════════════
    //  Satisfaction → payment modifier (REQ-023)
    // ════════════════════════════════════════════════════════════

    private static EmittedEffect Shock(GuestId g, double delta) =>
        new EmittedEffect.BehaviorEventTriggered(
            new RuleId("test"), 1L, new BehaviorOutcome.SatisfactionShock("test", delta), new[] { g }, null);

    private GuestWorld ModifierWorld(long seed) =>
        CleanWorld(seed: seed, totalCapacity: 1, roomCapacity: 8, crowdPref: "neutral", crowdMag: 0.0,
            wallet: 100000, alePrice: new Money(10), serviceTicks: 300,
            agenda: new[] { Want("drink", "ale"), Want("drink", "ale"), Want("drink", "ale") });

    [Fact]
    public void Modifier_is_one_at_neutral_satisfaction()
    {
        var world = ModifierWorld(seed: 11);
        var txns = (FakeTransactions)world.Transactions;
        DriveNight(CreateSut(world), serviceTicks: 300);

        Assert.True(txns.Requests.Count > 0, "guests should transact in a clean, affordable tavern");
        Assert.All(txns.Requests, r => Assert.Equal(1.0, r.SatisfactionModifier, precision: 6));
    }

    [Theory]
    [InlineData(+2.0, 1.5)]   // clamps to +1 satisfaction ⇒ 1 + 0.5·(+1) = 1.5
    [InlineData(-2.0, 0.5)]   // clamps to −1 satisfaction ⇒ 1 + 0.5·(−1) = 0.5
    public void Modifier_hits_its_bound_after_a_satisfaction_shock(double shockDelta, double expectedModifier)
    {
        var world = ModifierWorld(seed: 22);
        var txns = (FakeTransactions)world.Transactions;
        var h = CreateSut(world);

        h.Commands.BeginService();
        var g = AdvanceToFirstAgent(h);
        Assert.NotNull(g);

        // Shock before the guest has reached the room to transact, then let its whole agenda run.
        h.Commands.ApplyEffects(new[] { Shock(g!.Value, shockDelta) });
        var before = txns.Requests.Count;
        for (var i = 0; i < 300; i++) h.Commands.Tick(1);

        var post = txns.Requests.Skip(before).Where(r => r.Guest == g.Value).ToArray();
        Assert.True(post.Length > 0, "the shocked guest should still transact after the shock");
        Assert.All(post, r => Assert.Equal(expectedModifier, r.SatisfactionModifier, precision: 6));
    }

    [Fact]
    public void Every_transaction_modifier_stays_within_contract_bounds()
    {
        var world = CleanWorld(totalCapacity: 3, roomCapacity: 4, crowdPref: "hates", crowdMag: 1.0,
            wallet: 100000, serviceTicks: 250,
            agenda: new[] { Want("drink", "ale"), Want("drink", "ale") });
        var txns = (FakeTransactions)world.Transactions;
        DriveNight(CreateSut(world), serviceTicks: 250);

        Assert.All(txns.Requests, r => Assert.InRange(r.SatisfactionModifier, 0.5, 1.5));
    }

    // ════════════════════════════════════════════════════════════
    //  Effects in (ApplyEffects, REQ-042 / REQ-110)
    // ════════════════════════════════════════════════════════════

    private static EmittedEffect SpendBegan(GuestId g, double f, long ep = 1L) =>
        new EmittedEffect.SpendingMultiplierBegan(new RuleId("t"), ep, new[] { g }, f);
    private static EmittedEffect SpendEnded(long ep = 1L) =>
        new EmittedEffect.SpendingMultiplierEnded(new RuleId("t"), ep);
    private static EmittedEffect SatBegan(GuestId g, double rate, long ep = 2L) =>
        new EmittedEffect.SatisfactionModifierBegan(new RuleId("t"), ep, new[] { g }, rate);
    private static EmittedEffect SatEnded(long ep = 2L) =>
        new EmittedEffect.SatisfactionModifierEnded(new RuleId("t"), ep);
    private static EmittedEffect Leave(GuestId g) =>
        new EmittedEffect.BehaviorEventTriggered(new RuleId("t"), 3L, new BehaviorOutcome.GuestsLeave("brawl"), new[] { g }, null);
    private static EmittedEffect Burst(GuestId g, double f, int dur) =>
        new EmittedEffect.BehaviorEventTriggered(new RuleId("t"), 4L, new BehaviorOutcome.SpendingBurst("song", f, dur), new[] { g }, null);

    private static double SatOf(IGuestView v, GuestId g) => v.Agents.Single(a => a.Id == g).Satisfaction;

    private GuestWorld EffectWorld(long seed, int wants = 6, int baseDuration = 4) =>
        CleanWorld(seed: seed, totalCapacity: 1, roomCapacity: 8, crowdPref: "neutral", crowdMag: 0.0,
            wallet: 1_000_000, alePrice: new Money(10), serviceTicks: 400, baseDuration: baseDuration,
            agenda: Enumerable.Repeat(Want("drink", "ale"), wants).ToArray());

    [Fact]
    public void Spending_multiplier_began_is_carried_onto_transactions()
    {
        var world = EffectWorld(seed: 31);
        var txns = (FakeTransactions)world.Transactions;
        var h = CreateSut(world);

        h.Commands.BeginService();
        var g = AdvanceToFirstAgent(h);
        Assert.NotNull(g);
        h.Commands.ApplyEffects(new[] { SpendBegan(g!.Value, 1.5) });
        for (var i = 0; i < 400; i++) h.Commands.Tick(1);

        var reqs = txns.Requests.Where(r => r.Guest == g.Value).ToArray();
        Assert.True(reqs.Length > 0);
        Assert.All(reqs, r => Assert.Equal(1.5, r.SpendingMultiplier, precision: 6));
    }

    [Fact]
    public void Spending_multiplier_ended_restores_the_default_multiplier()
    {
        var world = EffectWorld(seed: 32);
        var txns = (FakeTransactions)world.Transactions;
        var h = CreateSut(world);

        h.Commands.BeginService();
        var g = AdvanceToFirstAgent(h);
        Assert.NotNull(g);
        h.Commands.ApplyEffects(new[] { SpendBegan(g!.Value, 1.5) });
        h.Commands.ApplyEffects(new[] { SpendEnded() });   // cancelled before any transaction
        for (var i = 0; i < 400; i++) h.Commands.Tick(1);

        var reqs = txns.Requests.Where(r => r.Guest == g.Value).ToArray();
        Assert.True(reqs.Length > 0);
        Assert.All(reqs, r => Assert.Equal(1.0, r.SpendingMultiplier, precision: 6));
    }

    [Fact]
    public void Satisfaction_modifier_drifts_while_active_then_holds_after_ended()
    {
        var world = CleanWorld(seed: 40, totalCapacity: 1, roomCapacity: 8, crowdPref: "neutral",
            crowdMag: 0.0, wallet: 1_000_000, serviceTicks: 400, baseDuration: 200,
            agenda: new[] { Want("drink", "ale") });
        var h = CreateSut(world);

        h.Commands.BeginService();
        var g = AdvanceToFirstAgent(h);
        Assert.NotNull(g);

        h.Commands.ApplyEffects(new[] { SatBegan(g!.Value, 0.02) });
        double? prev = null;
        for (var i = 0; i < 5; i++)
        {
            h.Commands.Tick(1);
            var sat = SatOf(h.View, g.Value);
            if (prev is double p) Assert.True(sat > p, $"drift should raise satisfaction ({p} → {sat})");
            prev = sat;
        }
        Assert.True(prev > 0, "positive drift should have raised satisfaction above 0");

        h.Commands.ApplyEffects(new[] { SatEnded() });
        h.Commands.Tick(1);                       // absorb the tick the drift is removed on
        var held = SatOf(h.View, g.Value);
        for (var i = 0; i < 3; i++)
        {
            h.Commands.Tick(1);
            Assert.Equal(held, SatOf(h.View, g.Value), precision: 6);
        }
    }

    [Fact]
    public void Behavior_guests_leave_removes_the_targeted_guest()
    {
        var world = EffectWorld(seed: 33, wants: 6, baseDuration: 200);   // long service ⇒ target stays present
        var h = CreateSut(world);

        h.Commands.BeginService();
        var g = AdvanceToFirstAgent(h);
        Assert.NotNull(g);

        var ev = new List<IDomainEvent>(h.Commands.ApplyEffects(new[] { Leave(g!.Value) }));
        ev.AddRange(h.Commands.Tick(1));

        Assert.Contains(ev, e => e is GuestLeft gl && gl.Id == g.Value && gl.Reason == LeaveReason.BehaviorEvent);
        Assert.DoesNotContain(h.View.Agents, a => a.Id == g.Value);
    }

    [Fact]
    public void Behavior_spending_burst_applies_a_multiplier_for_its_duration()
    {
        var world = EffectWorld(seed: 34);
        var txns = (FakeTransactions)world.Transactions;
        var h = CreateSut(world);

        h.Commands.BeginService();
        var g = AdvanceToFirstAgent(h);
        Assert.NotNull(g);
        h.Commands.ApplyEffects(new[] { Burst(g!.Value, 2.0, 1000) });   // covers the rest of the night
        for (var i = 0; i < 400; i++) h.Commands.Tick(1);

        var reqs = txns.Requests.Where(r => r.Guest == g.Value).ToArray();
        Assert.True(reqs.Length > 0);
        Assert.All(reqs, r => Assert.Equal(2.0, r.SpendingMultiplier, precision: 6));
    }

    [Fact]
    public void Behavior_satisfaction_shock_applies_an_immediate_delta()
    {
        var world = EffectWorld(seed: 35, wants: 6, baseDuration: 200);
        var h = CreateSut(world);

        h.Commands.BeginService();
        var g = AdvanceToFirstAgent(h);
        Assert.NotNull(g);
        Assert.Equal(0.0, SatOf(h.View, g!.Value), precision: 6);   // fresh guest starts at 0

        h.Commands.ApplyEffects(new[] { Shock(g!.Value, 0.5) });
        Assert.Equal(0.5, SatOf(h.View, g.Value), precision: 6);
    }

    // ════════════════════════════════════════════════════════════
    //  Lodger cycle (REQ-107)
    // ════════════════════════════════════════════════════════════

    private GuestWorld LodgingWorld(long seed = 50, int totalCapacity = 1) =>
        new(seed, 200,
            Catalog(Type("sleeper", agenda: new[] { Want("sleep", null) }, wallet: 1000)),
            Scenario.Structure(totalCapacity,
                Room(1, cellX: 1, capacity: 8, Service("sleep", ServiceKind.Lodging, 4))),
            FakeRoomServiceState.AllOpen(new[] { new RoomId(1) }),
            new FakeTransactions(new Money(10)),
            new FakeAttractionContext(Attract(new[] { "sleeper" })));

    private static readonly LodgerRecord SampleLodger =
        new(new GuestId(7), new GuestTypeId("sleeper"), new RoomId(1), new Money(50), 0.3);

    private static GuestsSnapshot LodgerSnapshot() =>
        new(1, new[] { SampleLodger }, Array.Empty<VipState>(), NextGuestIdValue: 8);

    [Fact]
    public void Restored_lodger_round_trips_through_a_snapshot()
    {
        var h = CreateSut(LodgingWorld());
        h.Commands.Restore(LodgerSnapshot());

        var snap = h.Commands.Capture();
        Assert.Equal(1, snap.SchemaVersion);
        var lodger = Assert.Single(snap.Lodgers);
        Assert.Equal(SampleLodger, lodger);   // Id / Type / room / wallet / satisfaction all preserved
    }

    [Fact]
    public void Restored_lodger_checks_out_at_the_next_begin_service()
    {
        var h = CreateSut(LodgingWorld());
        h.Commands.Restore(LodgerSnapshot());

        var events = h.Commands.BeginService();
        Assert.Contains(events, e => e is GuestLeft gl && gl.Id == new GuestId(7) && gl.Reason == LeaveReason.LodgingCheckout);
    }

    [Fact]
    public void End_night_never_drops_a_lodger_created_during_the_night()
    {
        // Organic lodgers are timing-dependent (a guest must reach + complete the lodging service),
        // so this asserts only the safe invariant: whatever lodgers exist at settlement still exist
        // after EndNight (EndNight keeps lodgers — REQ-107). Vacuously safe if none formed.
        var h = CreateSut(LodgingWorld(totalCapacity: 1));
        DriveNight(h, serviceTicks: 200);

        var atSettlement = h.Commands.Capture().Lodgers.Select(l => l.Id).ToHashSet();
        h.Commands.EndNight();
        var afterEndNight = h.Commands.Capture().Lodgers.Select(l => l.Id).ToHashSet();

        Assert.Superset(afterEndNight, atSettlement);   // EndNight keeps every settlement-time lodger
    }

    // ════════════════════════════════════════════════════════════
    //  VIPs (REQ-029 / REQ-050 / REQ-055)
    // ════════════════════════════════════════════════════════════

    private static readonly GuestTypeId Vip = new("critic");

    private GuestWorld VipWorld(long seed, long acclaim, double visitChance,
        int serviceTicks = 30, bool roomOpen = true, string crowdPref = "loves", double crowdMag = 0.4, int blockedWait = 20)
    {
        var vip = Type("critic", baseWeight: 0, isVip: true,
            agenda: new[] { Want("drink", "ale") }, crowdPref: crowdPref, crowdMag: crowdMag,
            blockedWait: blockedWait, wallet: 1000,
            vip: new VipSpec(visitChance, new[] { new VipCondition("lifetimeAcclaimAtLeast", null, 50) }));
        var room = Room(1, cellX: 1, capacity: 8, Service("drink", ServiceKind.MenuConsumption, 4));
        var rss = roomOpen
            ? FakeRoomServiceState.AllOpen(new[] { new RoomId(1) })
            : new FakeRoomServiceState(new Dictionary<RoomId, (RoomStaffState, double)> { [new RoomId(1)] = (RoomStaffState.Closed, 0.0) });
        return new GuestWorld(seed, serviceTicks, Catalog(vip), Scenario.Structure(3, room), rss,
            new FakeTransactions(new Money(10)),
            new FakeAttractionContext(Attract(new[] { "critic" }, arrivalRateFactor: 1.0, acclaim: acclaim)));
    }

    // One night: returns whether the VIP visited, plus all events, then closes the night.
    private static (bool Visited, List<IDomainEvent> Events) RunVipNight(IGuestSimTestHarness h, int serviceTicks)
    {
        var ev = new List<IDomainEvent>();
        ev.AddRange(h.Commands.BeginService());
        for (var i = 0; i < serviceTicks; i++) ev.AddRange(h.Commands.Tick(1));
        ev.AddRange(h.Commands.BeginDrain());
        for (var i = 0; i < serviceTicks * 3 && !ev.OfType<AllGuestsGone>().Any(); i++) ev.AddRange(h.Commands.Tick(1));
        ev.AddRange(h.Commands.EndNight());
        return (ev.OfType<VipVisited>().Any(v => v.Vip == Vip), ev);
    }

    [Fact]
    public void Vip_with_unmet_conditions_never_arrives_over_many_nights()
    {
        var h = CreateSut(VipWorld(seed: 1, acclaim: 0, visitChance: 0.5, serviceTicks: 12));   // acclaim < 50 ⇒ unmet
        var visits = 0;
        for (var night = 0; night < 1000; night++)
            if (RunVipNight(h, 12).Visited) visits++;
        Assert.Equal(0, visits);
    }

    [Fact]
    public void Vip_with_met_conditions_arrives_near_its_visit_chance()
    {
        var h = CreateSut(VipWorld(seed: 424242, acclaim: 100, visitChance: 0.15, serviceTicks: 12));   // met
        var visits = 0;
        for (var night = 0; night < 1000; night++)
            if (RunVipNight(h, 12).Visited) visits++;
        // Expected ≈ 150; a wide (~±5σ) band absorbs seed variance without ever being unsatisfiable.
        Assert.InRange(visits, 80, 220);
    }

    [Fact]
    public void Vip_is_marked_satisfied_only_when_departing_satisfaction_is_positive()
    {
        // Served in a "loves" room ⇒ positive crowd delta ⇒ VipSatisfied with FinalSatisfaction > 0.
        var satisfied = RunVipNight(CreateSut(
            VipWorld(seed: 7, acclaim: 100, visitChance: 1.0, serviceTicks: 80, roomOpen: true, crowdPref: "loves")), 80);
        Assert.True(satisfied.Visited);
        var sat = Assert.Single(satisfied.Events.OfType<VipSatisfied>());
        Assert.True(sat.FinalSatisfaction > 0);

        // Turned away at a closed room ⇒ blocked penalty ⇒ non-positive satisfaction ⇒ no VipSatisfied.
        var unsatisfied = RunVipNight(CreateSut(
            VipWorld(seed: 8, acclaim: 100, visitChance: 1.0, serviceTicks: 120, roomOpen: false, blockedWait: 20)), 120);
        Assert.True(unsatisfied.Visited);
        Assert.Empty(unsatisfied.Events.OfType<VipSatisfied>());
    }

    [Fact]
    public void Vip_can_revisit_on_a_later_night_after_an_unsatisfied_exit()
    {
        var h = CreateSut(VipWorld(seed: 9, acclaim: 100, visitChance: 1.0, serviceTicks: 120, roomOpen: false, blockedWait: 20));

        var night1 = RunVipNight(h, 120);
        Assert.True(night1.Visited);
        Assert.Empty(night1.Events.OfType<VipSatisfied>());   // left unsatisfied

        var night2 = RunVipNight(h, 120);
        Assert.True(night2.Visited, "an unsatisfied VIP is not barred from returning (REQ-055)");
    }
}
