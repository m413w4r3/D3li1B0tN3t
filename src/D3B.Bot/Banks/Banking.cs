using D3B.Bot.Characters;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Data.Banks;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Banks;

/// <summary>Banking: going to the nearest bank's banker, opening the bank, putting stacks and kamas in it, and leaving (D79, D80).</summary>
/// <remarks>
/// <para>
/// Measured (client.md): the bot talks to the banker, NPC action 3, from anywhere on the map; the server opens the dialog
/// and asks its question, offering two replies, one of which the pack lists among the banker's (D29). That reply ends the
/// dialog and opens the exchange with the character's storage in the same millisecond, the opening paid with as many
/// kamas as the storage holds stacks. Stacks go in by one request listing them, the server taking those it may; kamas by
/// another. Leaving ends the exchange, and the client then sends two requests to leave by itself, which the server does
/// not answer. Each answer comes 28 ms after its request.
/// </para>
/// <para>
/// The server refuses an action on an NPC the map lacks, or during a dialog, with <see cref="NpcActionErrorEvent"/>, asks
/// its question again for a reply it did not offer, and answers nothing to a stack or kama it does not take, nor to a
/// request outside the exchange. The character cannot walk during a dialog or an exchange: the server refuses the walk.
/// </para>
/// <para>
/// Every request waits until the client drew the map (<see cref="BotContext.SendAsync"/>, D77). Banking also waits the
/// client's display time after the banker's question, after the bank opens, and before leaving a dialog it could not
/// take to the bank (<see cref="BotDelays.Display"/>, D132): measured through the app, D3B replying 1 ms after the
/// question, and leaving the bank 39 ms after it opened, left both windows on the client's screen, though the server had
/// closed them and the client asked to leave each. As the client draws a map before it follows a walk, it shows a window
/// before it can close it.
/// </para>
/// </remarks>
public static class Banking
{
    private const int MostJourneys = 4;

    /// <summary>Goes to the nearest bank, puts stacks and kamas in it, and leaves it, as the bot's behavior.</summary>
    /// <param name="bot">The bot.</param>
    /// <param name="stacks">Tells the stacks to put in, among those the character carries and does not wear.</param>
    /// <param name="kamas">How many kamas to put in, at most what the character carries once the bank is open.</param>
    /// <param name="cancellationToken">Stops banking.</param>
    /// <returns>How banking ended.</returns>
    /// <exception cref="InvalidOperationException">The bot cannot act now: see <see cref="BotActor.RunAsync"/>.</exception>
    /// <exception cref="BotInterruptedException">The player acted, the game connection closed, or the host is stopping.</exception>
    public static Task<BankOutcome> BankAsync(this BotActor bot, Func<InventoryItem, bool> stacks, long kamas, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bot);
        ArgumentNullException.ThrowIfNull(stacks);
        ArgumentOutOfRangeException.ThrowIfNegative(kamas);

