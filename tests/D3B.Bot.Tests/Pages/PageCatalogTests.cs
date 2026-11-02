using System.Globalization;
using D3B.Bot.Localization;
using D3B.Bot.Pages;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Localization;
using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Tests.Pages;

public sealed class PageCatalogTests
{
    private const string Icon = "M4 4 H20 V20 H4 Z";

    [Fact]
    public void TheBotsModuleListsThePagesModulesRegisterInTheirOrder()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        bot.Services.AddPage("first", LocalizedText.From(TestTexts.Resources, "Greeting"), Icon, _ => new object());
        bot.Services.AddPage("second", LocalizedText.Invariant("Second"), null, _ => new object());
        bot.AddModule<BotsModule>();

        using var host = bot.Build();
        var catalog = host.Services.GetRequiredService<PageCatalog>();

        Assert.Equal(["first", "second"], catalog.All.Select(definition => definition.Id));
        Assert.Equal(["Bonjour", "Second"], catalog.All.Select(definition => definition.Title.In(CultureInfo.GetCultureInfo("fr"))));
        Assert.Equal([Icon, null], catalog.All.Select(definition => definition.Icon));
        Assert.True(catalog.TryGet("second", out var found));
        Assert.Same(catalog.All[1], found);
        Assert.False(catalog.TryGet("Second", out _));
    }

    [Fact]
    public void APageIsMadeWithTheContextTheAppGivesIt()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new TestPageContext(services, CultureInfo.GetCultureInfo("en"));
        PageContext? given = null;
        var definition = new PageDefinition("page", LocalizedText.Invariant("Page"), null, page =>
        {
            given = page;

            return "the page";
        });

        Assert.Equal("the page", definition.Create(context));
        Assert.Same(context, given);
    }

    [Fact]
    public void TwoPagesCannotShareAnId()
    {
        var services = new ServiceCollection();

        services.AddPage("page", LocalizedText.Invariant("Page"), null, _ => new object());

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddPage("page", LocalizedText.Invariant("Other"), null, _ => new object()));

        Assert.Equal("A page with the id page is already registered.", exception.Message);
    }

    [Fact]
    public void TheCatalogRefusesTwoPagesRegisteredWithOneId()
    {
        PageDefinition[] definitions =
        [
            new("page", LocalizedText.Invariant("Page"), null, _ => new object()),
            new("page", LocalizedText.Invariant("Other"), null, _ => new object()),
        ];

        var exception = Assert.Throws<InvalidOperationException>(() => new PageCatalog(definitions));

        Assert.Equal("Two pages have the id page.", exception.Message);
    }
}
