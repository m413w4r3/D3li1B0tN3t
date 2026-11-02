using System.Collections.Concurrent;
using System.Threading.Channels;
using D3B.Bot.Activities;
using D3B.Bot.Characters;
using D3B.Bot.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Tests.Activities;

/// <summary>An activity whose every step waits for a kamas update from the server, done once the kamas are 0.</summary>
internal sealed class TestActivity : BotActivity
{
    private readonly Channel<int> _stepping = Channel.CreateUnbounded<int>();
    private int _started;
    private volatile bool _emptied;

    /// <summary>Gets or sets when the character is full, until it is unloaded once; never, by default.</summary>
    public Func<CharacterState, bool> Full { get; init; } = _ => false;

    /// <summary>Gets or sets whether unloading empties the character.</summary>
    public bool UnloadEmpties { get; init; } = true;

    /// <summary>Gets or sets what every interruption leads to.</summary>
    public InterruptionResponse Response { get; init; } = InterruptionResponse.Resume;

    /// <summary>Gets or sets whether a step throws instead of waiting.</summary>
    public bool Throws { get; init; }

    /// <summary>Gets the number of steps started.</summary>
    public int Started => Volatile.Read(ref _started);

    /// <summary>Gets the number of times the character was unloaded.</summary>
    public int Unloads { get; private set; }

    /// <summary>Gets the interruptions the activity was told of, in order.</summary>
    public ConcurrentQueue<Interruption> Interruptions { get; } = new();

    /// <summary>Gets the interruptions the activity recovered the character after, in order.</summary>
    public ConcurrentQueue<Interruption> Recoveries { get; } = new();

    public override async Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (Throws)
            throw new InvalidOperationException("The step failed.");

        var update = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is KamasUpdateEvent);

        // The delay starts with the step, before the test moves the clock.
        var waiting = update.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

        _stepping.Writer.TryWrite(Interlocked.Increment(ref _started));

        return (await waiting as KamasUpdateEvent)?.Kamas is not 0;
    }

    public override bool IsFull(CharacterState state)
    {
        return !_emptied && Full(state);
    }

    public override Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        Unloads++;
        _emptied = UnloadEmpties;

        return Task.FromResult(true);
    }

    public override InterruptionResponse OnInterrupted(Interruption reason)
    {
        Interruptions.Enqueue(reason);

        return Response;
    }

    public override async Task RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        Recoveries.Enqueue(reason);
        await base.RecoverAsync(context, reason, cancellationToken);
    }

    /// <summary>Waits until a step started and the bot's loop has its expectation: a frame shown after it reaches it.</summary>
    public async Task<int> SteppingAsync(BotActor bot)
    {
        var step = await _stepping.Reader.ReadAsync(TestContext.Current.CancellationToken);

        await bot.ProcessedAsync();

        return step;
    }
}
