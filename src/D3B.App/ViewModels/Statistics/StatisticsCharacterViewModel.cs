using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Characters;

namespace D3B.App.ViewModels.Statistics;

/// <summary>A character the statistics page may count alone, or every one, a chip at the page's top (D146).</summary>
internal sealed partial class StatisticsCharacterViewModel : ObservableObject
{
    private readonly Action<StatisticsCharacterViewModel> _select;

    /// <summary>Gets the character, or <see langword="null"/> for every one.</summary>
    public CharacterKey? Key { get; }

    /// <summary>Gets its name: the character's, or the app's word for every one.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>Gets or sets whether the page counts it.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="StatisticsCharacterViewModel"/> class.</summary>
    /// <param name="key">The character, or <see langword="null"/> for every one.</param>
    /// <param name="name">Its name.</param>
    /// <param name="select">Counts it on the page.</param>
    public StatisticsCharacterViewModel(CharacterKey? key, string name, Action<StatisticsCharacterViewModel> select)
    {
        Key = key;
        Name = name;
        _select = select;
    }

    [RelayCommand]
    private void Select()
    {
        _select(this);
    }
}
