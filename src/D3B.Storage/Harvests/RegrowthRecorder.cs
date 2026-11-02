using System.Threading.Channels;
using D3B.Bot.Harvesting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Harvests;

/// <summary>Keeps how long the resources take to grow back, as the host's bots learn it, so that the next run knows it from its start (D145).</summary>
/// <remarks>
/// As the graphics' recorder does (D138): read back before the host starts, a skill learned on a relay's read loop queued and
/// written by the recorder's own loop with what the host knows of it then. A write that fails is logged, and the bots go on
/// knowing it until the host stops.
/// </remarks>
internal sealed class RegrowthRecorder : BackgroundService
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly Regrowths _regrowths;
    private readonly ILogger<RegrowthRecorder> _log;
    private readonly Channel<(int SkillId, TaskCompletionSource? Written)> _queue = Channel.CreateUnbounded<(int, TaskCompletionSource?)>(new UnboundedChannelOptions { SingleReader = true });

    public RegrowthRecorder(IDbContextFactory<StorageContext> contexts, Regrowths regrowths, ILogger<RegrowthRecorder> log)
    {
        _contexts = contexts;
        _regrowths = regrowths;
        _log = log;
        _regrowths.Learned += OnLearned;
    }

    private static TimeSpan? Of(long? milliseconds)
    {
        return milliseconds is { } value ? TimeSpan.FromMilliseconds(value) : null;
    }

    private static long? Of(TimeSpan? time)
    {
        return time is { } value ? (long)value.TotalMilliseconds : null;
    }

    /// <summary>Takes back what the database kept.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>How many skills.</returns>
    public async Task<int> LoadAsync(CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var regrowths = await context.Regrowths.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var regrowth in regrowths)
                _regrowths.Remember(regrowth.SkillId, new Regrowth(Of(regrowth.LongestMilliseconds), regrowth.Seen, Of(regrowth.AtMostMilliseconds)));

            return regrowths.Count;
        }
    }

    /// <summary>Waits until every growing back learned so far is written.</summary>
    /// <returns>A task that completes once written, or failed and logged.</returns>
    internal async Task WrittenAsync()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Writer.TryWrite((0, written));

        await written.Task.ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _regrowths.Learned -= OnLearned;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new HashSet<int>();
        var waiting = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Written is { } written)
                    waiting.Add(written);
                else
                    batch.Add(item.SkillId);
            }

            try
            {
                if (batch.Count > 0)
                    await WriteAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StorageLog.RegrowthsFailed(_log, exception);
            }

            batch.Clear();

            foreach (var written in waiting)
                written.SetResult();

            waiting.Clear();
        }
    }

    private void OnLearned(int skillId)
    {
        _queue.Writer.TryWrite((skillId, null));
    }

    private async Task WriteAsync(HashSet<int> batch, CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var known = await context.Regrowths.Where(regrowth => batch.Contains(regrowth.SkillId)).ToDictionaryAsync(regrowth => regrowth.SkillId, cancellationToken).ConfigureAwait(false);

            foreach (var skillId in batch)
            {
                if (!_regrowths.TryGet(skillId, out var regrowth))
                    continue;

                if (!known.TryGetValue(skillId, out var learned))
                    context.Regrowths.Add(learned = new LearnedRegrowth { SkillId = skillId });

                learned.LongestMilliseconds = Of(regrowth.Longest);
                learned.Seen = regrowth.Seen;
                learned.AtMostMilliseconds = Of(regrowth.AtMost);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
