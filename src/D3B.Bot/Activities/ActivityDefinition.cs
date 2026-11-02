using D3B.Bot.Localization;
using D3B.Bot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Activities;

/// <summary>An activity the app can start on a bot: its id, its name, its settings, and how one is made (D87, D92, D98).</summary>
/// <remarks>
/// A module registers it with <see cref="ActivityServiceCollectionExtensions.AddActivity{TActivity}(IServiceCollection, string, LocalizedText)"/>,
/// or with <see cref="ActivityServiceCollectionExtensions.AddActivity{TActivity, TSettings}"/> when it has settings;
/// <see cref="ActivityCatalog"/> lists them.
/// </remarks>
public sealed class ActivityDefinition
{
    private readonly Func<IServiceProvider, object?, BotActivity> _create;

    /// <summary>Gets the activity's id, unique in the host.</summary>
    public string Id { get; }

    /// <summary>Gets the activity's name, as the app shows it, in its language (D96).</summary>
    public LocalizedText Name { get; }

    /// <summary>Gets the settings the activity declares, <see cref="ActivitySettings.None"/> when it has none.</summary>
    public ActivitySettings Settings { get; }

    /// <summary>Initializes a new instance of the <see cref="ActivityDefinition"/> class, its name the same in every language.</summary>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it.</param>
    /// <param name="create">Makes the activity, a new one for each start, from the host's services.</param>
    public ActivityDefinition(string id, string name, Func<IServiceProvider, BotActivity> create)
        : this(id, LocalizedText.Invariant(name), create)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ActivityDefinition"/> class, for an activity with no setting.</summary>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it, in its language.</param>
    /// <param name="create">Makes the activity, a new one for each start, from the host's services.</param>
    public ActivityDefinition(string id, LocalizedText name, Func<IServiceProvider, BotActivity> create)
        : this(id, name, ActivitySettings.None, Created(create))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ActivityDefinition"/> class.</summary>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it, in its language.</param>
    /// <param name="settings">The settings the activity declares.</param>
    /// <param name="create">Makes the activity, a new one for each start, from the host's services and its settings.</param>
    public ActivityDefinition(string id, LocalizedText name, ActivitySettings settings, Func<IServiceProvider, object?, BotActivity> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(create);

        Id = id;
        Name = name;
        Settings = settings;
        _create = create;
    }

    private static Func<IServiceProvider, object?, BotActivity> Created(Func<IServiceProvider, BotActivity> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        return (services, _) => create(services);
    }

    /// <summary>Makes the activity with its default settings, to start it on a bot.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>A new activity: one activity runs on one bot, once.</returns>
    public BotActivity Create(IServiceProvider services)
    {
        return Create(services, Settings.CreateDefault());
    }

    /// <summary>Makes the activity with settings, to start it on a bot.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="settings">The activity's settings, an instance of <see cref="ActivitySettings.Type"/>, or <see langword="null"/> when it has none.</param>
    /// <returns>A new activity: one activity runs on one bot, once.</returns>
    /// <exception cref="ArgumentException">The settings are not the activity's.</exception>
    public BotActivity Create(IServiceProvider services, object? settings)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (Settings.Type is { } type ? !type.IsInstanceOfType(settings) : settings is not null)
            throw new ArgumentException($"The settings of {Id} are a {Settings.Type?.Name ?? "nothing"}.", nameof(settings));

        return _create(services, settings);
    }
}
