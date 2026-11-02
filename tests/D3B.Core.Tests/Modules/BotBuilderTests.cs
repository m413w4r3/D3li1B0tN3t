using Microsoft.Extensions.DependencyInjection;

namespace D3B.Core.Tests.Modules;

public sealed class BotBuilderTests
{
    [Fact]
    public void AModuleTypeIsBuiltOnce()
    {
        var first = new CountingModule();
        var bot = TestBots.Create().AddModule(first).AddModule(new CountingModule()).AddModule<CountingModule>();

        Assert.Equal(1, first.Builds);
        Assert.Same(first, Assert.Single(bot.Modules));
        Assert.True(bot.HasModule<CountingModule>());
    }

    [Fact]
    public void AModuleIsBuiltAfterTheModulesItRequires()
    {
        var bot = TestBots.Create().AddModule<RequiringModule>();

        Assert.Equal([typeof(RequiredModule), typeof(RequiringModule)], bot.Modules.Select(module => module.GetType()));
    }

    [Fact]
    public void ARequirementLeadingBackToAModuleBeingBuiltDoesNothing()
    {
        var bot = TestBots.Create().AddModule<CycleStartModule>();

        Assert.Equal([typeof(CycleEndModule), typeof(CycleStartModule)], bot.Modules.Select(module => module.GetType()));
    }

    [Fact]
    public void TheServicesOfAModuleResolveFromTheBuiltHost()
    {
        using var host = TestBots.Create().AddModule<ServiceModule>().Build();

        Assert.NotNull(host.Services.GetService<ModuleService>());
    }

    [Fact]
    public void ABuiltBuilderRejectsModules()
    {
        var bot = TestBots.Create();

        using var host = bot.Build();

        Assert.Throws<InvalidOperationException>(() => bot.AddModule<CountingModule>());
        Assert.Throws<InvalidOperationException>(() => bot.AddModules(new SampleGroup()));
        Assert.Throws<InvalidOperationException>(() => bot.Build());
    }
}
