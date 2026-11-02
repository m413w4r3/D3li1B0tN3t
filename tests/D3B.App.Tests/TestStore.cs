using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Network;
using D3B.Bot.Tests.Activities;
using D3B.Bot.Tests.Runtime;
using D3B.Bot.Tests.Settings;
using D3B.Data.Tests;
using D3B.Storage.Accounts;
using D3B.Storage.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests;

/// <summary>The bots a test plays, a database of its own, the game's values of a small world, and two activities: a harvest with settings of every kind, and a stroll with none.</summary>
internal sealed class TestStore : IAsyncDisposable
{
    public const string HarvestId = "harvest";
    public const string StrollId = "stroll";

    private readonly TemporaryDirectory _directory = new();
    private readonly TestGameData _game = new();
    private IHost? _host;

    public TestPlay Play { get; } = new();

    public IHost Host => _host!;

    public AccountBook Accounts => Host.Services.GetRequiredService<AccountBook>();

    public ProfileBook Profiles => Host.Services.GetRequiredService<ProfileBook>();

    public static ActivityDefinition Harvest()
    {
        var services = new ServiceCollection();

        services.AddActivity<SettingsActivity, TestSettings>(HarvestId, LocalizedText.Invariant("Harvest"), settings =>
        {
            settings.Section(LocalizedText.Invariant("What"));
            settings.Choice(s => s.Focus, LocalizedText.Invariant("Focus"), focus => LocalizedText.Invariant($"On {focus}"));
            settings.Job(s => s.Job, LocalizedText.Invariant("Job")).Required().VisibleWhen(s => s.Focus == TestFocus.Job);
            settings.Resources(s => s.Resources, LocalizedText.Invariant("Resources")).Within(s => s.Job).Ordered();
            settings.Section(LocalizedText.Invariant("Where"));
            settings.Area(s => s.Area, LocalizedText.Invariant("Area"));
            settings.SubAreas(s => s.SubAreas, LocalizedText.Invariant("Sub-areas")).Within(s => s.Area);
            settings.Section(LocalizedText.Invariant("How"));
            settings.Number(s => s.Walks, LocalizedText.Invariant("Walks"), 1, 100);
            settings.Flag(s => s.UseBank, LocalizedText.Invariant("Bank"));
            settings.Text(s => s.Greeting, LocalizedText.Invariant("Greeting"), 10);
            settings.Choices(s => s.Order, LocalizedText.Invariant("Order"), focus => LocalizedText.Invariant($"On {focus}")).Ordered();
            settings.Number(s => s.Lowest, LocalizedText.Invariant("Lowest"), 0, 10);
            settings.Number(s => s.Highest, LocalizedText.Invariant("Highest"), 0, 10);
            settings.Rule(s => s.Lowest <= s.Highest, LocalizedText.Invariant("The lowest is above the highest."));
        });

        return services.Select(descriptor => descriptor.ImplementationInstance).OfType<ActivityDefinition>().Single();
    }

    public static string Settings(TestSettings settings)
    {
        return Harvest().Settings.Write(settings);
    }

    public async Task<TestStore> OpenedAsync()
    {
        _host = await TestHosts.WithStorageAsync(Play, _directory.Combine("d3b.db"), _game, Harvest(), new ActivityDefinition(StrollId, "Stroll", _ => new TestActivity()));

        return this;
    }

    /// <summary>Records an account and its characters, as the relay would.</summary>
    public async Task SeeAsync(GameAccount account, params (long Id, string Name, int? Level)[] characters)
    {
        await Accounts.SeeAsync(account, characters, Play.Time.GetUtcNow(), TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _host?.Dispose();
        await Play.DisposeAsync();
        _game.Dispose();
        _directory.Dispose();
    }
}
