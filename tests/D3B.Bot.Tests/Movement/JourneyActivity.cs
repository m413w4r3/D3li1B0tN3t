using D3B.Bot.Activities;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;

namespace D3B.Bot.Tests.Movement;

internal sealed class JourneyActivity : BotActivity
{
    private readonly long _mapId;
    private readonly int _teleportChanges;

    public override int TeleportChanges => _teleportChanges;

    public JourneyActivity(long mapId, int teleportChanges)
    {
        _mapId = mapId;
        _teleportChanges = teleportChanges;
    }

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        await context.TravelAsync(new HashSet<long> { _mapId }, cancellationToken);

        return false;
    }
}
