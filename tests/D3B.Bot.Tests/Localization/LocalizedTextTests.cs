using System.Globalization;
using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Tests.Localization;

public sealed class LocalizedTextTests
{
    private static readonly CultureInfo _english = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo _french = CultureInfo.GetCultureInfo("fr");

    [Fact]
    public void AnInvariantTextIsTheSameInEveryLanguage()
    {
        var text = LocalizedText.Invariant("Patrol");

        Assert.Equal("Patrol", text.In(_english));
        Assert.Equal("Patrol", text.In(_french));
    }

    [Fact]
    public void AResourceIsInTheLanguageAskedFor()
    {
        var text = LocalizedText.From(TestTexts.Resources, "Greeting");

        Assert.Equal("Hello", text.In(_english));
        Assert.Equal("Bonjour", text.In(_french));
        Assert.Equal("Bonjour", text.In(CultureInfo.GetCultureInfo("fr-CA")));
        Assert.Equal("Hello", text.In(CultureInfo.GetCultureInfo("de")));
    }

    [Fact]
    public void AMissingResourceShowsItsKeyBetweenBrackets()
    {
        Assert.Equal("[Missing]", LocalizedText.From(TestTexts.Resources, "Missing").In(_french));
    }

    [Fact]
    public void ResourcesNamedWrongShowTheirKeysRatherThanThrow()
    {
        // As a module whose .resx moved to a folder, its manifest named after the folder.
        var resources = new ResourceManager("D3B.Bot.Tests.Resources.TestTexts", typeof(TestTexts).Assembly);

        Assert.Equal("[Greeting]", LocalizedText.From(resources, "Greeting").In(_french));
        Assert.Equal("[Greeting]", LocalizedText.From(resources, "Greeting").In(_english));
    }
}
