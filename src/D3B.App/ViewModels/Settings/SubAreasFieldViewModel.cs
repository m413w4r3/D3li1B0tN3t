using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A zone's sub-areas: those where the resources asked for grow, or the monsters asked for live, first, then any other (D127, D245).</summary>
/// <remarks>
/// The sub-areas chosen come first, in their order, then the others where they grow or live, the most of them first, then
/// the lowest; a click chooses or leaves one, and the box still adds any other. The zone they make is drawn beside the
/// settings, with the activity's preview (D159).
/// </remarks>
internal sealed partial class SubAreasFieldViewModel : GamesFieldViewModel
{
    private const int MostSuggestions = 8;

    private string? _shown;

    /// <summary>Gets the sub-areas suggested, those chosen first.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SuggestionViewModel> Suggestions { get; private set; } = [];

    /// <inheritdoc/>
    public override bool IsWide => true;

    /// <summary>Initializes a new instance of the <see cref="SubAreasFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public SubAreasFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        _shown = null;
        base.Relabel();
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        base.RefreshOptions();
        RefreshSuggestions();
    }

    /// <inheritdoc/>
    protected override void ReadItems()
    {
        base.ReadItems();
        RefreshSuggestions();
    }

    private void RefreshSuggestions()
    {
        if (Editor.Draft is null)
            return;

        var kind = Definition.SuggestedKind;
        var values = Editor.SuggestionsOf(Definition);
        var within = Editor.WithinOf(Definition);
        var chosen = Definition.GetValues(Draft).Cast<int>().ToList();
        var key = $"{kind}|{string.Join(',', values)}|{within}|{string.Join(',', chosen)}|{Editor.Localizer.Culture.Name}";

        // Read on every check of the settings: the lists are made again only when what they show changed.
        if (key == _shown)
            return;

        _shown = key;

        // Resources where they grow (D127), or monsters where they live (D245).
        List<(GameReference SubArea, int Maps, IReadOnlyList<int> Found)> holding = kind is SettingKind.Monster
            ? [.. (Editor.Game?.SubAreasLiving(values, within) ?? []).Select(subArea => (subArea.SubArea, subArea.Maps, subArea.Monsters))]
            : [.. (Editor.Game?.SubAreasHolding(values, within) ?? []).Select(subArea => (subArea.SubArea, subArea.Maps, subArea.Resources))];
        var suggestions = new List<SuggestionViewModel>();

        foreach (var id in chosen)
            suggestions.Add(Suggestion(id, kind, holding.FirstOrDefault(subArea => subArea.SubArea.Id == id), chosen));

        foreach (var subArea in holding.Where(subArea => !chosen.Contains(subArea.SubArea.Id)).Take(Math.Max(0, MostSuggestions - chosen.Count)))
            suggestions.Add(Suggestion(subArea.SubArea.Id, kind, subArea, chosen));

        Suggestions = suggestions;
    }

    private SuggestionViewModel Suggestion(int id, SettingKind kind, (GameReference SubArea, int Maps, IReadOnlyList<int> Found) held, IReadOnlyList<int> chosen)
    {
        var game = Editor.Game;
        var localizer = Editor.Localizer;
        var found = held.Found ?? [];
        var reference = found.Count > 0 ? held.SubArea : game?.Options(SettingKind.SubArea).FirstOrDefault(subArea => subArea.Id == id) ?? new GameReference(id, 0, null);
        var maps = found.Count > 0 ? held.Maps : game?.MapsOf(id).Count() ?? 0;
        var names = found.Select(value => Editor.NameOf(kind, game?.Options(kind).FirstOrDefault(option => option.Id == value) ?? new GameReference(value, 0, null)));
        var parts = new List<string>();

        if (reference.Level is { } level)
            parts.Add(localizer.Format("Level", level));

        parts.Add(localizer.Plural("Maps", maps));

        if (found.Count > 0)
            parts.Add(string.Join(", ", names));

        var place = chosen.ToList().IndexOf(id);

        return new SuggestionViewModel(id, Editor.NameOf(SettingKind.SubArea, reference), string.Join(", ", parts), place < 0 ? null : place, Toggle);
    }
}
