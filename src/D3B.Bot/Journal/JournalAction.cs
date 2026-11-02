using D3B.Bot.Runtime;

namespace D3B.Bot.Journal;

/// <summary>An action in progress within a behavior, which the journal writes once over, as deep as the actions it was begun within (D112).</summary>
/// <remarks>Disposing it ends it, so that an action cut short by an interruption leaves the next one as deep as before.</remarks>
internal readonly struct JournalAction : IDisposable
{
    private readonly BotContext _context;

    /// <summary>Gets how many actions it was begun within.</summary>
    public int Depth { get; }

    /// <summary>Gets when it began, by the bot's clock.</summary>
    public DateTimeOffset Started { get; }

    /// <summary>Gets whether it was begun within no other action, as what an activity asks.</summary>
    public bool IsOutermost => Depth is 0;

    public JournalAction(BotContext context, int depth, DateTimeOffset started)
    {
        _context = context;
        Depth = depth;
        Started = started;
    }

    public void Dispose()
    {
        _context.EndAction();
    }
}
