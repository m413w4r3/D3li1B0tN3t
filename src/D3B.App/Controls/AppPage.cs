using Avalonia;
using Avalonia.Controls;

namespace D3B.App.Controls;

/// <summary>A page of the app: its title, or a header of its own, its actions on the right, then its content (D159).</summary>
/// <remarks>
/// Every page has the same margins and the same width, the window's up to 1,200 pixels, from its theme in
/// <c>Themes/Controls.axaml</c>. A page that keeps its own lists in view, such as the logs, does not scroll as a whole.
/// </remarks>
internal sealed class AppPage : ContentControl
{
    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<AppPage, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Help"/> property.</summary>
    public static readonly StyledProperty<string?> HelpProperty = AvaloniaProperty.Register<AppPage, string?>(nameof(Help));

    /// <summary>Defines the <see cref="Header"/> property.</summary>
    public static readonly StyledProperty<object?> HeaderProperty = AvaloniaProperty.Register<AppPage, object?>(nameof(Header));

    /// <summary>Defines the <see cref="Actions"/> property.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<AppPage, object?>(nameof(Actions));

    /// <summary>Defines the <see cref="Scrolls"/> property.</summary>
    public static readonly StyledProperty<bool> ScrollsProperty = AvaloniaProperty.Register<AppPage, bool>(nameof(Scrolls), true);

    /// <summary>Gets or sets the page's title.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets what the page is for, shown on a mark beside its title.</summary>
    public string? Help
    {
        get => GetValue(HelpProperty);
        set => SetValue(HelpProperty, value);
    }

    /// <summary>Gets or sets a header of the page's own, in place of its title.</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Gets or sets the page's actions, on the right of its title.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>Gets or sets whether the page scrolls as a whole, or leaves its content the height it has.</summary>
    public bool Scrolls
    {
        get => GetValue(ScrollsProperty);
        set => SetValue(ScrollsProperty, value);
    }
}
