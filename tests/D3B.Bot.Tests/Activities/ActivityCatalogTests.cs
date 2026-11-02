using System.Globalization;
using D3B.Bot.Activities;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Localization;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Tests.Activities;

public sealed class ActivityCatalogTests
{
    [Fact]
    public void TheBotsModuleListsTheActivitiesModulesRegisterInTheirOrder()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        bot.Services.AddActivity<TestActivity>("test", "Test");
        bot.Services.AddActivity<DependentActivity>("dependent", "Dependent");
        bot.AddModule<BotsModule>();

        using var host = bot.Build();
        var catalog = host.Services.GetRequiredService<ActivityCatalog>();

        Assert.Equal(["test", "dependent"], catalog.All.Select(definition => definition.Id));
        Assert.Equal(["Test", "Dependent"], catalog.All.Select(definition => definition.Name.In(CultureInfo.InvariantCulture)));
        Assert.True(catalog.TryGet("dependent", out var found));
        Assert.Same(catalog.All[1], found);
        Assert.False(catalog.TryGet("Dependent", out _));
    }

    [Fact]
    public void AnActivityIsMadeAnewForEachStartWithTheHostsServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton(TimeProvider.System);
        services.AddActivity<DependentActivity>("dependent", "Dependent");

        using var provider = services.BuildServiceProvider();
        var definition = Assert.Single(new ActivityCatalog(provider.GetServices<ActivityDefinition>()).All);
        var first = Assert.IsType<DependentActivity>(definition.Create(provider));
        var second = Assert.IsType<DependentActivity>(definition.Create(provider));

        Assert.NotSame(first, second);
        Assert.Same(TimeProvider.System, first.Time);
    }

    [Fact]
    public void AnActivityIsNamedInTheLanguageTheAppAsksFor()
    {
        var services = new ServiceCollection();

        services.AddActivity<TestActivity>("test", LocalizedText.From(TestTexts.Resources, "Greeting"));

        using var provider = services.BuildServiceProvider();
        var definition = Assert.Single(provider.GetServices<ActivityDefinition>());

        Assert.Equal("Hello", definition.Name.In(CultureInfo.GetCultureInfo("en")));
        Assert.Equal("Bonjour", definition.Name.In(CultureInfo.GetCultureInfo("fr")));
    }

    [Fact]
    public void TwoActivitiesCannotShareAnId()
    {
        var services = new ServiceCollection();

        services.AddActivity<TestActivity>("test", "Test");

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddActivity<DependentActivity>("test", "Other"));

        Assert.Equal("An activity with the id test is already registered.", exception.Message);
    }

    [Fact]
    public void TheCatalogRefusesTwoActivitiesRegisteredWithOneId()
    {
        ActivityDefinition[] definitions =
        [
            new("test", "Test", _ => new TestActivity()),
            new("test", "Other", _ => new TestActivity()),
        ];

        var exception = Assert.Throws<InvalidOperationException>(() => new ActivityCatalog(definitions));

        Assert.Equal("Two activities have the id test.", exception.Message);
    }

    private sealed class DependentActivity : BotActivity
    {
        public TimeProvider Time { get; }

        public DependentActivity(TimeProvider time)
        {
            Time = time;
        }

        public override Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(false);
        }
    }
}
