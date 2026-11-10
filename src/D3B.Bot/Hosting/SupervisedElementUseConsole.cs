using System.Globalization;
using D3B.Bot.Interactives;
using D3B.Data.Maps;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>Reads only explicit supervised element-use commands from standard input.</summary>
/// <remarks>
/// The reader is registered only when <c>Supervised:ElementUse:Enabled</c> is true. Readiness is passive; a use occurs
/// only for an explicit command. A blocking console read must not hold host shutdown, so stopping cancels the loop
/// without awaiting the pending read.
/// </remarks>
internal sealed class SupervisedElementUseConsole : IHostedService, IDisposable
{
    private const string Usage = "p2j element readiness <mapId> <cell> <elementId> <skillId> | p2j element use <mapId> <cell> <elementId> <skillId> | p2j element candidates";
    private const string ActionDisabledMessage = "the use action is disabled; set Supervised:ElementUse:ActionEnabled=true only under an explicit one-action GO";

    private readonly SupervisedElementUseSession _session;
    private readonly ILogger _log;
    private CancellationTokenSource? _stopping;

    internal bool ActionEnabled { get; set; }

    /// <summary>Initializes a new instance of the <see cref="SupervisedElementUseConsole"/> class.</summary>
    /// <param name="session">The session holding the single use authorization.</param>
    /// <param name="logs">The host's logs.</param>
    public SupervisedElementUseConsole(SupervisedElementUseSession session, ILoggerFactory logs)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(logs);

        _session = session;
        _log = logs.CreateLogger<SupervisedElementUseConsole>();
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stopping = stopping;
        var token = stopping.Token;

        SupervisedElementUseLog.Enabled(_log);

        // The read may block on a terminal and ignore cancellation; the task stays in the background and the host stops.
        _ = Task.Run(() => LoopAsync(Console.In, Console.Out, token), CancellationToken.None);

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

