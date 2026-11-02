using D3B.Bot.Activities;
using D3B.Bot.Harvesting;
using D3B.Bot.Interactives;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Harvesting;

public sealed class RegrowthsTests
{
    private static readonly DateTimeOffset _start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The learner never got there.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void AGrowingBackWatchedWholeGivesTheLongestAndALaterVisitABoundFromAbove()
    {
        var regrowths = new Regrowths();
        var told = new List<int>();

        regrowths.Learned += told.Add;
        regrowths.Harvested(1, 10, 68, _start);
        regrowths.Harvested(1, 11, 68, _start);
        regrowths.Harvested(2, 10, 68, _start);

        Assert.True(regrowths.GrownBack(1, 10, 68, _start.AddSeconds(400), watchedSince: _start.AddSeconds(-5)));
        Assert.True(regrowths.GrownBack(1, 11, 68, _start.AddSeconds(380), watchedSince: _start.AddSeconds(-5)));

        // Found grown back on a visit: another player may have harvested it meanwhile, so it took no longer.
        Assert.True(regrowths.GrownBack(2, 10, 68, _start.AddSeconds(445), watchedSince: _start.AddSeconds(445)));

        // Seen once already, or never seen harvested: nothing more.
        Assert.False(regrowths.GrownBack(1, 10, 68, _start.AddSeconds(500), watchedSince: _start));
        Assert.False(regrowths.GrownBack(3, 10, 68, _start.AddSeconds(500), watchedSince: _start));

        Assert.True(regrowths.TryGet(68, out var nettle));
        Assert.Equal(new Regrowth(TimeSpan.FromSeconds(400), 2, TimeSpan.FromSeconds(445)), nettle);
        Assert.Equal(TimeSpan.FromSeconds(400), regrowths.Reckoned(68));
        Assert.Null(regrowths.Reckoned(124));
        Assert.Equal([68, 68, 68], told);
    }

    [Fact]
    public void ABoundAloneIsReckonedUntilAGrowingBackIsWatchedWhole()
    {
        var regrowths = new Regrowths();

        regrowths.Harvested(1, 10, 125, _start);
        regrowths.GrownBack(1, 10, 125, _start.AddSeconds(564), watchedSince: _start.AddSeconds(564));
        regrowths.Harvested(1, 10, 125, _start.AddSeconds(600));
        regrowths.GrownBack(1, 10, 125, _start.AddSeconds(1_200), watchedSince: _start.AddSeconds(1_200));

        Assert.Equal(TimeSpan.FromSeconds(564), regrowths.Reckoned(125));
    }

    [Fact]
    public async Task TheLearnerWatchesTheMapsTheBotsAreOn()
    {
        var regrowths = new Regrowths();
        var time = new FakeTimeProvider(_start);

        await using var pack = new TestPlay();
        await using var play = new TestPlay([new RegrowthLearner(regrowths, pack.Tables, time)]);
        var (connection, _) = await play.EnterAsync(433);

        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await UntilAsync(() => regrowths.TryGetHarvest(TestPlay.MapId, TestPlay.TreeElementId, out _));
        time.Advance(TimeSpan.FromSeconds(400));
        connection.Server(new InteractiveElementUpdatedEvent { Element = TestPlay.Tree(enabled: true) });
        await UntilAsync(() => regrowths.Count is 1);

        Assert.Equal(new Regrowth(TimeSpan.FromSeconds(400), 1, null), regrowths.TryGet(TestPlay.TreeSkillId, out var tree) ? tree : default);

        // Harvested again, then found grown back on entering the map: a bound only.
        connection.Server(new InteractiveUseEndedEvent { ElementId = TestPlay.TreeElementId, SkillId = TestPlay.TreeSkillId });
        await UntilAsync(() => regrowths.TryGetHarvest(TestPlay.MapId, TestPlay.TreeElementId, out _));
        time.Advance(TimeSpan.FromSeconds(500));
        connection.Server(TestPlay.Information(433));
        await UntilAsync(() => regrowths.TryGet(TestPlay.TreeSkillId, out var bound) && bound.AtMost is not null);

        Assert.Equal(new Regrowth(TimeSpan.FromSeconds(400), 1, TimeSpan.FromSeconds(500)), regrowths.TryGet(TestPlay.TreeSkillId, out var both) ? both : default);
    }

