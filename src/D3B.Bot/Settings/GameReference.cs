namespace D3B.Bot.Settings;

/// <summary>A value of the game a setting may name: its id, the text that names it, and its level, when it has one.</summary>
/// <param name="Id">The id the setting stores.</param>
/// <param name="NameId">The id of its name in the pack's texts.</param>
/// <param name="Level">A sub-area's level, or the level a profession gathers a resource from; <see langword="null"/> for the others.</param>
public readonly record struct GameReference(int Id, int NameId, int? Level);
