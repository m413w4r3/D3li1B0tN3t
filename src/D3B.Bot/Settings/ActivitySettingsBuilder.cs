using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;
using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>Describes an activity's settings, in code, as its module registers it (D98).</summary>
/// <typeparam name="TSettings">
/// The activity's settings: a class with a constructor of no parameter, whose public properties the module declares,
/// their initial values the defaults.
/// </typeparam>
/// <remarks>
/// Each method declares one property of the settings, by an expression such as <c>s =&gt; s.Walks</c>, its type that of
/// the setting: a <see cref="bool"/> flag, an <see cref="int"/> number, a <see cref="string"/> text, an enumeration's
/// choice or a list of them, and the game's values by their ids, an <see cref="int"/>? for one and a list of
/// <see cref="int"/>s for several. Settings show in the order they are declared, under the last section begun.
/// </remarks>
public sealed class ActivitySettingsBuilder<TSettings>
    where TSettings : class, new()
{
    private readonly List<SettingDefinition> _settings = [];
    private readonly List<SettingRule> _rules = [];
    private readonly List<(string Key, LocalizedText Name, Action<TSettings> Fill)> _presets = [];
    private LocalizedText? _section;
    private Func<object, IServiceProvider, IReadOnlyList<PreviewPlayer>, SettingsPreview?>? _preview;
    private Action<object, JsonObject>? _earlier;

    internal ActivitySettingsBuilder()
    {
    }

    private static PropertyInfo PropertyOf(LambdaExpression property)
    {
        if (property.Body is MemberExpression { Member: PropertyInfo info, Expression: ParameterExpression }
            && info.GetMethod is { IsPublic: true, IsStatic: false }
            && info.SetMethod is { IsPublic: true })
        {
            return info;
        }

        throw new ArgumentException("A setting is a public property of the settings with a getter and a setter, named as s => s.Walks.", nameof(property));
    }

    private static string Wrong(IEnumerable<SettingProblem> problems)
    {
        return string.Join(", ", problems.Select(problem => $"{problem.Setting?.Key ?? "a rule"} ({problem.Kind})"));
    }

    /// <summary>Begins a section: the settings declared next show under its title.</summary>
    /// <param name="title">The section's title, as the app shows it.</param>
    /// <returns>This builder.</returns>
    public ActivitySettingsBuilder<TSettings> Section(LocalizedText title)
    {
        ArgumentNullException.ThrowIfNull(title);

        _section = title;

        return this;
    }

    /// <summary>Declares a flag, on or off.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.UseBank</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Flag(Expression<Func<TSettings, bool>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Flag, false, name);
    }

    /// <summary>Declares a whole number between two bounds.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Walks</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <param name="minimum">The lowest value.</param>
    /// <param name="maximum">The highest value.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Number(Expression<Func<TSettings, int>> property, LocalizedText name, int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);

        return Add(property, SettingKind.Number, false, name, minimum, maximum);
    }

    /// <summary>Declares a text of bounded length.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Greeting</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <param name="maximumLength">The text's greatest length.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Text(Expression<Func<TSettings, string?>> property, LocalizedText name, int maximumLength = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);

        return Add(property, SettingKind.Text, false, name, maximumLength: maximumLength);
    }

    /// <summary>Declares a choice of one of an enumeration's values.</summary>
    /// <typeparam name="TEnum">The enumeration, whose values the app offers in their order.</typeparam>
    /// <param name="property">The property, such as <c>s =&gt; s.Focus</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <param name="option">Names each value, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Choice<TEnum>(Expression<Func<TSettings, TEnum>> property, LocalizedText name, Func<TEnum, LocalizedText> option)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(option);

        return Add(property, SettingKind.Choice, false, name, enumType: typeof(TEnum), option: value => option((TEnum)value));
    }

    /// <summary>Declares a choice of several of an enumeration's values, each once.</summary>
    /// <typeparam name="TEnum">The enumeration, whose values the app offers in their order.</typeparam>
    /// <param name="property">The property, such as <c>s =&gt; s.Banks</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <param name="option">Names each value, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Choices<TEnum>(Expression<Func<TSettings, IReadOnlyList<TEnum>>> property, LocalizedText name, Func<TEnum, LocalizedText> option)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(option);

        return Add(property, SettingKind.Choice, true, name, enumType: typeof(TEnum), option: value => option((TEnum)value));
    }

    /// <summary>Declares a profession of the game, or none.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Job</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Job(Expression<Func<TSettings, int?>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Job, false, name);
    }

    /// <summary>Declares several professions of the game, each once.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Jobs</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Jobs(Expression<Func<TSettings, IReadOnlyList<int>>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Job, true, name);
    }

    /// <summary>Declares an area of the world, or none.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Area</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Area(Expression<Func<TSettings, int?>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Area, false, name);
    }

    /// <summary>Declares several areas of the world, each once.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Areas</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Areas(Expression<Func<TSettings, IReadOnlyList<int>>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Area, true, name);
    }

    /// <summary>Declares a sub-area of the world, or none, which may be sought within an area (<see cref="SettingBuilder{TSettings}.Within"/>).</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.SubArea</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> SubArea(Expression<Func<TSettings, int?>> property, LocalizedText name)
    {
        return Add(property, SettingKind.SubArea, false, name);
    }

    /// <summary>Declares several sub-areas of the world, each once, which may be sought within an area.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.SubAreas</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> SubAreas(Expression<Func<TSettings, IReadOnlyList<int>>> property, LocalizedText name)
    {
        return Add(property, SettingKind.SubArea, true, name);
    }

    /// <summary>Declares a resource a profession gathers, by its item's id, or none, which may be sought within a profession.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Resource</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Resource(Expression<Func<TSettings, int?>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Resource, false, name);
    }

    /// <summary>Declares several resources professions gather, by their items' ids, each once, which may be sought within a profession.</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Resources</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Resources(Expression<Func<TSettings, IReadOnlyList<int>>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Resource, true, name);
    }

    /// <summary>Declares a setting of several items of the game, such as food (D148).</summary>
    /// <param name="property">The settings' property.</param>
    /// <param name="name">The setting's name.</param>
    /// <returns>The setting, to say more of it, such as the item types it offers alone.</returns>
    public SettingBuilder<TSettings> Items(Expression<Func<TSettings, IReadOnlyList<int>>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Item, true, name);
    }

    /// <summary>Declares several monsters, each once (D236).</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Monsters</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> Monsters(Expression<Func<TSettings, IReadOnlyList<int>>> property, LocalizedText name)
    {
        return Add(property, SettingKind.Monster, true, name);
    }

    /// <summary>Declares several families of monsters, each once (D247).</summary>
    /// <param name="property">The property, such as <c>s =&gt; s.Families</c>.</param>
    /// <param name="name">The setting's name, as the app shows it.</param>
    /// <returns>The setting's builder.</returns>
    public SettingBuilder<TSettings> MonsterFamilies(Expression<Func<TSettings, IReadOnlyList<int>>> property, LocalizedText name)
    {
        return Add(property, SettingKind.MonsterFamily, true, name);
    }

    /// <summary>Declares a rule the settings hold together, such as a minimum below a maximum.</summary>
    /// <param name="holds">Tells whether the settings hold it; it must be quick.</param>
    /// <param name="message">What the app says when they do not.</param>
    /// <returns>This builder.</returns>
    public ActivitySettingsBuilder<TSettings> Rule(Func<TSettings, bool> holds, LocalizedText message)
    {
        ArgumentNullException.ThrowIfNull(holds);
        ArgumentNullException.ThrowIfNull(message);

        _rules.Add(new SettingRule(settings => holds((TSettings)settings), message));

        return this;
    }

    /// <summary>Says what the settings lead to, reckoned from the pack, which the app shows beside them (D127).</summary>
    /// <param name="preview">Reckons it from the settings and the host's services, or gives nothing while the settings say too little.</param>
    /// <returns>This builder.</returns>
    public ActivitySettingsBuilder<TSettings> Preview(Func<TSettings, IServiceProvider, SettingsPreview?> preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        _preview = (settings, services, _) => preview((TSettings)settings, services);

        return this;
    }

    /// <summary>Says what the settings lead to for each character that plays them, reckoned from the pack, which the app shows beside them (D166).</summary>
    /// <param name="preview">Reckons it from the settings, the host's services and the characters, as their bots last saw them, or gives nothing while the settings say too little.</param>
    /// <returns>This builder.</returns>
    public ActivitySettingsBuilder<TSettings> Preview(Func<TSettings, IServiceProvider, IReadOnlyList<PreviewPlayer>, SettingsPreview?> preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        _preview = (settings, services, players) => preview((TSettings)settings, services, players);

        return this;
    }

    /// <summary>Reads what an earlier version of the settings stored, once the settings declared now are read: keys no longer declared, or settings declared since (D165).</summary>
    /// <param name="read">Sets the settings from what was stored, a value per key.</param>
    /// <returns>This builder.</returns>
    /// <remarks>Without it, a key no longer declared is dropped, and a setting declared since keeps its default (D98).</remarks>
    public ActivitySettingsBuilder<TSettings> ReadsEarlier(Action<TSettings, JsonObject> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        _earlier = (settings, stored) => read((TSettings)settings, stored);

        return this;
    }

    /// <summary>Gives settings ready to play, which D3B stores once as a profile, such as an automatic harvest (D168).</summary>
    /// <param name="key">What names the preset among the activity's, never shown and never changed, so that it is stored once.</param>
    /// <param name="name">The profile's name, as the app shows it.</param>
    /// <param name="fill">Sets the settings from the defaults.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The key is blank, or another preset's.</exception>
    public ActivitySettingsBuilder<TSettings> Preset(string key, LocalizedText name, Action<TSettings> fill)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(fill);

        if (_presets.Exists(preset => preset.Key == key))
            throw new ArgumentException($"The preset {key} is declared twice.", nameof(key));

        _presets.Add((key, name, fill));

        return this;
    }

    internal SettingDefinition Find(LambdaExpression property)
    {
        var info = PropertyOf(property);

        return _settings.Find(setting => setting.Key == info.Name)
            ?? throw new InvalidOperationException($"The setting {info.Name} is not declared before the one sought within it.");
    }

    /// <summary>Ends the description.</summary>
    /// <returns>The settings, declared once and for all.</returns>
    /// <remarks>
    /// A value the user must give, such as a profession, has no default, and a rule may wait for it: the defaults are
    /// checked for what the module itself wrote wrong, a number out of its bounds, a text too long, a value repeated. A
    /// preset is played as it is, so it must hold whole, rules included.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The defaults are out of the settings' own bounds, or a preset does not hold.</exception>
    internal ActivitySettings Build()
    {
        foreach (var setting in _settings)
            setting.Freeze();

        var settings = new ActivitySettings(typeof(TSettings), static () => new TSettings(), [.. _settings], [.. _rules], _preview, _earlier);
        var problems = settings.Check(settings.CreateDefault())
            .Where(problem => problem.Kind is not (SettingProblemKind.Required or SettingProblemKind.Rule))
            .ToList();

        if (problems.Count > 0)
            throw new InvalidOperationException($"The default settings of {typeof(TSettings).Name} do not hold: {Wrong(problems)}.");

        var presets = new List<SettingsPreset>();

        foreach (var (key, name, fill) in _presets)
        {
            var preset = new TSettings();

            fill(preset);

            if (settings.Check(preset) is { Count: > 0 } wrong)
                throw new InvalidOperationException($"The preset {key} of {typeof(TSettings).Name} does not hold: {Wrong(wrong)}.");

            presets.Add(new SettingsPreset(key, name, settings.Write(preset)));
        }

        settings.Presets = presets;

        return settings;
    }

    private SettingBuilder<TSettings> Add(
        LambdaExpression property,
        SettingKind kind,
        bool isList,
        LocalizedText name,
        int? minimum = null,
        int? maximum = null,
        int? maximumLength = null,
        Type? enumType = null,
        Func<object, LocalizedText>? option = null)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(name);

        var info = PropertyOf(property);

        if (_settings.Exists(setting => setting.Key == info.Name))
            throw new ArgumentException($"The setting {info.Name} is declared twice.", nameof(property));

        var setting = new SettingDefinition(info, kind, isList, name, _section, minimum, maximum, maximumLength, enumType, option);

        _settings.Add(setting);

        return new SettingBuilder<TSettings>(this, setting);
    }
}
