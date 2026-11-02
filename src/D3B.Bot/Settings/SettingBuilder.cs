using System.Linq.Expressions;
using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>Refines a setting an activity declares: its help, whether it needs a value, its order, when it shows, and what it is sought within.</summary>
/// <typeparam name="TSettings">The activity's settings.</typeparam>
public sealed class SettingBuilder<TSettings>
    where TSettings : class, new()
{
    private readonly ActivitySettingsBuilder<TSettings> _owner;
    private readonly SettingDefinition _setting;

    internal SettingBuilder(ActivitySettingsBuilder<TSettings> owner, SettingDefinition setting)
    {
        _owner = owner;
        _setting = setting;
    }

    /// <summary>Says something of the setting, below it in the app.</summary>
    /// <param name="help">What the app says.</param>
    /// <returns>This builder.</returns>
    public SettingBuilder<TSettings> Help(LocalizedText help)
    {
        ArgumentNullException.ThrowIfNull(help);

        _setting.SetHelp(help);

        return this;
    }

    /// <summary>Asks for a value: a text not blank, a value of the game chosen, a list not empty.</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting is a flag or a number, which always has a value.</exception>
    public SettingBuilder<TSettings> Required()
    {
        _setting.SetRequired();

        return this;
    }

    /// <summary>Makes a list's order matter, such as a priority: the app lets it be changed.</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting holds one value.</exception>
    public SettingBuilder<TSettings> Ordered()
    {
        _setting.SetOrdered();

        return this;
    }

    /// <summary>Shows the setting only when a condition holds of the settings; hidden, it is not checked.</summary>
    /// <param name="condition">Tells whether it shows, such as <c>s =&gt; s.Focus == Focus.Job</c>; it must be quick.</param>
    /// <returns>This builder.</returns>
    public SettingBuilder<TSettings> VisibleWhen(Func<TSettings, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _setting.SetVisibleWhen(settings => condition((TSettings)settings));

        return this;
    }

    /// <summary>Seeks the setting's values within another setting's: sub-areas within an area, resources within a profession.</summary>
    /// <param name="parent">The setting declared before, of one area or one profession, such as <c>s =&gt; s.Area</c>.</param>
    /// <returns>This builder.</returns>
    /// <remarks>
    /// The app offers the values within the parent's once it has one, and every one until then; a value not within it
    /// does not hold (<see cref="SettingProblemKind.NotWithin"/>).
    /// </remarks>
    /// <exception cref="InvalidOperationException">The parent is not declared before, or not of the kind the setting is sought within.</exception>
    public SettingBuilder<TSettings> Within(Expression<Func<TSettings, int?>> parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        _setting.SetWithin(_owner.Find(parent));

        return this;
    }

    /// <summary>Offers first, for a sub-area setting, the sub-areas where resources of another setting grow, or its monsters live (D127, D245).</summary>
    /// <param name="values">The resources' or monsters' setting, declared before, such as <c>s =&gt; s.Resources</c>.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting holds no sub-area, or the other is not declared before, or holds neither resources nor monsters.</exception>
    public SettingBuilder<TSettings> SuggestedBy(Expression<Func<TSettings, IReadOnlyList<int>>> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        _setting.SetSuggestedBy(_owner.Find(values));

        return this;
    }

    /// <summary>Offers first, for a sub-area setting, the sub-areas where resources the activity reckons from its settings grow, such as a profession's (D165).</summary>
    /// <param name="resources">Reckons the resources from the settings and the game's values; it must be quick.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting holds no sub-area.</exception>
    public SettingBuilder<TSettings> SuggestedBy(Func<TSettings, GameReferences, IReadOnlyList<int>> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        _setting.SetSuggesting((settings, game) => resources((TSettings)settings, game), SettingKind.Resource);

        return this;
    }

    /// <summary>Offers first, for a sub-area setting, the sub-areas where monsters the activity reckons from its settings live, such as those of a family chosen (D247).</summary>
    /// <param name="monsters">Reckons the monsters from the settings and the game's values; it must be quick.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting holds no sub-area.</exception>
    public SettingBuilder<TSettings> SuggestedByMonsters(Func<TSettings, GameReferences, IReadOnlyList<int>> monsters)
    {
        ArgumentNullException.ThrowIfNull(monsters);

        _setting.SetSuggesting((settings, game) => monsters((TSettings)settings, game), SettingKind.Monster);

        return this;
    }

    /// <summary>Offers, for a profession setting, the professions whose skills gather a resource alone, <c>Base</c> among them, which the app names "without a profession" (D165).</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting holds no profession.</exception>
    public SettingBuilder<TSettings> Gathering()
    {
        _setting.SetGathering();

        return this;
    }

    /// <summary>Makes a list of resources those to leave out: the app offers to exclude rather than add, and warns of no level lacking (D165).</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting is no list of resources or monsters.</exception>
    public SettingBuilder<TSettings> Excluding()
    {
        _setting.SetExcluding();

        return this;
    }

    /// <summary>Says what a number counts, which the app shows after it, such as "maps" (D167).</summary>
    /// <param name="unit">What it counts.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting is no number.</exception>
    public SettingBuilder<TSettings> Unit(LocalizedText unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        _setting.SetUnit(unit);

        return this;
    }

    /// <summary>Makes a number a percentage, which the app shows as a bar (D127).</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting is not a number between 0 and 100.</exception>
    public SettingBuilder<TSettings> Percent()
    {
        _setting.SetPercent();

        return this;
    }

    /// <summary>Restricts an item setting to some types of items, such as food's (D148).</summary>
    /// <param name="types">The item types, by id in the client's tables.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The setting holds no item.</exception>
    public SettingBuilder<TSettings> OfItemTypes(params int[] types)
    {
        ArgumentNullException.ThrowIfNull(types);

        _setting.SetItemTypes(types);

        return this;
    }
}
