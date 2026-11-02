using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A chip a list filters by: a profession, or none for <c>Base</c>'s resources (D150), a super race (D236), or every one.</summary>
internal sealed partial class FilterChipViewModel : ObservableObject
{
    private readonly Action<FilterChipViewModel> _select;

    /// <summary>Gets the profession or super race, or <see langword="null"/> for every one and for none.</summary>
    public int? Id { get; }

    /// <summary>Gets its name.</summary>
    public string Name { get; }

    /// <summary>Gets its icon, when the pack keeps one.</summary>
    public IImage? Icon { get; }

    /// <summary>Gets or sets whether it stands for the values of no group, such as <c>Base</c>'s resources.</summary>
    public bool IsBase { get; init; }

    /// <summary>Gets or sets whether it is the filter chosen.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="FilterChipViewModel"/> class.</summary>
    /// <param name="id">The profession or super race, or <see langword="null"/>.</param>
    /// <param name="name">Its name.</param>
    /// <param name="icon">Its icon, or <see langword="null"/>.</param>
    /// <param name="select">Chooses it.</param>
    public FilterChipViewModel(int? id, string name, IImage? icon, Action<FilterChipViewModel> select)
    {
        Id = id;
        Name = name;
        Icon = icon;
        _select = select;
    }

    [RelayCommand]
    private void Choose()
    {
        _select(this);
    }
}
