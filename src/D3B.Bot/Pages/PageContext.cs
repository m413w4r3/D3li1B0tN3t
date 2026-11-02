using System.Globalization;
using System.Resources;
using D3B.Data.Texts;

namespace D3B.Bot.Pages;

/// <summary>What a page sees of the app it shows in: the host's services, the app's language, and the window's refresh (D96).</summary>
/// <remarks>
/// <para>
/// The app makes one for each page, on the UI's thread, the first time the page shows, and keeps both until its window
/// is gone, or until the page fails. Every event is raised on the UI's thread.
/// </para>
/// <para>
/// A page reads the bots' snapshots when the window refreshes it, every 250 ms while it shows (<see cref="Refreshing"/>):
/// the bots' loops never wait for the app, and tell it nothing (D9, D94).
/// </para>
/// </remarks>
public abstract class PageContext
{
    private const string French = "fr";

    private readonly Dictionary<ResourceManager, PageTexts> _texts = [];

    /// <summary>Gets the host's services: the bots, the activities, the pack's data, and the services the page's module registered.</summary>
    public abstract IServiceProvider Services { get; }

    /// <summary>Gets the language the app shows its texts in: French or English.</summary>
    public abstract CultureInfo Culture { get; }

    /// <summary>Gets the language of the pack's texts that goes with the app's (<see cref="TextLanguages"/>).</summary>
    public string GameLanguage => Culture.TwoLetterISOLanguageName == French ? TextLanguages.French : TextLanguages.English;

    /// <summary>Raised when the app's language changes.</summary>
    public event EventHandler? CultureChanged;

    /// <summary>Raised when the window refreshes the page: every 250 ms while the page shows, and as it shows again.</summary>
    public event EventHandler? Refreshing;

    /// <summary>Gets texts from resources, in the app's language, which follow it as it changes, for the page to bind to.</summary>
    /// <param name="resources">The resources, such as the page's module's own.</param>
    /// <returns>The texts: the same each time for the same resources, which follow the language for as long as the page lives.</returns>
    public PageTexts Texts(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        if (!_texts.TryGetValue(resources, out var texts))
        {
            texts = new PageTexts(resources, this);
            _texts.Add(resources, texts);
        }

        return texts;
    }

    /// <summary>Raises <see cref="CultureChanged"/>, once the app's language changed.</summary>
    protected void OnCultureChanged()
    {
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raises <see cref="Refreshing"/>, as the window refreshes the page.</summary>
    protected void OnRefreshing()
    {
        Refreshing?.Invoke(this, EventArgs.Empty);
    }
}
