namespace D3B.Data;

/// <summary>What the pack's tables share: rows sorted by identifier and found by binary search, and slices of a shared array.</summary>
/// <remarks>
/// A table is an array of packed structs, which MemoryPack copies as raw bytes; a row's list, such as a sub-area's
/// resources, is a slice of one array the table holds, named by its first index and its length, so that no row holds an
/// array of its own.
/// </remarks>
internal static class Tables
{
    /// <summary>Rejects rows that are not in strictly ascending order of their key.</summary>
    /// <exception cref="ArgumentException">A row's key does not exceed the key of the row before.</exception>
    public static void CheckAscending<T>(ReadOnlySpan<T> rows, Func<T, long> key, string name)
    {
        for (var i = 1; i < rows.Length; i++)
        {
            if (key(rows[i]) <= key(rows[i - 1]))
                throw new ArgumentException($"The {name} are not in strictly ascending order: {key(rows[i])} follows {key(rows[i - 1])}.", name);
        }
    }

    /// <summary>Rejects slices that do not follow one another, in the order of their rows, across the whole of what they slice.</summary>
    /// <exception cref="ArgumentException">A slice does not start where the one before ends, or the slices do not cover the array.</exception>
    public static void CheckSlices<T>(ReadOnlySpan<T> rows, Func<T, (int First, int Count)> slice, int length, string name)
    {
        var next = 0;

        foreach (var row in rows)
        {
            var (first, count) = slice(row);

            if (first != next || count < 0)
                throw new ArgumentException($"The {name} do not follow one another: one starts at {first}, where the one before ends at {next}.", name);

            next += count;
        }

        if (next != length)
            throw new ArgumentException($"The {name} cover {next} items, where there are {length}.", name);
    }

    /// <summary>Finds a row by its key, in rows sorted by it.</summary>
    public static bool TryFind<T>(ReadOnlySpan<T> rows, long key, Func<T, long> keyOf, out T found)
    {
        var index = IndexOf(rows, key, keyOf);

        found = index >= 0 ? rows[index] : default!;

        return index >= 0;
    }

    /// <summary>Finds the index of a row by its key, in rows sorted by it, or -1.</summary>
    public static int IndexOf<T>(ReadOnlySpan<T> rows, long key, Func<T, long> keyOf)
    {
        int low = 0, high = rows.Length - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var current = keyOf(rows[middle]);

            if (current == key)
                return middle;

            if (current < key)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return -1;
    }
}
