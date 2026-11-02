using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace D3B.App.Controls;

/// <summary>Lays its children out in as many equal columns as its width holds, each row as tall as its tallest child.</summary>
/// <remarks>
/// The columns depend on the width alone, not on how many children there are, so that a bot's card keeps its size whether
/// the host plays one character or eight.
/// </remarks>
internal sealed class CardGrid : Panel
{
    /// <summary>Defines the <see cref="MinColumnWidth"/> property.</summary>
    public static readonly StyledProperty<double> MinColumnWidthProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(MinColumnWidth), 420);

    /// <summary>Defines the <see cref="MaxColumns"/> property.</summary>
    public static readonly StyledProperty<int> MaxColumnsProperty = AvaloniaProperty.Register<CardGrid, int>(nameof(MaxColumns), 3);

    /// <summary>Defines the <see cref="Spacing"/> property.</summary>
    public static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(Spacing), 16);

    /// <summary>Defines the attached property that makes a child take a whole row, such as a field that lays out wide (D127).</summary>
    public static readonly AttachedProperty<bool> IsWideProperty = AvaloniaProperty.RegisterAttached<CardGrid, Control, bool>("IsWide");

    /// <summary>Gets or sets the narrowest a column may be.</summary>
    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>Gets or sets the most columns, however wide the grid.</summary>
    public int MaxColumns
    {
        get => GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    /// <summary>Gets or sets the space between two columns and between two rows.</summary>
    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    static CardGrid()
    {
        AffectsMeasure<CardGrid>(MinColumnWidthProperty, MaxColumnsProperty, SpacingProperty);
        IsWideProperty.Changed.AddClassHandler<Control>((control, _) => (control.GetVisualParent() as CardGrid)?.InvalidateMeasure());
    }

    /// <summary>Gets whether a child takes a whole row.</summary>
    /// <param name="control">The child.</param>
    /// <returns>Whether it does.</returns>
    public static bool GetIsWide(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);

        return control.GetValue(IsWideProperty);
    }

    /// <summary>Makes a child take a whole row, or not.</summary>
    /// <param name="control">The child.</param>
    /// <param name="value">Whether it does.</param>
    public static void SetIsWide(Control control, bool value)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.SetValue(IsWideProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MinColumnWidth * MaxColumns + Spacing * (MaxColumns - 1) : availableSize.Width;
        var (columns, columnWidth) = Columns(width);
        var height = 0.0;

        foreach (var row in Rows(columns))
        {
            var tallest = 0.0;

            foreach (var child in row)
            {
                child.Measure(new Size(GetIsWide(child) ? width : columnWidth, double.PositiveInfinity));
                tallest = Math.Max(tallest, child.DesiredSize.Height);
            }

            height += (height > 0 ? Spacing : 0) + tallest;
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, columnWidth) = Columns(finalSize.Width);
        var top = 0.0;

        foreach (var row in Rows(columns))
        {
            var tallest = row.Max(child => child.DesiredSize.Height);

            for (var column = 0; column < row.Count; column++)
                row[column].Arrange(new Rect(column * (columnWidth + Spacing), top, GetIsWide(row[column]) ? finalSize.Width : columnWidth, tallest));

            top += tallest + Spacing;
        }

        return finalSize;
    }

    private (int Columns, double Width) Columns(double width)
    {
        var columns = Math.Clamp((int)((width + Spacing) / (MinColumnWidth + Spacing)), 1, Math.Max(1, MaxColumns));

        return (columns, Math.Max(0, (width - Spacing * (columns - 1)) / columns));
    }

    private IEnumerable<List<Control>> Rows(int columns)
    {
        var row = new List<Control>();

        foreach (var child in Children.Where(child => child.IsVisible))
        {
            // A wide child ends the row before it, and takes one of its own.
            if (GetIsWide(child) || row.Count == columns)
            {
                if (row.Count > 0)
                    yield return row;

                row = [];
            }

            row.Add(child);

            if (GetIsWide(child))
            {
                yield return row;
                row = [];
            }
        }

        if (row.Count > 0)
            yield return row;
    }
}
