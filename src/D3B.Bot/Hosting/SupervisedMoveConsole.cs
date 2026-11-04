using System.Globalization;
using D3B.Bot.Movement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>Reads the two explicit supervised-move commands from standard input, one line at a time, and nothing else.</summary>
/// <remarks>
/// The reader is only registered when <c>Supervised:Move:Enabled</c> is true. It never reacts to a game connection: a move
/// happens only after a line naming both maps. Checking readiness or a plan sends nothing and does not consume the move.
/// The first move line consumes the authorization for the rest of the host's life, whatever its outcome. A blocking
/// console read must not hold host shutdown, so stopping cancels the loop without awaiting the pending read.
/// </remarks>
internal sealed class SupervisedMoveConsole : IHostedService, IDisposable
{
    private const string Usage = "expected 'p2j readiness [<sourceMapId> <targetMapId>]' or 'p2j move <sourceMapId> <targetMapId>'";

    private readonly SupervisedMoveSession _session;
    private readonly ILogger _log;
    private CancellationTokenSource? _stopping;

    /// <summary>Initializes a new instance of the <see cref="SupervisedMoveConsole"/> class.</summary>
    /// <param name="session">The session holding the single move authorization.</param>
    /// <param name="logs">The host's logs.</param>
    public SupervisedMoveConsole(SupervisedMoveSession session, ILoggerFactory logs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(logs);

        _session = session;
        _log = logs.CreateLogger<SupervisedMoveConsole>();
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        SupervisedMoveLog.Enabled(_log);

        // The read may block on a terminal and ignore cancellation; the task stays in the background and the host stops.
        _ = Task.Run(() => LoopAsync(Console.In, Console.Out, _stopping.Token), CancellationToken.None);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping?.Cancel();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        var stopping = _stopping;

        _stopping = null;
        stopping?.Cancel();
        stopping?.Dispose();
    }

    /// <summary>Runs the reader on the given streams, for tests and the host's own standard input and output.</summary>
    /// <param name="input">The command lines.</param>
    /// <param name="output">Where the sanitized readiness and result lines go.</param>
    /// <param name="cancellationToken">Stops the reader.</param>
    /// <returns>A task that completes when the input ends or the reader is stopped.</returns>
    internal async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteLineAsync("P2J supervised move enabled. Commands: p2j readiness [<sourceMapId> <targetMapId>] | p2j move <sourceMapId> <targetMapId>").ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;

