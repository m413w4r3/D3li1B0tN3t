using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Localization;
using D3B.Bot.Settings;

namespace D3B.App.ViewModels.Settings;

/// <summary>The dialog choosing resources or monsters several at once: searched by name, filtered by profession or super race, resources its characters cannot gather yet dimmed (D159, D236).</summary>
/// <remarks>
/// The values checked are added in the order they were checked, after those the list holds. More groups than
/// <see cref="MostChips"/>, such as the monsters' super races, are a drop-down beside the search rather than chips, so that
/// the dialog keeps its height (D245).
/// </remarks>
internal sealed partial class GamePickerViewModel : ObservableObject
{
    /// <summary>The most groups shown as chips: the professions that gather are 7, the monsters' super races some 40.</summary>
    public const int MostChips = 12;

    /// <summary>The most values shown at once: the dialog lays every one out, and "All" of the monsters is some 1,600 (D246).</summary>
    public const int MostShown = 120;

    private readonly Dialogs _dialogs;
    private readonly PickedGamesFieldViewModel _field;
    private readonly Localizer _localizer;
    private readonly IReadOnlyList<GameChoiceViewModel> _all;
    private readonly List<GameChoiceViewModel> _checked = [];
    private readonly string _add;

    /// <summary>Gets the groups to filter by, every one first: professions or super races.</summary>
    public IReadOnlyList<FilterChipViewModel> Groups { get; }

    /// <summary>Gets whether the groups are too many for chips, and shown as a drop-down.</summary>
    public bool IsCompact => Groups.Count > MostChips;

    /// <summary>Gets or sets the group the values are filtered by, as the drop-down shows it.</summary>
    public FilterChipViewModel ChosenGroup
    {
        get => SelectedGroup;
        set
        {
            if (value is not null && !value.IsSelected)
                Select(value);
        }
    }

    /// <summary>Gets the values shown, by level then name.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial IReadOnlyList<GameChoiceViewModel> Shown { get; private set; } = [];

    /// <summary>Gets or sets what the name searched holds.</summary>
    [ObservableProperty]
    public partial string? Search { get; set; }

    /// <summary>Gets what the button adding them says, with how many are checked.</summary>
    [ObservableProperty]
    public partial string AddText { get; private set; } = "";

    /// <summary>Gets whether a value is checked.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial bool HasChecked { get; private set; }

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets whether no value matches.</summary>
    public bool IsEmpty => Shown.Count == 0;

    /// <summary>Gets whether more values match than are shown.</summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>Gets what the dialog says of the values it does not show, and how to see them.</summary>
    [ObservableProperty]
    public partial string MoreText { get; private set; } = "";

    private FilterChipViewModel SelectedGroup => Groups.First(group => group.IsSelected);

    /// <summary>Initializes a new instance of the <see cref="GamePickerViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs, which it closes.</param>
    /// <param name="field">The list it adds to.</param>
    /// <param name="editor">The editor the list belongs to.</param>
    /// <param name="offered">The values the list does not hold yet.</param>
    public GamePickerViewModel(Dialogs dialogs, PickedGamesFieldViewModel field, SettingsEditor editor, IReadOnlyList<OptionViewModel> offered)
    {
        _dialogs = dialogs;
        _field = field;
        _localizer = editor.Localizer;
        _add = field.ChooseKey;
        Title = _localizer[_add];

        var kind = field.Definition.Kind;
        var compare = StringComparer.Create(editor.Localizer.Culture, CompareOptions.IgnoreCase);

        _all =
        [
            .. offered
                .Select(option =>
                {
                    var id = (int)option.Value;
                    var (group, level) = editor.GroupOf(kind, id);

                    return new GameChoiceViewModel(option, group, level, kind is SettingKind.Resource && editor.MissingLevelOf(id) is not null, OnChecked);
                })
                .OrderBy(choice => choice.Level ?? 0)
                .ThenBy(choice => choice.Option.Name, compare),
        ];

        var groups = new List<FilterChipViewModel> { new(null, editor.Localizer["AllJobs"], null, Select) { IsSelected = true } };

        foreach (var groupId in _all.Select(choice => choice.GroupId).Distinct().OrderBy(id => id is null).ThenBy(id => id))
        {
            if (groupId is { } id)
            {
                var group = editor.GroupOption(kind, id);

                groups.Add(new FilterChipViewModel(id, group.Name, group.Icon, Select));
            }
            else
            {
                groups.Add(new FilterChipViewModel(null, editor.Localizer[kind is SettingKind.Resource ? "NoJob" : "NoGroup"], null, Select) { IsBase = true });
            }
        }

        Groups = groups;
        AddText = editor.Localizer[_add];
        Filter();
    }

    private void Select(FilterChipViewModel group)
    {
        foreach (var other in Groups)
            other.IsSelected = other == group;

        OnPropertyChanged(nameof(ChosenGroup));
        Filter();
    }

    private void Filter()
    {
        var group = SelectedGroup;
        var search = Search?.Trim() ?? "";

        List<GameChoiceViewModel> matching =
        [
            .. _all.Where(choice => (group.Id is null && !group.IsBase) || (group.IsBase ? choice.GroupId is null : choice.GroupId == group.Id))
                .Where(choice => search.Length == 0 || choice.Option.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)),
        ];

        Shown = matching.Count > MostShown ? matching[..MostShown] : matching;
        HasMore = matching.Count > MostShown;
        MoreText = HasMore ? _localizer.Format("PickerMore", MostShown, matching.Count) : "";
    }

    private void OnChecked(GameChoiceViewModel choice)
    {
        if (choice.IsChecked)
            _checked.Add(choice);
        else
            _checked.Remove(choice);

        HasChecked = _checked.Count > 0;
        AddText = _checked.Count == 0 ? _localizer[_add] : _localizer.Format(_add + "Count", _checked.Count);
    }

    [RelayCommand(CanExecute = nameof(HasChecked))]
    private void Add()
    {
        _dialogs.Close(this);
        _field.AddAll([.. _checked.Select(choice => choice.Option)]);
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }

    partial void OnSearchChanged(string? value)
    {
        Filter();
    }
}
