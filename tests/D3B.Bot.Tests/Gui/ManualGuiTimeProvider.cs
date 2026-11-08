namespace D3B.Bot.Tests.Gui;

internal sealed class ManualGuiTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; }

    public ManualGuiTimeProvider(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return UtcNow;
    }
}
