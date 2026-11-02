using D3B.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Core.Tests.Modules;

internal sealed class ServiceModule : IModule
{
    public void Build(BotBuilder bot)
    {
        bot.Services.AddSingleton<ModuleService>();
    }
}
