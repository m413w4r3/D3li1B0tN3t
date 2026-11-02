using Avalonia;
using Avalonia.Controls.Primitives;

namespace D3B.App.Controls;

/// <summary>A status said in a few words beside a dot of its tone; the dot alone when it has no text.</summary>
/// <remarks>Its look is the control theme in <c>Themes/Controls.axaml</c>, one colour per <see cref="Controls.Tone"/>.</remarks>
internal sealed class StatusPill : TemplatedControl
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<StatusPill, string?>(nameof(Text));

    /// <summary>Defines the <see cref="Tone"/> property.</summary>
    public static readonly StyledProperty<Tone> ToneProperty = AvaloniaProperty.Register<StatusPill, Tone>(nameof(Tone));

    /// <summary>Gets or sets the status, said.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets what the status says at a glance.</summary>
    public Tone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }
}