            try
            {
                line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (line is null)
                return;

            if (cancellationToken.IsCancellationRequested)
                return;

            await HandleAsync(line, output, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(string line, TextWriter output, CancellationToken cancellationToken)
    {
        if (!TryParse(line, out var command, out var problem))
        {
            SupervisedMoveLog.CommandRefused(_log, problem);
            await output.WriteLineAsync($"P2J COMMAND invalid: {problem}").ConfigureAwait(false);

            return;
        }

        if (command.Kind is CommandKind.Readiness)
        {
            if (command.SourceMapId is { } source && command.TargetMapId is { } target)
            {
                var plan = _session.Check(source, target);

                await PrintReadinessAsync(output, plan.Readiness).ConfigureAwait(false);
                await PrintPlanAsync(output, source, target, plan).ConfigureAwait(false);

                return;
            }

            await PrintReadinessAsync(output, _session.Readiness()).ConfigureAwait(false);

            return;
        }

        var result = await _session.MoveAsync(command.SourceMapId!.Value, command.TargetMapId!.Value, cancellationToken).ConfigureAwait(false);

        await PrintResultAsync(output, result).ConfigureAwait(false);
    }

    private async Task PrintReadinessAsync(TextWriter output, MovementReadiness readiness)
    {
        var atFirstPlacement = Optional(readiness.TranslationBlockedAtFirstPlacement);
        var guard = readiness.Guard.ToString();
        var provenance = readiness.Provenance.ToString();
        var line = $"P2J READY connected={Lower(readiness.Connected)} session={Lower(readiness.SessionAttached)} translation_blocked={Lower(readiness.TranslationBlocked)} "
            + $"translation_blocked_at_first_placement={atFirstPlacement} paused={Lower(readiness.Paused)} acting={Lower(readiness.Acting)} "
            + $"haven_bag={Lower(readiness.InHavenBag)} guard={guard} map={Number(readiness.MapId)} cell={Number(readiness.Cell)} "
            + $"provenance={provenance} trusted={Lower(readiness.PositionTrusted)} can_move={Lower(readiness.CanMove)} reason={readiness.Reason}";

        SupervisedMoveLog.Readiness(_log, readiness.Connected, readiness.SessionAttached, readiness.TranslationBlocked, atFirstPlacement, guard, readiness.MapId, readiness.Cell, provenance, readiness.PositionTrusted, readiness.Reason);

        await output.WriteLineAsync(line).ConfigureAwait(false);
    }

    private async Task PrintPlanAsync(TextWriter output, long source, long target, SupervisedMovePlan plan)
    {
        var transition = plan.Transition?.ToString() ?? "none";
        var guard = plan.Readiness.Guard.ToString();
        var line = $"P2J PLAN ready={Lower(plan.Ready)} reason={plan.Reason} source={Number(source)} target={Number(target)} transition={transition} "
            + $"exit_cell={Number(plan.ExitCell)} guard={guard} translation_blocked={Lower(plan.Readiness.TranslationBlocked)} trusted={Lower(plan.Readiness.PositionTrusted)}";

        SupervisedMoveLog.Plan(_log, plan.Ready, plan.Reason, source, target, transition, plan.ExitCell, guard, plan.Readiness.TranslationBlocked);

        await output.WriteLineAsync(line).ConfigureAwait(false);
    }

    private async Task PrintResultAsync(TextWriter output, SupervisedMoveResult result)
    {
        var evidence = result.Evidence;
        var outcome = result.Outcome.ToString().ToUpperInvariant();
        var transition = evidence.Transition?.ToString() ?? "none";
        var provenance = evidence.EndProvenance.ToString();
        var line = $"P2J MOVE outcome={outcome} reason={result.Reason} source={Number(evidence.SourceMapId)} target={Number(evidence.TargetMapId)} "
            + $"start_map={Number(evidence.StartMapId)} start_cell={Number(evidence.StartCell)} start_provenance={evidence.StartProvenance} "
            + $"transition={transition} exit_cell={Number(evidence.ExitCell)} end_map={Number(evidence.EndMapId)} end_cell={Number(evidence.EndCell)} "
            + $"end_provenance={provenance} end_trusted={Lower(evidence.EndPositionTrusted)} detail={result.Detail ?? "none"}";

        SupervisedMoveLog.Result(_log, outcome, result.Reason, evidence.SourceMapId, evidence.TargetMapId, transition, evidence.EndMapId, evidence.EndCell, provenance, evidence.EndPositionTrusted);

        await output.WriteLineAsync(line).ConfigureAwait(false);
    }

    private static bool TryParse(string line, out Command command, out string problem)
    {
        command = default;
        problem = Usage;

        var tokens = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 2 || !tokens[0].Equals("p2j", StringComparison.OrdinalIgnoreCase))
            return false;

        if (tokens[1].Equals("readiness", StringComparison.OrdinalIgnoreCase))
        {
            if (tokens.Length is not (2 or 4))
                return false;

            if (tokens.Length is 2)
            {
                command = new Command(CommandKind.Readiness, null, null);

                return true;
            }

            if (!TryParseMapId(tokens[2], out var source) || !TryParseMapId(tokens[3], out var target))
            {
                problem = "map identifiers must be positive integers";

                return false;
            }

            command = new Command(CommandKind.Readiness, source, target);

            return true;
        }

        if (!tokens[1].Equals("move", StringComparison.OrdinalIgnoreCase))
            return false;

        if (tokens.Length is not 4)
            return false;

        if (!TryParseMapId(tokens[2], out var moveSource) || !TryParseMapId(tokens[3], out var moveTarget))
        {
            problem = "map identifiers must be positive integers";

            return false;
        }

        command = new Command(CommandKind.Move, moveSource, moveTarget);

        return true;
    }

    private static bool TryParseMapId(string token, out long mapId)
    {
        return long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out mapId) && mapId > 0;
    }

    private static string Lower(bool value)
    {
        return value ? "true" : "false";
    }

    private static string Optional(bool? value)
    {
        return value is { } known ? Lower(known) : "unknown";
    }

    private static string Number(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
    }

    private static string Number(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Number(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
    }

    private async Task LoopAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(input, output, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            SupervisedMoveLog.ReaderFailed(_log, exception.GetType().Name);
        }
    }

    private enum CommandKind
    {
        Readiness,
        Move,
    }

    private readonly record struct Command(CommandKind Kind, long? SourceMapId, long? TargetMapId);
}
