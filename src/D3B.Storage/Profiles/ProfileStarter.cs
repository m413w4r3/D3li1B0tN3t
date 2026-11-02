using D3B.Bot.Activities;
using D3B.Bot.Runtime;
using D3B.Bot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Storage.Profiles;

/// <summary>Starts a profile on a bot: its activity, with its settings, once they hold (D99).</summary>
/// <remarks>
/// A setting whose stored value no longer reads stops the start, as settings that do not hold do: the profile is looked
/// at in the app before the bot runs on defaults it was not given.
/// </remarks>
public sealed class ProfileStarter
{
    private readonly IServiceProvider _services;
    private readonly ActivityCatalog _catalog;

    /// <summary>Initializes a new instance of the <see cref="ProfileStarter"/> class.</summary>
    /// <param name="services">The host's services, which make the activity, and the game's values the settings name.</param>
    /// <param name="catalog">The activities the modules registered.</param>
    public ProfileStarter(IServiceProvider services, ActivityCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(catalog);

        _services = services;
        _catalog = catalog;
    }

    /// <summary>Starts a profile on a bot.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>The activity's run, or why it did not start.</returns>
    public ProfileStart Start(BotActor bot, Profile profile)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(profile);

        if (!_catalog.TryGet(profile.ActivityId, out var definition))
            return new ProfileStart(null, ProfileStartFailure.ActivityMissing, [], null);

        var reading = definition.Settings.Read(profile.Settings);

        if (reading.Reset.Count > 0)
            return new ProfileStart(null, ProfileStartFailure.SettingsReset, [], null);

        var problems = definition.Settings.Check(reading.Settings, _services.GetService<GameReferences>());

        if (problems.Count > 0)
            return new ProfileStart(null, ProfileStartFailure.SettingsInvalid, problems, null);

        try
        {
            return new ProfileStart(bot.StartActivity(definition.Create(_services, reading.Settings), profile.Name), null, [], null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // An activity may fail as it is made; the bot may already run one, or be stopped with the host.
            return new ProfileStart(null, ProfileStartFailure.NotStarted, [], exception.Message);
        }
    }
}
