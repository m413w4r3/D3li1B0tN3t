using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>Several of the game's values, each once, added from a box that searches them by name, within the setting they are sought within.</summary>
internal partial class GamesFieldViewModel : ListFieldViewModel
{
    private int? _within;
    private IReadOnlyList<OptionViewModel> _options = [];

    /// <summary>Gets the values offered and not chosen yet.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OptionViewModel> Available { get; private set; } = [];

    /// <summary>Gets or sets the value to add: chosen, it is added at the end, and the box empties again.</summary>
    [ObservableProperty]
    public partial OptionViewModel? ToAdd { get; set; }

    /// <summary>Gets or sets what is typed in the box.</summary>
    [ObservableProperty]
    public partial string? Search { get; set; }

    /// <summary>Gets whether the host has the game's values to offer.</summary>
    public bool HasGame => Editor.Game is not null;

    /// <summary>Gets the values the setting offers now, within the setting it is sought within.</summary>
    protected IReadOnlyList<OptionViewModel> Offered => _options;

    /// <summary>Initializes a new instance of the <see cref="GamesFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public GamesFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        _within = editor.WithinOf(definition);
        _options = editor.OptionsOf(definition, _within);
        ReadItems();
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        _options = Editor.OptionsOf(Definition, _within);
        ReadItems();
    }

    /// <inheritdoc/>
    protected override OptionViewModel OptionOf(object value)
    {
        var id = (int)value;

        return _options.FirstOrDefault(option => (int)option.Value == id) ?? Editor.OptionOf(Definition, id);
    }

    /// <inheritdoc/>
    protected override void ReadItems()
    {
        base.ReadItems();

        var chosen = Definition.GetValues(Draft);

        Available = [.. _options.Where(option => !chosen.Contains(option.Value))];
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        var within = Editor.WithinOf(Definition);

        if (within == _within)
            return;

        _within = within;
        _options = Editor.OptionsOf(Definition, within);
        ReadItems();
    }

    /// <summary>Adds a value at the end, or removes it when the list holds it.</summary>
    /// <param name="id">The value's id.</param>
    protected void Toggle(int id)
    {
        var values = Definition.GetValues(Draft);
        var index = values.ToList().FindIndex(value => (int)value == id);

        if (index >= 0)
            Remove(index);
        else
            Add(OptionOf(id));
    }

    partial void OnToAddChanged(OptionViewModel? value)
    {
        if (value is null)
            return;

        Add(value);
        Quiet(() =>
        {
            ToAdd = null;
            Search = "";
        });
    }
}
