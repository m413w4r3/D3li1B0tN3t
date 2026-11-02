namespace D3B.Bot.Journal;

/// <summary>What a bot did or met, as its journal keeps it for the app: an action once over, or what became of the bot and its activity (D112).</summary>
/// <remarks>
/// Entries name what they are about by the game's ids, never by the protocol's messages: the app names them from the pack,
/// in its language. An action is written once over, so an entry made within another, such as a travel's map change, comes
/// before the one it was made for.
/// </remarks>
/// <param name="At">When it happened, by the bot's clock: an action's end.</param>
/// <param name="Depth">How many actions it was made within: 0 for what an activity asks, 1 for a travel's map changes.</param>
public abstract record JournalEntry(DateTimeOffset At, int Depth);
