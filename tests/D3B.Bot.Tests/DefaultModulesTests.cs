using D3B.Bot.Data;
using D3B.Bot.Fighting;
using D3B.Bot.Harvesting;
using D3B.Bot.Network;
using D3B.Bot.Protocol;
using D3B.Bot.Runtime;
using D3B.Core.Modules;
using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Tests;

public sealed class DefaultModulesTests
{
    [Fact]
    public void TheDataModuleBuildsFirst()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<DataModule>(bot.Modules[0]);
    }

    [Fact]
    public void TheProtocolModuleBuildsAfterTheDataModule()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<ProtocolModule>(bot.Modules[1]);
    }

    [Fact]
    public void TheNetworkModuleBuildsAfterTheProtocolModule()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<NetworkModule>(bot.Modules[2]);
    }

    [Fact]
    public void TheBotsModuleBuildsAfterTheNetworkModule()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<BotsModule>(bot.Modules[3]);
    }

    [Fact]
    public void TheHarvestingModuleBuildsAfterTheBotsModule()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<HarvestingModule>(bot.Modules[4]);
    }

    [Fact]
    public void TheFightingModuleBuildsAfterTheHarvestingModule()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules());

        Assert.IsType<FightingModule>(bot.Modules[5]);
    }

    [Fact]
    public void TheDataModuleCanBeDisabledWithTheModulesRequiringIt()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true })
            .AddModules(new DefaultModules(), group => group.Disable<FightingModule>().Disable<HarvestingModule>().Disable<BotsModule>().Disable<NetworkModule>().Disable<ProtocolModule>().Disable<DataModule>());

        Assert.False(bot.HasModule<DataModule>());
    }

    [Fact]
    public void AModuleRequiringTheDataModuleBuildsItEvenDisabled()
    {
        var bot = new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true }).AddModules(new DefaultModules(), group => group.Disable<DataModule>());

        Assert.True(bot.HasModule<DataModule>());
    }
}
