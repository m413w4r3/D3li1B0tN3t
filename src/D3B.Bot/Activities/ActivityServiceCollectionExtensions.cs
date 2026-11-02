using D3B.Bot.Localization;
using D3B.Bot.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Activities;

/// <summary>Registers the activities a module brings, for the app to start (D92).</summary>
public static class ActivityServiceCollectionExtensions
{
    /// <summary>Registers an activity in the host's <see cref="ActivityCatalog"/>, its name the same in every language.</summary>
    /// <typeparam name="TActivity">The activity, made anew for each start with its constructor's services.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it.</param>
    /// <returns>The services.</returns>
    /// <exception cref="InvalidOperationException">An activity with the id is already registered.</exception>
    public static IServiceCollection AddActivity<TActivity>(this IServiceCollection services, string id, string name)
        where TActivity : BotActivity
    {
        return services.AddActivity<TActivity>(id, LocalizedText.Invariant(name));
    }

    /// <summary>Registers an activity in the host's <see cref="ActivityCatalog"/>.</summary>
    /// <typeparam name="TActivity">The activity, made anew for each start with its constructor's services.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it, in its language (D96).</param>
    /// <returns>The services.</returns>
    /// <exception cref="InvalidOperationException">An activity with the id is already registered.</exception>
    public static IServiceCollection AddActivity<TActivity>(this IServiceCollection services, string id, LocalizedText name)
        where TActivity : BotActivity
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddDefinition(new ActivityDefinition(id, name, provider => ActivatorUtilities.CreateInstance<TActivity>(provider)));
    }

    /// <summary>Registers an activity with settings in the host's <see cref="ActivityCatalog"/> (D98).</summary>
    /// <typeparam name="TActivity">The activity, made anew for each start with its constructor's services and its settings.</typeparam>
    /// <typeparam name="TSettings">The activity's settings, which its constructor takes, their initial values the defaults.</typeparam>
    /// <param name="services">The host's services.</param>
    /// <param name="id">The activity's id, unique in the host.</param>
    /// <param name="name">The activity's name, as the app shows it, in its language (D96).</param>
    /// <param name="describe">Declares each setting, as <c>settings.Number(s =&gt; s.Walks, name, 1, 100)</c>.</param>
    /// <returns>The services.</returns>
    /// <exception cref="InvalidOperationException">An activity with the id is already registered, or the default settings do not hold.</exception>
    /// <exception cref="ArgumentException">A setting is not a public property of the settings, or is declared twice.</exception>
    public static IServiceCollection AddActivity<TActivity, TSettings>(this IServiceCollection services, string id, LocalizedText name, Action<ActivitySettingsBuilder<TSettings>> describe)
        where TActivity : BotActivity
        where TSettings : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(describe);

        var builder = new ActivitySettingsBuilder<TSettings>();

        describe(builder);

        // The settings reach the activity's constructor, beside the services it asks for.
        return services.AddDefinition(new ActivityDefinition(id, name, builder.Build(), (provider, settings) => ActivatorUtilities.CreateInstance<TActivity>(provider, settings!)));
    }

    private static IServiceCollection AddDefinition(this IServiceCollection services, ActivityDefinition definition)
    {
        // While the modules build, so that a module reusing an id stops the host from starting (D93).
        if (services.Any(descriptor => descriptor.ImplementationInstance is ActivityDefinition registered && registered.Id == definition.Id))
            throw new InvalidOperationException($"An activity with the id {definition.Id} is already registered.");

        services.AddSingleton(definition);

        return services;
    }
}