        return bot.RunAsync((context, token) => context.BankAsync(stacks, kamas, token), cancellationToken);
    }

    /// <summary>Goes to the nearest bank, puts stacks and kamas in it, and leaves it, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="stacks">Tells the stacks to put in, among those the character carries and does not wear.</param>
    /// <param name="kamas">How many kamas to put in, at most what the character carries once the bank is open.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How banking ended: the first step that did not end <see cref="BankOutcome.Done"/>, the bank being left whatever the deposit's outcome.</returns>
    public static async Task<BankOutcome> BankAsync(this BotContext context, Func<InventoryItem, bool> stacks, long kamas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(stacks);
        ArgumentOutOfRangeException.ThrowIfNegative(kamas);

        using var action = context.BeginAction();
        var depth = action.Depth;
        var asked = (Stacks: 0, Kamas: 0L);
        var outcome = await context.GoToBankerAsync(cancellationToken).ConfigureAwait(false);

        if (outcome is BankOutcome.Done)
            outcome = await context.OpenBankAsync(cancellationToken).ConfigureAwait(false);

        if (outcome is BankOutcome.Done)
        {
            var state = context.State;
            var uids = state.Inventory.Values.Where(stack => !stack.IsWorn && stacks(stack)).Select(stack => stack.Uid).ToList();

            asked = (uids.Count, Math.Min(kamas, state.Kamas));

            var deposit = await context.DepositAsync(uids, asked.Kamas, cancellationToken).ConfigureAwait(false);
            var left = await context.LeaveBankAsync(cancellationToken).ConfigureAwait(false);

            outcome = deposit is BankOutcome.Done ? left : deposit;
        }

        BotLog.Banked(context.Log, context.State.Key.CharacterId, context.State.MapId ?? 0, outcome);
        context.Record(action, (at, took) => new BankEntry(at, depth, context.State.MapId, asked.Stacks, asked.Kamas, outcome, took));

        return outcome;
    }

    /// <summary>Goes where a banker stands: the nearest bank's inside the character may reach, or the map it stands on when a banker stands there.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the journey ended: <see cref="BankOutcome.Done"/> once a banker stands on the character's map.</returns>
    /// <remarks>
    /// Which of a bank's insides holds the banker only the server says (D29): an inside where none stands is told to the
    /// host's <see cref="BankDirectory"/>, which no bot seeks any more, and the journey goes on to the nearest other (D79).
    /// The doors' criteria are answered strictly, as on every journey (D76).
    /// </remarks>
    public static async Task<BankOutcome> GoToBankerAsync(this BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var banks = context.Banks;

        for (var journeys = 0; ; journeys++)
        {
            var state = context.State;

            if (state.MapId is not { } mapId || state.Cell is null || state.IsBusy)
                return journeys is 0 ? BankOutcome.Busy : BankOutcome.NotTravelled;

            var banker = banks.TryFindBanker(state, out _, out _);

            banks.Seen(mapId, banker);

            if (banker)
                return BankOutcome.Done;

            if (banks.Insides.Contains(mapId))
                BotLog.NoBanker(context.Log, state.Key.CharacterId, mapId);

            if (journeys >= MostJourneys)
                return BankOutcome.NotTravelled;

            var sought = banks.Sought();

            if (sought.Count is 0)
                return BankOutcome.NoBank;

            switch (await context.TravelAsync(sought, cancellationToken).ConfigureAwait(false))
            {
                case TravelOutcome.Arrived:
                    break;
                case TravelOutcome.NoPath:
                    return BankOutcome.NoBank;
                case TravelOutcome.Busy:
                    return journeys is 0 ? BankOutcome.Busy : BankOutcome.NotTravelled;
                default:
                    return BankOutcome.NotTravelled;
            }
        }
    }

    /// <summary>Opens the bank: talks to a banker of the character's map, then gives the reply of his that the question offers.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How the opening ended: <see cref="BankOutcome.Done"/> once the exchange with the storage is open.</returns>
    /// <remarks>A dialog the bot opened and could not take to the bank is left, so that the character can walk again.</remarks>
    public static async Task<BankOutcome> OpenBankAsync(this BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The banker is talked to from where the character stands, which the server may refuse while it holds a walk (D89).
        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

        var state = context.State;

        if (state.MapId is not { } mapId || state.Cell is null || state.IsBusy)
            return BankOutcome.Busy;

        if (!context.Banks.TryFindBanker(state, out var npc, out var banker))
            return BankOutcome.NoBank;

        BotLog.OpeningBank(context.Log, state.Key.CharacterId, mapId, npc.NpcId);

        var outcome = await TalkAsync(context, mapId, npc, banker, cancellationToken).ConfigureAwait(false);

        if (outcome is not BankOutcome.Done && context.State.Dialog is not null)
        {
            // A dialog left as soon as it opened would stay on the client's screen, as a reply sent too soon does.
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);
            await LeaveAsync(context, cancellationToken).ConfigureAwait(false);
        }

        BotLog.OpenedBank(context.Log, state.Key.CharacterId, mapId, outcome);

        return outcome;
    }

    /// <summary>Puts stacks and kamas in the open bank: the stacks by one request, then the kamas by another.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="uids">The stacks to put in, by their own id: those the character does not carry, or wears, are left out.</param>
    /// <param name="kamas">How many kamas to put in, at most what the character carries; none puts none.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>
    /// <see cref="BankOutcome.Done"/> once the server took what it may, or when there was nothing to put in;
    /// <see cref="BankOutcome.Refused"/> when it took none of the stacks or none of the kamas, which it answers with nothing;
    /// <see cref="BankOutcome.Busy"/> when the bank is not open, nothing sent. The stacks it leaves, such as a quest's
    /// objects, stay in the inventory, which the state shows.
    /// </returns>
    public static async Task<BankOutcome> DepositAsync(this BotContext context, IReadOnlyCollection<int> uids, long kamas, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(uids);
        ArgumentOutOfRangeException.ThrowIfNegative(kamas);

        var state = context.State;

        if (state.Storage is null)
            return BankOutcome.Busy;

        var stacks = uids.Distinct().Where(uid => state.Inventory.TryGetValue(uid, out var stack) && !stack.IsWorn).ToList();
        var amount = Math.Min(kamas, state.Kamas);
        var outcome = BankOutcome.Done;

        if (stacks.Count > 0)
        {
            // The server answers with the inventory's stacks deleted, then the storage's and the weight, in one millisecond.
            var deleted = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is ObjectsDeletedEvent gone && gone.Uids.Any(stacks.Contains));
            var weight = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is InventoryWeightEvent);

            await context.SendAsync(new ExchangeTransferFromInventoryRequest { Uids = { stacks } }, cancellationToken).ConfigureAwait(false);

            if (await deleted.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) is null)
                outcome = BankOutcome.Refused;
            else
                await weight.WaitAsync(context.Delays.BankFollow, cancellationToken).ConfigureAwait(false);
        }

        if (amount > 0)
        {
            // The storage's kamas, then the character's, in one millisecond.
            var stored = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is StorageKamasUpdateEvent);
            var carried = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is KamasUpdateEvent);

            await context.SendAsync(new ExchangeKamasRequest { Quantity = amount }, cancellationToken).ConfigureAwait(false);

            if (await stored.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) is null)
                outcome = BankOutcome.Refused;
            else
                await carried.WaitAsync(context.Delays.BankFollow, cancellationToken).ConfigureAwait(false);
        }

        var inventory = context.State.Inventory;
        var taken = stacks.Count(uid => !inventory.ContainsKey(uid));

        BotLog.Deposited(context.Log, state.Key.CharacterId, stacks.Count, taken, amount, outcome);

        return outcome;
    }

    /// <summary>Leaves the bank, or the dialog the character is in.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns><see cref="BankOutcome.Done"/> once the server ended it, or when the character is in none.</returns>
    /// <remarks>The client then sends two requests to leave by itself, which are not the player's (D81).</remarks>
    public static async Task<BankOutcome> LeaveBankAsync(this BotContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.State.IsInDialog ? await LeaveAsync(context, cancellationToken).ConfigureAwait(false) : BankOutcome.Done;
    }

    private static async Task<BankOutcome> TalkAsync(BotContext context, long mapId, MapNpc npc, Banker banker, CancellationToken cancellationToken)
    {
        var asked = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is NpcDialogQuestionEvent or NpcActionErrorEvent);

        await context.SendAsync(new NpcActionRequest { Action = Banker.TalkAction, MapId = mapId, NpcActorId = npc.ActorId }, cancellationToken).ConfigureAwait(false);

        switch (await asked.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false))
        {
            case null:
                return BankOutcome.NoAnswer;
            case NpcActionErrorEvent:
                return BankOutcome.Refused;
        }

        if (context.State.Dialog is not { } dialog)
            return BankOutcome.Refused;

        if (!context.Banks.TryFindReply(banker, dialog, out var reply))
        {
            // Measured in Sarakech's port (D251): the banker's NPC stands there for another part, and the next banking seeks
            // the bank's other insides rather than ask him again.
            context.Banks.Refuse(mapId);

            return BankOutcome.Refused;
        }

        // The client shows the dialog before the reply closes it.
        await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

        // The storage's content comes last of what opens the bank; a question again refuses the reply.
        var opened = context.Expect((side, message, state) => side is RelayDirection.FromServer
            && (message is StorageContentEvent && state.Storage is not null || message is NpcDialogQuestionEvent));

        await context.SendAsync(new NpcDialogReplyRequest { ReplyId = reply }, cancellationToken).ConfigureAwait(false);

        var outcome = await opened.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) switch
        {
            null => BankOutcome.NoAnswer,
            NpcDialogQuestionEvent => BankOutcome.Refused,
            _ => BankOutcome.Done,
        };

        // Then the bank, before anything goes in and the leaving closes it.
        if (outcome is BankOutcome.Done)
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

        return outcome;
    }

    private static async Task<BankOutcome> LeaveAsync(BotContext context, CancellationToken cancellationToken)
    {
        var left = context.Expect((side, message, _) => side is RelayDirection.FromServer && message is ExchangeLeaveEvent or DialogLeaveEvent);

        await context.SendAsync(new DialogLeaveRequest(), cancellationToken).ConfigureAwait(false);

        return await left.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) is null ? BankOutcome.NoAnswer : BankOutcome.Done;
    }
}
