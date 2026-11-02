using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A text of bounded length, as a box.</summary>
internal sealed partial class TextFieldViewModel : SettingFieldViewModel
{
    /// <summary>Gets the text's greatest length.</summary>
    public int MaximumLength => Definition.MaximumLength ?? 100;

    /// <summary>Gets or sets the text.</summary>
    [ObservableProperty]
    public partial string Value { get; set; } = "";

    /// <summary>Initializes a new instance of the <see cref="TextFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public TextFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
        Quiet(() => Value = (string)definition.GetValue(Draft)!);
    }

    partial void OnValueChanged(string value)
    {
        Write(value);
    }
}
