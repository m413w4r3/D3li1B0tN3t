using D3B.Bot.Characters;
using D3B.Bot.Runtime;

namespace D3B.Bot.Movement;

/// <summary>The one supervised move a host session may attempt, and the passive checks that may precede it.</summary>
/// <remarks>
/// A move request consumes the authorization before checking anything: even a refused or failed first request leaves the
/// session unable to launch another. Checking readiness or a plan never consumes it and never sends a request. The host
/// session must be restarted for a new authorization, which itself needs a new owner GO.
/// </remarks>
internal sealed class SupervisedMoveSession
{
    private readonly Bots _bots;
    private int _consumed;

    /// <summary>Initializes a new instance of the <see cref="SupervisedMoveSession"/> class.</summary>
    /// <param name="bots">The host's bots, among which exactly one must be connected.</param>
    public SupervisedMoveSession(Bots bots)
    {
        ArgumentNullException.ThrowIfNull(bots);

        _bots = bots;
    }

    /// <summary>Reads the passive readiness of the one connected bot, without sending a request.</summary>
    /// <returns>The readiness, or why no single connected bot could be read.</returns>
    public MovementReadiness Readiness()
    {
        var bot = ConnectedBot(out var reason);

        return bot is null ? NoBotReadiness(reason) : MovementReadiness.Read(bot);
    }

    /// <summary>Checks one explicit source and target pair without consuming the move authorization.</summary>
    /// <param name="sourceMapId">The map the character must stand on.</param>
    /// <param name="targetMapId">The directly neighbouring map to reach.</param>
    /// <returns>The plan, ready or naming its first blocker.</returns>
    public SupervisedMovePlan Check(long sourceMapId, long targetMapId)
    {
        var bot = ConnectedBot(out var reason);

        return bot is null
            ? SupervisedMovePlan.Blocked(reason, NoBotReadiness(reason))
            : SupervisedMove.Plan(bot, sourceMapId, targetMapId);
    }

    /// <summary>Consumes the single authorization and attempts one move.</summary>
    /// <param name="sourceMapId">The map the character must stand on.</param>
    /// <param name="targetMapId">The directly neighbouring map to reach.</param>
    /// <param name="cancellationToken">Stops the move before or while it runs.</param>
    /// <returns>The verdict and its evidence; a second call is refused without any send.</returns>
    public Task<SupervisedMoveResult> MoveAsync(long sourceMapId, long targetMapId, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _consumed, 1) is 1)
        {
            var evidence = EvidenceOf(sourceMapId, targetMapId);

            return Task.FromResult(new SupervisedMoveResult(SupervisedMoveOutcome.Blocked, "ALREADY_CONSUMED", evidence));
        }

        var bot = ConnectedBot(out var reason);

        if (bot is null)
        {
            var evidence = EvidenceOf(sourceMapId, targetMapId);

            return Task.FromResult(new SupervisedMoveResult(SupervisedMoveOutcome.Blocked, reason, evidence));
        }

        return SupervisedMove.ExecuteAsync(bot, sourceMapId, targetMapId, cancellationToken);
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

    private static MovementReadiness NoBotReadiness(string reason)
    {
        return new MovementReadiness(
            false,
            false,
            false,
            null,
            false,
            false,
            false,
            null,
            null,
            CellProvenance.Unknown,
            false,
            MovementRefusal.Disconnected,
            reason);
    }

    private static SupervisedMoveEvidence EvidenceOf(long sourceMapId, long targetMapId)
    {
        return new SupervisedMoveEvidence
        {
            SourceMapId = sourceMapId,
            TargetMapId = targetMapId,
        };
    }
}
