using D3B.Core.Modules;
using Microsoft.Extensions.Hosting;

namespace D3B.Core.Tests;

internal static class TestBots
{
    public static BotBuilder Create()
    {
        // Without the Generic Host's defaults, a test never reads the machine's environment or configuration files.
        return new BotBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
    }
}
