namespace D3B.Bot.Settings;

/// <summary>A sub-area where monsters asked for live: what a sub-area setting suggests for them (D245).</summary>
/// <param name="SubArea">The sub-area, with its level.</param>
/// <param name="Maps">How many of its maps a character may travel to.</param>
/// <param name="Monsters">The monsters asked for that live there, in the order asked.</param>
public sealed record SubAreaMonsters(GameReference SubArea, int Maps, IReadOnlyList<int> Monsters);
