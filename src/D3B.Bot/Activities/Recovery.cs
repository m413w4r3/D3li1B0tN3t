using D3B.Bot.Banks;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;

namespace D3B.Bot.Activities;

/// <summary>Putting the character back in order after an interruption, before an activity goes on (D89).</summary>
/// <remarks>
/// <para>
/// What an interruption leaves, measured (client.md): the player may have left a dialog or the bank open, a harvest or a
/// walk may still go on, and a walk the client did not follow may leave the server holding the character (D85). A new
/// game connection starts from what the server sends, the character standing where its last walk the client confirmed
/// left it, the server holding nothing; a fight ends with the character on the map it began on.
/// </para>
/// <para>
/// So the recovery lets a use end, then a walk, settling one the client does not confirm where the server has the
/// character, leaves a dialog or exchange still open, and lets go of a walk the server holds. Each wait is bounded
/// (<see cref="BotDelays.Recovery"/>, <see cref="BotDelays.Confirmation"/>): past it, the next step finds the state as it
/// is.
/// </para>
/// </remarks>
public static class Recovery
{
    /// <summary>Puts the character back in order, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>A task that completes once the character is in order, or each wait passed.</returns>
    public static async Task SettleAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A harvest goes on to its end, which a request would not shorten: the server refuses a walk during one.
        if (context.State.IsUsing)
            await context.WaitUntilAsync(state => !state.IsUsing, context.Delays.Recovery, cancellationToken).ConfigureAwait(false);

        if (context.State is { IsWalking: true } walking
            && !await context.WaitUntilAsync(state => !state.IsWalking, context.Delays.Confirmation(walking.Path.Length - 1), cancellationToken).ConfigureAwait(false))
        {
            await context.SettleWalkAsync().ConfigureAwait(false);
        }

        if (context.State.IsInDialog)
            await context.LeaveBankAsync(cancellationToken).ConfigureAwait(false);

        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);
    }
}
