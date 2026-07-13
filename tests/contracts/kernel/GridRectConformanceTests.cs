using TavernIdler.Kernel;

namespace TavernIdler.Tests.Contracts.Kernel;

// CON-001 conformance: GridRect.Contains edge inclusivity and Area.
public class GridRectConformanceTests
{
    // Rect covers columns [2,6) and rows [3,8).
    private static readonly GridRect Rect = new(2, 3, 4, 5);

    [Fact]
    public void Area_is_width_times_height()
    {
        Assert.Equal(20, Rect.Area);
        Assert.Equal(1, new GridRect(0, 0, 1, 1).Area);
    }

    [Theory]
    [InlineData(2, 3, true)]    // bottom-left corner (origin) inclusive
    [InlineData(5, 7, true)]    // top-right interior cell
    [InlineData(6, 3, false)]   // right edge exclusive (X == X+Width)
    [InlineData(2, 8, false)]   // top edge exclusive (Y == Y+Height)
    [InlineData(1, 3, false)]   // left of rect
    [InlineData(2, 2, false)]   // below rect
    [InlineData(6, 8, false)]   // far corner exclusive
    public void Contains_respects_inclusive_exclusive_bounds(int x, int y, bool expected)
    {
        Assert.Equal(expected, Rect.Contains(new CellCoord(x, y)));
    }
}
