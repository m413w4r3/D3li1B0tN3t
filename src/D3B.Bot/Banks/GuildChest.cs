using D3B.Bot.Characters;
using D3B.Bot.Interactives;
using D3B.Bot.Journal;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;

namespace D3B.Bot.Banks;

/// <summary>The guild's chest: going to the nearest one, putting stacks in it one at a time, and leaving (D147, D153).</summary>
/// <remarks>
/// <para>
/// Measured on client 3.7.12.12: the chest is an element of a bank's inside, interactive 388 drawn with the pack's graphic
/// 70671, which JondoEmu named, used with skill 184 and no NPC's dialog. The use opens an exchange of type 21 in the same
/// millisecond, then what the open tab holds, with no kamas. A stack goes in whole or in part by its own request; the server
/// answers 28 ms later with the chest's stack, the inventory's stack deleted or changed, and the weight. It refuses an object
/// bound to the account with text 356 of the error kind, and moves nothing. Leaving ends the exchange, as the bank's.
/// </para>
/// <para>
/// The guild captured had one tab: the stacks go in the tab the chest opens on. As the bank does, the chest waits the
/// client's display time once open and before leaving (D132).
/// </para>
/// </remarks>
public static class GuildChest
{
    /// <summary>The graphic the pack draws the guild's chest with, in banks' insides.</summary>
    public const int GfxId = 70_671;

    /// <summary>The guild chest's interactive, as the server names its element.</summary>
    public const int InteractiveId = 388;

    /// <summary>The skill that opens it.</summary>
    public const int SkillId = 184;

    /// <summary>The text the server refuses a stack with, such as an object bound to the account.</summary>
    public const int RefusedTextId = 356;

    private const int MostJourneys = 3;

    /// <summary>Goes to the nearest guild's chest, puts stacks in it, and leaves, within a behavior.</summary>
    /// <param name="context">The behavior's context.</param>
    /// <param name="chests">The maps a guild's chest stands on, from the pack.</param>
    /// <param name="stacks">Tells the stacks to put in, among those the character carries and does not wear.</param>
    /// <param name="cancellationToken">The behavior's token.</param>
    /// <returns>How it ended: <see cref="BankOutcome.Done"/> once every stack went in, <see cref="BankOutcome.Refused"/> when some were refused, which stay in the inventory.</returns>
    public static async Task<BankOutcome> GuildChestAsync(this BotContext context, IReadOnlySet<long> chests, Func<InventoryItem, bool> stacks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(chests);
        ArgumentNullException.ThrowIfNull(stacks);

        using var action = context.BeginAction();
        var depth = action.Depth;
        var asked = 0;
        var outcome = await GoToChestAsync(context, chests, cancellationToken).ConfigureAwait(false);

        if (outcome is BankOutcome.Done)
            outcome = await OpenAsync(context, cancellationToken).ConfigureAwait(false);

        if (outcome is BankOutcome.Done)
        {
            var uids = context.State.Inventory.Values.Where(stack => !stack.IsWorn && stacks(stack)).Select(stack => stack.Uid).ToList();

            asked = uids.Count;

            for (var index = 0; index < uids.Count; index++)
            {
                // Measured: moved 28 ms apart, the stacks went faster than the client showed them.
                if (index > 0)
                    await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

                var moved = await MoveAsync(context, uids[index], cancellationToken).ConfigureAwait(false);

                if (moved is not BankOutcome.Done)
                    outcome = moved is BankOutcome.Refused ? BankOutcome.Refused : moved;
            }

            // The client shows the chest's last change before the leaving closes it.
            await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

            var left = await context.LeaveBankAsync(cancellationToken).ConfigureAwait(false);

            if (outcome is BankOutcome.Done)
                outcome = left;
        }

        context.Record(action, (at, took) => new BankEntry(at, depth, context.State.MapId, asked, 0, outcome, took, GuildChest: true));

        return outcome;
    }

    private static bool TryFindChest(CharacterState state, out int elementId)
    {
        var chest = state.Elements.Values.FirstOrDefault(element => element.InteractiveId == InteractiveId && element.EnabledSkills.Any(skill => skill.SkillId == SkillId));

        elementId = chest?.ElementId ?? 0;

        return chest is not null;
    }

    private static async Task<BankOutcome> GoToChestAsync(BotContext context, IReadOnlySet<long> chests, CancellationToken cancellationToken)
    {
        for (var journeys = 0; ; journeys++)
        {
            var state = context.State;

            if (state.MapId is not { } mapId || state.Cell is null || state.IsBusy)
                return journeys is 0 ? BankOutcome.Busy : BankOutcome.NotTravelled;

            if (TryFindChest(state, out _))
                return BankOutcome.Done;

            if (chests.Count is 0 || chests.Contains(mapId) || journeys >= MostJourneys)
                return chests.Count is 0 || chests.Contains(mapId) ? BankOutcome.NoBank : BankOutcome.NotTravelled;

            switch (await context.TravelAsync(chests, cancellationToken).ConfigureAwait(false))
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

    private static async Task<BankOutcome> OpenAsync(BotContext context, CancellationToken cancellationToken)
    {
        // The chest is used from where the character stands, which the server may refuse while it holds a walk (D89).
        await context.ReleaseHeldWalkAsync(cancellationToken).ConfigureAwait(false);

        if (!TryFindChest(context.State, out var elementId))
            return BankOutcome.NoBank;

        var opened = context.Expect((side, message, state) => side is RelayDirection.FromServer && message is StorageContentEvent && state.Storage is { IsGuildChest: true });

        switch (await context.UseAsync(elementId, SkillId, cancellationToken).ConfigureAwait(false))
        {
            case UseOutcome.Used:
                break;
            case UseOutcome.Busy:
                return BankOutcome.Busy;
            case UseOutcome.NoAnswer or UseOutcome.NotEnded:
                return BankOutcome.NoAnswer;
            default:
                return BankOutcome.Refused;
        }

        if (context.State.Storage is not { IsGuildChest: true } && await opened.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) is null)
            return BankOutcome.NoAnswer;

        // The client shows the chest before anything goes in.
        await Task.Delay(context.Delays.Display, context.Time, cancellationToken).ConfigureAwait(false);

        return BankOutcome.Done;
    }

    private static async Task<BankOutcome> MoveAsync(BotContext context, int uid, CancellationToken cancellationToken)
    {
        if (context.State.Storage is not { IsGuildChest: true } || !context.State.Inventory.TryGetValue(uid, out var stack))
            return BankOutcome.Busy;

        var answered = context.Expect((side, message, _) => side is RelayDirection.FromServer && message switch
        {
            ObjectDeletedEvent deleted => deleted.Uid == uid,
            ObjectsDeletedEvent deleted => deleted.Uids.Contains(uid),
            ObjectQuantityEvent { Object: { } changed } => changed.Uid == uid,
            TextInformationEvent text => text.TextId == RefusedTextId,
            _ => false,
        });

        await context.SendAsync(new ExchangeObjectMoveRequest { Uid = uid, Quantity = stack.Quantity }, cancellationToken).ConfigureAwait(false);

        // Measured: the chest may take part of a stack, the rest staying in the inventory, which the character's own chest then takes.
        return await answered.WaitAsync(context.Delays.Answer, cancellationToken).ConfigureAwait(false) switch
        {
            null => BankOutcome.NoAnswer,
            TextInformationEvent or ObjectQuantityEvent => BankOutcome.Refused,
            _ => BankOutcome.Done,
        };
    }
}
