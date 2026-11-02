using System.Collections.Immutable;

namespace D3B.Bot.Localization;

/// <summary>A text the app says in its language, naming the game's values by their ids, never by words (D112).</summary>
/// <remarks>
/// <see cref="Format"/> is a composite format, its placeholders the values in order, such as <c>"Harvests {0}"</c> with an
/// item: the app names each value from the pack in its language, then formats. A text that counts gives its plural apart
/// (<see cref="Many"/>), which the app chooses by <see cref="Count"/> as its language has it, French's 0 singular.
/// </remarks>
/// <param name="Format">The format, in the app's language.</param>
/// <param name="Values">The values its placeholders name, in order.</param>
public sealed record GameText(LocalizedText Format, ImmutableArray<GameTextValue> Values)
{
    /// <summary>Gets the format of the plural, for a text that counts.</summary>
    public LocalizedText? Many { get; init; }

    /// <summary>Gets what a text that counts counts, which chooses between <see cref="Format"/> and <see cref="Many"/>.</summary>
    public long Count { get; init; }

    /// <summary>Gets a text.</summary>
    /// <param name="format">The format, in the app's language.</param>
    /// <param name="values">The values its placeholders name, in order.</param>
    /// <returns>The text.</returns>
    public static GameText Of(LocalizedText format, params GameTextValue[] values)
    {
        ArgumentNullException.ThrowIfNull(format);

        return new GameText(format, [.. values]);
    }

    /// <summary>Gets a text that counts, its first value the count.</summary>
    /// <param name="one">The format of the singular.</param>
    /// <param name="many">The format of the plural.</param>
    /// <param name="count">What it counts, its placeholder <c>{0}</c>.</param>
    /// <param name="values">The other values, from <c>{1}</c>.</param>
    /// <returns>The text.</returns>
    public static GameText Counted(LocalizedText one, LocalizedText many, long count, params GameTextValue[] values)
    {
        ArgumentNullException.ThrowIfNull(one);
        ArgumentNullException.ThrowIfNull(many);

        return new GameText(one, [GameTextValue.Number(count), .. values]) { Many = many, Count = count };
    }
}
