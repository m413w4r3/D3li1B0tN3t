using System.Collections.Immutable;
using D3B.Bot.Fights;
using D3B.Protocol.Game;

namespace D3B.Bot.Characters;

/// <summary>What a bot knows of its character, at one moment: an immutable snapshot, which only the bot's own loop replaces (D9).</summary>
/// <remarks>
/// <para>
/// The game server tells it all: the character choice names the character, then the server sends its inventory, weight
/// and professions, the map it is on and what stands there, and every change after. <see cref="CharacterEvents"/> turns
/// each message into the next snapshot; logic code reads snapshots, never the messages.
/// </para>
/// <para>
/// What the server has not said yet is <see langword="null"/> or empty: the map and cell until the map is named and its
/// information sent, with the map's elements, NPCs and groups of monsters, the inventory until its content is sent, and
/// the zaaps the character knows until the server lists them.
/// </para>
/// </remarks>
public sealed record CharacterState
{
    /// <summary>Gets who the bot plays.</summary>
    public required CharacterKey Key { get; init; }

    /// <summary>Gets the character's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the character's level, when the character choice gave it.</summary>
    public int? Level { get; init; }

    /// <summary>Gets the map the character is on, once the server named it.</summary>
    public long? MapId { get; init; }

    /// <summary>Gets the sub-area of the map, once the map's information was sent.</summary>
    public int? SubAreaId { get; init; }

    /// <summary>Gets the cell the character stands on, or the one its walk started from while it walks.</summary>
    /// <remarks>Known once the map's information placed the character, then after each walk, refusal or stop.</remarks>
    public int? Cell { get; init; }

    /// <summary>Gets the path the character walks, every cell from the first, as the server showed it; empty when it stands still.</summary>
    public ImmutableArray<int> Path { get; init; } = [];

    /// <summary>Gets whether the character walks: the server showed it a path, and has not answered the client's confirmation yet.</summary>
    public bool IsWalking => !Path.IsDefaultOrEmpty;

    /// <summary>Gets whether the server may still hold the character walking a path the client never confirmed, which the state settled on its last cell (D85).</summary>
    /// <remarks>
    /// Measured (client.md): the server then refuses what is not a walk from the path's last cell, such as a map change or a
    /// teleport, and walks one from there, which the client follows. The next walk, refusal or map lets it go.
    /// </remarks>
    public bool IsWalkHeld { get; init; }

    /// <summary>Gets the last cell of the last walk the server showed the character, whatever map it was on.</summary>
    /// <remarks>
    /// Measured (9g5): the client does not follow a walk ending on the cell its previous walk ended on, even on another map,
    /// 36 times of 39 in the captures, against 9 of 695 others; <see cref="Movement.MapWalk"/> never sends one.
    /// </remarks>
    public int? LastWalkEnd { get; init; }

    /// <summary>Gets the interactive elements of the map, by id, as the server described them; empty until the map's information is sent.</summary>
    /// <remarks>Only the map's own: the information also lists the neighbouring maps' elements, which cannot be used from it.</remarks>
    public ImmutableSortedDictionary<int, MapElement> Elements { get; init; } = ImmutableSortedDictionary<int, MapElement>.Empty;

    /// <summary>Gets the use of an element the character is in, such as a harvest, from the server's start to its end.</summary>
    /// <remarks>A use with no duration, such as a door's, is over as it starts: the character is never in it.</remarks>
    public CurrentUse? Use { get; init; }

    /// <summary>Gets whether the character uses an element: the server started a use that lasts, and has not ended it yet.</summary>
    public bool IsUsing => Use is not null;

    /// <summary>Gets the non-player characters of the map, by actor id, as the map's information placed them; empty until it is sent.</summary>
    public ImmutableSortedDictionary<int, MapNpc> Npcs { get; init; } = ImmutableSortedDictionary<int, MapNpc>.Empty;

    /// <summary>Gets the groups of monsters of the map, by actor id, as the server placed them; empty until the map's information is sent.</summary>
    /// <remarks>The server shows a group coming onto the map, moves it, and removes it once attacked, by the character or another player.</remarks>
    public ImmutableSortedDictionary<long, MapMonsterGroup> MonsterGroups { get; init; } = ImmutableSortedDictionary<long, MapMonsterGroup>.Empty;

    /// <summary>Gets the dialog with an NPC the character is in, such as a banker's, from the server's opening it to its end.</summary>
    public NpcDialog? Dialog { get; init; }

    /// <summary>Gets the exchange with a storage the character is in, such as its bank's, from the server's opening it to its end.</summary>
    public OpenStorage? Storage { get; init; }

    /// <summary>Gets the dialog of a zaap the character used, with its destinations, from the server's listing them to the dialog's end (D83).</summary>
    public ZaapDialog? ZaapDialog { get; init; }

    /// <summary>Gets whether the character is in a dialog with an NPC or a zaap, or an exchange with a storage, which a request to leave ends.</summary>
    public bool IsInDialog => Dialog is not null || Storage is not null || ZaapDialog is not null;

