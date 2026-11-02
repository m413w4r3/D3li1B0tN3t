using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Bot.Localization;
using D3B.Bot.Runtime;

namespace D3B.Bot.Activities;

/// <summary>What a bot does over time: steps chosen one after another from the character's state, written by a module (D87, D116).</summary>
/// <remarks>
/// <para>
/// A step is a behavior's worth of work, such as harvesting the nearest resource, going to the next map or banking, chosen
/// from the state as it is when the step starts. The bot runs the steps one after another (<see cref="BotActor.StartActivity"/>),
/// each as a behavior, so an interruption cancels the step in progress: the player acting, the game connection closing,
/// a fight beginning (D88), the host stopping. Once the bot can act again, the activity recovers the character
/// (<see cref="RecoverAsync"/>) and goes on with its next step: since steps start from the state, the activity resumes
/// where the state allows, and keeps no position of its own.
/// </para>
/// <para>
/// Every hook but <see cref="StepAsync"/> has a default, so that an activity overrides only what it needs: when the
/// character is full and how it is unloaded, what each interruption leads to, and how the character is put back in
/// order. An activity may as well decide everything in its steps.
/// </para>
/// </remarks>
public abstract class BotActivity
{
    /// <summary>Gets the activity's name, for the logs.</summary>
    public virtual string Name => GetType().Name;

    /// <summary>Gets what the activity says of itself now, for the app (D131): none by default.</summary>
    /// <remarks>Read from any thread, while the activity runs: an activity replaces its report whole, and never changes one.</remarks>
    public virtual ActivityReport? Report => null;

    /// <summary>Gets what the activity brought so far, in figures D3B keeps (D146): none by default.</summary>
    /// <remarks>Read from any thread, while the activity runs: an activity replaces them whole.</remarks>
    public virtual ActivityStatistics? Statistics => null;

    /// <summary>Gets how many map changes a teleport counts as on the activity's journeys, its banking's included: a zaap is taken only when it saves as many (D167).</summary>
    /// <remarks>1 by default: a zaap as soon as it saves a change, as before.</remarks>
    public virtual int TeleportChanges => 1;

    /// <summary>Gets whether the AI plays the fights that cut the activity's steps, then the activity answers them once over (D198, D202).</summary>
    /// <remarks>None by default: the player plays them, as before step 9, unless the bot's switch has the AI play every fight.</remarks>
    public virtual bool FightsItsFights => false;

    /// <summary>Runs the next step, chosen from the character's state now.</summary>
    /// <param name="context">The step's behavior context.</param>
    /// <param name="cancellationToken">The step's token: an interruption or the activity's stop cancels it.</param>
    /// <returns>Whether the activity goes on: <see langword="false"/> once it is done.</returns>
    public abstract Task<bool> StepAsync(BotContext context, CancellationToken cancellationToken);

    /// <summary>Tells whether the character carries too much to go on, before each step: then the activity unloads it instead.</summary>
    /// <param name="state">The character's state.</param>
    /// <returns>Whether it is full; never, by default.</returns>
    public virtual bool IsFull(CharacterState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return false;
    }

    /// <summary>Unloads the character, such as at its bank, once it is full.</summary>
    /// <param name="context">The unloading's behavior context.</param>
    /// <param name="cancellationToken">The unloading's token.</param>
    /// <returns>Whether the character was unloaded: by default it cannot be, and a full character ends the activity.</returns>
    public virtual Task<bool> UnloadAsync(BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(false);
    }

    /// <summary>Tells what an interruption leads to, once it cancelled a step; the host stopping always ends the activity.</summary>
    /// <param name="reason">Why the step stopped.</param>
    /// <returns>
    /// <see cref="InterruptionResponse.Resume"/> by default: the activity waits as long as it must for the bot to act again,
    /// resumed after the player, played again by a new connection of the client, out of the fight, then goes on.
    /// </returns>
    public virtual InterruptionResponse OnInterrupted(Interruption reason)
    {
        return InterruptionResponse.Resume;
    }

    /// <summary>Tells what a fight the AI played for the activity leads to, once over (D198, D202).</summary>
    /// <param name="ending">How the fight ended for the character, or null when it ended unsaid, such as the connection closing.</param>
    /// <param name="response">What <see cref="OnInterrupted"/> answered as the fight cut the step.</param>
    /// <returns>That answer, by default.</returns>
    /// <remarks>Asked only of an activity that fights its own fights (<see cref="FightsItsFights"/>): the run waits for the fight's end before it pauses or stops.</remarks>
    public virtual InterruptionResponse OnFightOver(FightEnd? ending, InterruptionResponse response)
    {
        return response;
    }

    /// <summary>Says what an interruption was, for the bot's journal, once <see cref="OnInterrupted"/> answered it, such as a fight's kind (D144).</summary>
    /// <param name="reason">Why the step stopped.</param>
    /// <returns>What the activity says of it; nothing by default.</returns>
    public virtual GameText? Describe(Interruption reason)
    {
        return null;
    }

    /// <summary>Puts the character back in order once the bot can act again after an interruption, before the next step.</summary>
    /// <param name="context">The recovery's behavior context.</param>
    /// <param name="reason">Why the step stopped.</param>
    /// <param name="cancellationToken">The recovery's token.</param>
    /// <returns>A task that completes once the character is in order: by default as <see cref="Recovery.SettleAsync"/> leaves it.</returns>
    public virtual Task RecoverAsync(BotContext context, Interruption reason, CancellationToken cancellationToken)
    {
        return Recovery.SettleAsync(context, cancellationToken);
    }
}
