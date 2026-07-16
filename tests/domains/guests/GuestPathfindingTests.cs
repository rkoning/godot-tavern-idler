using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Guests;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Domains.Guests;

/// <summary>
/// Unit tests for <see cref="GuestPathfinding"/> — the BFS the guest sim uses to walk to the nearest
/// room offering a wanted service (CON-005 agenda: "nearest by path length; ties → lowest RoomId").
/// </summary>
public sealed class GuestPathfindingTests
{
    /// A ground-row corridor [0..length) with each (room→cell) mapping applied.
    private static TraversalGraph Row(int length, params (int cellX, int roomId)[] rooms)
    {
        var walk = new HashSet<CellCoord>();
        for (var x = 0; x < length; x++) walk.Add(new CellCoord(x, 0));
        var roomAtCell = rooms.ToDictionary(r => new CellCoord(r.cellX, 0), r => new RoomId(r.roomId));
        return new TraversalGraph(1, walk, roomAtCell, new HashSet<CellCoord>());
    }

    [Fact]
    public void Paths_to_the_nearest_target_room_excluding_the_start_cell()
    {
        var graph = Row(6, (2, 2), (4, 4));

        var path = GuestPathfinding.PathToNearestRoom(graph, new CellCoord(0, 0), new HashSet<RoomId> { new(2), new(4) });

        Assert.NotNull(path);
        Assert.Equal(new[] { new CellCoord(1, 0), new CellCoord(2, 0) }, path);   // reaches room 2, start excluded
    }

    [Fact]
    public void Breaks_equal_distance_ties_by_lowest_room_id()
    {
        var graph = Row(6, (1, 1), (5, 5));   // room 1 at cell (1,0), room 5 at cell (5,0); both 2 from start (3,0)

        var path = GuestPathfinding.PathToNearestRoom(graph, new CellCoord(3, 0), new HashSet<RoomId> { new(5), new(1) });

        Assert.NotNull(path);
        Assert.Equal(new CellCoord(1, 0), path!.Last());   // room 1 (lower id) wins the tie, at cell (1,0)
    }

    [Fact]
    public void Returns_null_when_no_target_room_is_reachable()
    {
        var graph = Row(4, (1, 1));

        var path = GuestPathfinding.PathToNearestRoom(graph, new CellCoord(0, 0), new HashSet<RoomId> { new(9) });

        Assert.Null(path);   // room 9 does not exist in the graph
    }

    [Fact]
    public void Empty_path_when_already_standing_on_the_target_room_cell()
    {
        var graph = Row(4, (2, 2));

        var path = GuestPathfinding.PathToNearestRoom(graph, new CellCoord(2, 0), new HashSet<RoomId> { new(2) });

        Assert.NotNull(path);
        Assert.Empty(path!);   // already there
    }
}