    [Fact]
    public async Task ATourHoldsBackTheMapsWhoseWantedElementsStillGrowBack()
    {
        await using var play = new TestPlay();
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] };
        var zone = HarvestZone.Of(settings, play.World.Atlas, play.World.Graph, play.Tables.Jobs, play.Maps);
        var sightings = new HarvestSightings();
        var regrowths = new Regrowths();
        var time = new FakeTimeProvider(_start);
        var tour = new HarvestTour(sightings, new GraphicSkills(), regrowths, time);

        sightings.SeenElements(TestPlay.MapId, [new SightedElement(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestPlay.Ash, IsEnabled: false)], _start);
        regrowths.Harvested(TestPlay.MapId, TestPlay.TreeElementId, TestPlay.TreeSkillId, _start.AddSeconds(-60));

        // Its resource's growing back not known yet, the map is held back by nothing.
        Assert.Contains(TestPlay.MapId, tour.Remaining(zone));

        regrowths.Remember(TestPlay.TreeSkillId, new Regrowth(TimeSpan.FromMinutes(5), 1, null));

        Assert.DoesNotContain(TestPlay.MapId, tour.Remaining(zone));
        Assert.Equal(1, tour.Growing(zone));
        Assert.Equal(ZoneMapState.Growing, tour.Maps(zone).Single(map => map.MapId == TestPlay.MapId).State);

        // The rest of the zone gone through, the insides last.
        while (tour.Remaining(zone) is { Count: > 0 } left)
        {
            foreach (var map in left)
                tour.Visit(map);
        }

        Assert.Empty(tour.Remaining(zone));
        Assert.Equal(_start.AddMinutes(4), tour.NextReady(zone));

        time.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal([TestPlay.MapId], tour.Remaining(zone));
        Assert.Null(tour.NextReady(zone));
    }

    [Fact]
    public async Task AnElementAnotherHarvestedGrowsBackFromWhenABotFirstSawItSoWhateverTheSightingsSince()
    {
        await using var play = new TestPlay();
        var settings = new HarvestSettings { Resources = [TestPlay.Ash], SubAreas = [TestPlay.SubAreaId] };
        var zone = HarvestZone.Of(settings, play.World.Atlas, play.World.Graph, play.Tables.Jobs, play.Maps);
        var sightings = new HarvestSightings();
        var regrowths = new Regrowths();
        var time = new FakeTimeProvider(_start);
        var tour = new HarvestTour(sightings, new GraphicSkills(), regrowths, time);
        var harvested = new SightedElement(TestPlay.TreeElementId, TestPlay.TreeSkillId, TestPlay.Ash, IsEnabled: false);

        regrowths.Remember(TestPlay.TreeSkillId, new Regrowth(TimeSpan.FromMinutes(5), 1, null));

        // Seen harvested by another, then again 3 minutes later: it should grow back 5 minutes after the first sighting.
        sightings.SeenElements(TestPlay.MapId, [harvested], _start);
        sightings.SeenElements(TestPlay.MapId, [harvested], _start.AddMinutes(3));

        while (tour.Remaining(zone) is { Count: > 0 } left)
        {
            foreach (var map in left)
                tour.Visit(map);
        }

        Assert.Equal(_start.AddMinutes(5), tour.NextReady(zone));

        // Seen offering its skill, then harvested again: from that new sighting.
        sightings.SeenElements(TestPlay.MapId, [harvested with { IsEnabled = true }], _start.AddMinutes(6));
        sightings.SeenElements(TestPlay.MapId, [harvested], _start.AddMinutes(7));

        Assert.True(sightings.TryGet(TestPlay.MapId, out var sighting));
        Assert.Equal(_start.AddMinutes(7), sighting.Elements.Single().DisabledSince);
    }
}
