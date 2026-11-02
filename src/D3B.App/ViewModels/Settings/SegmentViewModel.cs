using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels.Settings;

/// <summary>A value of a short choice, as a segment beside the others: chosen or not, a click choosing it (D127).</summary>
internal sealed partial class SegmentViewModel
{
    private readonly Action<OptionViewModel> _choose;

    /// <summary>Gets the value.</summary>
    public OptionViewModel Option { get; }

    /// <summary>Gets whether it is the one chosen.</summary>
    public bool IsSelected { get; }

    /// <summary>Initializes a new instance of the <see cref="SegmentViewModel"/> class.</summary>
    /// <param name="option">The value.</param>
    /// <param name="selected">Whether it is the one chosen.</param>
    /// <param name="choose">Chooses a value.</param>
    public SegmentViewModel(OptionViewModel option, bool selected, Action<OptionViewModel> choose)
    {
        Option = option;
        IsSelected = selected;
        _choose = choose;
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(Option);
    }
}
