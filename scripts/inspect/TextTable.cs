namespace D3B.Scripts.Inspect;

/// <summary>A plain-text table: left-aligned text columns, right-aligned numeric ones.</summary>
internal sealed class TextTable
{
    private const string Gap = "  ";

    private readonly string[] _headers;
    private readonly bool[] _numeric;
    private readonly List<string[]> _rows = [];

    /// <param name="headers">The column headers. A header that starts with <c>#</c> marks a right-aligned column.</param>
    public TextTable(params string[] headers)
    {
        _numeric = [.. headers.Select(header => header.StartsWith('#'))];
        _headers = [.. headers.Select(header => header.TrimStart('#'))];
    }

    private static string Format(object? cell)
    {
        return cell switch
        {
            null => string.Empty,
            int or long or uint or ulong => $"{cell:N0}",
            _ => cell.ToString() ?? string.Empty,
        };
    }

    public void Add(params object?[] cells)
    {
        if (cells.Length != _headers.Length)
            throw new ArgumentException($"A row has {_headers.Length} cells, not {cells.Length}.", nameof(cells));

        _rows.Add([.. cells.Select(Format)]);
    }

    public void Write(TextWriter writer)
    {
        var widths = new int[_headers.Length];

        foreach (var row in _rows.Prepend(_headers))
        {
            for (var i = 0; i < row.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);
        }

        WriteRow(writer, _headers, widths);
        WriteRow(writer, [.. widths.Select(width => new string('-', width))], widths);

        foreach (var row in _rows)
            WriteRow(writer, row, widths);
    }

    private void WriteRow(TextWriter writer, string[] row, int[] widths)
    {
        var cells = row.Select((cell, i) => _numeric[i] ? cell.PadLeft(widths[i]) : cell.PadRight(widths[i]));

        writer.WriteLine(string.Join(Gap, cells).TrimEnd());
    }
}