    /// <summary>Runs the reader on the given streams, for tests and the host's standard input and output.</summary>
    /// <param name="input">The command lines.</param>
    /// <param name="output">Where sanitized preparation and result lines go.</param>
    /// <param name="cancellationToken">Stops the reader.</param>
    /// <returns>A task that completes when the input ends or the reader is stopped.</returns>
    internal async Task RunAsync(TextReader input, TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteLineAsync($"P2J supervised element use enabled. Commands: {Usage} action={Lower(ActionEnabled)}").ConfigureAwait(false);

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

            if (line is null || cancellationToken.IsCancellationRequested)
                return;

            await HandleAsync(line, output, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(string line, TextWriter output, CancellationToken cancellationToken)
    {
        if (!TryParse(line, out var command, out var problem))
        {
            SupervisedElementUseLog.CommandRefused(_log, problem);
            await output.WriteLineAsync($"P2J ELEMENT COMMAND invalid: {problem}; usage: {Usage}").ConfigureAwait(false);

            return;
        }

        if (command.Kind is CommandKind.Candidates)
        {
            await PrintCandidatesAsync(output, _session.Candidates(ActionEnabled)).ConfigureAwait(false);

            return;
        }

        if (command.Kind is CommandKind.Readiness)
        {
            await PrintPreparationAsync(output, _session.Prepare(command.Target)).ConfigureAwait(false);

            return;
        }

        if (!ActionEnabled)
        {
            SupervisedElementUseLog.CommandRefused(_log, ActionDisabledMessage);
            await output.WriteLineAsync($"P2J ELEMENT COMMAND refused: {ActionDisabledMessage}").ConfigureAwait(false);

            return;
        }

        var result = await _session.UseAsync(command.Target, cancellationToken).ConfigureAwait(false);

        await PrintResultAsync(output, result).ConfigureAwait(false);
    }

    private async Task PrintPreparationAsync(TextWriter output, ElementUsePreparation preparation)
    {
        var state = preparation.State.ToString();
        var provenance = preparation.Provenance.ToString();
        var inputMode = preparation.InputMode.ToString();
        var line = $"P2J ELEMENT PREP state={state} reason={preparation.Reason} consumed={Lower(preparation.AttemptConsumed)} "
            + $"session_epoch={Number(preparation.SessionEpoch)} state_revision={Number(preparation.StateRevision)} connected={Lower(preparation.Connected)} "
            + $"session={Lower(preparation.SessionAttached)} translation_blocked={Lower(preparation.TranslationBlocked)} paused={Lower(preparation.Paused)} "
            + $"acting={Lower(preparation.Acting)} input_mode={inputMode} map={Number(preparation.MapId)} cell={Number(preparation.Cell)} "
            + $"provenance={provenance} trusted={Lower(preparation.PositionTrusted)} element={Number(preparation.ElementId)} "
            + $"interactive={Number(preparation.InteractiveId)} element_cell={Number(preparation.ElementCell)} gfx={Number(preparation.GfxId)} "
            + $"skill={Number(preparation.SkillId)} instance={Number(preparation.SkillInstanceId)} item={Number(preparation.ExpectedItemId)} "
            + $"min_level={Number(preparation.SkillMinLevel)} uses_from_current_cell={Lower(preparation.UsesFromCurrentCell)}";

        SupervisedElementUseLog.Preparation(_log, line);

        await output.WriteLineAsync(line).ConfigureAwait(false);
    }

    private async Task PrintResultAsync(TextWriter output, SupervisedElementUseResult result)
    {
        var evidence = result.Evidence;
        var gain = evidence.InventoryBefore is { } before && evidence.InventoryAfter is { } after
            ? Number((long)after - before)
            : "unknown";
        var inputMode = evidence.InputMode.ToString();
        var detail = result.Detail ?? "none";
        var line = $"P2J ELEMENT USE outcome={result.Code} reason={result.Reason} request_sent={Lower(evidence.RequestSent)} "
            + $"request_may_have_been_sent={Lower(evidence.RequestMayHaveBeenSent)} used={Lower(evidence.InteractiveUsedObserved)} "
            + $"ended={Lower(evidence.InteractiveUseEndedObserved)} target_map={Number(evidence.Target.MapId)} target_cell={Number(evidence.Target.Cell)} "
            + $"element={Number(evidence.Target.ElementId)} skill={Number(evidence.Target.SkillId)} instance={Number(evidence.SkillInstanceId)} "
            + $"item={Number(evidence.ExpectedItemId)} inventory_before={Number(evidence.InventoryBefore)} inventory_after={Number(evidence.InventoryAfter)} "
            + $"gain={gain} translation_blocked={Lower(evidence.TranslationBlocked)} input_mode={inputMode} detail={detail}";

        SupervisedElementUseLog.Result(_log, line);

        await output.WriteLineAsync(line).ConfigureAwait(false);
    }

    private async Task PrintCandidatesAsync(TextWriter output, ElementCandidateReport report)
    {
        var snapshot = report.Snapshot;
        var state = snapshot?.State;
        var header = $"P2J ELEMENT CANDIDATES version=1 reason={report.Reason} "
            + $"session_epoch={Number(snapshot?.SessionEpoch)} state_revision={Number(snapshot?.Revision)} "
            + $"connected={Lower(snapshot?.Connected ?? false)} session={Lower(snapshot?.SessionAttached ?? false)} "
            + $"translation_blocked={Lower(snapshot?.TranslationBlocked ?? false)} paused={Lower(snapshot?.Paused ?? false)} "
            + $"acting={Lower(snapshot?.Acting ?? false)} input_mode={snapshot?.InputMode.ToString() ?? "unknown"} "
            + $"action_enabled={Lower(report.ActionEnabled)} consumed={Lower(report.AttemptConsumed)} "
            + $"map={Number(state?.MapId)} cell={Number(state?.Cell)} provenance={state?.Provenance.ToString() ?? "unknown"} "
            + $"trusted={Lower(state?.IsPositionTrusted ?? false)} candidates={report.Candidates.Length} "
            + $"target={TargetName(report.Target)} action={ActionName(report.Action)} action_reason={report.ActionReason}";

        SupervisedElementUseLog.Candidates(_log, header);
        await output.WriteLineAsync(header).ConfigureAwait(false);

        foreach (var candidate in report.Candidates)
        {
            var line = $"P2J ELEMENT CANDIDATE version=1 element={Number(candidate.ElementId)} map={Number(candidate.MapId)} "
                + $"server_known={Lower(candidate.ServerKnown)} pack_known={Lower(candidate.PackKnown)} "
                + $"interactive={Number(candidate.InteractiveId)} server_cell={Number(candidate.ServerCell)} "
                + $"pack_cell={Number(candidate.PackCell)} gfx={Number(candidate.GfxId)} state={Number(candidate.ElementState)} "
                + $"skill={Number(candidate.SkillId)} instance={Number(candidate.SkillInstanceId)} "
                + $"skills={Numbers(candidate.SkillIds)} skill_enabled={Lower(candidate.SkillEnabled)} catalog={candidate.Catalog} "
                + $"item={Number(candidate.GatheredItemId)} item_name={Quoted(candidate.GatheredItemName)} "
                + $"possible_items={Numbers(candidate.PossibleItemIds)} min_level={Number(candidate.SkillMinLevel)} "
                + $"uses_from_current_cell={Lower(candidate.UsesFromCurrentCell)} identity={candidate.Identity} "
                + $"identity_source={candidate.IdentitySource} cell_conflict={Lower(candidate.CellConflict)} "
                + $"visual_identity=unconfirmed prep={candidate.PreparationState} prep_reason={candidate.PreparationReason}";

            SupervisedElementUseLog.Candidates(_log, line);
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    private static bool TryParse(string line, out Command command, out string problem)
    {
        command = default;
        problem = "invalid syntax";

        var tokens = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length >= 3
            && tokens[0].Equals("p2j", StringComparison.OrdinalIgnoreCase)
            && tokens[1].Equals("element", StringComparison.OrdinalIgnoreCase)
            && tokens[2].Equals("candidates", StringComparison.OrdinalIgnoreCase))
        {
            if (tokens.Length is not 3)
            {
                problem = "candidates takes no arguments";

                return false;
            }

            command = new Command(CommandKind.Candidates, default);

            return true;
        }

        if (tokens.Length is not 7
            || !tokens[0].Equals("p2j", StringComparison.OrdinalIgnoreCase)
            || !tokens[1].Equals("element", StringComparison.OrdinalIgnoreCase))
            return false;

        CommandKind kind;

        if (tokens[2].Equals("readiness", StringComparison.OrdinalIgnoreCase))
            kind = CommandKind.Readiness;
        else if (tokens[2].Equals("use", StringComparison.OrdinalIgnoreCase))
            kind = CommandKind.Use;
        else
            return false;

        if (!TryParsePositiveLong(tokens[3], out var mapId))
        {
            problem = "mapId must be a positive integer";
            return false;
        }

        if (!int.TryParse(tokens[4], NumberStyles.None, CultureInfo.InvariantCulture, out var cell) || cell is < 0 or >= MapData.CellCount)
        {
            problem = $"cell must be between 0 and {MapData.CellCount - 1}";
            return false;
        }

        if (!int.TryParse(tokens[5], NumberStyles.None, CultureInfo.InvariantCulture, out var elementId) || elementId < 0)
        {
            problem = "elementId must be a non-negative integer";
            return false;
        }

        if (!TryParsePositiveInt(tokens[6], out var skillId))
        {
            problem = "skillId must be a positive integer";
            return false;
        }

        command = new Command(kind, new ElementUseTarget(mapId, cell, elementId, skillId));

        return true;
    }

    private static bool TryParsePositiveLong(string token, out long value)
    {
        return long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    private static bool TryParsePositiveInt(string token, out int value)
    {
        return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    private static string Lower(bool value)
    {
        return value ? "true" : "false";
    }

    private static string Lower(bool? value)
    {
        return value?.ToString().ToLowerInvariant() ?? "unknown";
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

    private static string Number(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Numbers(IEnumerable<int> values)
    {
        var numbers = values.Select(value => value.ToString(CultureInfo.InvariantCulture)).ToArray();

        return numbers.Length is 0 ? "unknown" : string.Join(",", numbers);
    }

    private static string Quoted(string? value)
    {
        if (value is null)
            return "unknown";

        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }

    private static string TargetName(ElementCandidateTargetVerdict target)
    {
        return target switch
        {
            ElementCandidateTargetVerdict.Identified => "TARGET_IDENTIFIED",
            ElementCandidateTargetVerdict.Ambiguous => "TARGET_AMBIGUOUS",
            _ => "TARGET_UNKNOWN",
        };
    }

    private static string ActionName(ElementCandidateActionVerdict action)
    {
        return action switch
        {
            ElementCandidateActionVerdict.Ready => "ACTION_READY",
            _ => "ACTION_BLOCKED",
        };
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
            SupervisedElementUseLog.ReaderFailed(_log, exception.GetType().Name);
        }
    }

    private enum CommandKind
    {
        Readiness,
        Use,
        Candidates,
    }

    private readonly record struct Command(CommandKind Kind, ElementUseTarget Target);
}
