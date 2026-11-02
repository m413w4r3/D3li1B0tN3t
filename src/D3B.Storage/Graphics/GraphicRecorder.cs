using System.Threading.Channels;
using D3B.Bot.Interactives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Graphics;

/// <summary>Keeps what the host's bots learn of the maps' graphics, so that the next run knows it from its start (D138).</summary>
/// <remarks>
/// The database's graphics are taken back before the host starts (<see cref="StoragePreparation"/>). A graphic learned on a
/// relay's read loop, which must not wait, is queued, and written by the recorder's own loop with what the host knows of
/// it then, so that two bots teaching one graphic at once never write it older. A write that fails is logged, and the bots
/// go on knowing it until the host stops.
/// </remarks>
internal sealed class GraphicRecorder : BackgroundService
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly GraphicSkills _graphics;
    private readonly ILogger<GraphicRecorder> _log;
    private readonly Channel<(int GfxId, TaskCompletionSource? Written)> _queue = Channel.CreateUnbounded<(int, TaskCompletionSource?)>(new UnboundedChannelOptions { SingleReader = true });

    public GraphicRecorder(IDbContextFactory<StorageContext> contexts, GraphicSkills graphics, ILogger<GraphicRecorder> log)
    {
        _contexts = contexts;
        _graphics = graphics;
        _log = log;
        _graphics.Learned += OnLearned;
    }

    /// <summary>Takes back the graphics the database kept.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>How many.</returns>
    public async Task<int> LoadAsync(CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var graphics = await context.Graphics.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var graphic in graphics)
                _graphics.Remember(graphic.GfxId, graphic.Skills);

            return graphics.Count;
        }
    }

    /// <summary>Waits until every graphic learned so far is written.</summary>
    /// <returns>A task that completes once written, or failed and logged.</returns>
    internal async Task WrittenAsync()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Writer.TryWrite((0, written));

        await written.Task.ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _graphics.Learned -= OnLearned;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new HashSet<int>();
        var waiting = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            // Entering a map teaches a few graphics at once: they go in one write.
            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Written is { } written)
                    waiting.Add(written);
                else
                    batch.Add(item.GfxId);
            }

            try
            {
                if (batch.Count > 0)
                    await WriteAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StorageLog.GraphicsFailed(_log, exception);
            }

            batch.Clear();

            foreach (var written in waiting)
                written.SetResult();

            waiting.Clear();
        }
    }

    private void OnLearned(int gfxId)
    {
        _queue.Writer.TryWrite((gfxId, null));
    }

    private async Task WriteAsync(HashSet<int> batch, CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var known = await context.Graphics.Where(graphic => batch.Contains(graphic.GfxId)).ToDictionaryAsync(graphic => graphic.GfxId, cancellationToken).ConfigureAwait(false);

            foreach (var gfxId in batch)
            {
                if (!_graphics.TryGet(gfxId, out var skills))
                    continue;

                List<int> sorted = [.. skills.Order()];

                if (known.TryGetValue(gfxId, out var graphic))
                    graphic.Skills = sorted;
                else
                    context.Graphics.Add(new LearnedGraphic { GfxId = gfxId, Skills = sorted });
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
