using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace D3B.Bot.Runtime;

/// <summary>How long the bots' behaviors wait: each a fixed value, the one measured by default, which a user may raise (D151).</summary>
/// <remarks>
/// <para>
/// The client follows what the server shows it only once it has drawn it: a map before a walk (D77), a window before it
/// is closed (D132). The values measured on the owner's machine are the least: on a slower one the client has not drawn
/// yet, and the bot acts too soon, so the user raises them, in the app's settings or in the host's configuration, in
/// seconds (<c>Delays:Display</c> and the others). No delay is ever drawn at random (D152).
/// </para>
/// <para>
/// The others bound a wait for the server, or for the client to follow it: past one, the behavior goes on from the
/// state, as when the answer never came. A delay ends early whenever the answer comes.
/// </para>
/// </remarks>
public sealed record BotDelays
{
    /// <summary>The setting naming the client's display time.</summary>
    public const string DisplayKey = "Delays:Display";

    /// <summary>The setting naming how long a behavior waits for the server's answer.</summary>
    public const string AnswerKey = "Delays:Answer";

    /// <summary>The setting naming how long the client takes to confirm a walk past its steps.</summary>
    public const string WalkArrivalKey = "Delays:WalkArrival";

    /// <summary>The setting naming how long the client takes for each step of a walk.</summary>
    public const string WalkStepKey = "Delays:WalkStep";

    /// <summary>The setting naming how long a map change waits for the character to be placed on the new map.</summary>
    public const string PlacedKey = "Delays:Placed";

    /// <summary>The setting naming how long a use may last past the duration the server gave it.</summary>
    public const string UseEndKey = "Delays:UseEnd";

    /// <summary>The setting naming how long banking waits for what follows a deposit's answer.</summary>
    public const string BankFollowKey = "Delays:BankFollow";

    /// <summary>The setting naming how long recovery lets a use go on before the activity goes on.</summary>
    public const string RecoveryKey = "Delays:Recovery";

    /// <summary>The setting naming how long an activity lets pass between two of its steps.</summary>
    public const string BetweenStepsKey = "Delays:BetweenSteps";

    /// <summary>The configuration's key for <see cref="FightAction"/>.</summary>
    public const string FightActionKey = "Delays:FightAction";

    /// <summary>Gets the delays measured, D3B's own, which are each the least.</summary>
    public static BotDelays Measured { get; } = new();

    /// <summary>Gets the most each delay may be raised to.</summary>
    /// <remarks>Past these, a wait that went wrong would hold the bot longer than it takes the player to see it and act.</remarks>
    public static BotDelays Most { get; } = new()
    {
        Display = TimeSpan.FromSeconds(3),
        Answer = TimeSpan.FromSeconds(30),
        WalkArrival = TimeSpan.FromSeconds(10),
        WalkStep = TimeSpan.FromSeconds(3),
        Placed = TimeSpan.FromSeconds(60),
        UseEnd = TimeSpan.FromSeconds(30),
        BankFollow = TimeSpan.FromSeconds(10),
        Recovery = TimeSpan.FromMinutes(5),
        BetweenSteps = TimeSpan.FromSeconds(3),
        FightAction = TimeSpan.FromSeconds(10),
    };

