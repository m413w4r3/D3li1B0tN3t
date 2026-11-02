using System.Numerics;
using System.Runtime.CompilerServices;
using D3B.Data.Maps;

namespace D3B.Engine.Fights;

/// <summary>A set of a map's cells, one bit a cell: what a fight's rules pass around many times a search, without allocating.</summary>
/// <remarks>
/// A mutable value: a copy is a set of its own, and <see cref="Add"/> changes the variable it is called on. Its cells
/// enumerate in ascending order.
/// </remarks>
public struct CellSet : IEquatable<CellSet>
{
    private const int WordCount = (MapData.CellCount + 63) / 64;

    private Words _words;

    /// <summary>Gets a set of every cell of a map.</summary>
    public static CellSet All
    {
        get
        {
            var all = default(CellSet);

            for (var cell = 0; cell < MapData.CellCount; cell++)
                all.Add(cell);

            return all;
        }
    }

    /// <summary>Gets how many cells the set holds.</summary>
    public readonly int Count
    {
        get
        {
            var count = 0;

            foreach (var word in _words)
                count += BitOperations.PopCount(word);

            return count;
        }
    }

    /// <summary>Gets whether the set holds no cell.</summary>
    public readonly bool IsEmpty
    {
        get
        {
            foreach (var word in _words)
            {
                if (word is not 0)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Makes a set of cells.</summary>
    /// <param name="cells">The cells, each of the map.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A cell is not one of a map's.</exception>
    public static CellSet Of(params ReadOnlySpan<int> cells)
    {
        var set = default(CellSet);

        foreach (var cell in cells)
            set.Add(cell);

        return set;
    }

    private static void Check(int cell)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cell);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cell, MapData.CellCount);
    }

    /// <summary>Tells whether the set holds a cell.</summary>
    /// <param name="cell">A cell, which may lie off the map: such a cell is never held.</param>
    /// <returns>Whether it holds it.</returns>
    public readonly bool Contains(int cell)
    {
        return cell is >= 0 and < MapData.CellCount && (_words[cell >> 6] & (1UL << (cell & 63))) is not 0;
    }

    /// <summary>Adds a cell.</summary>
    /// <param name="cell">A cell of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException">The cell is not one of a map's.</exception>
    public void Add(int cell)
    {
        Check(cell);

        _words[cell >> 6] |= 1UL << (cell & 63);
    }

    /// <summary>Removes a cell.</summary>
    /// <param name="cell">A cell of the map.</param>
    /// <exception cref="ArgumentOutOfRangeException">The cell is not one of a map's.</exception>
    public void Remove(int cell)
    {
        Check(cell);

        _words[cell >> 6] &= ~(1UL << (cell & 63));
    }

    /// <summary>Adds every cell of another set.</summary>
    /// <param name="other">The other set.</param>
    public void UnionWith(in CellSet other)
    {
        for (var index = 0; index < WordCount; index++)
            _words[index] |= other._words[index];
    }

    /// <summary>Keeps only the cells another set holds too.</summary>
    /// <param name="other">The other set.</param>
    public void IntersectWith(in CellSet other)
    {
        for (var index = 0; index < WordCount; index++)
            _words[index] &= other._words[index];
    }

    /// <summary>Removes every cell another set holds.</summary>
    /// <param name="other">The other set.</param>
    public void ExceptWith(in CellSet other)
    {
        for (var index = 0; index < WordCount; index++)
            _words[index] &= ~other._words[index];
    }

    /// <summary>Lists the cells, in ascending order.</summary>
    /// <returns>The cells.</returns>
    public readonly int[] ToArray()
    {
        var cells = new int[Count];
        var next = 0;

        foreach (var cell in this)
            cells[next++] = cell;

        return cells;
    }

    /// <summary>Enumerates the cells, in ascending order.</summary>
    /// <returns>The enumerator.</returns>
    public readonly Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    /// <inheritdoc/>
    public readonly bool Equals(CellSet other)
    {
        for (var index = 0; index < WordCount; index++)
        {
            if (_words[index] != other._words[index])
                return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj)
    {
        return obj is CellSet other && Equals(other);
    }

    /// <inheritdoc/>
    public override readonly int GetHashCode()
    {
        var hash = default(HashCode);

        foreach (var word in _words)
            hash.Add(word);

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override readonly string ToString()
    {
        return $"{{{string.Join(", ", ToArray())}}}";
    }

    /// <summary>Compares two sets.</summary>
    /// <param name="left">A set.</param>
    /// <param name="right">Another set.</param>
    /// <returns>Whether they hold the same cells.</returns>
    public static bool operator ==(CellSet left, CellSet right)
    {
        return left.Equals(right);
    }

    /// <summary>Compares two sets.</summary>
    /// <param name="left">A set.</param>
    /// <param name="right">Another set.</param>
    /// <returns>Whether a cell is in one and not the other.</returns>
    public static bool operator !=(CellSet left, CellSet right)
    {
        return !left.Equals(right);
    }

    /// <summary>Enumerates a set's cells in ascending order.</summary>
    public struct Enumerator
    {
        private readonly CellSet _set;
        private int _cell;

        /// <summary>Gets the cell the enumerator stands on.</summary>
        public readonly int Current => _cell;

        internal Enumerator(CellSet set)
        {
            _set = set;
            _cell = -1;
        }

        /// <summary>Moves to the next cell of the set.</summary>
        /// <returns>Whether there is one.</returns>
        public bool MoveNext()
        {
            var cell = _cell + 1;

            while (cell < MapData.CellCount)
            {
                var word = _set._words[cell >> 6] >> (cell & 63);

                if (word is not 0)
                {
                    _cell = cell + BitOperations.TrailingZeroCount(word);

                    return true;
                }

                cell = ((cell >> 6) + 1) << 6;
            }

            _cell = MapData.CellCount;

            return false;
        }
    }

    [InlineArray(WordCount)]
    private struct Words
    {
        private ulong _element;
    }
}
