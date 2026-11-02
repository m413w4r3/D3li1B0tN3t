using System.ComponentModel;
using System.Globalization;
using System.Resources;
using D3B.Data.Texts;

namespace D3B.App.Localization;

/// <summary>The app's texts, in French or English, which every view follows when the language changes (D14).</summary>
/// <remarks>
/// The texts are the resources of <c>Strings.resx</c>, in English, and <c>Strings.fr.resx</c>. The language starts as the
/// system's, French when it is French, else English. Views bind to the indexer through <see cref="TrExtension"/>, which
/// changes with the language; view models read texts each time they refresh.
/// </remarks>
internal sealed class Localizer : INotifyPropertyChanged
{
    private static readonly ResourceManager _strings = new("D3B.App.Localization.Strings", typeof(Localizer).Assembly);

    private CultureInfo _culture;

    /// <summary>Gets English, the language of the neutral resources.</summary>
    public static CultureInfo English { get; } = CultureInfo.GetCultureInfo("en");

    /// <summary>Gets French.</summary>
    public static CultureInfo French { get; } = CultureInfo.GetCultureInfo("fr");

    /// <summary>Gets the app's texts.</summary>
    public static Localizer Instance { get; } = new(CultureInfo.CurrentUICulture);

    /// <summary>Gets the language the texts are in.</summary>
    public CultureInfo Culture => _culture;

    /// <summary>Gets the language of the game's texts in the pack that goes with the app's (<see cref="TextLanguages"/>).</summary>
    public string GameLanguage => _culture.Equals(French) ? TextLanguages.French : TextLanguages.English;

    /// <summary>Gets a text.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text in the language chosen, or its key between brackets when the resources lack it.</returns>
    public string this[string key] => _strings.GetString(key, _culture) ?? $"[{key}]";

    /// <summary>Raised when the language changes, for the indexer and <see cref="Culture"/>.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Initializes a new instance of the <see cref="Localizer"/> class.</summary>
    /// <param name="culture">The system's language: French when it is French, else English.</param>
    internal Localizer(CultureInfo culture)
    {
        _culture = Supported(culture);
    }

    private static CultureInfo Supported(CultureInfo culture)
    {
        return culture.TwoLetterISOLanguageName == French.TwoLetterISOLanguageName ? French : English;
    }

    /// <summary>Gets a text whose placeholders take values, formatted in the language chosen.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <param name="values">The values of its placeholders.</param>
    /// <returns>The text.</returns>
    public string Format(string key, params object?[] values)
    {
        return string.Format(_culture, this[key], values);
    }

    /// <summary>Gets a text that counts something, its singular or its plural as the language chosen has it.</summary>
    /// <param name="key">The text's key in the resources, without the <c>One</c> or <c>Other</c> that ends it.</param>
    /// <param name="count">How many there are, the text's first value.</param>
    /// <returns>The text.</returns>
    public string Plural(string key, long count)
    {
        // French counts 0 as singular, English as plural.
        var one = _culture.Equals(French) ? count is 0 or 1 : count is 1;

        return Format(key + (one ? "One" : "Other"), count);
    }

    /// <summary>Changes the language of every text.</summary>
    /// <param name="culture">The language: French when it is French, else English.</param>
    public void Use(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        _culture = Supported(culture);

        // The indexer's own name, the only one Avalonia's bindings to an indexer listen for, not WPF's "Item[]".
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }
}
