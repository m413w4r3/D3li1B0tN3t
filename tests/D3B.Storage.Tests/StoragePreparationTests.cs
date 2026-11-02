using D3B.Bot.Hosting;
using D3B.Bot.Tests;
using D3B.Data.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace D3B.Storage.Tests;

public sealed class StoragePreparationTests
{
    [Fact]
    public async Task TheDatabaseIsCreatedInItsDirectoryWithEveryMigrationApplied()
    {
        await using var storage = new TestStorage();

        Assert.False(File.Exists(storage.Settings.Path));

        await storage.StartedAsync();

        var contexts = storage.Services.GetRequiredService<IDbContextFactory<StorageContext>>();

        await using var context = await contexts.CreateDbContextAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(storage.Settings.Path));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Contains(storage.Log.GetSnapshot(), record => record.Message == $"Opened the database {storage.Settings.Path}: 0 accounts, 0 characters, 0 profiles, 0 graphics learned, 0 resources' growing back, 0 fights' placements, 0 runs");
    }

    [Fact]
    public async Task ADatabaseThatDoesNotOpenSaysWhy()
    {
        using var directory = new TemporaryDirectory();

        // A directory where the file should be: SQLite cannot open it.
        Directory.CreateDirectory(directory.Combine("d3b.db"));

        await using var storage = new TestStorage(directory.Combine("d3b.db"));
        var failure = await storage.Services.GetRequiredService<IHostPreparation>().PrepareAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(failure);
        Assert.StartsWith($"The database {storage.Settings.Path} does not open: ", failure, StringComparison.Ordinal);
        Assert.EndsWith("name another file with --Storage:Path <file>.", failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostWithTheDatabaseStartsAndKeepsItWhereItIsTold()
    {
        using var install = new TestInstall();
        using var directory = new TemporaryDirectory();
        var log = new FakeLogCollector();

        install.WritePack();
        install.WriteProtocol();

        var path = directory.Combine("shared", "d3b.db");
        var bot = BotHost.CreateBuilder([.. install.Arguments, "--Storage:Path", path])
            .AddModule<StorageModule>();

        bot.Logging.AddProvider(new FakeLoggerProvider(log));

        var start = await BotHost.StartAsync(bot, TestContext.Current.CancellationToken);

        Assert.Null(start.Failure);

        using (var host = start.Host!)
        {
            Assert.Equal(path, host.Services.GetRequiredService<StorageSettings>().Path);
            Assert.True(File.Exists(path));
            Assert.Contains(log.GetSnapshot(), record => record.Message.StartsWith($"Opened the database {path}", StringComparison.Ordinal));
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void TheDatabaseLivesBesideThePacksByDefault()
    {
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "d3b.db"),
            StorageSettings.DefaultPath);
        Assert.Contains("Pooling=False", new StorageSettings { Path = "d3b.db" }.ConnectionString, StringComparison.Ordinal);
    }
}
