using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Harvesting;

public sealed class HarvestPreviewsTests : IAsyncDisposable
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");

    private readonly TestPlay _play = new();

    public ValueTask DisposeAsync()
    {
        return _play.DisposeAsync();
    }

    private static string Say(GameText text)
    {
        var format = text.Many is { } many && text.Count != 1 ? many : text.Format;

        return string.Format(_english, format.In(_english), [.. text.Values.Select(value => value.Text ?? value.Id.ToString(_english))]);
    }

    private HarvestPreviews Previews(GraphicSkills? graphics = null)
    {
        return new HarvestPreviews(_play.World, _play.Tables, _play.Maps, new HarvestSightings(), graphics ?? new GraphicSkills(), _play.Places);
    }

    [Fact]
    public void ThePreviewCountsTheZonesMapsItsResourcesAndTheWayToTheNearestBank()
    {
        var preview = Previews().Of(new HarvestSettings { Resources = [TestPlay.Chestnut, TestPlay.Ash, 9_999], Area = 1 })!;

        // The inside without elements holds nothing: the tour leaves it out.
        Assert.Equal(
            ["4: maps to go through", "0: probable maps", "2 of 3: resources present", "1 map: to the nearest bank"],
            preview.Figures.Select(figure => $"{Say(figure.Value)}: {Say(figure.Label)}"));
        Assert.Equal(5, preview.Zone.Length);
        Assert.Equal(4, preview.Zone.Count(map => map.State is ZoneMapState.ToVisit));
        Assert.Contains(preview.Zone, map => map.MapId == TestPlay.OtherInsideMapId && map.State is ZoneMapState.Empty);
        Assert.Equal(TestPlay.MapId, preview.LandmarkMapId);
    }

    [Fact]
    public void ThePreviewShowsTheProbableMapsOnceTheHostLearnedTheirGraphics()
    {
        var graphics = new GraphicSkills();
        var previews = Previews(graphics);
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], Area = 1 };
        var before = previews.Of(settings)!;

        graphics.Learn(TestPlay.TreeGfxId, [TestPlay.TreeSkillId]);

        var after = previews.Of(settings)!;

        Assert.NotSame(before, after);
        Assert.Equal("2: probable maps", $"{Say(after.Figures[1].Value)}: {Say(after.Figures[1].Label)}");
        Assert.Equal(new[] { TestPlay.NorthMapId, TestPlay.MapId }.Order(), after.Zone.Where(map => map.State is ZoneMapState.Probable).Select(map => map.MapId).Order());
    }

    [Fact]
    public void SettingsNamingNoResourceOrNoPlaceHaveNoPreview()
    {
        var previews = Previews();

        Assert.Null(previews.Of(new HarvestSettings { Area = 1 }));
        Assert.Null(previews.Of(new HarvestSettings { Resources = [TestPlay.Ash] }));
    }

    [Fact]
    public void ThePreviewOfTheSameZoneAndResourcesIsKept()
    {
        var previews = Previews();
        var first = previews.Of(new HarvestSettings { Resources = [TestPlay.Ash], Area = 1 });

        Assert.Same(first, previews.Of(new HarvestSettings { Resources = [TestPlay.Ash], Area = 1, FullAt = 70 }));
        Assert.NotSame(first, previews.Of(new HarvestSettings { Resources = [TestPlay.Chestnut], Area = 1 }));
    }

    [Fact]
    public void WhenTheActivityChoosesThePlaceEachCharacterIsToldItsByItsLevelsAndTheFirstsIsDrawn()
    {
        var settings = new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Place = HarvestPlace.Automatic };
        PreviewPlayer[] players =
        [
            new("Ortie", 60, new Dictionary<int, int> { [TestPlay.Lumberjack] = 20 }, null),
            new("Kel", 5, new Dictionary<int, int> { [TestPlay.Lumberjack] = 1 }, null),
            new("New", 0, new Dictionary<int, int>(), null),
        ];
        var preview = Previews().Of(settings, players)!;

        // The sub-area is above the second's level: its monsters would attack it.
        Assert.Equal(["Ortie", "Kel", "New"], preview.PlayerNotes.Select(note => note.Name));
        Assert.StartsWith($"{TestPlay.SubAreaId}, ", Say(preview.PlayerNotes[0].Note), StringComparison.Ordinal);
        Assert.Equal("Nothing to harvest at its levels", Say(preview.PlayerNotes[1].Note));
        Assert.Equal("Levels not seen yet", Say(preview.PlayerNotes[2].Note));
        Assert.NotEmpty(preview.Zone);
        Assert.Contains(preview.Figures, figure => Say(figure.Label) == "resources in the zone");
    }

    [Fact]
    public void WithoutACharacterTheChosenPlaceIsSaidAutomatic()
    {
        var preview = Previews().Of(new HarvestSettings { Mode = HarvestMode.ByProfession, Jobs = [TestPlay.Lumberjack], Place = HarvestPlace.Automatic })!;

        Assert.Empty(preview.Zone);
        Assert.Equal(["Auto: place chosen for each character"], preview.Figures.Select(figure => $"{Say(figure.Value)}: {Say(figure.Label)}"));
        Assert.Null(Previews().Of(new HarvestSettings { Mode = HarvestMode.ByProfession, Place = HarvestPlace.Automatic }));
    }
}
