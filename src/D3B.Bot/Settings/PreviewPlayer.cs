namespace D3B.Bot.Settings;

/// <summary>A character that plays settings, as its bot last saw it, for what they lead to for it (D166).</summary>
/// <param name="Name">The character's name.</param>
/// <param name="Level">Its level; 0 while no bot saw it.</param>
/// <param name="JobLevels">Its professions' levels, by profession; empty while no bot saw them.</param>
/// <param name="MapId">The map it stands on, if its bot saw it.</param>
public sealed record PreviewPlayer(string Name, int Level, IReadOnlyDictionary<int, int> JobLevels, long? MapId);
