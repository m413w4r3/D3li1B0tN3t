using D3B.Core.Modules;

namespace D3B.Core.Tests.Modules;

internal sealed class RequiringModule : IModule
{
    public void Build(BotBuilder bot)
    {
        bot.AddModule<RequiredModule>();
    }
}
