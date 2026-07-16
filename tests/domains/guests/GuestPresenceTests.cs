using System.Linq;
using TavernIdler.Domains.Guests;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;
using TavernIdler.Tests.Contracts.Guests;
using static TavernIdler.Tests.Contracts.Guests.Scenario;

namespace TavernIdler.Tests.Domains.Guests;

/// <summary>
/// Unit tests for <see cref="IGuestPresence"/> (CON-005 / CON-012 bridge input). Not gated by a frozen
/// suite in this ticket (G11), so its prose is pinned here: <c>Room == null</c> while a guest walks the
/// circulation, the current room while it is served or lodging, and <c>Consuming</c> set only during a
/// MenuConsumption service (DOM006-Q1).
/// </summary>
public sealed class GuestPresenceTests
{
    private static GuestWorld World(ServiceKind kind, int baseDuration, string? menuItem) =>
        new(Seed: 7, ServiceDurationTicks: 300,
            Catalog(Type("dwarf", agenda: new[] { Want("stay", menuItem) })),
            Scenario.Structure(totalCapacity: 1, Room(1, cellX: 3, capacity: 4, Service("stay", kind, baseDuration))),
            FakeRoomServiceState.AllOpen(new[] { new RoomId(1) }),
            new FakeTransactions(new Money(10)),
            new FakeAttractionContext(Attract(new[] { "dwarf" })));

    private static GuestPresenceEntry? PresenceInActivity(GuestSimHarness h, GuestActivity activity)
    {
        for (var i = 0; i < 200; i++)
        {
            h.Commands.Tick(1);
            var agent = h.View.Agents.FirstOrDefault(a => a.Activity == activity);
            if (agent is not null)
                return h.Presence.CurrentPresence().Single(p => p.Id == agent.Id);
        }
        return null;
    }

    [Fact]
    public void A_walking_guest_reports_no_room_and_is_not_consuming()
    {
        var h = new GuestSimHarness(World(ServiceKind.MenuConsumption, baseDuration: 20, menuItem: "ale"));
        h.Commands.BeginService();

        var walking = PresenceInActivity(h, GuestActivity.Walking);

        Assert.NotNull(walking);
        Assert.Null(walking!.Room);          // walking the circulation ⇒ no room
        Assert.Null(walking.Consuming);
    }

    [Fact]
    public void A_menu_consumer_reports_its_room_and_the_item_it_is_consuming()
    {
        var h = new GuestSimHarness(World(ServiceKind.MenuConsumption, baseDuration: 40, menuItem: "ale"));
        h.Commands.BeginService();

        var served = PresenceInActivity(h, GuestActivity.BeingServed);

        Assert.NotNull(served);
        Assert.Equal(new RoomId(1), served!.Room);
        Assert.Equal(new MenuItemId("ale"), served.Consuming);   // DOM006-Q1
    }

    [Fact]
    public void A_lodger_reports_its_room_but_is_not_consuming()
    {
        var h = new GuestSimHarness(World(ServiceKind.Lodging, baseDuration: 4, menuItem: null));
        h.Commands.BeginService();

        var lodging = PresenceInActivity(h, GuestActivity.Lodging);

        Assert.NotNull(lodging);
        Assert.Equal(new RoomId(1), lodging!.Room);
        Assert.Null(lodging.Consuming);                          // lodging is not a menu consumption
    }
}
