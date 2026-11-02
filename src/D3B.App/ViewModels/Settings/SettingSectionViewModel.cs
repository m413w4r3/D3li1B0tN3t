using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Localization;

namespace D3B.App.ViewModels.Settings;

/// <summary>Settings the activity groups under a title, or those before its first section, under none.</summary>
internal sealed partial class SettingSectionViewModel : ObservableObject
{
    private readonly LocalizedText? _title;

    /// <summary>Gets the section's settings.</summary>
    public IReadOnlyList<SettingFieldViewModel> Fields { get; }

    /// <summary>Gets the section's title, if it has one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitle))]
    public partial string? Title { get; private set; }

    /// <summary>Gets whether any of its settings shows.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; } = true;

    /// <summary>Gets whether the section has a title.</summary>
    public bool HasTitle => Title is not null;

    /// <summary>Initializes a new instance of the <see cref="SettingSectionViewModel"/> class.</summary>
    /// <param name="title">The title the activity gave it.</param>
    /// <param name="fields">Its settings.</param>
    /// <param name="editor">The editor it belongs to.</param>
    public SettingSectionViewModel(LocalizedText? title, IReadOnlyList<SettingFieldViewModel> fields, SettingsEditor editor)
    {
        _title = title;
        Fields = fields;
        Title = title?.In(editor.Localizer.Culture);
    }

    /// <summary>Shows the section as its settings show.</summary>
    /// <param name="editor">The editor it belongs to.</param>
    public void Update(SettingsEditor editor)
    {
        Title = _title?.In(editor.Localizer.Culture);
        IsVisible = Fields.Any(field => field.IsVisible);
    }
}
