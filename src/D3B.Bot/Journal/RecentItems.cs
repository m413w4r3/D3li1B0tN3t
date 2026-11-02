namespace D3B.Bot.Journal;

/// <summary>The last items written, kept up to a capacity: the app reads them as they come, older ones being dropped (D113).</summary>
/// <typeparam name="T">The items.</typeparam>
/// <remarks>
/// Written from the bots' loops, behaviors and the relay's, read from the app's thread: every access takes the lock, and
/// reading copies. A reader keeps how many it has seen written and asks for what came after (<see cref="After"/>), so that
/// a page refreshed four times a second copies only the new ones.
/// </remarks>
public sealed class RecentItems<T>
{
    private readonly Lock _lock = new();
    private readonly Queue<T> _items;
    private long _written;

    /// <summary>Gets the number of items kept at most.</summary>
    public int Capacity { get; }

    /// <summary>Gets the number of items written so far, kept or dropped.</summary>
    public long Written
    {
        get
        {
            lock (_lock)
                return _written;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="RecentItems{T}"/> class.</summary>
    /// <param name="capacity">The number of items kept at most.</param>
    public RecentItems(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        Capacity = capacity;
        _items = new Queue<T>(Math.Min(capacity, 256));
    }

    /// <summary>Gets the items kept, oldest first.</summary>
    /// <returns>A copy.</returns>
    public IReadOnlyList<T> All()
    {
        lock (_lock)
            return [.. _items];
    }

    /// <summary>Gets the items written after the first ones, those still kept, oldest first.</summary>
    /// <param name="written">How many items the reader had seen written, 0 at first; moved past those given.</param>
    /// <returns>A copy, empty when nothing came since.</returns>
    public IReadOnlyList<T> After(ref long written)
    {
        lock (_lock)
        {
            var fresh = _written - Math.Max(written, 0);

            written = _written;

            if (fresh <= 0)
                return [];

            return [.. _items.Skip(_items.Count - (int)Math.Min(fresh, _items.Count))];
        }
    }

    /// <summary>Writes an item, dropping the oldest once the capacity is reached.</summary>
    /// <param name="item">The item.</param>
    public void Add(T item)
    {
        lock (_lock)
        {
            if (_items.Count == Capacity)
                _items.Dequeue();

            _items.Enqueue(item);
            _written++;
        }
    }
}
