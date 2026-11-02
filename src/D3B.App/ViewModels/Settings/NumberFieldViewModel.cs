using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A whole number between its bounds, as a box with steps.</summary>
internal sealed partial class NumberFieldViewModel : SettingFieldViewModel
{
    private bool _empty;

    /// <summary>Gets the lowest value.</summary>
    public decimal Minimum => Definition.Minimum ?? int.MinValue;

    /// <summary>Gets the highest value.</summary>
    public decimal Maximum => Definition.Maximum ?? int.MaxValue;

    /// <summary>Gets or sets the number, or <see langword="null"/> once the box is emptied.</summary>
    [ObservableProperty]
    public partial decimal? Value { get; set; }

    /// <summary>Gets what the box takes, from its lowest value to its highest.</summary>
    [ObservableProperty]
    public partial string Range { get; private set; } = "";

    /// <summary>Gets the number, as a percentage, said.</summary>
    [ObservableProperty]
    public partial string PercentText { get; private set; } = "";

    /// <summary>Gets whether the number is a percentage, shown as a bar (D127).</summary>
    public bool IsPercent => Definition.IsPercent;

    /// <summary>Gets whether the number is shown as a box.</summary>
    public bool IsBox => !Definition.IsPercent;

    /// <summary>Gets what the number counts, after it, in the app's language (D167).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnit))]
    public partial string? Unit { get; private set; }

    /// <summary>Gets whether the number counts something it says.</summary>
    public bool HasUnit => Unit is not null;

    /// <summary>Gets the lowest value, for the bar of a percentage.</summary>
    public double SliderMinimum => (double)Minimum;

    /// <summary>Gets the highest value, for the bar of a percentage.</summary>
    public double SliderMaximum => (double)Maximum;

    /// <summary>Gets or sets the number, as the bar of a percentage moves it.</summary>
    public double Slider
    {
        get => (double)(Value ?? Minimum);
        set => Value = (decimal)Math.Round(value);
    }

    /// <inheritdoc/>
    public override bool HasInputProblem => _empty;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public NumberFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Range = editor.Localizer.Format("NumberRange", Minimum, Maximum);
        Unit = definition.Unit?.In(editor.Localizer.Culture);
        Quiet(() => Value = (int)definition.GetValue(Draft)!);
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        Range = Editor.Localizer.Format("NumberRange", Minimum, Maximum);
        Unit = Definition.Unit?.In(Editor.Localizer.Culture);
        PercentText = Value is { } shown ? Editor.Localizer.Format("PercentValue", shown) : "";
    }

    /// <inheritdoc/>
    protected override string? InputProblem()
    {
        return _empty ? Editor.Localizer["SettingRequired"] : null;
    }

    partial void OnValueChanged(decimal? value)
    {
        _empty = value is null;
        PercentText = value is { } shown ? Editor.Localizer.Format("PercentValue", shown) : "";
        OnPropertyChanged(nameof(Slider));

        if (value is { } number)
            Write((int)Math.Round(Math.Clamp(number, int.MinValue, int.MaxValue)));
        else
            Touched();
    }
}
