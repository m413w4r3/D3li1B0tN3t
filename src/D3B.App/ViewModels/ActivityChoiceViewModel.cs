using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.Bot.Activities;
using D3B.Storage.Profiles;

namespace D3B.App.ViewModels;

/// <summary>What a bot can be given: a profile, its activity with its settings, or an activity with its defaults, named in the app's language (D96, D99).</summary>
internal sealed partial class ActivityChoiceViewModel : ObservableObject
{
    private readonly Func<CultureInfo, string?>? _detail;

    /// <summary>Gets the activity, as its module registered it, or <see langword="null"/> for a profile whose activity no module registers.</summary>
    public ActivityDefinition? Definition { get; }

    /// <summary>Gets the profile, or <see langword="null"/> for an activity with its defaults.</summary>
    public Profile? Profile { get; }

    /// <summary>Gets the profile's name, or the activity's in the app's language.</summary>
    [ObservableProperty]
    public partial string Name { get; private set; }

    /// <summary>Gets what the app says beside the name: a profile's activity, or that an activity runs with its defaults.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial string? Detail { get; private set; }

    /// <summary>Gets whether the app says something beside the name.</summary>
    public bool HasDetail => Detail is not null;

    /// <summary>Initializes a new instance of the <see cref="ActivityChoiceViewModel"/> class, for an activity.</summary>
    /// <param name="definition">The activity.</param>
    /// <param name="culture">The app's language.</param>
    /// <param name="detail">What the app says beside its name, in a language.</param>
    public ActivityChoiceViewModel(ActivityDefinition definition, CultureInfo culture, Func<CultureInfo, string?>? detail = null)
    {
        Definition = definition;
        _detail = detail;
        Name = definition.Name.In(culture);
        Detail = detail?.Invoke(culture);
    }

    /// <summary>Initializes a new instance of the <see cref="ActivityChoiceViewModel"/> class, for a profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="definition">Its activity, or <see langword="null"/> when no module registers it.</param>
    /// <param name="culture">The app's language.</param>
    /// <param name="detail">What the app says beside its name, in a language.</param>
    public ActivityChoiceViewModel(Profile profile, ActivityDefinition? definition, CultureInfo culture, Func<CultureInfo, string?> detail)
    {
        Profile = profile;
        Definition = definition;
        _detail = detail;
        Name = profile.Name;
        Detail = detail(culture);
    }

    /// <summary>Names the choice again, in the app's language.</summary>
    /// <param name="culture">The app's language.</param>
    public void Refresh(CultureInfo culture)
    {
        Name = Profile?.Name ?? Definition!.Name.In(culture);
        Detail = _detail?.Invoke(culture);
    }
}
