using D3B.Protocol.Game;

namespace D3B.Bot.Fights;

/// <summary>A sequence the server opened and has not closed yet: what it frames, whose it is, and whether it holds a cast already.</summary>
/// <param name="Type">What the sequence frames.</param>
/// <param name="AuthorId">The fighter whose sequence it is.</param>
/// <param name="HasCast">Whether a spell was cast in it: the first cast of a fighter's sequence of a spell is the one it chose (client.md).</param>
public readonly record struct FightSequence(SequenceType Type, long AuthorId, bool HasCast);
