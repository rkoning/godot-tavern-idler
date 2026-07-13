using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Structure;

/// <summary>
/// CON-003 conformance: the normative <see cref="TraversalGraph.Neighbors"/> edge rule.
///
/// Edges: horizontal (same row, adjacent column) exist when BOTH cells are walkable;
/// vertical (same column, adjacent row) exist ONLY when BOTH cells are stairs.
/// <see cref="TraversalGraph"/> is a concrete value record owned by the contract, so these
/// tests run directly here (no SUT/subclass required).
/// </summary>
public class TraversalGraphConformanceTests
{
    private static TraversalGraph Graph(
        IEnumerable<CellCoord> walkable,
        IEnumerable<CellCoord>? stairs = null,
        int version = 1) =>
        new TraversalGraph(
            version,
            new HashSet<CellCoord>(walkable),
            new Dictionary<CellCoord, RoomId>(),
            new HashSet<CellCoord>(stairs ?? Enumerable.Empty<CellCoord>()));

    private static CellCoord[] Neighbors(TraversalGraph g, CellCoord c) =>
        g.Neighbors(c).OrderBy(n => n.X).ThenBy(n => n.Y).ToArray();

    [Fact]
    public void Horizontal_neighbor_when_both_walkable()
    {
        var g = Graph(new[] { new CellCoord(0, 0), new CellCoord(1, 0) });
        Assert.Equal(new[] { new CellCoord(1, 0) }, Neighbors(g, new CellCoord(0, 0)));
        Assert.Equal(new[] { new CellCoord(0, 0) }, Neighbors(g, new CellCoord(1, 0)));
    }

    [Fact]
    public void No_horizontal_edge_when_neighbor_not_walkable()
    {
        var g = Graph(new[] { new CellCoord(0, 0) });
        Assert.Empty(g.Neighbors(new CellCoord(0, 0)));
    }

    [Fact]
    public void Both_horizontal_neighbors_returned()
    {
        var g = Graph(new[] { new CellCoord(0, 0), new CellCoord(1, 0), new CellCoord(2, 0) });
        Assert.Equal(
            new[] { new CellCoord(0, 0), new CellCoord(2, 0) },
            Neighbors(g, new CellCoord(1, 0)));
    }

    [Fact]
    public void Non_walkable_source_cell_has_no_neighbors()
    {
        // Cell (5,5) itself is not walkable even though its horizontal neighbor is.
        var g = Graph(new[] { new CellCoord(6, 5) });
        Assert.Empty(g.Neighbors(new CellCoord(5, 5)));
    }

    [Fact]
    public void Vertical_edge_only_between_two_stairs()
    {
        // Two vertically-adjacent stair cells (also walkable, as circulation is).
        var cells = new[] { new CellCoord(3, 0), new CellCoord(3, 1) };
        var g = Graph(cells, stairs: cells);
        Assert.Equal(new[] { new CellCoord(3, 1) }, Neighbors(g, new CellCoord(3, 0)));
        Assert.Equal(new[] { new CellCoord(3, 0) }, Neighbors(g, new CellCoord(3, 1)));
    }

    [Fact]
    public void No_vertical_edge_when_upper_cell_is_walkable_but_not_a_stair()
    {
        // Lower cell is a stair; upper cell is walkable (e.g. a room) but not a stair.
        // Per the rule vertical edges require BOTH to be stairs, so no edge.
        var walkable = new[] { new CellCoord(3, 0), new CellCoord(3, 1) };
        var g = Graph(walkable, stairs: new[] { new CellCoord(3, 0) });
        Assert.Empty(g.Neighbors(new CellCoord(3, 0)).Where(n => n.Y != 0));
        Assert.Empty(g.Neighbors(new CellCoord(3, 1)));
    }

    [Fact]
    public void No_vertical_edge_between_two_non_stair_walkables()
    {
        // Two stacked room cells, neither a stair: not vertically connected.
        var walkable = new[] { new CellCoord(3, 0), new CellCoord(3, 1) };
        var g = Graph(walkable);
        Assert.Empty(g.Neighbors(new CellCoord(3, 0)));
        Assert.Empty(g.Neighbors(new CellCoord(3, 1)));
    }

    [Fact]
    public void Stair_cell_still_has_horizontal_walkable_edges()
    {
        // A stair cell is walkable and neighbors a walkable room cell horizontally,
        // and a stair cell above it vertically.
        var walkable = new[]
        {
            new CellCoord(3, 0), new CellCoord(4, 0), new CellCoord(3, 1)
        };
        var stairs = new[] { new CellCoord(3, 0), new CellCoord(3, 1) };
        var g = Graph(walkable, stairs: stairs);
        Assert.Equal(
            new[] { new CellCoord(3, 1), new CellCoord(4, 0) },
            Neighbors(g, new CellCoord(3, 0)));
    }

    [Fact]
    public void Neighbors_are_not_self_or_diagonal()
    {
        var walkable = new[]
        {
            new CellCoord(0, 0), new CellCoord(1, 1) // diagonal pair
        };
        var g = Graph(walkable);
        Assert.Empty(g.Neighbors(new CellCoord(0, 0)));
    }
}
