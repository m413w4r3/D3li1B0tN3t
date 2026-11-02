using CommunityToolkit.Mvvm.Input;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>Resources or monsters, in their order, chosen several at once in a dialog (D159, D236): a resource with its profession and level, and a warning when a character playing the profile lacks it (D127).</summary>
internal sealed partial class PickedGamesFieldViewModel : GamesFieldViewModel
{
    /// <inheritdoc/>
    public override bool IsWide => true;

    /// <summary>Initializes a new instance of the <see cref="PickedGamesFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public PickedGamesFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
    }

    /// <summary>Gets whether the dialog choosing them can open: the editor shows in a window, and the host has the game's values.</summary>
    public bool CanChoose => Editor.Dialogs is not null && HasGame;

    /// <summary>Gets what choosing does: adding them, or leaving them out (D165).</summary>
    public string ChooseText => Editor.Localizer[ChooseKey];

    /// <summary>Gets the key of what choosing says, such as <c>AddMonsters</c> or <c>ExcludeResources</c>, its count's the same with <c>Count</c> after.</summary>
    public string ChooseKey => (Definition.IsExcluding ? "Exclude" : "Add") + Definition.Kind switch
    {
        SettingKind.Monster => "Monsters",
        _ => "Resources",
    };

    /// <inheritdoc/>
    public override void Relabel()
    {
        base.Relabel();
        OnPropertyChanged(nameof(ChooseText));
    }

    /// <inheritdoc/>
    protected override string? WarningOf(object value)
    {
        // A resource left out is never harvested: no level it asks is lacking; monsters ask none.
        return Definition.IsExcluding || Definition.Kind is not SettingKind.Resource ? null : Editor.MissingLevelOf((int)value);
    }

    [RelayCommand]
    private void Choose()
    {
        if (Editor.Dialogs is { } dialogs)
            dialogs.Show(new GamePickerViewModel(dialogs, this, Editor, Available));
    }
}
