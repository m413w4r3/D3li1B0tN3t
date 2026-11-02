using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Runs;

/// <summary>Keeps each run of the host's bots for the statistics, as it goes and once over (D146).</summary>
/// <remarks>
/// <para>
/// It looks at the bots every few seconds, off their loops: a run seen first is written at once; then again when it banked,
/// once a minute while its figures change, and when it is over, so that a crash loses at most a minute of it. The host
/// stopping writes what it has.
/// </para>
/// <para>
/// A profession's experience is told from the character as the run found it; one the bot had not seen then, such as before
/// the server described the professions, counts from the first time this recorder saw it.
/// </para>
/// </remarks>
internal sealed class RunRecorder : BackgroundService
{
    private static readonly TimeSpan _period = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _writeEvery = TimeSpan.FromMinutes(1);

    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly RunBook _book;
    private readonly IServiceProvider _services;
    private readonly TimeProvider _time;
    private readonly ILogger<RunRecorder> _log;
    private readonly Dictionary<ActivityRun, Tracked> _runs = new(ReferenceEqualityComparer.Instance);

    public RunRecorder(IDbContextFactory<StorageContext> contexts, RunBook book, IServiceProvider services, TimeProvider time, ILogger<RunRecorder> log)
    {
        _contexts = contexts;
        _book = book;
        _services = services;
        _time = time;
        _log = log;
    }

    private static string Signature(RunRecord record)
    {
        return $"{string.Join(',', record.Items.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"))}|{string.Join(',', record.Jobs.Select(job => $"{job.JobId}:{job.Experience}"))}|{record.Banks}|{record.Protectors}|{record.Aggressions}|{record.Outcome}";
    }

    private static void Fill(RunRecord record, BotActor bot, ActivityRun run, Tracked tracked, DateTimeOffset now)
    {
        var statistics = run.Activity.Statistics;

        record.CharacterName = bot.State.Name;
        record.LastAt = run.EndedAt ?? now;
        record.EndedAt = run.EndedAt;
        record.Outcome = run.IsOver ? run.Completion.Result : null;
        record.Items = statistics?.Items.ToDictionary() ?? [];
        record.Banks = statistics?.Banks ?? 0;
        record.Protectors = statistics?.Fights.GetValueOrDefault(FightKind.Protector) ?? 0;
        record.Aggressions = statistics?.Fights.GetValueOrDefault(FightKind.Aggression) ?? 0;

        var jobs = new List<RunJob>();

        foreach (var job in bot.State.Jobs.Values)
        {
            if (!tracked.Baseline.TryGetValue(job.JobId, out var from))
                tracked.Baseline[job.JobId] = from = (job.Level, job.Experience);

            if (job.Experience > from.Experience || job.Level > from.Level)
                jobs.Add(new RunJob(job.JobId, from.Level, job.Level, Math.Max(0, job.Experience - from.Experience)));
        }

        record.Jobs = jobs;
    }

    /// <summary>Looks at the bots once, and writes what changed; for the tests, which move the clock.</summary>
    /// <param name="cancellationToken">Stops the writes.</param>
    /// <returns>A task that completes once written.</returns>
    internal async Task PollAsync(CancellationToken cancellationToken)
    {
        if (_services.GetService<Bots>() is not { } bots)
            return;

        var now = _time.GetUtcNow();
        var written = false;

        foreach (var bot in bots.All)
        {
            if (bot.Activity is not { } run)
                continue;

            if (!_runs.TryGetValue(run, out var tracked))
            {
                tracked = new Tracked(new RunRecord
                {
                    AccountId = bot.Key.AccountId,
                    CharacterId = bot.Key.CharacterId,
                    Title = run.Title,
                    Activity = run.Activity.Name,
                    StartedAt = run.StartedAt,
                });

                foreach (var job in run.StartedWith.Jobs.Values)
                    tracked.Baseline[job.JobId] = (job.Level, job.Experience);

                _runs[run] = tracked;
            }

            if (tracked.IsDone)
                continue;

            var record = tracked.Record;
            var banks = record.Banks;

            Fill(record, bot, run, tracked, now);

            var signature = Signature(record);
            var over = run.IsOver;

            if (record.Id is 0 || over || record.Banks != banks || (signature != tracked.Signature && now - tracked.WrittenAt >= _writeEvery))
            {
                await WriteAsync(record, cancellationToken).ConfigureAwait(false);
                tracked.Signature = signature;
                tracked.WrittenAt = now;
                tracked.IsDone = over;
                written = true;
            }
        }

        if (written)
            await _book.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_period, _time);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await PollQuietlyAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host stops: what the runs brought since the last look is written below.
        }

        await PollQuietlyAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PollQuietlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PollAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StorageLog.RunsFailed(_log, exception);
        }
    }

    private async Task WriteAsync(RunRecord record, CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            if (record.Id is 0)
                context.Runs.Add(record);
            else
                context.Runs.Update(record);

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed class Tracked
    {
        public RunRecord Record { get; }

        public Dictionary<int, (int Level, long Experience)> Baseline { get; } = [];

        public string? Signature { get; set; }

        public DateTimeOffset WrittenAt { get; set; }

        public bool IsDone { get; set; }

        public Tracked(RunRecord record)
        {
            Record = record;
        }
    }
}
