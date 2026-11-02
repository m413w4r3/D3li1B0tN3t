using D3B.Bot.Activities;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Settings;

/// <summary>An activity that keeps the settings and the services its constructor was given.</summary>
internal sealed class SettingsActivity : BotActivity
{
    public TestSettings Settings { get; }

    public TimeProvider Time { get; }

    public SettingsActivity(TestSettings settings, TimeProvider time)
    {
        Settings = settings;
        Time = time;
    }

    public override Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }
}
