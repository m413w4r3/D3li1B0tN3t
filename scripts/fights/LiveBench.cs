using D3B.Bot.Characters;
using D3B.Bot.Fights;
using D3B.Network.Captures;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Google.Protobuf;

namespace D3B.Scripts.Fights;

/// <summary>Counts what D3B did in the fights of the captures: who played each, how it ended, and what came of the AI's requests.</summary>
/// <remarks>
/// A fight is the AI's when D3B sent the server a request of the fight's itself (a <see cref="CaptureRecordKind.Injected"/>
/// frame), the player's otherwise; a request of the fight's the client sent in an AI's fight is the player's hand (D202). A
/// cast D3B sent is cast when the server announces the character's cast before anything else answers it, refused when an
/// error text comes first (D203). It prints counts, never a value of one fight.
/// </remarks>
internal sealed class LiveBench
{
    public Tally Tally { get; } = new();

    private static bool OfTheFight(IMessage message)
    {
        return message is GameActionFightCastRequest or MapMovementRequest or FightTurnFinishRequest or FightReadyRequest or FightPlacementPositionRequest or FightOptionToggleRequest;
    }

    public void Replay(List<(CaptureRecordKind Kind, IMessage Message)> messages)
    {
        CharacterState? state = null;
        Fight? fight = null;

        foreach (var (kind, message) in messages)
        {
            if (kind is CaptureRecordKind.FromServer && message is CharacterSelectionEvent { Success.Character: { } chosen })
                state = CharacterState.Chosen(new CharacterKey(0, chosen.Id), chosen);

            if (state is null)
                continue;

            var self = state.Key.CharacterId;

            if (kind is not CaptureRecordKind.FromServer)
            {
                if (fight is not null && OfTheFight(message))
                    Request(fight, kind, message);

                // The fight's options D3B set, in the order it set them, and what the server answered each (D216).
                if (fight is not null && message is FightOptionToggleRequest toggle)
                {
                    fight.Toggled++;
                    fight.Toggling = toggle.Option;
                    Tally.Count($"options toggled: {(kind is CaptureRecordKind.Injected ? "by D3B" : "by the client")}, {Ordinal(fight.Toggled)} {toggle.Option}");
                }

                continue;
            }

            if (fight?.Toggling is { } option)
            {
                if (message is FightOptionUpdateEvent update)
                    Tally.Count($"options toggled: {option} answered, option {update.Option} {(update.Enabled ? "set" : "unset")}");
                else if (message is TextInformationEvent text)
                    Tally.Count($"options toggled: {option} answered, text {text.TextId} of the {text.Type} kind");
                else if (message is not FightOptionUpdateEvent)
                    fight.Toggling = null;
            }

            if (message is FightStartingEvent)
                fight = new Fight();

            if (fight is not null)
                Answer(fight, self, message, state);

            state = CharacterEvents.Apply(state, RelayDirection.FromServer, message);

            if (fight is null)
                continue;

            if (message is FightTurnStartPlayingEvent && state.Fight is { IsOwnTurn: true } playing)
            {
                fight.Turns++;
                fight.Acted = 0;
                fight.Breed ??= playing.Fighters.GetValueOrDefault(self)?.BreedId;
                fight.Life ??= playing.Fighters.GetValueOrDefault(self)?.Life;
            }

            if (message is FightTurnEndEvent ended && ended.FighterId == self && fight.Acted is 0)
                fight.Idle++;

            if (message is FightEndEvent)
            {
                Settle(fight, state.Fight, self);
                fight = null;
            }
        }
    }

    private static string Ordinal(int count)
    {
        return count switch
        {
            1 => "first",
            2 => "second",
            _ => "later",
        };
    }

