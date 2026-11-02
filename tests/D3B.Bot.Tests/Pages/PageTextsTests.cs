using System.Globalization;
using System.Resources;
using D3B.Bot.Pages;
using D3B.Bot.Tests.Localization;
using D3B.Data.Texts;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Tests.Pages;

public sealed class PageTextsTests
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");

    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void TextsFollowTheLanguageOfThePage()
    {
        var context = new TestPageContext(_services, _english);
        var texts = context.Texts(TestTexts.Resources);
        var changed = new List<string?>();

        texts.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal("Hello", texts["Greeting"]);
        Assert.Equal("Arrived at 1,234.5", texts.Format("Arrived", 1234.5));

        context.Use(_french);

        // The indexer's own name, the only one Avalonia's bindings to an indexer listen for.
        Assert.Equal(["Item", nameof(PageTexts.Culture)], changed);
        Assert.Same(_french, texts.Culture);
        Assert.Equal("Bonjour", texts["Greeting"]);
        Assert.Equal("Arrivé à 1 234,5", texts.Format("Arrived", 1234.5));
        Assert.Equal("[Missing]", texts["Missing"]);
    }

    [Fact]
    public void APageGetsTheSameTextsForTheSameResources()
    {
        var context = new TestPageContext(_services, _english);

        Assert.Same(context.Texts(TestTexts.Resources), context.Texts(TestTexts.Resources));
    }

    [Fact]
    public void TextsWhoseResourcesCannotBeFoundShowTheirKeys()
    {
        var texts = new TestPageContext(_services, _french).Texts(new ResourceManager("No.Such.Strings", typeof(TestTexts).Assembly));

        Assert.Equal("[Greeting]", texts["Greeting"]);
    }

    [Theory]
    [InlineData("en", 0, "0 walks")]
    [InlineData("en", 1, "1 walk")]
    [InlineData("en", 2, "2 walks")]
    [InlineData("fr", 0, "0 marche")]
    [InlineData("fr", 1, "1 marche")]
    [InlineData("fr", 2, "2 marches")]
    public void ATextThatCountsTakesTheNumberOfItsLanguage(string language, long count, string expected)
    {
        var texts = new TestPageContext(_services, CultureInfo.GetCultureInfo(language)).Texts(TestTexts.Resources);

        Assert.Equal(expected, texts.Plural("Walks", count));
    }

    [Fact]
    public void ThePacksTextsGoWithTheAppsLanguage()
    {
        Assert.Equal(TextLanguages.French, new TestPageContext(_services, CultureInfo.GetCultureInfo("fr-BE")).GameLanguage);
        Assert.Equal(TextLanguages.English, new TestPageContext(_services, _english).GameLanguage);
        Assert.Equal(TextLanguages.English, new TestPageContext(_services, CultureInfo.GetCultureInfo("de")).GameLanguage);
    }

    [Fact]
    public void APageIsRefreshedThroughItsContext()
    {
        var context = new TestPageContext(_services, _english);
        var refreshes = 0;

        context.Refreshing += (_, _) => refreshes++;
        context.Refresh();
        context.Refresh();

        Assert.Equal(2, refreshes);
    }
}
