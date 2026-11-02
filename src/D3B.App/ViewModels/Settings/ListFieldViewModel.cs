using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>Several values, each once, in order: added from what the setting offers, moved when the order matters, removed.</summary>
internal abstract partial class ListFieldViewModel : SettingFieldViewModel
{
    /// <summary>Gets the values, in order.</summary>
    public ObservableCollection<ListItemViewModel> Items { get; } = [];

    /// <summary>Gets whether the list holds no value.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>Gets the app's language, which numbers the values.</summary>
    public CultureInfo Culture => Editor.Localizer.Culture;

    /// <summary>Initializes a new instance of the <see cref="ListFieldViewModel"/> class.</summary>
    /// <param name="definition">The setting.</param>
    /// <param name="editor">The editor it belongs to.</param>
    protected ListFieldViewModel(SettingDefinition definition, SettingsEditor editor)
        : base(definition, editor)
    {
    }

    /// <summary>Moves a value up or down.</summary>
    /// <param name="index">Its place.</param>
    /// <param name="delta">-1 to move it up, 1 down.</param>
    public void Move(int index, int delta)
    {
        var values = Definition.GetValues(Draft).ToList();
        var target = index + delta;

        if (index < 0 || index >= values.Count || target < 0 || target >= values.Count)
            return;

        (values[index], values[target]) = (values[target], values[index]);
        WriteValues(values);
    }

    /// <summary>Removes a value.</summary>
    /// <param name="index">Its place.</param>
    public void Remove(int index)
    {
        var values = Definition.GetValues(Draft).ToList();

        if (index < 0 || index >= values.Count)
            return;

        values.RemoveAt(index);
        WriteValues(values);
    }

    /// <summary>Adds values at the end, in their order.</summary>
    /// <param name="options">The values.</param>
    public void AddAll(IReadOnlyList<OptionViewModel> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count > 0)
            WriteValues([.. Definition.GetValues(Draft), .. options.Select(option => option.Value)]);
    }

    /// <summary>Adds a value at the end.</summary>
    /// <param name="option">The value.</param>
    protected void Add(OptionViewModel option)
    {
        WriteValues([.. Definition.GetValues(Draft), option.Value]);
    }

    /// <summary>Names a value of the list.</summary>
    /// <param name="value">The value.</param>
    /// <returns>It, named.</returns>
    protected abstract OptionViewModel OptionOf(object value);

    /// <summary>Says what the list warns of a value, if anything.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The warning; none by default.</returns>
    protected virtual string? WarningOf(object value)
    {
        return null;
    }

    /// <summary>Shows the values as the settings hold them.</summary>
    protected virtual void ReadItems()
    {
        var values = Definition.GetValues(Draft);

        Items.Clear();

        for (var index = 0; index < values.Count; index++)
            Items.Add(new ListItemViewModel(this, OptionOf(values[index]), index, values.Count, WarningOf(values[index])));

        IsEmpty = Items.Count == 0;
    }

    private void WriteValues(List<object> values)
    {
        Write(values);
        ReadItems();
    }
}
