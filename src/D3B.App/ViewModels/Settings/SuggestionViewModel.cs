using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A sub-area a zone's field suggests, where resources asked for grow: chosen or not, a click choosing or leaving it (D127).</summary>
internal sealed partial class SuggestionViewModel
{
    private readonly Action<int> _toggle;

    /// <summary>Gets the sub-area's id.</summary>
    public int Id { get; }

    /// <summary>Gets its name.</summary>
    public string Name { get; }

    /// <summary>Gets its level, its maps and the resources asked for it holds.</summary>
    public string Detail { get; }

    /// <summary>Gets whether it is chosen.</summary>
    public bool IsChosen { get; }

    /// <summary>Gets whether it is chosen in the zone's second part, which its map tints apart.</summary>
    public bool IsSecond { get; }

    /// <summary>Initializes a new instance of the <see cref="SuggestionViewModel"/> class.</summary>
    /// <param name="id">The sub-area's id.</param>
    /// <param name="name">Its name.</param>
    /// <param name="detail">Its level, maps and resources.</param>
    /// <param name="chosen">Its place among those chosen, or <see langword="null"/>.</param>
    /// <param name="toggle">Chooses or leaves a sub-area.</param>
    public SuggestionViewModel(int id, string name, string detail, int? chosen, Action<int> toggle)
    {
        Id = id;
        Name = name;
        Detail = detail;
        IsChosen = chosen is not null;
        IsSecond = chosen is { } place && place % 2 is 1;
        _toggle = toggle;
    }

    [RelayCommand]
    private void Toggle()
    {
        _toggle(Id);
    }
}
