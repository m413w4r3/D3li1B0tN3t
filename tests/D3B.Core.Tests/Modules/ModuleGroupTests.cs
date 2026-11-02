using D3B.Core.Modules;

namespace D3B.Core.Tests.Modules;

public sealed class ModuleGroupTests
{
    [Fact]
    public void AGroupAddsItsModulesInOrder()
    {
        var bot = TestBots.Create().AddModules(new SampleGroup());

        Assert.Equal([typeof(RequiredModule), typeof(CountingModule)], bot.Modules.Select(module => module.GetType()));
    }

    [Fact]
    public void AGroupListsAModuleTypeOnce()
    {
        var first = new CountingModule();
        var group = new ModuleGroupBuilder().Add(first).Add(new CountingModule()).Add<CountingModule>();

        Assert.Same(first, Assert.Single(group.Modules));
    }

    [Fact]
    public void AGroupSkipsTheModulesAlreadyAdded()
    {
        var first = new CountingModule();
        var bot = TestBots.Create().AddModule(first).AddModules(new SampleGroup());

        Assert.Equal(1, first.Builds);
        Assert.Equal([typeof(CountingModule), typeof(RequiredModule)], bot.Modules.Select(module => module.GetType()));
    }

    [Fact]
    public void ADisabledModuleOfAGroupIsNotAdded()
    {
        var bot = TestBots.Create().AddModules(new SampleGroup(), group => group.Disable<CountingModule>());

        Assert.False(bot.HasModule<CountingModule>());
        Assert.True(bot.HasModule<RequiredModule>());
    }

    [Fact]
    public void ADisabledModuleIsStillBuiltWhenAnotherModuleRequiresIt()
    {
        var bot = TestBots.Create().AddModules(new SampleGroup(), group => group.Disable<RequiredModule>()).AddModule<RequiringModule>();

        Assert.True(bot.HasModule<RequiredModule>());
    }

    [Fact]
    public void DisablingAModuleOutsideTheGroupFails()
    {
        var bot = TestBots.Create();

        Assert.Throws<InvalidOperationException>(() => bot.AddModules(new SampleGroup(), group => group.Disable<ServiceModule>()));
    }
}
