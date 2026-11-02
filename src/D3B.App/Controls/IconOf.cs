using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>Gives the app's icon of a value's name: <c>Icon</c> and the name, among the geometries of <c>Themes/Icons.axaml</c>.</summary>
/// <remarks>Lets a view model say what a line is about by an enumeration, never by a geometry of the UI's framework.</remarks>
internal sealed class IconOf : IValueConverter
{
    /// <summary>Gets the converter.</summary>
    public static IconOf Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null && Application.Current is { } app && app.TryGetResource("Icon" + value, null, out var icon) && icon is Geometry geometry
            ? geometry
            : null;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
