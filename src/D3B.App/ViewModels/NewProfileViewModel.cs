using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels;

/// <summary>The dialog starting a profile: its name and activity, from the activity's defaults or a copy of another's settings (D159).</summary>
internal sealed partial class NewProfileViewModel : ObservableObject
{
    private readonly ProfilesViewModel _page;
    private readonly ProfileViewModel? _source;

    /// <summary>Gets the dialog's title.</summary>
    public string Title { get; }

    /// <summary>Gets the activities a profile may run.</summary>
    public IReadOnlyList<ActivityChoiceViewModel> Activities => _page.Activities;

    /// <summary>Gets whether the profile is a copy, its activity the other's.</summary>
    public bool IsCopy => _source is not null;

    /// <summary>Gets or sets the profile's name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>Gets or sets the profile's activity.</summary>
    [ObservableProperty]
    public partial ActivityChoiceViewModel? Activity { get; set; }

    /// <summary>Gets what is wrong with the name, if anything, once one was typed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNameProblem))]
    public partial string? NameProblem { get; private set; }

    /// <summary>Gets whether the profile can be made: a name of its own and an activity.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool CanCreate { get; private set; }

    /// <summary>Gets whether something is wrong with the name.</summary>
    public bool HasNameProblem => NameProblem is not null;

    /// <summary>Initializes a new instance of the <see cref="NewProfileViewModel"/> class.</summary>
    /// <param name="page">The profiles page, which opens the profile's editor.</param>
    /// <param name="source">The profile copied, or <see langword="null"/> for a new one.</param>
    public NewProfileViewModel(ProfilesViewModel page, ProfileViewModel? source)
    {
        _page = page;
        _source = source;
        Title = page.Localizer[source is null ? "NewProfile" : "DuplicateProfile"];
        Activity = source?.SavedActivity ?? (Activities.Count > 0 ? Activities[0] : null);
        Name = source is null ? "" : page.CopyName(source.Name);
        Check(quiet: source is null);
    }

    private void Check(bool quiet)
    {
        var name = Name.Trim();
        var problem = name.Length == 0 ? _page.Localizer["NameRequired"]
            : _page.Profiles?.IsTaken(name, null) == true ? _page.Localizer["NameTaken"]
            : null;

        // An empty name is no mistake while the dialog opens: the button waits for one.
        NameProblem = quiet && name.Length == 0 ? null : problem;
        CanCreate = problem is null && Activity?.Definition is not null;
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        if (Activity is not { Definition: not null } activity)
            return;

        _page.Dialogs.Close(this);
        _page.Create(Name.Trim(), activity, _source?.SavedSettings);
    }

    [RelayCommand]
    private void Cancel()
    {
        _page.Dialogs.Close(this);
    }

    partial void OnNameChanged(string value)
    {
        Check(quiet: false);
    }

    partial void OnActivityChanged(ActivityChoiceViewModel? value)
    {
        Check(quiet: Name.Length == 0);
    }
}
