using System.Collections.Immutable;
using System.Globalization;
using System.Threading.Channels;
using D3B.Bot.Fights;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Fights;

/// <summary>Keeps every fight's placement cells, the map fought on and the one left, as the host's bots see fights begin, so that they outlive the run (D275).</summary>
/// <remarks>
/// As the growing back's recorder does (D145): read back before the host starts, a set of cells seen on a relay's read loop
/// queued and written by the recorder's own loop. A write that fails is logged, and the host goes on knowing it until it stops.
/// </remarks>
internal sealed class PlacementRecorder : BackgroundService
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly FightPlacements _placements;
    private readonly ILogger<PlacementRecorder> _log;
    private readonly Channel<(FightPlacement? Placement, TaskCompletionSource? Written)> _queue = Channel.CreateUnbounded<(FightPlacement?, TaskCompletionSource?)>(new UnboundedChannelOptions { SingleReader = true });

    public PlacementRecorder(IDbContextFactory<StorageContext> contexts, FightPlacements placements, ILogger<PlacementRecorder> log)
    {
        _contexts = contexts;
        _placements = placements;
        _log = log;
        _placements.Learned += OnLearned;
    }

    private static string Of(ImmutableArray<int> cells)
    {
        return string.Join(',', cells.Select(cell => cell.ToString(CultureInfo.InvariantCulture)));
    }

    private static ImmutableArray<int> Of(string cells)
    {
        return [.. cells.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(cell => int.Parse(cell, CultureInfo.InvariantCulture))];
    }

    /// <summary>Takes back what the database kept.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>How many sets of cells.</returns>
    public async Task<int> LoadAsync(CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var placements = await context.Placements.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var placement in placements)
                _placements.Remember(new FightPlacement(placement.FromMapId, placement.FromSubAreaId, placement.FromAreaId, placement.PosX, placement.PosY, placement.MapId, Of(placement.RedCells), Of(placement.BlueCells), placement.Seen, placement.FirstSeenAt, placement.LastSeenAt));

            return placements.Count;
        }
    }

    /// <summary>Waits until every set of cells seen so far is written.</summary>
    /// <returns>A task that completes once written, or failed and logged.</returns>
    internal async Task WrittenAsync()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Writer.TryWrite((null, written));

        await written.Task.ConfigureAwait(false);
    }

    public override void Dispose()
    {
        _placements.Learned -= OnLearned;
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new Dictionary<(long, long, string, string), FightPlacement>();
        var waiting = new List<TaskCompletionSource>();

        while (await _queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
        {
            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Written is { } written)
                    waiting.Add(written);
                else if (item.Placement is { } placement)
                    batch[(placement.FromMapId, placement.MapId, Of(placement.RedCells), Of(placement.BlueCells))] = placement;
            }

            try
            {
                if (batch.Count > 0)
                    await WriteAsync(batch, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StorageLog.PlacementsFailed(_log, exception);
            }

            batch.Clear();

            foreach (var written in waiting)
                written.SetResult();

            waiting.Clear();
        }
    }

    private void OnLearned(FightPlacement placement)
    {
        _queue.Writer.TryWrite((placement, null));
    }

    private async Task WriteAsync(Dictionary<(long FromMapId, long MapId, string Red, string Blue), FightPlacement> batch, CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var maps = batch.Keys.Select(key => key.FromMapId).Distinct().ToList();
            var known = await context.Placements.Where(placement => maps.Contains(placement.FromMapId)).ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var ((fromMapId, mapId, red, blue), placement) in batch)
            {
                var learned = known.Find(stored => stored.FromMapId == fromMapId && stored.MapId == mapId && stored.RedCells == red && stored.BlueCells == blue);

                if (learned is null)
                {
                    learned = new LearnedPlacement
                    {
                        FromMapId = fromMapId,
                        FromAreaId = placement.FromAreaId,
                        FromSubAreaId = placement.FromSubAreaId,
                        PosX = placement.PosX,
                        PosY = placement.PosY,
                        MapId = mapId,
                        RedCells = red,
                        BlueCells = blue,
                        FirstSeenAt = placement.FirstSeen,
                    };
                    context.Placements.Add(learned);
                    known.Add(learned);
                }

                learned.Seen = placement.Seen;
                learned.LastSeenAt = placement.LastSeen;
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
