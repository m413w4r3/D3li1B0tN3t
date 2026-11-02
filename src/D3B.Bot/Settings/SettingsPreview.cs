using System.Collections.Immutable;
using D3B.Bot.Activities;
using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>What an activity's settings lead to, as the activity reckons it from the pack, for the app to show beside them (D127).</summary>
/// <remarks>Such as the maps a zone holds, the resources it has of those asked for, and how far its nearest bank is.</remarks>
public sealed record SettingsPreview
{
    /// <summary>Gets the figures to show, each with what it counts.</summary>
    public ImmutableArray<PreviewFigure> Figures { get; init; } = [];

    /// <summary>Gets the maps the settings lead the activity through, each in its part of the zone, which the app draws.</summary>
    public ImmutableArray<ZoneMap> Zone { get; init; } = [];

    /// <summary>Gets a map to mark beside the zone, such as the nearest bank's.</summary>
    public long? LandmarkMapId { get; init; }

    /// <summary>Gets what the settings lead to for each character that plays them, such as the zone chosen for its levels (D166).</summary>
    public ImmutableArray<PreviewPlayerNote> PlayerNotes { get; init; } = [];

    /// <summary>Gets what the app says in the zone's place while the settings lead to none, such as a place chosen at each character's level and no character to choose it for (D246).</summary>
    public GameText? Message { get; init; }
}
