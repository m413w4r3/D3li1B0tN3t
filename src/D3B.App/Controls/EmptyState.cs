using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>What a page says while it has nothing to show: an icon, a few words, and a line saying what to do (D162).</summary>
/// <remarks>Its look is the control theme in <c>Themes/Controls.axaml</c>, the same on every page.</remarks>
internal sealed class EmptyState : TemplatedControl
{
    /// <summary>Defines the <see cref="Icon"/> property.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(Icon));

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(Text));

    /// <summary>Gets or sets the page's icon.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Gets or sets what is missing, in a few words.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets what to do about it, in a line.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}
