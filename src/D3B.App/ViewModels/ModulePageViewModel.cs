using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.App.Pages;
using D3B.Bot.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.App.ViewModels;

/// <summary>A page a module registered, in the app's rail: its title and icon, then the page itself once chosen (D96).</summary>
/// <remarks>
/// The page is made the first time it shows, and kept: a page that throws as it is made, as it refreshes, as the language
/// changes or in its own code on the UI's thread is replaced by what it threw, and runs no more; the bots and the other
/// pages go on.
/// </remarks>
internal sealed partial class ModulePageViewModel : ObservableObject
{
    private readonly PageDefinition _definition;
    private readonly IServiceProvider _services;
    private readonly Localizer _localizer;
    private readonly Action<ModulePageViewModel> _show;
    private readonly ILogger _log;
    private AppPageContext? _context;
    private bool _failed;

    /// <summary>Gets the page's id.</summary>
    public string Id => _definition.Id;

    /// <summary>Gets the route the sidebar marks it by, apart from the app's own pages.</summary>
    public string Route => "page:" + _definition.Id;

    /// <summary>Gets the page's icon, or <see langword="null"/> when it gave none the app can draw: then the app's own.</summary>
    public Geometry? Icon { get; }

    /// <summary>Gets the page's title, in the app's language.</summary>
    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    /// <summary>Gets or sets whether the page is the one the window shows.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets what the page shows: its control once made, what it threw if it failed, nothing until it first shows.</summary>
    [ObservableProperty]
    public partial object? Content { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="ModulePageViewModel"/> class.</summary>
    /// <param name="definition">The page, as its module registered it.</param>
    /// <param name="services">The host's services.</param>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="show">Shows the page in the window.</param>
    public ModulePageViewModel(PageDefinition definition, IServiceProvider services, Localizer localizer, Action<ModulePageViewModel> show)
    {
        _definition = definition;
        _services = services;
        _localizer = localizer;
        _show = show;
        _log = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(ModulePageViewModel).FullName!) ?? NullLogger.Instance;
        Icon = IconOf(definition.Icon);
        Title = definition.Title.In(localizer.Culture);
    }

    private static Geometry? IconOf(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return null;

        try
        {
            return Geometry.Parse(icon);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            // A module's icon is data: one the app cannot read gets the app's own, as one it was never given.
            return null;
        }
    }

    private static string Describe(Exception exception)
    {
        // The type says what the message often leaves out, as for a type that failed to initialize, and the inner
        // exceptions what a reflection or a XAML load wrapped.
        var lines = new List<string>();

        for (var current = exception; current is not null; current = current.InnerException)
            lines.Add($"{current.GetType().FullName}: {current.Message}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Reads the page again: its title in the app's language and, while it shows, the page itself, made the first time.</summary>
    public void Refresh()
    {
        Title = _definition.Title.In(_localizer.Culture);

        if (!IsSelected || _failed)
            return;

        if (Content is null && !TryCreate())
            return;

        _context?.Refresh();
    }

    /// <summary>Replaces the page by what it threw, when its own code threw it on the UI's thread, which nothing else caught.</summary>
    /// <param name="exception">What was thrown.</param>
    /// <returns>Whether the page's code threw it: a method of its module's assembly is in the exception's stack.</returns>
    public bool TryFailWith(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (new StackTrace(current, false).GetFrames().Any(frame => frame.GetMethod()?.Module.Assembly == _definition.Owner))
            {
                Fail(exception);

                return true;
            }
        }

        return false;
    }

    [RelayCommand]
    private void Show()
    {
        _show(this);
    }

    private bool TryCreate()
    {
        _context = new AppPageContext(_services, _localizer, Fail);

        object page;

        try
        {
            page = _definition.Create(_context);
        }
        catch (Exception exception)
        {
            Fail(exception);

            return false;
        }

        if (page is not Control)
        {
            Fail(new InvalidOperationException($"The page {Id} is not an Avalonia control, but {page?.GetType().FullName ?? "null"}."));

            return false;
        }

        Content = page;

        return true;
    }

    private void Fail(Exception exception)
    {
        // The first fault is the cause: what the page does after it, as the language changes, is not shown.
        if (_failed)
            return;

        _failed = true;
        _context?.Close();
        PageLog.Failed(_log, Id, exception);
        Content = new PageFailureViewModel(Describe(exception));
    }
}
