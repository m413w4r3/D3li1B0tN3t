using D3B.App.Controls;

namespace D3B.App.ViewModels.Statistics;

/// <summary>A run the statistics page lists: when it began, who ran what, how long, what it brought, and how it ended (D146).</summary>
/// <param name="When">When it began, said.</param>
/// <param name="Who">The character, and the profile or activity it ran.</param>
/// <param name="StartsGroup">Whether it is the first of the runs of the same, which it names.</param>
/// <param name="Duration">How long it ran, said.</param>
/// <param name="Total">How many resources it brought, said.</param>
/// <param name="Outcome">How it ended, or that it goes on.</param>
/// <param name="Tone">The tone of how it ended.</param>
/// <param name="HasOutcome">Whether how it ended is said: it goes on, or failed.</param>
internal sealed record StatisticsRunViewModel(string When, string Who, bool StartsGroup, string Duration, string Total, string Outcome, Tone Tone, bool HasOutcome);
