using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Hosting;
using D3B.Storage.Accounts;
using D3B.Storage.Fights;
using D3B.Storage.Graphics;
using D3B.Storage.Harvests;
using D3B.Storage.Profiles;
using D3B.Storage.Runs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace D3B.Storage;

/// <summary>Opens the database before the host starts: creates it, applies its migrations, stores the activities' presets never stored, and reads the accounts, the profiles, the graphics learned, the resources' growing back, the fights' placement cells and the runs (D100, D138, D145, D146, D168, D275).</summary>
/// <remarks>The presets are named in the system's language, the one the app starts in by default.</remarks>
internal sealed class StoragePreparation : IHostPreparation
{
    private readonly StorageSettings _settings;
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly AccountBook _accounts;
    private readonly ProfileBook _profiles;
    private readonly GraphicRecorder _graphics;
    private readonly RegrowthRecorder _regrowths;
    private readonly PlacementRecorder _placements;
    private readonly RunBook _runs;
    private readonly ILogger<StoragePreparation> _log;
    private readonly ActivityCatalog? _activities;

    public StoragePreparation(StorageSettings settings, IDbContextFactory<StorageContext> contexts, AccountBook accounts, ProfileBook profiles, GraphicRecorder graphics, RegrowthRecorder regrowths, PlacementRecorder placements, RunBook runs, ILogger<StoragePreparation> log, ActivityCatalog? activities = null)
    {
        _activities = activities;
        _settings = settings;
        _contexts = contexts;
        _accounts = accounts;
        _profiles = profiles;
        _graphics = graphics;
        _regrowths = regrowths;
        _placements = placements;
        _runs = runs;
        _log = log;
    }

    public async Task<string?> PrepareAsync(CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settings.Path)!);

            var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            await using (context.ConfigureAwait(false))
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

            await _accounts.LoadAsync(cancellationToken).ConfigureAwait(false);
            await _profiles.LoadAsync(cancellationToken).ConfigureAwait(false);

            if (_activities is not null && await _profiles.StorePresetsAsync(_activities.All, CultureInfo.CurrentUICulture, cancellationToken).ConfigureAwait(false) is > 0 and var presets)
                StorageLog.PresetsStored(_log, presets);

            var graphics = await _graphics.LoadAsync(cancellationToken).ConfigureAwait(false);
            var regrowths = await _regrowths.LoadAsync(cancellationToken).ConfigureAwait(false);
            var placements = await _placements.LoadAsync(cancellationToken).ConfigureAwait(false);

            await _runs.LoadAsync(cancellationToken).ConfigureAwait(false);
            StorageLog.Opened(_log, _settings.Path, _accounts.Accounts.Count, _accounts.Characters.Count, _profiles.Profiles.Count, graphics, regrowths, placements, _runs.Runs.Count);

            return null;
        }
        catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or DbUpdateException)
        {
            return $"The database {_settings.Path} does not open: {exception.Message} Close what else holds it, or name another file with --{StorageSettings.PathKey} <file>.";
        }
    }
}
