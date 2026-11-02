using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>One of an enumeration's values, as a list to choose from, or segments side by side for a choice of three at most (D127).</summary>
internal sealed partial class ChoiceFieldViewModel : SettingFieldViewModel
{
    private const int MostSegments = 3;

    /// <summary>Gets the values the choice offers, named in the app's language.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSegmented), nameof(IsList))]
    public partial IReadOnlyList<OptionViewModel> Options { get; private set; } = [];

    /// <summary>Gets or sets the value chosen.</summary>
    [ObservableProperty]
    public partial OptionViewModel? Selected { get; set; }

    /// <summary>Gets the values as segments, the one chosen marked.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SegmentViewModel> Segments { get; private set; } = [];

    /// <summary>Gets whether the choice shows as segments: three values at most.</summary>
    public bool IsSegmented => Options.Count is > 1 and <= MostSegments;

    /// <summary>Gets whether the choice shows as a list.</summary>
    public bool IsList => !IsSegmented;

    /// <summary>Initializes a new instance of the <see cref="ChoiceFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public ChoiceFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Read();
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        Read();
    }

    private void Read()
    {
        var culture = Editor.Localizer.Culture;
        var value = Definition.GetValue(Draft);

        Quiet(() =>
        {
            Options = [.. Definition.Options.Select(option => new OptionViewModel(option.Value, option.Text.In(culture)))];
            Selected = Options.FirstOrDefault(option => option.Value.Equals(value));
        });
    }

    partial void OnSelectedChanged(OptionViewModel? value)
    {
        Segments = [.. Options.Select(option => new SegmentViewModel(option, option == value, chosen => Selected = chosen))];

        if (value is not null)
            Write(value.Value);
    }
}
