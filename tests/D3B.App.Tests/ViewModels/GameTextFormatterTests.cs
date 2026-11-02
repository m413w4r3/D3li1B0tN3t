using System.Globalization;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Bot.Localization;
using D3B.Bot.Tests.Runtime;

namespace D3B.App.Tests.ViewModels;

public sealed class GameTextFormatterTests : IAsyncDisposable
{
    private readonly TestPlay _play = new();
    private readonly TestGameData _game = new();

    public async ValueTask DisposeAsync()
    {
        await _play.DisposeAsync();
        _game.Dispose();
    }

    [Fact]
    public void ATextNamesTheGamesValuesInTheAppsLanguageAndCountsAsItCounts()
    {
        var localizer = new Localizer(Localizer.English);
        var says = new GameTextFormatter(localizer, _play.World, _game.Texts, _play.Tables);
        var text = GameText.Of(LocalizedText.Invariant("{0} by a {1} on {2}, {3}"), GameTextValue.Item(TestPlay.Ash), GameTextValue.Job(TestPlay.Lumberjack), GameTextValue.Map(TestPlay.MapId), GameTextValue.Number(1_500));
        var none = GameText.Counted(LocalizedText.Invariant("{0} bank"), LocalizedText.Invariant("{0} banks"), 0);

        Assert.Equal("Ash by a Lumberjack on 4,-18, 1,500", says.Say(text));
        Assert.Equal("0 banks", says.Say(none));
        Assert.Equal("Resource 9999", says.Item(9_999));

        localizer.Use(CultureInfo.GetCultureInfo("fr"));

        Assert.Equal("0 bank", says.Say(none));
        Assert.StartsWith("Frêne by a Bûcheron", says.Say(text), StringComparison.Ordinal);
    }

    [Fact]
    public void AFormatItsValuesDoNotFillIsSaidAsItIs()
    {
        var says = new GameTextFormatter(new Localizer(Localizer.English), null, null, null);

        Assert.Equal("Harvests {1}", says.Say(GameText.Of(LocalizedText.Invariant("Harvests {1}"), GameTextValue.Item(TestPlay.Ash))));
    }
}
