namespace D3B.Data.Packs;

/// <summary>Keeps the entries last read from a pack, up to a budget, and drops the least recently used beyond it.</summary>
/// <remarks>
/// <para>
/// Entries are weighed by their decompressed length, which the index gives before reading and which follows the memory
/// they take once deserialized. An entry heavier than the whole budget is still returned, and kept alone until the next
/// one.
/// </para>
/// <para>
/// Safe for any number of threads. Two threads asking for one missing entry at once both read it, and the first to
/// finish is kept: entries are immutable, so either copy serves.
/// </para>
/// </remarks>
public sealed class PackCache
{
    private readonly GamePack _pack;
    private readonly Dictionary<string, LinkedListNode<CachedEntry>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<CachedEntry> _recency = [];
    private readonly Lock _lock = new();
    private long _weight;

    /// <summary>Gets the most the kept entries may weigh together, in bytes.</summary>
    public long Capacity { get; }

    /// <summary>Gets what the kept entries weigh together, in bytes.</summary>
    public long Weight
    {
        get
        {
            lock (_lock)
                return _weight;
        }
    }

    /// <summary>Gets how many entries are kept.</summary>
    public int Count
    {
        get
        {
            lock (_lock)
                return _entries.Count;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="PackCache"/> class.</summary>
    /// <param name="pack">The pack to read from.</param>
    /// <param name="capacity">The most the kept entries may weigh together, in bytes of decompressed entries.</param>
    public PackCache(GamePack pack, long capacity)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _pack = pack;
        Capacity = capacity;
    }

    /// <summary>Gets an entry, from the cache or else from the pack.</summary>
    /// <typeparam name="T">The type the entry was written as.</typeparam>
    /// <param name="name">The entry's name.</param>
    /// <returns>The entry, the same instance for as long as it stays in the cache.</returns>
    /// <exception cref="KeyNotFoundException">The pack holds no entry of that name.</exception>
    /// <exception cref="PackFormatException">The entry was written as another type, or it is damaged.</exception>
    public T Get<T>(string name)
        where T : class
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(name, out var node))
            {
                _recency.Remove(node);
                _recency.AddFirst(node);

                return Cast<T>(node.Value);
            }
        }

        var value = _pack.Read<T>(name);
        var weight = _pack.LengthOf(name);

        lock (_lock)
        {
            if (_entries.TryGetValue(name, out var node))
                return Cast<T>(node.Value);

            node = _recency.AddFirst(new CachedEntry { Name = name, Value = value, Weight = weight });
            _entries.Add(name, node);
            _weight += weight;

            while (_weight > Capacity && _recency.Last != node && _recency.Last is { } last)
            {
                _recency.RemoveLast();
                _entries.Remove(last.Value.Name);
                _weight -= last.Value.Weight;
            }

            return value;
        }
    }

    private static T Cast<T>(CachedEntry entry)
    {
        // The exact type, as GamePack.Read asks: a cached entry must not be read where an uncached one would fail.
        return entry.Value.GetType() == typeof(T)
            ? (T)entry.Value
            : throw new PackFormatException($"The {entry.Name} entry holds a {entry.Value.GetType().FullName}, not a {typeof(T).FullName}.");
    }

    private sealed class CachedEntry
    {
        public required string Name { get; init; }

        public required object Value { get; init; }

        public required long Weight { get; init; }
    }
}
