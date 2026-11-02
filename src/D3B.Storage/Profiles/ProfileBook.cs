using System.Globalization;
using D3B.Bot.Activities;
using D3B.Storage.Accounts;
using Microsoft.EntityFrameworkCore;

namespace D3B.Storage.Profiles;

/// <summary>The profiles, kept in the database and in memory: a name, an activity and its settings, which characters share (D99).</summary>
/// <remarks>
/// The app reads what is in memory, every refresh, without the database; each write goes to the database first, then
/// reads every profile again, and counts a new version. Deleting a profile leaves its characters without one.
/// </remarks>
public sealed class ProfileBook : IDisposable
{
    private readonly IDbContextFactory<StorageContext> _contexts;
    private readonly AccountBook _accounts;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _writing = new(1, 1);
    private volatile Snapshot _snapshot = new([], 0);

    /// <summary>Gets the profiles, by name.</summary>
    public IReadOnlyList<Profile> Profiles => _snapshot.Profiles;

    /// <summary>Gets a number that changes each time the profiles change, for a view to tell it must read them again.</summary>
    public int Version => _snapshot.Version;

    /// <summary>Initializes a new instance of the <see cref="ProfileBook"/> class.</summary>
    /// <param name="contexts">Makes the database's contexts.</param>
    /// <param name="accounts">The characters, which a deleted profile leaves without one.</param>
    /// <param name="time">The clock that dates each save.</param>
    public ProfileBook(IDbContextFactory<StorageContext> contexts, AccountBook accounts, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(time);

        _contexts = contexts;
        _accounts = accounts;
        _time = time;
    }

    private static string Unique(string name, HashSet<string> names)
    {
        var trimmed = name.Trim();
        var unique = trimmed;

        for (var number = 2; names.Contains(unique); number++)
            unique = $"{trimmed} {number}";

        return unique.Length <= Profile.NameLength ? unique : unique[..Profile.NameLength];
    }

    /// <summary>Finds a profile.</summary>
    /// <param name="id">The profile's id.</param>
    /// <returns>The profile, or <see langword="null"/>.</returns>
    public Profile? Find(int id)
    {
        return Profiles.FirstOrDefault(profile => profile.Id == id);
    }

    /// <summary>Tells whether a name is another profile's, whatever its case.</summary>
    /// <param name="name">The name.</param>
    /// <param name="exceptId">The profile the name is for, which may keep its own.</param>
    /// <returns>Whether another profile has it.</returns>
    public bool IsTaken(string name, int? exceptId = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        var trimmed = name.Trim();

        return Profiles.Any(profile => profile.Id != exceptId && string.Equals(profile.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Saves a profile: a new one when its id is 0, else the one of its id.</summary>
    /// <param name="id">The profile's id, or 0 for a new one.</param>
    /// <param name="name">Its name, unique whatever its case, trimmed.</param>
    /// <param name="activityId">The id of the activity it runs.</param>
    /// <param name="settings">The activity's settings, as JSON (D98).</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The profile saved.</returns>
    /// <exception cref="ArgumentException">The name is blank, too long, or another profile's.</exception>
    public async Task<Profile> SaveAsync(int id, string name, string activityId, string settings, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(activityId);
        ArgumentNullException.ThrowIfNull(settings);

        var trimmed = name.Trim();

        if (trimmed.Length > Profile.NameLength)
            throw new ArgumentException($"A profile's name is {Profile.NameLength} characters at most.", nameof(name));

        if (IsTaken(trimmed, id))
            throw new ArgumentException($"A profile is already named {trimmed}.", nameof(name));

        var profile = new Profile { Id = id, Name = trimmed, ActivityId = activityId, Settings = settings, Updated = _time.GetUtcNow() };

        await WriteAsync(
            context =>
            {
                if (id == 0)
                    context.Profiles.Add(profile);
                else
                    context.Profiles.Update(profile);

                return Task.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        return Profiles.First(saved => string.Equals(saved.Name, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Deletes a profile: its characters are left without one.</summary>
    /// <param name="id">The profile's id.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>A task that completes once deleted.</returns>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await WriteAsync(
            async context =>
            {
                // A character with no profile has nothing to start on connection.
                await context.Characters
                    .Where(character => character.ProfileId == id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(character => character.StartsOnConnection, false), cancellationToken)
                    .ConfigureAwait(false);
                await context.Profiles.Where(profile => profile.Id == id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        await _accounts.LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stores, as profiles, the presets the activities give that were never stored, even if their profiles were deleted since (D168).</summary>
    /// <param name="activities">The activities, whose presets to store.</param>
    /// <param name="culture">The language the profiles are named in.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>How many profiles were stored.</returns>
    /// <remarks>A name another profile has takes a number after it, so that the user's profile keeps its own.</remarks>
    public async Task<int> StorePresetsAsync(IEnumerable<ActivityDefinition> activities, CultureInfo culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activities);
        ArgumentNullException.ThrowIfNull(culture);

        var stored = 0;

        await WriteAsync(
            async context =>
            {
                var known = (await context.Presets.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                    .Select(preset => (preset.ActivityId, preset.Key))
                    .ToHashSet();
                var names = (await context.Profiles.AsNoTracking().Select(profile => profile.Name).ToListAsync(cancellationToken).ConfigureAwait(false))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var now = _time.GetUtcNow();

                foreach (var activity in activities)
                {
                    foreach (var preset in activity.Settings.Presets)
                    {
                        if (!known.Add((activity.Id, preset.Key)))
                            continue;

                        var name = Unique(preset.Name.In(culture), names);

                        names.Add(name);
                        context.Profiles.Add(new Profile { Name = name, ActivityId = activity.Id, Settings = preset.Json, Updated = now });
                        context.Presets.Add(new StoredPreset { ActivityId = activity.Id, Key = preset.Key, Stored = now });
                        stored++;
                    }
                }
            },
            cancellationToken).ConfigureAwait(false);

        return stored;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _writing.Dispose();
    }

    /// <summary>Reads the profiles from the database again.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>A task that completes once read.</returns>
    internal Task LoadAsync(CancellationToken cancellationToken)
    {
        return WriteAsync(_ => Task.CompletedTask, cancellationToken);
    }

    private async Task WriteAsync(Func<StorageContext, Task> write, CancellationToken cancellationToken)
    {
        await _writing.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var context = await _contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            await using (context.ConfigureAwait(false))
            {
                await write(context).ConfigureAwait(false);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var profiles = await context.Profiles.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

                _snapshot = new Snapshot([.. profiles.OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)], _snapshot.Version + 1);
            }
        }
        finally
        {
            _writing.Release();
        }
    }

    private sealed record Snapshot(IReadOnlyList<Profile> Profiles, int Version);
}
