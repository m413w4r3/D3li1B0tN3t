using System.Reflection;
using D3B.Bot.Localization;

namespace D3B.Bot.Pages;

/// <summary>A page a module brings to the app: its id, its title and icon in the app's rail, and how it is made (D96).</summary>
/// <remarks>
/// A module registers it with <see cref="PageServiceCollectionExtensions.AddPage"/>; <see cref="PageCatalog"/> lists them.
/// The page is the app's UI framework's own control, made only by the app: a host without the app never makes one, so
/// this contract names none of the framework's types, which neither <c>D3B.Bot</c> nor <c>D3B.Host</c> holds (D96).
/// </remarks>
public sealed class PageDefinition
{
    private readonly Func<PageContext, object> _create;

    /// <summary>Gets the page's id, unique in the host.</summary>
    public string Id { get; }

    /// <summary>Gets the page's title, in the app's rail and as the page names itself.</summary>
    public LocalizedText Title { get; }

    /// <summary>Gets the page's icon in the app's rail: lines on a grid of 24 units, in the path syntax of SVG, or <see langword="null"/> for the app's own.</summary>
    /// <remarks>The app draws it as its own icons, with round ends and joins, in the colour of the text around it.</remarks>
    public string? Icon { get; }

    /// <summary>Gets the assembly the page's factory is in: the module's, whose page's faults the app tells by it (D96).</summary>
    public Assembly Owner => _create.Method.Module.Assembly;

    /// <summary>Initializes a new instance of the <see cref="PageDefinition"/> class.</summary>
    /// <param name="id">The page's id, unique in the host.</param>
    /// <param name="title">The page's title.</param>
    /// <param name="icon">The page's icon, or <see langword="null"/> for the app's own.</param>
    /// <param name="create">Makes the page, an Avalonia control, once, on the UI's thread.</param>
    public PageDefinition(string id, LocalizedText title, string? icon, Func<PageContext, object> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(create);

        Id = id;
        Title = title;
        Icon = icon;
        _create = create;
    }

    /// <summary>Makes the page, to show it in the app.</summary>
    /// <param name="context">What the page sees of the app.</param>
    /// <returns>The page: an Avalonia control.</returns>
    public object Create(PageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _create(context);
    }
}
