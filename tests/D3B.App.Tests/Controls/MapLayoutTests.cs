using Avalonia;
using D3B.App.Controls;
using D3B.Data.Maps;
using D3B.Engine.Maps;

namespace D3B.App.Tests.Controls;

public sealed class MapLayoutTests
{
    private static readonly MapLayout _layout = MapLayout.Fit(new Size(1000, 700));

    [Fact]
    public void TheMapKeepsItsProportionsCentredWithRoomForTheLabels()
    {
        var bounds = _layout.Bounds;

        Assert.Equal(_layout.CellWidth / 2, _layout.CellHeight);
        Assert.Equal(14.5 / 10.25, bounds.Width / bounds.Height, 6);
        Assert.Equal(1000 - bounds.Right, bounds.Left, 6);
        Assert.Equal(700 - bounds.Bottom, bounds.Top, 6);
        Assert.True(bounds.Left >= MapLayout.SideRoom && bounds.Top >= MapLayout.EdgeRoom);
        Assert.True(bounds.Left - MapLayout.SideRoom < 1e-9 || bounds.Top - MapLayout.EdgeRoom < 1e-9, "The map fills the room it has one way.");
    }

    [Fact]
    public void EachCellIsFoundAtItsCentreAndNearItsCorners()
    {
        var quarter = _layout.CellWidth / 4;

        for (var cell = 0; cell < MapData.CellCount; cell++)
        {
            var center = _layout.Center(cell);

            Assert.Equal(cell, _layout.CellAt(center));
            Assert.Equal(cell, _layout.CellAt(new Point(center.X + quarter, center.Y)));
            Assert.Equal(cell, _layout.CellAt(new Point(center.X, center.Y - (_layout.CellHeight / 4))));
        }
    }

    [Fact]
    public void CellsLieAsTheGeometrySaysOddRowsHalfACellEast()
    {
        Assert.Equal(_layout.Center(0).X + (_layout.CellWidth / 2), _layout.Center(MapGeometry.Columns).X, 6);
        Assert.Equal(_layout.Center(0).Y + (_layout.CellHeight / 2), _layout.Center(MapGeometry.Columns).Y, 6);

        // A neighbour east lies a cell to the right, one south two rows below, as the client draws them.
        Assert.Equal(_layout.Center(100).X + _layout.CellWidth, _layout.Center(MapGeometry.Neighbour(100, 0)).X, 6);
        Assert.Equal(_layout.Center(100).Y + _layout.CellHeight, _layout.Center(MapGeometry.Neighbour(100, 2)).Y, 6);
    }

    [Fact]
    public void APointOffTheMapIsNoCell()
    {
        var bounds = _layout.Bounds;

        Assert.Equal(-1, _layout.CellAt(new Point(bounds.Left - 5, bounds.Top + 5)));
        Assert.Equal(-1, _layout.CellAt(new Point(bounds.Right + 5, bounds.Bottom - 5)));
        Assert.Equal(-1, _layout.CellAt(new Point(bounds.Left + 1, bounds.Top + 1)));
        Assert.Equal(-1, MapLayout.Fit(new Size(10, 10)).CellAt(new Point(5, 5)));
    }
}