    /// <summary>Gets the maps of the zaaps the character knows, as the server listed them on entering the game and each time it learnt one.</summary>
    public ImmutableSortedSet<long> KnownZaaps { get; init; } = [];

    /// <summary>Gets the owner of the haven bag the character is in, once the map's information said so: the character itself in its own.</summary>
    public long? HavenBagOwnerId { get; init; }

    /// <summary>Gets whether the character is in a haven bag, which the world graph does not hold: it leaves it to travel (D83).</summary>
    public bool IsInHavenBag => HavenBagOwnerId is not null;

    /// <summary>Gets the character's fight: from the server's creating a fight's context to its creating the roleplay's again (D88, D179).</summary>
    /// <remarks>During a fight the client walks the character with the same requests, on the fight's own map: the bot sends none yet.</remarks>
    public FightState? Fight { get; init; }

    /// <summary>Gets whether the character fights.</summary>
    public bool IsFighting => Fight is not null;

    /// <summary>Gets whether the character is in the middle of something a behavior must not start over: a walk, a use, a dialog or exchange, or a fight.</summary>
    public bool IsBusy => IsWalking || IsUsing || IsInDialog || IsFighting;

    /// <summary>Gets the stacks the character carries, by their own id.</summary>
    public ImmutableSortedDictionary<int, InventoryItem> Inventory { get; init; } = ImmutableSortedDictionary<int, InventoryItem>.Empty;

    /// <summary>Gets the weapon the character wears, the stack at <see cref="InventoryItem.Weapon"/>, or null for none.</summary>
    public InventoryItem? Weapon => Inventory.Values.FirstOrDefault(item => item.Position is InventoryItem.Weapon);

    /// <summary>Gets the kamas the character carries.</summary>
    public long Kamas { get; init; }

    /// <summary>Gets the character's life, as the server last said it: in its characteristics, or as its regeneration stopped (D153).</summary>
    /// <remarks>Out of fights it regenerates since, a point each <see cref="LifeRegenInterval"/>: <see cref="Runtime.BotActor.LifeAt"/> reckons it now.</remarks>
    public int? Life { get; init; }

    /// <summary>Gets the most life the character has, as the server last said it.</summary>
    public int? MaxLife { get; init; }

    /// <summary>Gets the character's characteristics by id, each its whole value, as the server last gave them all (D263).</summary>
    /// <remarks>
    /// Measured on 158 fights: at the character's first turn, every characteristic the fight gives it equals this one's, the
    /// life lacking aside, where its fighter at the placement lacks the elements', the power and the damage bonuses.
    /// </remarks>
    public ImmutableSortedDictionary<int, int> Characteristics { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    /// <summary>Gets every how many tenths of a second the life regenerates a point, while it does; none in a fight, or before the server said.</summary>
    public int? LifeRegenInterval { get; init; }

    /// <summary>Gets the weight the character carries, in pods.</summary>
    public int Weight { get; init; }

    /// <summary>Gets the most the character can carry, in pods.</summary>
    public int MaxWeight { get; init; }

    /// <summary>Gets how full the character is: the weight it carries over the most it can, 0 until the server said.</summary>
    public double Load => MaxWeight > 0 ? (double)Weight / MaxWeight : 0;

    /// <summary>Gets the character's professions, by id.</summary>
    public ImmutableSortedDictionary<int, CharacterJob> Jobs { get; init; } = ImmutableSortedDictionary<int, CharacterJob>.Empty;

    /// <summary>Gets the spells the server listed as the character entered the game, each with its grade, by spell (D199).</summary>
    /// <remarks>The server lists them once: those a level gained since unlocks, and the grades it raises, come from the pack (<see cref="Fights.CharacterSpells"/>).</remarks>
    public ImmutableSortedDictionary<int, int> Spells { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    /// <summary>Gets the only spells the character casts while an alteration transforms it, such as into a bouftou, by spell; empty untransformed (D203).</summary>
    public ImmutableSortedDictionary<int, int> TransformedSpells { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    /// <summary>Gets when the account's subscription ends, as the connection phase's identification said, or <see langword="null"/> when it gave no date (D72).</summary>
    public DateTimeOffset? SubscriptionEnd { get; init; }

    /// <summary>Gets the state of a character the client just chose, as the server's confirmation describes it.</summary>
    /// <param name="key">Who the bot plays.</param>
    /// <param name="character">The character, as the server's confirmation gives it.</param>
    /// <returns>The state, nothing known yet but the character's name and level.</returns>
    public static CharacterState Chosen(CharacterKey key, Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        var basics = character.Basics;

        return new CharacterState
        {
            Key = key,
            Name = basics?.Name ?? "",
            Level = basics is { HasLevel: true } ? basics.Level : null,
        };
    }

    /// <summary>Tells whether the account is subscribed at a moment.</summary>
    /// <param name="now">The moment, by the bot's clock.</param>
    /// <returns>Whether its subscription ends after it: an account whose end is not known counts as not subscribed (D72).</returns>
    public bool IsSubscribedAt(DateTimeOffset now)
    {
        return SubscriptionEnd is { } end && end > now;
    }
}
