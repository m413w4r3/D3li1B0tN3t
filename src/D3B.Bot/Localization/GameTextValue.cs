namespace D3B.Bot.Localization;

/// <summary>A value a <see cref="GameText"/> names: a number, a text, or something of the game's by its id (D112).</summary>
/// <param name="Kind">What it names.</param>
/// <param name="Id">The number, or the game's id.</param>
/// <param name="Text">The text, for <see cref="GameTextKind.Text"/>.</param>
public readonly record struct GameTextValue(GameTextKind Kind, long Id, string? Text = null)
{
    /// <summary>Gets a number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The value.</returns>
    public static GameTextValue Number(long value)
    {
        return new GameTextValue(GameTextKind.Number, value);
    }

    /// <summary>Gets a text written as it is.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    public static GameTextValue Of(string text)
    {
        return new GameTextValue(GameTextKind.Text, 0, text);
    }

    /// <summary>Gets an item of the game.</summary>
    /// <param name="itemId">The item's id.</param>
    /// <returns>The value.</returns>
    public static GameTextValue Item(int itemId)
    {
        return new GameTextValue(GameTextKind.Item, itemId);
    }

    /// <summary>Gets a map of the game.</summary>
    /// <param name="mapId">The map's id.</param>
    /// <returns>The value.</returns>
    public static GameTextValue Map(long mapId)
    {
        return new GameTextValue(GameTextKind.Map, mapId);
    }

    /// <summary>Gets a sub-area of the world.</summary>
    /// <param name="subAreaId">The sub-area's id.</param>
    /// <returns>The value.</returns>
    public static GameTextValue SubArea(int subAreaId)
    {
        return new GameTextValue(GameTextKind.SubArea, subAreaId);
    }

    /// <summary>Gets a profession.</summary>
    /// <param name="jobId">The profession's id.</param>
    /// <returns>The value.</returns>
    public static GameTextValue Job(int jobId)
    {
        return new GameTextValue(GameTextKind.Job, jobId);
    }
}
