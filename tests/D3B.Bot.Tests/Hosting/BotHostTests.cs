using System.Globalization;
using System.Net;
using System.Net.Sockets;
using D3B.Bot.Hosting;
using D3B.Bot.Localization;
using D3B.Bot.Pages;
using D3B.Core.Modules;
using D3B.Data.Packs;
using D3B.Network.Relay;
using D3B.Protocol.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Bot.Tests.Hosting;

public sealed class BotHostTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly TestInstall _install = new();
    private readonly FakeLogCollector _log = new();

    private static bool IsStarted(FakeLogRecord record)
    {
        return record.Category == "Microsoft.Hosting.Lifetime" && record.Message.StartsWith("Application started", StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public async Task TheHostStartsOnThePackOfTheInstalledBuildAndStopsCleanly()
    {
        _install.WritePack();
        _install.WriteProtocol();

        var bot = CreateBuilder();

        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Contains(_log.GetSnapshot(), record => record.Message == $"Opened the pack of client {TestInstall.Version}, format {GamePack.FormatVersion}: {_install.PackPath}");
        Assert.Contains(_log.GetSnapshot(), record => record.Message.StartsWith($"Loaded the mapping of client {TestInstall.Version}, format {MappingFile.FormatVersion}: ", StringComparison.Ordinal)
            && record.Message.EndsWith($" values audited, {_install.MappingPath}", StringComparison.Ordinal));
        Assert.Contains(_log.GetSnapshot(), IsStarted);
        Assert.Contains(_log.GetSnapshot(), record => record.Message.StartsWith("Clients reach D3B: their configuration sends localhost to 127.0.13.59", StringComparison.Ordinal));
        Assert.EndsWith($"ConfigUrl={_install.ConfigUrl}\n", File.ReadAllText(_install.VersionFile), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostDoesNotStartWithoutAPack()
    {
        var bot = CreateBuilder();

        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.Contains($"No pack for client {TestInstall.Version}", failure.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/extract.cs", failure.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(_install.PackPath));
    }

    [Fact]
    public async Task StartingTheHostSaysWhyItDidNotStart()
    {
        var bot = CreateBuilder();

        var start = await BotHost.StartAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Null(start.Host);
        Assert.NotNull(start.Failure);
        Assert.Contains($"No pack for client {TestInstall.Version}", start.Failure, StringComparison.Ordinal);
        Assert.EndsWith(start.Failure, failure.Message, StringComparison.Ordinal);
        Assert.Equal(TestInstall.Version, start.MissingBuild);
    }

    [Fact]
    public async Task AMappingMissingNamesTheBuildAppsLack()
    {
        _install.WritePack();

        var start = await BotHost.StartAsync(CreateBuilder(), TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Null(start.Host);
        Assert.Equal(TestInstall.Version, start.MissingBuild);
    }

    [Fact]
    public async Task AMappingThatDoesNotHoldNamesNoBuildMissing()
    {
        _install.WritePack();
        _install.WriteProtocol("3.7.11.1");

        var start = await BotHost.StartAsync(CreateBuilder(), TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Null(start.Host);
        Assert.Contains("maps client 3.7.11.1", start.Failure, StringComparison.Ordinal);
        Assert.Null(start.MissingBuild);
    }

    [Fact]
    public async Task TheHostDoesNotStartWithoutAMapping()
    {
        _install.WritePack();

        var bot = CreateBuilder();

        // A new build, which no one has mapped yet.
        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.Contains($"No schema for client {TestInstall.Version}", failure.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/schema.cs", failure.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet run --file scripts/mapping.cs", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostDoesNotStartOnAMappingThatDoesNotHold()
    {
        _install.WritePack();
        _install.WriteProtocol(edit: mapping => mapping.Messages.RemoveAt(0));

        var bot = CreateBuilder();

        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.StartsWith($"The host does not start. The mapping of client {TestInstall.Version} does not hold: ", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostDoesNotStartWhenItsRelayCannotListen()
    {
        _install.WritePack();
        _install.WriteProtocol();

        // Another program holding the port on D3B's address, as one might hold 443.
        using var holder = new TcpListener(RelaySettings.DefaultAddress, 0);

        holder.Start();

        var port = ((IPEndPoint)holder.LocalEndpoint).Port;
        var bot = CreateBuilder("--Network:Ports:0", port.ToString(CultureInfo.InvariantCulture));

        bot.Services.AddHostedService<StopOnceStarted>();

        var exitCode = await BotHost.RunAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Equal(1, exitCode);
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
        Assert.Contains($"D3B cannot listen on {RelaySettings.DefaultAddress} on ports {port}", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostDoesNotStartWithTwoPagesOfOneId()
    {
        var bot = CreateBuilder();

        // Registered without AddPage, which refuses a second id as the modules build: the catalogue refuses it before the host starts.
        bot.Services.AddSingleton(new PageDefinition("page", LocalizedText.Invariant("Page"), null, _ => new object()));
        bot.Services.AddSingleton(new PageDefinition("page", LocalizedText.Invariant("Other"), null, _ => new object()));

        var start = await BotHost.StartAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var failure = Assert.Single(_log.GetSnapshot(), record => record.Level is LogLevel.Critical);

        Assert.Null(start.Host);
        Assert.Equal("Two pages have the id page.", start.Failure);
        Assert.Equal("The host does not start. Two pages have the id page.", failure.Message);
    }

    [Fact]
    public async Task AModulesPreparationThatFailsStopsTheHostOnceTheDataAndProtocolAreReady()
    {
        _install.WritePack();
        _install.WriteProtocol();

        var bot = CreateBuilder();
        var first = new TestPreparation(null);
        var failing = new TestPreparation("The database is locked.");
        var never = new TestPreparation(null);

        bot.Services.AddSingleton<IHostPreparation>(first);
        bot.Services.AddSingleton<IHostPreparation>(failing);
        bot.Services.AddSingleton<IHostPreparation>(never);

        var start = await BotHost.StartAsync(bot, TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Null(start.Host);
        Assert.Equal("The database is locked.", start.Failure);
        Assert.True(first.Prepared && failing.Prepared);
        Assert.False(never.Prepared);
        Assert.Contains(_log.GetSnapshot(), record => record.Message.StartsWith("Loaded the mapping", StringComparison.Ordinal));
        Assert.Contains(_log.GetSnapshot(), record => record.Level is LogLevel.Critical && record.Message == "The host does not start. The database is locked.");
        Assert.DoesNotContain(_log.GetSnapshot(), IsStarted);
    }

    private BotBuilder CreateBuilder(params string[] arguments)
    {
        var bot = BotHost.CreateBuilder([.. _install.Arguments, .. arguments]);

        bot.Logging.AddProvider(new FakeLoggerProvider(_log));

        return bot;
    }
}