    private static void Request(Fight fight, CaptureRecordKind kind, IMessage message)
    {
        if (kind is CaptureRecordKind.Injected)
        {
            fight.Requests[message.Descriptor.Name] = fight.Requests.GetValueOrDefault(message.Descriptor.Name) + 1;

            if (message is GameActionFightCastRequest or MapMovementRequest)
                fight.Acted++;

            if (message is GameActionFightCastRequest)
                fight.Pending = true;

            // A weapon's blow is spell 0, which the AI strikes with a sword it wears (D221).
            if (message is GameActionFightCastRequest { SpellId: 0 })
            {
                fight.Blows++;
                fight.Striking = true;
            }

            return;
        }

        // The client sends its readiness for each turn by itself, and acknowledges sequences: neither is the player's hand.
        if (message is not FightReadyRequest)
            fight.Hand++;
        else
            fight.ClientReady++;
    }

    private static void Answer(Fight fight, long self, IMessage message, CharacterState state)
    {
        if (!fight.Pending)
            return;

        if (message is GameActionFightEvent { ActionCase: GameActionFightEvent.ActionOneofCase.Cast } cast && cast.SourceId == self)
        {
            fight.Cast++;
            fight.Struck += fight.Striking && cast.ActionId is 303 ? 1 : 0;
            fight.Pending = false;
            fight.Striking = false;
        }
        else if (message is TextInformationEvent { Type: TextInformationType.Error } refusal)
        {
            fight.Refused[refusal.TextId] = fight.Refused.GetValueOrDefault(refusal.TextId) + 1;
            fight.Pending = false;
        }
        else if (message is FightTurnEndEvent ended && ended.FighterId == self || state.Fight is null)
        {
            fight.Unanswered++;
            fight.Pending = false;
        }
    }

    private void Settle(Fight fight, FightState? state, long self)
    {
        var ai = fight.Requests.Count > 0;
        var who = ai ? "the AI's" : "the player's";
        var outcome = state?.End?.Outcome switch
        {
            FightOutcome.Won => "won",
            FightOutcome.Lost => "lost",
            null => "ended unsaid",
            _ => "other outcome",
        };

        Tally.Count($"fights: {who}, {outcome}");

        if (!ai)
            return;

        Tally.Count($"the AI's fights: by breed {fight.Breed?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}");
        Tally.Count("the AI's fights: rounds", state?.Round ?? 0);
        Tally.Count("the AI's fights: the character's turns", fight.Turns);
        Tally.Count("the AI's fights: turns with no cast nor walk", fight.Idle);
        Tally.Count("the AI's fights: casts the server announced", fight.Cast);
        Tally.Count("the AI's fights: casts unanswered", fight.Unanswered);
        Tally.Count("the AI's fights: weapon's blows sent", fight.Blows);
        Tally.Count("the AI's fights: weapon's blows the server announced", fight.Struck);
        Tally.Count("the AI's fights: the player's requests", fight.Hand);
        Tally.Count("the AI's fights: the client's readiness for a turn", fight.ClientReady);

        if (fight.Life is { } life && state?.Fighters.GetValueOrDefault(self) is { } end)
            Tally.Count("the AI's fights: the character's life lost", Math.Max(0, life - (end.IsAlive ? end.Life : 0)));

        foreach (var (name, count) in fight.Requests)
            Tally.Count($"D3B's requests: {name}", count);

        foreach (var (text, count) in fight.Refused)
            Tally.Count($"casts refused: text {text}", count);
    }

    private sealed class Fight
    {
        public Dictionary<string, int> Requests { get; } = [];

        public Dictionary<int, int> Refused { get; } = [];

        public int Turns { get; set; }

        public int Idle { get; set; }

        public int Acted { get; set; }

        public int Cast { get; set; }

        public int Unanswered { get; set; }

        public int Blows { get; set; }

        public int Struck { get; set; }

        public bool Striking { get; set; }

        public int Hand { get; set; }

        public int ClientReady { get; set; }

        public bool Pending { get; set; }

        public int? Breed { get; set; }

        public int? Life { get; set; }

        public int Toggled { get; set; }

        public FightOption? Toggling { get; set; }
    }
}
