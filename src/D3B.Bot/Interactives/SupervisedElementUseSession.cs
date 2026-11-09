using D3B.Bot.Runtime;
using D3B.Data;

namespace D3B.Bot.Interactives;

/// <summary>Owns the one supervised element-use authorization for a host session.</summary>
internal sealed class SupervisedElementUseSession
{
    private readonly Bots _bots;
    private readonly GameTables _tables;
    private int _consumed;

    /// <summary>Initializes a new instance of the <see cref="SupervisedElementUseSession"/> class.</summary>
    /// <param name="bots">The host's bots, among which exactly one must be connected.</param>
    /// <param name="tables">The installed build's game tables.</param>
    public SupervisedElementUseSession(Bots bots, GameTables tables)
    {
        ArgumentNullException.ThrowIfNull(bots);
        ArgumentNullException.ThrowIfNull(tables);

        _bots = bots;
        _tables = tables;
    }

    /// <summary>Passively prepares one target without consuming the attempt.</summary>
    /// <param name="target">The exact map, cell, element and skill requested.</param>
    /// <returns>The preparation, including the first blocker or unavailable datum.</returns>
    public ElementUsePreparation Prepare(ElementUseTarget target)
    {
        var bot = ConnectedBot(out var reason);

        if (bot is null)
            return SupervisedElementUse.BlockedPreparation(target, reason);

        return SupervisedElementUse.Prepare(bot, _tables, target, attemptConsumed: Volatile.Read(ref _consumed) is not 0);
    }

    /// <summary>Attempts the single supervised use, consuming authorization only after a ready preflight.</summary>
    /// <param name="target">The exact map, cell, element and skill requested.</param>
    /// <param name="cancellationToken">Stops the attempt.</param>
    /// <returns>The verdict and its evidence.</returns>
    public async Task<SupervisedElementUseResult> UseAsync(ElementUseTarget target, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _consumed) is not 0)
            return SupervisedElementUse.Blocked(SupervisedElementUse.BlockedPreparation(target, "ALREADY_CONSUMED"));

        var bot = ConnectedBot(out var reason);

        if (bot is null)
            return SupervisedElementUse.Blocked(SupervisedElementUse.BlockedPreparation(target, reason));

        var preparation = SupervisedElementUse.Prepare(bot, _tables, target, attemptConsumed: false);

        if (!preparation.CanAttempt)
            return SupervisedElementUse.Blocked(preparation, bot.State);

        if (Interlocked.CompareExchange(ref _consumed, 1, 0) is not 0)
            return SupervisedElementUse.Blocked(SupervisedElementUse.BlockedPreparation(target, "ALREADY_CONSUMED"));

        return await SupervisedElementUse.ExecuteAsync(bot, _tables, preparation, cancellationToken).ConfigureAwait(false);
    }

    private BotActor? ConnectedBot(out string reason)
    {
        var connected = _bots.All.Where(bot => bot.IsConnected).ToArray();

        if (connected.Length is 0)
        {
            reason = "NO_CONNECTED_BOT";
            return null;
        }

        if (connected.Length > 1)
        {
            reason = "AMBIGUOUS_BOT";
            return null;
        }

        reason = "READY";
        return connected[0];
    }
}
