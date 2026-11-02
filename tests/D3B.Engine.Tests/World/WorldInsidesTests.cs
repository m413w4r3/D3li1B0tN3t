using D3B.Engine.World;

namespace D3B.Engine.Tests.World;

public sealed class WorldInsidesTests
{
    private const long Front = 1;
    private const long Bank = 2;
    private const long House = 3;
    private const long Street = 4;
    private const long Elsewhere = 5;

    // The map in front of a bank, as some are: two insides at its coordinates, a door to another map of the world, and a
    // scroll to the street.
    private static TestWorld Town()
    {
        return new TestWorld()
            .Place(Front, 4, -18).Place(Bank, 4, -18, -1).Place(House, 4, -18, -1).Place(Street, 5, -18).Place(Elsewhere, 4, -17, -1)
            .Interactive(Front, Bank, 288, element: 10)
            .Interactive(Front, House, 300, criterion: "Qa=404", element: 11)
            .Interactive(Front, Elsewhere, 310, element: 12)
            .Scroll(Front, Street, 0)
            .MapAction(Bank, Front, 400);
    }

    [Fact]
    public void TheInsidesAreTheMapsTheDoorsLeadIntoAtTheMapsCoordinates()
    {
        var town = Town();

        Assert.Equal([Bank, House], WorldInsides.Of(town.Graph(), town.Atlas(), Front));
    }

    [Fact]
    public void AnInteractiveTransitionOtherThanADoorLeadsIntoNoInside()
    {
        var world = new TestWorld().Place(Front, 4, -18).Place(Bank, 4, -18, -1).Interactive(Front, Bank, 288, skill: 114);

        Assert.Empty(WorldInsides.Of(world.Graph(), world.Atlas(), Front));
    }

    [Fact]
    public void AMapWithoutDoorsOrAPlaceHasNoInside()
    {
        var town = Town();

        Assert.Empty(WorldInsides.Of(town.Graph(), town.Atlas(), Street));
        Assert.Empty(WorldInsides.Of(town.Graph(), town.Atlas(), 42));
    }
}
