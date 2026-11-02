using D3B.Bot.Runtime;
using D3B.Storage.Accounts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Profiles;

/// <summary>Starts a character's profile once its client first connects while the host runs, if the character asks for it (D99).</summary>
/// <remarks>
/// Once per host's run, when the bot is made: a new connection of the client carries the activity on (D89), and an
/// activity the owner stopped stays stopped. A profile that does not start says why in the bot's journal. Off the
/// relay's read loop, which raises <see cref="Bots.Added"/> and must not wait.
/// </remarks>
internal sealed class StartOnConnection : IHostedService
{
    private readonly Bots _bots;
    private readonly AccountBook _accounts;
    private readonly ProfileBook _profiles;
    private readonly ProfileStarter _starter;
    private readonly TimeProvider _time;
    private readonly ILogger<StartOnConnection> _log;

    public StartOnConnection(Bots bots, AccountBook accounts, ProfileBook profiles, ProfileStarter starter, TimeProvider time, ILogger<StartOnConnection> log)
    {
        _bots = bots;
        _accounts = accounts;
        _profiles = profiles;
        _starter = starter;
        _time = time;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _bots.Added += OnAdded;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _bots.Added -= OnAdded;

        return Task.CompletedTask;
    }

    private void OnAdded(object? sender, BotActor bot)
    {
        if (_accounts.Find(bot.Key) is not { StartsOnConnection: true, ProfileId: { } id } || _profiles.Find(id) is not { } profile)
            return;

        _ = Task.Run(() => Start(bot, profile));
    }

    private void Start(BotActor bot, Profile profile)
    {
        var start = _starter.Start(bot, profile);

        if (start.Run is not null)
        {
            StorageLog.StartedOnConnection(_log, bot.Key.CharacterId, profile.Name);

            return;
        }

        StorageLog.NotStartedOnConnection(_log, bot.Key.CharacterId, profile.Name, start.Failure, start.Error);
        bot.Journal.Add(new ProfileNotStartedEntry(_time.GetUtcNow(), profile.Name, profile.ActivityId, start.Failure ?? ProfileStartFailure.NotStarted, start.Error));
    }
}
