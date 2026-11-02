using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>Several of an enumeration's values, each once, added from a list of those not chosen yet.</summary>
internal sealed partial class ChoicesFieldViewModel : ListFieldViewModel
{
    private IReadOnlyList<OptionViewModel> _options = [];

    /// <summary>Gets the values not chosen yet.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OptionViewModel> Available { get; private set; } = [];

    /// <summary>Gets or sets the value to add: chosen, it is added at the end, and the list empties again.</summary>
    [ObservableProperty]
    public partial OptionViewModel? ToAdd { get; set; }

    /// <summary>Initializes a new instance of the <see cref="ChoicesFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public ChoicesFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Name();
        ReadItems();
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        Name();
        ReadItems();
    }

    /// <inheritdoc/>
    protected override OptionViewModel OptionOf(object value)
    {
        return _options.FirstOrDefault(option => option.Value.Equals(value)) ?? new OptionViewModel(value, value.ToString() ?? "");
    }

    /// <inheritdoc/>
    protected override void ReadItems()
    {
        base.ReadItems();

        var chosen = Definition.GetValues(Draft);

        Available = [.. _options.Where(option => !chosen.Contains(option.Value))];
    }

    private void Name()
    {
        var culture = Editor.Localizer.Culture;

        _options = [.. Definition.Options.Select(option => new OptionViewModel(option.Value, option.Text.In(culture)))];
    }

    partial void OnToAddChanged(OptionViewModel? value)
    {
        if (value is null)
            return;

        Add(value);
        Quiet(() => ToAdd = null);
    }
}
