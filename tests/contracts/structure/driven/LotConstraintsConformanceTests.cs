using System.Collections.Generic;
using System.Linq;
using TavernIdler.Domains.Structure;
using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Structure.Driven;

/// <summary>
/// CON-004 abstract conformance suite for <see cref="ILotConstraints"/> — the venue lot the
/// Structure domain builds within. A concrete subclass (venue bridge over CON-013, TKT-019/020)
/// supplies the implementation. Abstract ⇒ nothing runs until then.
/// </summary>
public abstract class LotConstraintsConformanceTests
{
    /// Build an <see cref="ILotConstraints"/> exposing <paramref name="lot"/>,
    /// <paramref name="entrance"/>, and <paramref name="terrain"/>.
    protected abstract ILotConstraints CreateLot(GridRect lot, CellCoord entrance, IReadOnlyList<TerrainFeature> terrain);

    private static readonly GridRect Lot = new(0, 0, 10, 6);
    private static readonly CellCoord Entrance = new(3, 0);

    private static IReadOnlyList<TerrainFeature> SampleTerrain() => new List<TerrainFeature>
    {
        new(new CellCoord(5, 0), new TerrainEffect.EnablesRoomType(new RoomTypeId("spa"))),
        new(new CellCoord(2, 1), new TerrainEffect.ModifiesRoom(new RoomStatModifier(1.0, 1.2, 1.0))),
    };

    [Fact]
    public void Entrance_is_on_the_ground_row_inside_the_lot()
    {
        var lot = CreateLot(Lot, Entrance, SampleTerrain());
        Assert.Equal(0, lot.Entrance.Y);                       // REQ-084: ground row
        Assert.True(lot.Lot.Contains(lot.Entrance));
    }

    [Fact]
    public void Terrain_cells_lie_within_the_lot()
    {
        var lot = CreateLot(Lot, Entrance, SampleTerrain());
        Assert.All(lot.Terrain, feature => Assert.True(lot.Lot.Contains(feature.Cell)));
    }

    [Fact]
    public void Reads_are_stable_across_repeated_access_within_a_run()
    {
        var lot = CreateLot(Lot, Entrance, SampleTerrain());
        Assert.Equal(lot.Lot, lot.Lot);
        Assert.Equal(lot.Entrance, lot.Entrance);
        Assert.Equal(
            lot.Terrain.Select(t => t.Cell).ToList(),
            lot.Terrain.Select(t => t.Cell).ToList());
    }
}
