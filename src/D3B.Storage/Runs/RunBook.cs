using Microsoft.EntityFrameworkCore;

namespace D3B.Storage.Runs;

/// <summary>The runs D3B kept, which the app's statistics read (D146).</summary>
/// <remarks>Read whole once the database opens, then again after each write: the app reads it every refresh, and its version tells it they changed.</remarks>
public sealed class RunBook
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private volatile Snapshot _snapshot = new([], 0);

    /// <summary>Gets the runs, the latest begun first.</summary>
    public IReadOnlyList<RunRecord> Runs => _snapshot.Runs;

    /// <summary>Gets a number that changes each time the runs are read again.</summary>
    public int Version => _snapshot.Version;

    /// <summary>Initializes a new instance of the <see cref="RunBook"/> class.</summary>
    /// <param name="contexts">Opens the database.</param>
    public RunBook(IDbContextFactory<StorageContext> contexts)
    {
        _contexts = contexts;
    }

    /// <summary>Reads the runs again.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>A task that completes once read.</returns>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await using (context.ConfigureAwait(false))
        {
            var runs = await context.Runs.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

            // SQLite orders no time with its offset: they are few enough to order here.
            _snapshot = new Snapshot([.. runs.OrderByDescending(run => run.StartedAt).ThenByDescending(run => run.Id)], _snapshot.Version + 1);
        }
    }

    private sealed record Snapshot(IReadOnlyList<RunRecord> Runs, int Version);
}
