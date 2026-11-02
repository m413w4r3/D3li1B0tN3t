using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace D3B.App.ViewModels.Settings;

/// <summary>A value a setting offers: a choice's, or one of the game's, named in the app's language.</summary>
internal sealed class OptionViewModel
{
    /// <summary>The size an icon is drawn at beside a name, an item's or a profession's.</summary>
    public const double ItemIconSize = 24;

    /// <summary>The size a monster's icon is drawn at: a whole body on its picture, which a smaller square shrinks too far (D246).</summary>
    public const double PortraitSize = 36;

    private readonly Lazy<IImage?> _icon;

    /// <summary>Gets what a search keeps: the names that hold the text typed, whatever its case and accents, as "frene" finds Frêne.</summary>
    public static AutoCompleteFilterPredicate<object?> Filter { get; } = (search, item) => item is OptionViewModel option
        && (string.IsNullOrWhiteSpace(search)
            || CultureInfo.InvariantCulture.CompareInfo.IndexOf(option.Name, search.Trim(), CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    /// <summary>Gets the value the setting stores: a value of an enumeration, or an id of the game's.</summary>
    public object Value { get; }

    /// <summary>Gets its name.</summary>
    public string Name { get; }

    /// <summary>Gets what the app says beside its name, such as its level.</summary>
    public string? Detail { get; }

    /// <summary>Gets whether it has a detail.</summary>
    public bool HasDetail => Detail is not null;

    /// <summary>Gets its icon, such as a resource's or a profession's, when the pack keeps one (D135), drawn once first shown.</summary>
    public IImage? Icon => _icon.Value;

    /// <summary>Gets how large its icon is drawn beside its name.</summary>
    public double IconSize { get; init; } = ItemIconSize;

    /// <summary>Gets the icon of what its detail names, such as a resource's profession.</summary>
    public IImage? DetailIcon { get; }

    /// <summary>Initializes a new instance of the <see cref="OptionViewModel"/> class.</summary>
    /// <param name="value">The value the setting stores.</param>
    /// <param name="name">Its name.</param>
    /// <param name="detail">What the app says beside its name.</param>
    /// <param name="icon">Draws its icon, once first shown: a list of monsters offers some 1,600 (D246).</param>
    /// <param name="detailIcon">The icon of what its detail names.</param>
    public OptionViewModel(object value, string name, string? detail = null, Func<IImage?>? icon = null, IImage? detailIcon = null)
    {
        Value = value;
        Name = name;
        Detail = detail;
        _icon = new Lazy<IImage?>(icon ?? (() => null), LazyThreadSafetyMode.None);
        DetailIcon = detailIcon;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        // The text a search box shows once the option is chosen.
        return Name;
    }
}
