namespace D3B.Scripts.Inspect;

/// <summary>Formats byte counts in binary units.</summary>
internal static class Sizes
{
    private static readonly string[] _units = ["B", "KiB", "MiB", "GiB", "TiB"];

    public static string Format(long bytes)
    {
        double value = bytes;
        var unit = 0;

        while (Math.Abs(value) >= 1024 && unit < _units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit is 0 ? $"{bytes} B" : $"{value:0.0} {_units[unit]}";
    }
}
