using D3B.Bot.Interactives;

namespace D3B.Bot.Journal;

/// <summary>A use of an element of the map with a skill, such as a harvest, until it was over (D112).</summary>
/// <param name="At">When it ended.</param>
/// <param name="Depth">How many actions it was made within.</param>
/// <param name="MapId">The map the element is on.</param>
/// <param name="ElementId">The element's id on the map.</param>
/// <param name="InteractiveId">What the element is, as the server named it on the map, or <see langword="null"/> when it did not.</param>
/// <param name="SkillId">The skill's id in the client's tables.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="Duration">How long it took, the walk to the element included.</param>
public sealed record UseEntry(DateTimeOffset At, int Depth, long MapId, int ElementId, int? InteractiveId, int SkillId, UseOutcome Outcome, TimeSpan Duration)
    : JournalEntry(At, Depth);
