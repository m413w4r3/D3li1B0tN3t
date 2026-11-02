using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace D3B.App.Controls;

/// <summary>Gives a margin on the left alone, from a length: how far a line is set in.</summary>
internal sealed class LeftMargin : IValueConverter
{
    /// <summary>Gets the converter.</summary>
    public static LeftMargin Instance { get; } = new();

    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new Thickness(value is double left ? left : 0, 0, 0, 0);
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
