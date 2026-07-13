using System;
using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Traits;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Traits.Driven;

/// <summary>
/// CON-012 (Traits Driven Ports v1.0) abstract conformance suite for <see cref="IPresenceSource"/> —
/// the per-tick presence snapshot the rule engine evaluates. The presence bridge (composing CON-005
/// guest presence, CON-009 staff presence, CON-003 rooms, CON-008 menu traits) is implemented and
/// subclassed by the bridge ticket (TKT-019); abstract ⇒ nothing runs until then.
///
/// The suite scripts scenarios in engine-neutral terms (<see cref="PresenceScenario"/>) so it never
/// references the not-yet-frozen source contracts; the concrete harness maps a scenario onto stubbed
/// sources and returns the real bridge. Expected carriers are derived here from the CON-012
/// composition + ordering + InBroadcaster rules, so a passing bridge implements exactly those rules.
/// </summary>
public abstract class PresenceSourceConformanceTests
{
    /// Build a harness whose <see cref="IPresenceSource"/> composes the stubbed sources described by
    /// <paramref name="initial"/>. <see cref="IPresenceSourceHarness.Update"/> mutates that stub state.
    protected abstract IPresenceSourceHarness CreateHarness(PresenceScenario initial);

    // ── engine-neutral scripting surface ────────────────────────
    public interface IPresenceSourceHarness
    {
        IPresenceSource Source { get; }
        void Update(PresenceScenario scenario);
    }

    public sealed record PresenceScenario(
        IReadOnlyList<GuestSpec> Guests,
        IReadOnlyList<EmployeeSpec> Employees,
        IReadOnlyList<RoomSpec> Rooms,
        IReadOnlyList<ConsumptionSpec> Consumptions)
    {
        public static PresenceScenario Of(
            IEnumerable<GuestSpec>? guests = null,
            IEnumerable<EmployeeSpec>? employees = null,
            IEnumerable<RoomSpec>? rooms = null,
            IEnumerable<ConsumptionSpec>? consumptions = null) =>
            new(
                (guests ?? Enumerable.Empty<GuestSpec>()).ToArray(),
                (employees ?? Enumerable.Empty<EmployeeSpec>()).ToArray(),
                (rooms ?? Enumerable.Empty<RoomSpec>()).ToArray(),
                (consumptions ?? Enumerable.Empty<ConsumptionSpec>()).ToArray());
    }

    public sealed record GuestSpec(GuestId Id, RoomId? Room, IReadOnlyList<TraitId> Traits);
    public sealed record EmployeeSpec(EmployeeId Id, RoomId Room, IReadOnlyList<TraitId> Traits);
    public sealed record RoomSpec(RoomId Id, bool Active, bool Broadcaster, IReadOnlyList<TraitId> Traits);
    public sealed record ConsumptionSpec(GuestId Guest, MenuItemId Item, IReadOnlyList<TraitId> ItemTraits);

    private static IReadOnlyList<TraitId> T(params string[] ids) => ids.Select(s => new TraitId(s)).ToArray();

    /// The carrier list CON-012 requires the bridge to produce for <paramref name="s"/>.
    private static IReadOnlyList<PresentCarrier> Expected(PresenceScenario s)
    {
        bool Broadcast(RoomId? room) =>
            room is { } r && s.Rooms.Any(rs => rs.Id == r && rs.Active && rs.Broadcaster);

        var carriers = new List<PresentCarrier>();
        foreach (var g in s.Guests.OrderBy(g => g.Id.Value))
            carriers.Add(new PresentCarrier(new CarrierRef.Guest(g.Id), g.Room, true, g.Traits, Broadcast(g.Room)));
        foreach (var e in s.Employees.OrderBy(e => e.Id.Value))
            carriers.Add(new PresentCarrier(new CarrierRef.Employee(e.Id), e.Room, false, e.Traits, Broadcast(e.Room)));
        foreach (var r in s.Rooms.Where(r => r.Active && r.Traits.Count > 0).OrderBy(r => r.Id.Value))
            carriers.Add(new PresentCarrier(new CarrierRef.Room(r.Id), r.Id, false, r.Traits, Broadcast(r.Id)));
        foreach (var c in s.Consumptions.OrderBy(c => c.Guest.Value))
        {
            var room = s.Guests.First(g => g.Id == c.Guest).Room;
            carriers.Add(new PresentCarrier(new CarrierRef.ConsumedItem(c.Item, c.Guest), room, false, c.ItemTraits, Broadcast(room)));
        }
        return carriers;
    }

    private static void AssertCarriers(IReadOnlyList<PresentCarrier> expected, PresenceSnapshot actual)
    {
        Assert.Equal(expected.Count, actual.Carriers.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var a = actual.Carriers[i];
            Assert.Equal(e.Ref, a.Ref);
            Assert.Equal(e.Room, a.Room);
            Assert.Equal(e.IsGuest, a.IsGuest);
            Assert.Equal(e.InBroadcaster, a.InBroadcaster);
            Assert.Equal(e.Traits, a.Traits);
        }
    }

    private static RoomId Rm(int n) => new(n);

