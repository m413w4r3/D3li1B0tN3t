using System.ComponentModel;
using System.Globalization;
using D3B.App.Localization;
using D3B.Bot.Pages;

namespace D3B.App.Pages;

/// <summary>What a module's page sees of the app: the host's services, the app's language, and the window's refresh (D96).</summary>
/// <remarks>
/// The page's code runs on the UI's thread of the process that holds every client's connection (D91): whatever it throws
/// as the language changes or the window refreshes it goes to <c>failed</c>, which replaces the page, rather than to the
/// window, whose fall would disconnect the clients. Once closed, it raises nothing more.
/// </remarks>
internal sealed class AppPageContext : PageContext
{
    private readonly Localizer _localizer;
    private readonly Action<Exception> _failed;

    /// <inheritdoc/>
    public override IServiceProvider Services { get; }

    /// <inheritdoc/>
    public override CultureInfo Culture => _localizer.Culture;

    /// <summary>Initializes a new instance of the <see cref="AppPageContext"/> class.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="localizer">The app's texts, whose language the page follows.</param>
    /// <param name="failed">Told of what the page's code threw.</param>
    public AppPageContext(IServiceProvider services, Localizer localizer, Action<Exception> failed)
    {
        Services = services;
        _localizer = localizer;
        _failed = failed;
        _localizer.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>Raises nothing more: the page failed.</summary>
    public void Close()
    {
        _localizer.PropertyChanged -= OnLanguageChanged;
    }

    /// <summary>Refreshes the page, as the window refreshes.</summary>
    public void Refresh()
    {
        try
        {
            OnRefreshing();
        }
        catch (Exception exception)
        {
            _failed(exception);
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Culture))
            return;

        try
        {
            OnCultureChanged();
        }
        catch (Exception exception)
        {
            _failed(exception);
        }
    }
}
