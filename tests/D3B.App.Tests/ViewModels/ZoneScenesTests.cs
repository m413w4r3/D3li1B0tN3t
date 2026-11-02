using Avalonia;
using D3B.App.Controls;
using D3B.App.ViewModels;
using D3B.Bot.Activities;
using D3B.Data.World;

namespace D3B.App.Tests.ViewModels;

public sealed class ZoneScenesTests
{
    /// <summary>A strip of four maps of the world map at y = 0, an inside sharing the first's coordinates, and a map far away.</summary>
    private static WorldAtlas Atlas()
    {
        MapPosition[] positions =
        [
            new(1, 0, 0, 10, 1, 0, 0),
            new(2, 1, 0, 10, 1, 0, 0),
            new(3, 2, 0, 11, 1, 0, 0),
            new(4, 3, 0, 12, 1, 0, 0),
            new(5, 0, 0, 10, -1, 0, 0),
            new(6, 30, 0, 12, 1, 0, 0),
        ];

        return new WorldAtlas(positions, Array.Empty<SubArea>(), Array.Empty<Area>(), Array.Empty<Waypoint>(), Array.Empty<int>(), Array.Empty<int>());
    }

    [Fact]
    public void AZonesMapsAreTintedByTheirPartWithTheMapsAroundDim()
    {
        var scene = ZoneScenes.Of([new ZoneMap(1, ZoneMapState.ToVisit, 0), new ZoneMap(3, ZoneMapState.ToVisit, 1)], Atlas(), tour: false, landmark: 4)!;

        Assert.Equal(
            [new ZoneSquare(0, 0, ZoneSquareKind.GroupA), new ZoneSquare(1, 0, ZoneSquareKind.Context), new ZoneSquare(2, 0, ZoneSquareKind.GroupB), new ZoneSquare(3, 0, ZoneSquareKind.Context)],
            scene.Squares);
        Assert.Equal(new PixelPoint(3, 0), scene.Landmark);
    }

    [Fact]
    public void ATourShowsWhereItStandsAndALandmarkFarAwayIsLeftOut()
    {
        var scene = ZoneScenes.Of(
            [new ZoneMap(1, ZoneMapState.Visited), new ZoneMap(5, ZoneMapState.ToVisit), new ZoneMap(2, ZoneMapState.Empty)],
            Atlas(),
            tour: true,
            landmark: 6,
            character: 2)!;

        // The inside shares the first map's coordinates: the map the tour still goes through shows there.
        Assert.Equal([new ZoneSquare(0, 0, ZoneSquareKind.ToVisit), new ZoneSquare(1, 0, ZoneSquareKind.Empty)], scene.Squares);
        Assert.Null(scene.Landmark);
        Assert.Equal(new PixelPoint(1, 0), scene.Character);
    }

    [Fact]
    public void AProbableMapShowsOnATourAndIsMarkedOverItsPartsColour()
    {
        ZoneMap[] zone = [new(1, ZoneMapState.ToVisit), new(5, ZoneMapState.Probable), new(2, ZoneMapState.Empty, 1), new(3, ZoneMapState.Probable, 1)];

        // The inside shares the first map's coordinates: the probable one shows there.
        Assert.Equal(
            [new ZoneSquare(0, 0, ZoneSquareKind.Probable), new ZoneSquare(1, 0, ZoneSquareKind.Empty), new ZoneSquare(2, 0, ZoneSquareKind.Probable)],
            ZoneScenes.Of(zone, Atlas(), tour: true)!.Squares);
        Assert.Equal(
            [new ZoneSquare(0, 0, ZoneSquareKind.GroupA, IsProbable: true), new ZoneSquare(1, 0, ZoneSquareKind.Empty), new ZoneSquare(2, 0, ZoneSquareKind.GroupB, IsProbable: true)],
            ZoneScenes.Of(zone, Atlas(), tour: false)!.Squares);
    }

    [Fact]
    public void AZoneOfNoPlacedMapHasNoScene()
    {
        Assert.Null(ZoneScenes.Of([new ZoneMap(99, ZoneMapState.ToVisit)], Atlas(), tour: true));
    }
}