    // ════════════════════════════════════════════════════════════
    //  Composition + ordering + InBroadcaster
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Composes_all_carrier_kinds_in_deterministic_order()
    {
        // Rooms: 1 active+broadcaster (trait), 2 active but no traits (no room carrier), 3 inactive+broadcaster.
        // Specs are supplied out of id order to exercise the required sort.
        var s = PresenceScenario.Of(
            guests: new[]
            {
                new GuestSpec(new GuestId(5), Rm(3), T("outlaw")),   // in the inactive broadcaster room
                new GuestSpec(new GuestId(1), null, T("rowdy")),     // walking
                new GuestSpec(new GuestId(2), Rm(1), T("bard")),     // in the active broadcaster room
            },
            employees: new[]
            {
                new EmployeeSpec(new EmployeeId(2), Rm(1), T("lawful")),
                new EmployeeSpec(new EmployeeId(1), Rm(2), T("stern")),
            },
            rooms: new[]
            {
                new RoomSpec(Rm(2), Active: true, Broadcaster: false, T()),          // empty traits ⇒ no carrier
                new RoomSpec(Rm(1), Active: true, Broadcaster: true, T("cozy")),
                new RoomSpec(Rm(3), Active: false, Broadcaster: true, T("grand")),   // inactive ⇒ no carrier
            },
            consumptions: new[]
            {
                new ConsumptionSpec(new GuestId(2), new MenuItemId("ale"), T("strong")),
            });

        var h = CreateHarness(s);
        AssertCarriers(Expected(s), h.Source.Current());
    }

    // ════════════════════════════════════════════════════════════
    //  Inactive rooms — excluded as carriers and for InBroadcaster
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Inactive_room_is_not_a_carrier_and_does_not_broadcast_to_occupants()
    {
        var s = PresenceScenario.Of(
            guests: new[] { new GuestSpec(new GuestId(1), Rm(9), T("bard")) },
            rooms: new[] { new RoomSpec(Rm(9), Active: false, Broadcaster: true, T("grand")) });

        var snapshot = CreateHarness(s).Source.Current();

        Assert.DoesNotContain(snapshot.Carriers, c => c.Ref is CarrierRef.Room);
        var guest = Assert.Single(snapshot.Carriers, c => c.Ref is CarrierRef.Guest);
        Assert.False(guest.InBroadcaster);
    }

    // ════════════════════════════════════════════════════════════
    //  Walking guests
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Walking_guest_has_null_room_and_is_not_in_broadcaster()
    {
        var s = PresenceScenario.Of(guests: new[] { new GuestSpec(new GuestId(1), null, T("rowdy")) });

        var guest = Assert.Single(CreateHarness(s).Source.Current().Carriers);
        Assert.Null(guest.Room);
        Assert.False(guest.InBroadcaster);
    }

    // ════════════════════════════════════════════════════════════
    //  Consumed items
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Consumed_item_appears_only_while_consumed_and_carries_item_traits_in_the_guests_room()
    {
        var withConsumption = PresenceScenario.Of(
            guests: new[] { new GuestSpec(new GuestId(1), Rm(4), T("bard")) },
            rooms: new[] { new RoomSpec(Rm(4), Active: true, Broadcaster: false, T()) },
            consumptions: new[] { new ConsumptionSpec(new GuestId(1), new MenuItemId("ale"), T("strong")) });

        var h = CreateHarness(withConsumption);

        var item = Assert.Single(h.Source.Current().Carriers, c => c.Ref is CarrierRef.ConsumedItem);
        Assert.Equal(new CarrierRef.ConsumedItem(new MenuItemId("ale"), new GuestId(1)), item.Ref);
        Assert.Equal(Rm(4), item.Room);                    // the consuming guest's room
        Assert.Equal(T("strong"), item.Traits);
        Assert.False(item.IsGuest);

        // Consumption ends → the item carrier disappears.
        h.Update(PresenceScenario.Of(
            guests: new[] { new GuestSpec(new GuestId(1), Rm(4), T("bard")) },
            rooms: new[] { new RoomSpec(Rm(4), Active: true, Broadcaster: false, T()) }));
        Assert.DoesNotContain(h.Source.Current().Carriers, c => c.Ref is CarrierRef.ConsumedItem);
    }

    // ════════════════════════════════════════════════════════════
    //  Freshness / no hidden mutation
    // ════════════════════════════════════════════════════════════

    [Fact]
    public void Repeated_calls_are_stable_then_reflect_an_update()
    {
        var s = PresenceScenario.Of(guests: new[] { new GuestSpec(new GuestId(1), Rm(1), T("rowdy")) });
        var h = CreateHarness(s);

        AssertCarriers(Expected(s), h.Source.Current());
        AssertCarriers(Expected(s), h.Source.Current());   // two consecutive calls equal (no hidden mutation)

        var s2 = PresenceScenario.Of(guests: new[]
        {
            new GuestSpec(new GuestId(1), Rm(1), T("rowdy")),
            new GuestSpec(new GuestId(2), Rm(1), T("bard")),
        });
        h.Update(s2);
        AssertCarriers(Expected(s2), h.Source.Current());   // reflects the change on the next call
    }
}
