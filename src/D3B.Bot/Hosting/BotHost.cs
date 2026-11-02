using D3B.Bot.Activities;
using D3B.Bot.Data;
using D3B.Bot.Pages;
using D3B.Bot.Protocol;
using D3B.Core.Modules;
using D3B.Data.Packs;
using D3B.Network.Relay;
using D3B.Protocol.Mapping;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>Assembles the bots' host from its modules, and starts it once its game data and protocol are ready.</summary>
/// <remarks>
/// The app runs this host in its own process, and <c>D3B.Host</c> runs it without the app, for measurements and tests (D91).
/// </remarks>
public static class BotHost
{
    /// <summary>Creates the builder of the bots' host, with the default modules.</summary>
    /// <param name="args">The command-line arguments, read as configuration.</param>
    /// <returns>The builder, to which a caller may add modules before it starts the host.</returns>
    public static BotBuilder CreateBuilder(string[] args)
    {
        return CreateBuilder(args, new Dictionary<string, string?>());
    }

    /// <summary>Creates the builder of the bots' host, with the default modules and settings of the caller's own.</summary>
    /// <param name="args">The command-line arguments, read as configuration.</param>
    /// <param name="settings">
    /// Settings such as the app's (D151), read before every other source: the host's own configuration and its command
    /// line win over them. One with no value is not set.
    /// </param>
    /// <returns>The builder, to which a caller may add modules before it starts the host.</returns>
    public static BotBuilder CreateBuilder(string[] args, IReadOnlyDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The modules read the configuration as they are built, below: the settings must be in it before.
        var configuration = new ConfigurationManager();

        configuration.AddInMemoryCollection(settings.Where(setting => setting.Value is not null));

        var bot = new BotBuilder(new HostApplicationBuilderSettings { Args = args, Configuration = configuration });

        bot.AddModules(new DefaultModules());

        return bot;
    }

    /// <summary>Builds the host, gets the game data and the protocol ready, then starts it.</summary>
    /// <param name="bot">The host's builder.</param>
    /// <param name="cancellationToken">Stops getting the host ready.</param>
    /// <returns>
    /// The host, started, which the caller stops and disposes; or why it did not start, for lack of game data, of a mapping
    /// that holds, or of the address its relay listens on, which is also logged.
    /// </returns>
    public static async Task<BotHostStart> StartAsync(BotBuilder bot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var host = bot.Build();

        try
        {
            LogComposition(host, bot.Modules);

            var failure = PrepareCatalogs(host)
                ?? PrepareData(host)
                ?? PrepareProtocol(host)
                ?? await PrepareModulesAsync(host, cancellationToken).ConfigureAwait(false)
                ?? await StartHostAsync(host, cancellationToken).ConfigureAwait(false);

            if (failure is null)
                return new BotHostStart(host, null);

            host.Dispose();

            return failure;
        }
        catch
        {
            host.Dispose();

            throw;
        }
    }

    /// <summary>Starts the host as <see cref="StartAsync"/> does, then runs it until it is stopped.</summary>
    /// <param name="bot">The host's builder.</param>
    /// <param name="cancellationToken">Stops the host.</param>
    /// <returns>
    /// The process's exit code: 0 once stopped, 1 when the host did not start for lack of game data, of a mapping that
    /// holds, or of the address its relay listens on.
    /// </returns>
    public static async Task<int> RunAsync(BotBuilder bot, CancellationToken cancellationToken)
    {
        var start = await StartAsync(bot, cancellationToken).ConfigureAwait(false);

        if (start.Host is not { } host)
            return 1;

        using (host)
            await host.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);

        return 0;
    }

    private static void LogComposition(IHost host, IReadOnlyList<IModule> modules)
    {
        var log = Logger(host);
        var names = string.Join(", ", modules.Select(module => module.GetType().Name));

        HostLog.ModulesBuilt(log, modules.Count, names);
    }

    private static BotHostStart? PrepareCatalogs(IHost host)
    {
        try
        {
            // Before the host starts: two activities or pages sharing an id, however registered, stop it here, rather than
            // the app that reads the catalogues later, which holds every client's connection (D91).
            host.Services.GetService<ActivityCatalog>();
            host.Services.GetService<PageCatalog>();

            return null;
        }
        catch (InvalidOperationException exception)
        {
            return Fails(host, exception.Message);
        }
    }

    private static BotHostStart? PrepareData(IHost host)
    {
        // A host whose data module was disabled runs without game data.
        if (host.Services.GetService<PackSource>() is not { } source)
            return null;

        try
        {
            // Before the host starts, so that a pack missing or unreadable stops it before any service reads it.
            var pack = host.Services.GetRequiredService<GamePack>();
            var log = Logger(host);

            HostLog.PackOpened(log, pack.Info.ClientVersion, GamePack.FormatVersion, source.PackPath);

            return null;
        }
        catch (DataUnavailableException exception)
        {
            return Fails(host, exception.Message, exception.MissingBuild);
        }
    }

    private static BotHostStart? PrepareProtocol(IHost host)
    {
        // A host whose protocol module was disabled, and that no module requires, reads no client's wire.
        if (host.Services.GetService<ProtocolSource>() is not { } source)
            return null;

        try
        {
            // The version gate (D11): before the host starts, so that no connection is relayed without a mapping that holds.
            var translation = host.Services.GetRequiredService<ProtocolTranslation>();
            var audit = translation.Audit;
            var log = Logger(host);

            HostLog.MappingLoaded(log, translation.ClientVersion, MappingFile.FormatVersion, audit.Messages, audit.Fields, audit.Enums, audit.Values, source.MappingPath);

            return null;
        }
        catch (ProtocolUnavailableException exception)
        {
            return Fails(host, exception.Message, exception.MissingBuild);
        }
    }

    private static async Task<BotHostStart?> PrepareModulesAsync(IHost host, CancellationToken cancellationToken)
    {
        foreach (var preparation in host.Services.GetServices<IHostPreparation>())
        {
            if (await preparation.PrepareAsync(cancellationToken).ConfigureAwait(false) is { } failure)
                return Fails(host, failure);
        }

        return null;
    }

    private static async Task<BotHostStart?> StartHostAsync(IHost host, CancellationToken cancellationToken)
    {
        try
        {
            await host.StartAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (RelayUnavailableException exception)
        {
            return Fails(host, exception.Message);
        }
    }

    private static BotHostStart Fails(IHost host, string failure, string? missingBuild = null)
    {
        var log = Logger(host);

        HostLog.DoesNotStart(log, failure);

        return new BotHostStart(null, failure, missingBuild);
    }

    private static ILogger Logger(IHost host)
    {
        return host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(BotHost).FullName!);
    }
}
