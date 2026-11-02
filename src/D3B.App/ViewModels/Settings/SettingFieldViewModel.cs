using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>A setting as the profile's editor shows it: its name, its value, what is wrong with it, and whether it shows (D98).</summary>
/// <remarks>
/// Each kind edits its value its own way, writes it into the editor's settings, and the editor checks them all again:
/// a change to one setting may show or hide another, or change what another offers.
/// </remarks>
internal abstract partial class SettingFieldViewModel : ObservableObject
{
    private bool _quiet;

    /// <summary>Gets the setting.</summary>
    public SettingDefinition Definition { get; }

    /// <summary>Gets the setting's name.</summary>
    [ObservableProperty]
    public partial string Label { get; private set; } = "";

    /// <summary>Gets what the activity says of the setting, if anything.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHelp))]
    public partial string? Help { get; private set; }

    /// <summary>Gets what is wrong with the setting's value, if anything.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    /// <summary>Gets whether the setting shows, as the activity says from the other settings.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; } = true;

    /// <summary>Gets whether the activity says something of the setting.</summary>
    public bool HasHelp => Help is not null;

    /// <summary>Gets whether something is wrong with the setting's value.</summary>
    public bool HasProblem => Problem is not null;

    /// <summary>Gets whether what was typed is no value at all, such as an empty number.</summary>
    public virtual bool HasInputProblem => false;

    /// <summary>Gets whether the field takes a whole row of its section, as a list of tiles or a zone with its map does (D127).</summary>
    public virtual bool IsWide => false;

    /// <summary>Gets the editor the setting belongs to.</summary>
    protected SettingsEditor Editor { get; }

    /// <summary>Gets the settings being edited.</summary>
    protected object Draft => Editor.Draft!;

    /// <summary>Initializes a new instance of the <see cref="SettingFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    protected SettingFieldViewModel(SettingDefinition definition, SettingsEditor editor)
    {
        Definition = definition;
        Editor = editor;
        Label = definition.Name.In(editor.Localizer.Culture);
        Help = definition.Help?.In(editor.Localizer.Culture);
    }

    /// <summary>Names the setting again, in the app's language.</summary>
    public virtual void Relabel()
    {
        Label = Definition.Name.In(Editor.Localizer.Culture);
        Help = Definition.Help?.In(Editor.Localizer.Culture);
    }

    /// <summary>Shows the setting as the editor checked it.</summary>
    /// <param name="problem">What is wrong with its value, if anything.</param>
    public void Update(SettingProblem? problem)
    {
        IsVisible = Editor.Settings.IsVisible(Definition, Editor.Draft);
        Quiet(RefreshOptions);
        Problem = InputProblem() ?? (problem is null ? null : Editor.ProblemText(problem));
    }

    /// <summary>Says what was typed that is no value at all.</summary>
    /// <returns>The problem, or <see langword="null"/>.</returns>
    protected virtual string? InputProblem()
    {
        return null;
    }

    /// <summary>Offers again what the setting offers, once another setting it is sought within changed.</summary>
    protected virtual void RefreshOptions()
    {
    }

    /// <summary>Changes what the view shows without writing it back, as when the editor reads the settings.</summary>
    /// <param name="change">The change.</param>
    protected void Quiet(Action change)
    {
        var quiet = _quiet;

        _quiet = true;

        try
        {
            change();
        }
        finally
        {
            _quiet = quiet;
        }
    }

    /// <summary>Writes a value the owner gave into the settings, and checks them all again.</summary>
    /// <param name="value">The value, of the setting's own type.</param>
    protected void Write(object? value)
    {
        if (_quiet)
            return;

        Definition.SetValue(Draft, value);
        Editor.Changed();
    }

    /// <summary>Tells the editor the field changed without a value to write, such as a number emptied.</summary>
    protected void Touched()
    {
        if (!_quiet)
            Editor.Changed();
    }
}
