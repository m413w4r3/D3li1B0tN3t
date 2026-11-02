using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>One of the game's values, or none, as a box that searches them by name, within the setting it is sought within.</summary>
internal sealed partial class GameFieldViewModel : SettingFieldViewModel
{
    private int? _within;

    /// <summary>Gets the values offered: the game's, within the one the setting is sought within once it has one.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<OptionViewModel> Options { get; private set; } = [];

    /// <summary>Gets or sets the value chosen, or <see langword="null"/> for none.</summary>
    [ObservableProperty]
    public partial OptionViewModel? Selected { get; set; }

    /// <summary>Gets whether the host has the game's values to offer.</summary>
    public bool HasGame => Editor.Game is not null;

    /// <summary>Initializes a new instance of the <see cref="GameFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public GameFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        _within = editor.WithinOf(definition);
        Quiet(Read);
    }

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        Quiet(Read);
    }

    /// <inheritdoc/>
    protected override void RefreshOptions()
    {
        var within = Editor.WithinOf(Definition);

        if (within == _within)
            return;

        _within = within;
        Read();
    }

    private void Read()
    {
        Options = Editor.OptionsOf(Definition, _within);
        Selected = Definition.GetValue(Draft) is int id ? Options.FirstOrDefault(option => (int)option.Value == id) ?? Editor.OptionOf(Definition, id) : null;
    }

    [RelayCommand]
    private void Clear()
    {
        Selected = null;
    }

    partial void OnSelectedChanged(OptionViewModel? value)
    {
        Write(value?.Value);
    }
}
