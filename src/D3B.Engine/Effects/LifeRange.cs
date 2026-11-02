namespace D3B.Engine.Effects;

/// <summary>The least and the most of what an effect does, its roll's bounds carried through its formula.</summary>
/// <param name="Min">The least, from the least roll.</param>
/// <param name="Max">The most, from the most roll.</param>
public readonly record struct LifeRange(int Min, int Max)
{
    /// <summary>Gets a range of nothing.</summary>
    public static LifeRange Zero { get; } = new(0, 0);

    /// <summary>Adds two ranges, bound by bound.</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns>Their sum.</returns>
    public static LifeRange Add(LifeRange left, LifeRange right)
    {
        return new LifeRange(left.Min + right.Min, left.Max + right.Max);
    }

    /// <summary>Tells whether a value lies within the range, its bounds included.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Whether it does.</returns>
    public bool Contains(int value)
    {
        return value >= Min && value <= Max;
    }

    /// <summary>Gets the range whose bounds a function changed, each on its own.</summary>
    /// <param name="change">The function, applied to each bound.</param>
    /// <returns>The range changed.</returns>
    public LifeRange Map(Func<int, int> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return new LifeRange(change(Min), change(Max));
    }

    /// <summary>Adds two ranges, bound by bound.</summary>
    /// <param name="left">The first.</param>
    /// <param name="right">The second.</param>
    /// <returns>Their sum.</returns>
    public static LifeRange operator +(LifeRange left, LifeRange right)
    {
        return Add(left, right);
    }
}
