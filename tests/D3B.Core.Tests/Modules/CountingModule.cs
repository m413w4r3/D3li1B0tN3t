using D3B.Core.Modules;

namespace D3B.Core.Tests.Modules;

internal sealed class CountingModule : IModule
{
    public int Builds { get; private set; }

    public void Build(BotBuilder bot)
    {
        Builds++;
    }
}
