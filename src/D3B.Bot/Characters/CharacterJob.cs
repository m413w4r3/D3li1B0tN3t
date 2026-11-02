using System.Collections.Immutable;

namespace D3B.Bot.Characters;

/// <summary>A profession of the character: how far it has gone, and the skills it lets the character use.</summary>
/// <param name="JobId">The profession's id in the client's tables (D29).</param>
/// <param name="Level">The profession's level, 0 until the server says.</param>
/// <param name="Experience">Its experience, between the level's floor and the next level's.</param>
/// <param name="LevelFloor">The experience the level starts at.</param>
/// <param name="NextLevelFloor">The experience the next level starts at.</param>
/// <param name="Skills">The skills it lets the character use, by id in the client's tables, empty until the server says.</param>
public sealed record CharacterJob(int JobId, int Level, long Experience, long LevelFloor, long NextLevelFloor, ImmutableArray<int> Skills);
