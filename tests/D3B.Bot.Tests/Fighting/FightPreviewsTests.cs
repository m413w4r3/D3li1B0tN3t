using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Fighting;
using D3B.Bot.Localization;
using D3B.Bot.Settings;
using D3B.Bot.Tests.Runtime;

namespace D3B.Bot.Tests.Fighting;

/// <summary>What fighting settings lead to, for the profile editor (D245): the ground's maps, its monsters and their levels, its bank, and the place for each character.</summary>
public sealed class FightPreviewsTests : IAsyncDisposable
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

    [Fact]
    public void ThePreviewCountsTheGroundsMapsItsMonstersAndTheWayToTheNearestBank()
    {
        var previews = new FightPreviews(_play.World, _play.Tables);
        var settings = new FightSettings { Mode = FightMode.Chosen, Monsters = [TestPlay.LarvaId, 9_999], Place = FightPlace.Chosen, SubAreas = [TestPlay.SubAreaId] };
        var preview = previews.Of(settings)!;
        var ground = HuntingGround.Of(settings, _play.World.Atlas, _play.World.Graph, _play.Tables.Monsters, 1, subscribed: true);

        Assert.Equal(
            [$"{ground.Maps.Count}: maps to go through", "1 of 2: monsters chosen living there", "10 to 14: levels of the monsters", "1 map: to the nearest bank"],
            preview.Figures.Select(figure => $"{Say(figure.Value)}: {Say(figure.Label)}"));
        Assert.Equal(ground.Maps.Keys.Order(), preview.Zone.Select(map => map.MapId).Order());
        Assert.All(preview.Zone, map => Assert.Equal(ZoneMapState.ToVisit, map.State));
        Assert.Equal(TestPlay.MapId, preview.LandmarkMapId);
        Assert.Null(preview.Message);
        Assert.Same(preview, previews.Of(settings));
        Assert.Null(previews.Of(new FightSettings { Place = FightPlace.Chosen, Area = 404 }));
    }

    [Fact]
    public void ThePlaceTheActivityChoosesIsSaidForEachCharacterByItsLevel()
    {
        var previews = new FightPreviews(_play.World, _play.Tables);
        PreviewPlayer[] players = [new("Ortie", 20, new Dictionary<int, int>(), null), new("Unseen", 0, new Dictionary<int, int>(), null), new("Novice", 5, new Dictionary<int, int>(), null)];
        var preview = previews.Of(new FightSettings(), players)!;
        var ground = HuntingGround.Of(new FightSettings(), _play.World.Atlas, _play.World.Graph, _play.Tables.Monsters, 20, subscribed: true);

        // The larvae's sub-area, of fewer maps than the activity asks, is still the only one (D245); none at level 5, below it.
        Assert.Equal(
            [$"Ortie: {TestPlay.SubAreaId}, {ground.Maps.Count} maps", "Unseen: Level not seen yet", "Novice: No monster to fight at its level"],
            preview.PlayerNotes.Select(note => $"{note.Name}: {Say(note.Note)}"));
        Assert.Equal($"{ground.Maps.Count}: maps to go through", $"{Say(preview.Figures[0].Value)}: {Say(preview.Figures[0].Label)}");
        Assert.NotEmpty(preview.Zone);
    }

    [Fact]
    public void WithNoCharactersLevelTheMonstersChosenShowThePlaceAtTheirLevelElseTheAppIsToldWhy()
    {
        var previews = new FightPreviews(_play.World, _play.Tables);
        var chosen = previews.Of(new FightSettings { Mode = FightMode.Chosen, Monsters = [TestPlay.LarvaId] })!;
        var automatic = previews.Of(new FightSettings(), [new PreviewPlayer("Unseen", 0, new Dictionary<int, int>(), null)])!;

        Assert.Equal("Level 14: the place for a character of the monsters' level", $"{Say(chosen.Figures[0].Value)}: {Say(chosen.Figures[0].Label)}");
        Assert.NotEmpty(chosen.Zone);
        Assert.Null(chosen.Message);
        Assert.Equal(["Auto: place chosen for each character"], automatic.Figures.Select(figure => $"{Say(figure.Value)}: {Say(figure.Label)}"));
        Assert.Empty(automatic.Zone);
        Assert.StartsWith("The place is chosen at the level of each character", Say(automatic.Message!), StringComparison.Ordinal);
    }
}
