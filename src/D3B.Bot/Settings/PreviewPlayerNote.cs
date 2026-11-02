using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>What settings lead to for one of the characters that play them, such as the zone chosen for its levels (D166).</summary>
/// <param name="Name">The character's name, as <see cref="PreviewPlayer.Name"/> gave it.</param>
/// <param name="Note">What they lead to for it.</param>
public sealed record PreviewPlayerNote(string Name, GameText Note);