    /// <summary>Gets the client's display time: how long it takes to draw a map once the server placed the character on it, and to show a window once the server opened it (D77, D132).</summary>
    /// <remarks>Measured: half a second, the walks then sent 533 to 549 ms after each map's information all confirmed, the bank's windows all closed.</remarks>
    public TimeSpan Display { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets how long a behavior waits for the server's answer to a request: a walk, a map change, a use, banking's.</summary>
    /// <remarks>Measured: the server answered every request within 150 ms.</remarks>
    public TimeSpan Answer { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Gets how long the client takes to confirm a walk once its steps are walked.</summary>
    /// <remarks>Measured: the client confirmed 0.38 to 2.9 s after the server showed the walk, and the server answered within 100 ms.</remarks>
    public TimeSpan WalkArrival { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets how long the client takes at most for each step of a walk.</summary>
    /// <remarks>Measured: a step took at most 186 ms running and 491 ms walking.</remarks>
    public TimeSpan WalkStep { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets how long a map change waits for the character to be placed once the server named the new map.</summary>
    /// <remarks>Measured: the client asked what is on the new map 0.6 to 1.4 s later, and the server answered within 101 ms.</remarks>
    public TimeSpan Placed { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets how long a use may last past the duration the server gave it, before the behavior stops waiting for its end.</summary>
    /// <remarks>Measured: a harvest ended its duration later, to the millisecond.</remarks>
    public TimeSpan UseEnd { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets how long banking waits for the weight or the kamas that follow a deposit's answer.</summary>
    /// <remarks>Measured: they come in the same millisecond.</remarks>
    public TimeSpan BankFollow { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets how long recovery lets a use the interruption left go on, before the activity goes on (D89).</summary>
    public TimeSpan Recovery { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how long an activity lets pass between two of its steps, each an action such as a harvest, a journey or a deposit (D154).</summary>
    /// <remarks>Measured through the app: steps sent one after another went faster than the client showed them; the client's display time, half a second, is the least.</remarks>
    public TimeSpan BetweenSteps { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Gets how long the fight's AI lets pass before each of its actions, its placement, its readiness and its turn's end, once the client showed what came before (D202).</summary>
    /// <remarks>
    /// Measured on the owner's 49 captured fights, the player's pace: 1.3 s halfway from a turn's start to its first action and
    /// from an action shown to the next, 0.6 s before ending a turn, 1.2 s from the placement's cells to the first move. The owner,
    /// who asked to see what the AI does, chose 0.9 s, the least: above the client's display time, below the player's median.
    /// </remarks>
    public TimeSpan FightAction { get; init; } = TimeSpan.FromMilliseconds(900);

    /// <summary>Reads the delays from a configuration, in seconds, the measured ones for what it does not set.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The delays.</returns>
    /// <exception cref="InvalidOperationException">A delay is not a number of seconds from its measured value to its most.</exception>
    public static BotDelays Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new BotDelays
        {
            Display = ReadDelay(configuration, DisplayKey, Measured.Display, Most.Display),
            Answer = ReadDelay(configuration, AnswerKey, Measured.Answer, Most.Answer),
            WalkArrival = ReadDelay(configuration, WalkArrivalKey, Measured.WalkArrival, Most.WalkArrival),
            WalkStep = ReadDelay(configuration, WalkStepKey, Measured.WalkStep, Most.WalkStep),
            Placed = ReadDelay(configuration, PlacedKey, Measured.Placed, Most.Placed),
            UseEnd = ReadDelay(configuration, UseEndKey, Measured.UseEnd, Most.UseEnd),
            BankFollow = ReadDelay(configuration, BankFollowKey, Measured.BankFollow, Most.BankFollow),
            Recovery = ReadDelay(configuration, RecoveryKey, Measured.Recovery, Most.Recovery),
            BetweenSteps = ReadDelay(configuration, BetweenStepsKey, Measured.BetweenSteps, Most.BetweenSteps),
            FightAction = ReadDelay(configuration, FightActionKey, Measured.FightAction, Most.FightAction),
        };
    }

    /// <summary>Gets the configuration these delays read back from, in seconds: a delay left as measured is not set.</summary>
    /// <returns>Each delay's setting, with no value for one left as measured, so that it follows D3B's measured value.</returns>
    public IReadOnlyDictionary<string, string?> ToConfiguration()
    {
        return new Dictionary<string, string?>
        {
            [DisplayKey] = Seconds(Display, Measured.Display),
            [AnswerKey] = Seconds(Answer, Measured.Answer),
            [WalkArrivalKey] = Seconds(WalkArrival, Measured.WalkArrival),
            [WalkStepKey] = Seconds(WalkStep, Measured.WalkStep),
            [PlacedKey] = Seconds(Placed, Measured.Placed),
            [UseEndKey] = Seconds(UseEnd, Measured.UseEnd),
            [BankFollowKey] = Seconds(BankFollow, Measured.BankFollow),
            [RecoveryKey] = Seconds(Recovery, Measured.Recovery),
            [BetweenStepsKey] = Seconds(BetweenSteps, Measured.BetweenSteps),
            [FightActionKey] = Seconds(FightAction, Measured.FightAction),
        };
    }

    /// <summary>Gets how long the client takes at most to confirm a walk of some steps the server showed, once shown.</summary>
    /// <param name="steps">The walk's steps.</param>
    /// <returns>The delay.</returns>
    public TimeSpan Confirmation(int steps)
    {
        return WalkArrival + steps * WalkStep;
    }

    private static TimeSpan ReadDelay(IConfiguration configuration, string key, TimeSpan least, TimeSpan most)
    {
        if (configuration[key] is not { Length: > 0 } value)
            return least;

        // Below what was measured, the bot would act before the client or the server is done: a delay is only raised.
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !(seconds >= least.TotalSeconds && seconds <= most.TotalSeconds))
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"{key} is '{value}', which is not a number of seconds from {least.TotalSeconds} to {most.TotalSeconds}."));

        return TimeSpan.FromSeconds(seconds);
    }

    private static string? Seconds(TimeSpan delay, TimeSpan measured)
    {
        return delay == measured ? null : delay.TotalSeconds.ToString(CultureInfo.InvariantCulture);
    }
}
