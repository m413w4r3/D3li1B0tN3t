using System.Collections.Immutable;

namespace D3B.Bot.Characters;

/// <summary>A dialog with an NPC the character is in, from the server's opening it to its end.</summary>
/// <param name="NpcActorId">The NPC's actor id on the map.</param>
public sealed record NpcDialog(int NpcActorId)
{
    /// <summary>Gets the question the NPC asks, by id in the client's tables, once the server sent it.</summary>
    public int? MessageId { get; init; }

    /// <summary>Gets the replies the question offers, by id in the client's tables: none before the question, or for a question that offers none.</summary>
    public ImmutableArray<int> Replies { get; init; } = [];
}
