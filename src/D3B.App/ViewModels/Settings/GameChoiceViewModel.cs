using CommunityToolkit.Mvvm.ComponentModel;

namespace D3B.App.ViewModels.Settings;

/// <summary>A value the dialog offers, a resource or a monster: its name and icon, its group and level, checked or not.</summary>
internal sealed partial class GameChoiceViewModel : ObservableObject
{
    private readonly Action<GameChoiceViewModel> _checked;

    /// <summary>Gets the value.</summary>
    public OptionViewModel Option { get; }

    /// <summary>Gets its group: the profession that gathers a resource, or <see langword="null"/> for one of <c>Base</c>'s (D150); a monster's super race.</summary>
    public int? GroupId { get; }

    /// <summary>Gets its level, a resource's its profession gathers it from, a monster's its first grade's, if it has one.</summary>
    public int? Level { get; }

    /// <summary>Gets whether a character playing the profile cannot gather it yet, as its bot last saw it.</summary>
    public bool IsTooHigh { get; }

    /// <summary>Gets its level, said, or nothing.</summary>
    public string LevelText => Level?.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? "";

    /// <summary>Gets or sets whether it is checked, to be added.</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    /// <summary>Initializes a new instance of the <see cref="GameChoiceViewModel"/> class.</summary>
    /// <param name="option">The value.</param>
    /// <param name="groupId">Its group, or <see langword="null"/>.</param>
    /// <param name="level">Its level, if any.</param>
    /// <param name="tooHigh">Whether a character playing the profile cannot gather it yet.</param>
    /// <param name="checked">Told when it is checked or unchecked.</param>
    public GameChoiceViewModel(OptionViewModel option, int? groupId, int? level, bool tooHigh, Action<GameChoiceViewModel> @checked)
    {
        Option = option;
        GroupId = groupId;
        Level = level;
        IsTooHigh = tooHigh;
        _checked = @checked;
    }

    partial void OnIsCheckedChanged(bool value)
    {
        _checked(this);
    }
}
