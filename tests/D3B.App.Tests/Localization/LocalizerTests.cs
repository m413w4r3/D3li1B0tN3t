using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using D3B.App.Localization;
using D3B.Data.Texts;

namespace D3B.App.Tests.Localization;

public sealed class LocalizerTests
{
    private static readonly ResourceManager _strings = new("D3B.App.Localization.Strings", typeof(Localizer).Assembly);

    private static SortedSet<string> Keys(CultureInfo culture)
    {
        var resources = _strings.GetResourceSet(culture, createIfNotExists: true, tryParents: false);

        Assert.NotNull(resources);

        return new SortedSet<string>(resources.Cast<DictionaryEntry>().Select(entry => (string)entry.Key), StringComparer.Ordinal);
    }

    [Fact]
    public void EveryTextIsInEnglishAndInFrench()
    {
        var english = Keys(CultureInfo.InvariantCulture);

        Assert.NotEmpty(english);
        Assert.Equal(english, Keys(Localizer.French));
    }

    [Theory]
    [InlineData("fr-FR", "fr")]
    [InlineData("fr-CA", "fr")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")]
    public void TheLanguageStartsAsTheSystemsWhenTheAppHasIt(string system, string expected)
    {
        var localizer = new Localizer(CultureInfo.GetCultureInfo(system));

        Assert.Equal(expected, localizer.Culture.Name);
    }

    [Fact]
    public void TextsAreInTheLanguageChosen()
    {
        var localizer = new Localizer(Localizer.English);

        Assert.Equal("Start", localizer["Start"]);
        Assert.Equal(TextLanguages.English, localizer.GameLanguage);

        localizer.Use(Localizer.French);

        Assert.Equal("Démarrer", localizer["Start"]);
        Assert.Equal(TextLanguages.French, localizer.GameLanguage);
    }

    [Fact]
    public void ValuesAreFormattedInTheLanguageChosen()
    {
        var localizer = new Localizer(Localizer.French);
        var separator = Localizer.French.NumberFormat.NumberGroupSeparator;

        Assert.Equal($"1{separator}234 kamas", localizer.Format("KamasOf", 1_234));

        localizer.Use(Localizer.English);

        Assert.Equal("1,234 kamas", localizer.Format("KamasOf", 1_234));
    }

    [Theory]
    [InlineData("en", 0, "0 steps")]
    [InlineData("en", 1, "1 step")]
    [InlineData("en", 2, "2 steps")]
    [InlineData("fr", 0, "0 étape")]
    [InlineData("fr", 1, "1 étape")]
    [InlineData("fr", 2, "2 étapes")]
    public void ACountTakesTheSingularOrThePluralAsTheLanguageHasIt(string language, long count, string expected)
    {
        Assert.Equal(expected, new Localizer(CultureInfo.GetCultureInfo(language)).Plural("Steps", count));
    }

    [Fact]
    public void AMissingTextShowsItsKey()
    {
        Assert.Equal("[Nothing]", new Localizer(Localizer.English)["Nothing"]);
    }

    [Fact]
    public void ChangingTheLanguageTellsWhatIsBoundToTheTexts()
    {
        var localizer = new Localizer(Localizer.English);
        var changed = new List<string?>();

        ((INotifyPropertyChanged)localizer).PropertyChanged += (_, change) => changed.Add(change.PropertyName);
        localizer.Use(Localizer.French);

        Assert.Equal(["Item", nameof(Localizer.Culture)], changed);
    }
}
