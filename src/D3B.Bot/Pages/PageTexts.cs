using System.ComponentModel;
using System.Globalization;
using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Pages;

/// <summary>Texts of resources in the app's language, for a page to bind to, <c>{Binding Texts[Key]}</c>, following the language as it changes (D96).</summary>
/// <remarks>
/// A change of language raises <see cref="PropertyChanged"/> for the indexer by its own name, <c>Item</c>, the only one
/// Avalonia's bindings to an indexer listen for (D94).
/// </remarks>
public sealed class PageTexts : INotifyPropertyChanged
{
    private const string French = "fr";

    private readonly ResourceManager _resources;
    private readonly PageContext _context;

    /// <summary>Gets the language the texts are in: the app's.</summary>
    public CultureInfo Culture => _context.Culture;

    /// <summary>Gets a text.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text in the app's language, or its key between brackets when the resources lack it, or cannot be found.</returns>
    public string this[string key] => LocalizedText.Lookup(_resources, key, Culture);

    /// <summary>Raised when the app's language changes, for the indexer and <see cref="Culture"/>.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    internal PageTexts(ResourceManager resources, PageContext context)
    {
        _resources = resources;
        _context = context;
        _context.CultureChanged += OnCultureChanged;
    }

    /// <summary>Gets a text whose placeholders take values, formatted in the app's language.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <param name="values">The values of its placeholders.</param>
    /// <returns>The text.</returns>
    public string Format(string key, params object?[] values)
    {
        return string.Format(Culture, this[key], values);
    }

    /// <summary>Gets a text that counts something, its singular or its plural as the app's language has it.</summary>
    /// <param name="key">The text's key in the resources, without the <c>One</c> or <c>Other</c> that ends it.</param>
    /// <param name="count">How many there are, the text's first value.</param>
    /// <returns>The text.</returns>
    public string Plural(string key, long count)
    {
        // French counts 0 as singular, English as plural, as the app's own texts do (D94).
        var one = Culture.TwoLetterISOLanguageName == French ? count is 0 or 1 : count is 1;

        return Format(key + (one ? "One" : "Other"), count);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }
}
