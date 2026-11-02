namespace D3B.Scripts.Fights;

/// <summary>Counts of what the bench measured, by a key naming the section and the outcome, printed in order.</summary>
internal sealed class Tally
{
    private readonly SortedDictionary<string, int> _counts = new(StringComparer.Ordinal);

    public void Count(string key, int by = 1)
    {
        _counts[key] = _counts.GetValueOrDefault(key) + by;
    }

    public void Write(TextWriter writer)
    {
        string? section = null;

        foreach (var (key, count) in _counts)
        {
            var colon = key.IndexOf(':', StringComparison.Ordinal);
            var head = colon < 0 ? key : key[..colon];

            if (head != section)
            {
                writer.WriteLine(head);
                section = head;
            }

            writer.WriteLine($"  {count,6} {(colon < 0 ? key : key[(colon + 1)..].Trim())}");
        }
    }
}
