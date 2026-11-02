using D3B.Core.Modules;

namespace D3B.Core.Tests.Modules;

internal sealed class CycleEndModule : IModule
{
    public void Build(BotBuilder bot)
    {
        bot.AddModule<CycleStartModule>();
    }
}
