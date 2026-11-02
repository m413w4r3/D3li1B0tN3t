using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A flag, on or off, as a switch.</summary>
internal sealed partial class FlagFieldViewModel : SettingFieldViewModel
{
    /// <summary>Gets or sets whether the flag is on.</summary>
    [ObservableProperty]
    public partial bool Value { get; set; }

    /// <summary>Initializes a new instance of the <see cref="FlagFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public FlagFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = (bool)definition.GetValue(Draft)!);
    }

    partial void OnValueChanged(bool value)
    {
        Write(value);
    }
}
