using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using D3B.Bot.Localization;

namespace D3B.Bot.Settings;

/// <summary>A setting an activity declares: a property of its settings, what it holds, its name and its bounds (D98).</summary>
/// <remarks>
/// Made by <see cref="ActivitySettingsBuilder{TSettings}"/> as a module registers its activity. The app edits the
/// settings through it, a value of the property's own type at a time, and <see cref="ActivitySettings"/> stores and checks
/// them.
/// </remarks>
public sealed class SettingDefinition
{
    private readonly PropertyInfo _property;
    private readonly Type? _enum;
    private Func<object, bool>? _visibleWhen;
    private Func<object, GameReferences, IReadOnlyList<int>>? _suggesting;
    private SettingKind _suggestingKind = SettingKind.Resource;
    private bool _frozen;

    /// <summary>Gets the setting's key: its property's name, under which its value is stored.</summary>
    public string Key => _property.Name;

    /// <summary>Gets what the setting holds.</summary>
    public SettingKind Kind { get; }

    /// <summary>Gets whether the setting holds several values: a list, each value once.</summary>
    public bool IsList { get; }

    /// <summary>Gets whether a list's order matters, such as a priority: the app lets it be changed.</summary>
    public bool IsOrdered { get; private set; }

    /// <summary>Gets whether the setting needs a value: a text not blank, a value of the game chosen, a list not empty.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>Gets whether the setting shows only when a condition of the activity's holds.</summary>
    public bool IsConditional => _visibleWhen is not null;

    /// <summary>Gets the setting's name, as the app shows it.</summary>
    public LocalizedText Name { get; }

    /// <summary>Gets what the app says of the setting below it, if anything.</summary>
    public LocalizedText? Help { get; private set; }

    /// <summary>Gets the title of the section the setting belongs to, if the activity groups its settings.</summary>
    public LocalizedText? Section { get; }

    /// <summary>Gets a number's lowest value.</summary>
    public int? Minimum { get; }

    /// <summary>Gets a number's highest value.</summary>
    public int? Maximum { get; }

    /// <summary>Gets a text's greatest length.</summary>
    public int? MaximumLength { get; }

    /// <summary>Gets the values a choice offers, in the enumeration's order; empty for every other kind.</summary>
    public IReadOnlyList<SettingOption> Options { get; }

    /// <summary>Gets the setting a value of the game is sought within, such as the area of a sub-area, or the profession of a resource.</summary>
    public SettingDefinition? Within { get; private set; }

    /// <summary>Gets the resources or monsters whose sub-areas a sub-area setting offers first, those where they grow or live (D127, D245).</summary>
    public SettingDefinition? SuggestedBy { get; private set; }

    /// <summary>Gets whether a sub-area setting offers first the sub-areas where some resources grow or some monsters live: those of a resource or monster setting, or those the activity reckons from its settings (D127, D165, D245, D247).</summary>
    public bool IsSuggested => SuggestedBy is not null || _suggesting is not null;

    /// <summary>Gets what a sub-area setting's <see cref="Suggestions"/> are: resources, or monsters (D245).</summary>
    public SettingKind SuggestedKind => SuggestedBy?.Kind ?? _suggestingKind;

    /// <summary>Gets whether a profession setting offers the professions whose skills gather a resource alone, <c>Base</c> among them (D165).</summary>
    public bool IsGathering { get; private set; }

    /// <summary>Gets what a number counts, which the app shows after it, such as "maps" (D167).</summary>
    public LocalizedText? Unit { get; private set; }

    /// <summary>Gets whether a list of resources holds those to leave out (D165).</summary>
    public bool IsExcluding { get; private set; }

    /// <summary>Gets whether a number is a percentage, which the app shows as a bar (D127).</summary>
    public bool IsPercent { get; private set; }

    /// <summary>Gets the item types an item setting offers alone, such as food's (D148); every item when none.</summary>
    public IReadOnlySet<int>? ItemTypes { get; private set; }

    private Type ElementType => Kind switch
    {
        SettingKind.Choice => _enum!,
        _ => typeof(int),
    };

    internal SettingDefinition(
        PropertyInfo property,
        SettingKind kind,
        bool isList,
        LocalizedText name,
        LocalizedText? section,
        int? minimum = null,
        int? maximum = null,
        int? maximumLength = null,
        Type? enumType = null,
        Func<object, LocalizedText>? option = null)
    {
        _property = property;
        _enum = enumType;
        Kind = kind;
        IsList = isList;
        Name = name;
        Section = section;
        Minimum = minimum;
        Maximum = maximum;
        MaximumLength = maximumLength;
        Options = enumType is null || option is null ? [] : [.. Enum.GetValues(enumType).Cast<object>().Select(value => new SettingOption(value, option(value)))];
    }

    private static List<object> Distinct(IEnumerable values)
    {
        var distinct = new List<object>();

        foreach (var value in values)
        {
            if (value is not null && !distinct.Contains(value))
                distinct.Add(value);
        }

        return distinct;
    }

    private static bool HasRepeats(IReadOnlyList<object> values)
    {
        return values.Distinct().Count() != values.Count;
    }

    /// <summary>Gets the setting's value in settings.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <returns>
    /// The property's value: a <see cref="bool"/>, an <see cref="int"/>, a <see cref="string"/>, a value of the choice's
    /// enumeration, an id of the game's or <see langword="null"/>; for a list, its values, never <see langword="null"/>.
    /// </returns>
    public object? GetValue(object settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var value = _property.GetValue(settings);

        if (IsList && value is null)
            return Array.CreateInstance(ElementType, 0);

        return Kind is SettingKind.Text ? value ?? "" : value;
    }

    /// <summary>Gives the setting a value in settings.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="value">
    /// A value of the property's own type; for a list, any sequence of its values, each kept once, in order.
    /// </param>
    /// <exception cref="ArgumentException">The value is not one the setting holds.</exception>
    public void SetValue(object settings, object? value)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _property.SetValue(settings, Normalize(value));
    }

    /// <summary>Gets the values of a list setting, boxed, in order.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <returns>The values.</returns>
    public IReadOnlyList<object> GetValues(object settings)
    {
        return GetValue(settings) is IEnumerable values ? [.. values.Cast<object>()] : [];
    }

    /// <summary>Tells whether the setting shows for settings: always, unless the activity said when.</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <returns>Whether it shows; a condition that throws shows it.</returns>
    public bool IsVisible(object settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_visibleWhen is not { } condition)
            return true;

        try
        {
            return condition(settings);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // An activity's condition runs in the app, which holds every client's connection (D91): it must not throw there.
            return true;
        }
    }

    internal void SetHelp(LocalizedText help)
    {
        ThrowIfFrozen();
        Help = help;
    }

    internal void SetRequired()
    {
        ThrowIfFrozen();

        if (Kind is SettingKind.Flag or SettingKind.Number)
            throw new InvalidOperationException($"The setting {Key} always has a value: it cannot be required.");

        IsRequired = true;
    }

    internal void SetOrdered()
    {
        ThrowIfFrozen();

        if (!IsList)
            throw new InvalidOperationException($"The setting {Key} holds one value: only a list is ordered.");

        IsOrdered = true;
    }

    internal void SetVisibleWhen(Func<object, bool> condition)
    {
        ThrowIfFrozen();
        _visibleWhen = condition;
    }

    internal void SetWithin(SettingDefinition parent)
    {
        ThrowIfFrozen();

        var expected = Kind switch
        {
            SettingKind.SubArea => SettingKind.Area,
            SettingKind.Resource => SettingKind.Job,
            _ => throw new InvalidOperationException($"The setting {Key} is sought within nothing: only sub-areas are, within an area, and resources, within a profession."),
        };

        if (parent.Kind != expected || parent.IsList)
            throw new InvalidOperationException($"The setting {Key} is sought within one {expected}, which {parent.Key} is not.");

        Within = parent;
    }

    internal void SetSuggestedBy(SettingDefinition values)
    {
        ThrowIfFrozen();

        if (Kind is not SettingKind.SubArea)
            throw new InvalidOperationException($"The setting {Key} is suggested by nothing: only sub-areas are, by resources or monsters.");

        if (values.Kind is not (SettingKind.Resource or SettingKind.Monster))
            throw new InvalidOperationException($"The setting {Key} is suggested by resources or monsters, which {values.Key} is not.");

        SuggestedBy = values;
    }

    internal void SetSuggesting(Func<object, GameReferences, IReadOnlyList<int>> values, SettingKind kind)
    {
        ThrowIfFrozen();

        if (Kind is not SettingKind.SubArea)
            throw new InvalidOperationException($"The setting {Key} is suggested by nothing: only sub-areas are, by resources or monsters.");

        _suggesting = values;
        _suggestingKind = kind;
    }

    internal void SetUnit(LocalizedText unit)
    {
        ThrowIfFrozen();

        if (Kind is not SettingKind.Number)
            throw new InvalidOperationException($"The setting {Key} is no number: only a number counts something.");

        Unit = unit;
    }

    internal void SetExcluding()
    {
        ThrowIfFrozen();

        if (Kind is not (SettingKind.Resource or SettingKind.Monster) || !IsList)
            throw new InvalidOperationException($"The setting {Key} is no list of resources or monsters: only one leaves them out.");

        IsExcluding = true;
    }

    internal void SetGathering()
    {
        ThrowIfFrozen();

        if (Kind is not SettingKind.Job)
            throw new InvalidOperationException($"The setting {Key} holds no profession: only a profession setting offers those that gather.");

        IsGathering = true;
    }

    /// <summary>Gets the resources or monsters whose sub-areas a sub-area setting offers first (<see cref="IsSuggested"/>, <see cref="SuggestedKind"/>).</summary>
    /// <param name="settings">The activity's settings.</param>
    /// <param name="game">The game's values.</param>
    /// <returns>
    /// Those the activity reckons, or the resource or monster setting's while it shows; while it is hidden, every resource
    /// the game has, as when the activity harvests by profession, and no monster, as when the activity fights the groups at
    /// the character's level; none when the setting is suggested by nothing, or the reckoning throws.
    /// </returns>
    public IReadOnlyList<int> Suggestions(object settings, GameReferences game)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(game);

        if (_suggesting is { } suggesting)
        {
            try
            {
                return suggesting(settings, game);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // An activity's reckoning runs in the app, which holds every client's connection (D91): it must not throw there.
                return [];
            }
        }

        if (SuggestedBy is not { } by)
            return [];

        if (by.IsVisible(settings))
            return [.. by.GetValues(settings).Cast<int>()];

        return by.Kind is SettingKind.Monster ? [] : [.. game.Options(SettingKind.Resource).Select(resource => resource.Id)];
    }

    internal void SetPercent()
    {
        ThrowIfFrozen();

        if (Kind is not SettingKind.Number || Minimum < 0 || Maximum > 100)
            throw new InvalidOperationException($"The setting {Key} is no percentage: only a number between 0 and 100 is.");

        IsPercent = true;
    }

    internal void SetItemTypes(IReadOnlyCollection<int> types)
    {
        ThrowIfFrozen();

        if (Kind is not SettingKind.Item)
            throw new InvalidOperationException($"The setting {Key} holds no item: only an item setting is restricted to types.");

        ItemTypes = types.ToHashSet();
    }

    internal void Freeze()
    {
        _frozen = true;
    }

    internal JsonNode? ToJson(object settings)
    {
        var value = GetValue(settings);

        return Kind switch
        {
            SettingKind.Flag => JsonValue.Create((bool)value!),
            SettingKind.Number => JsonValue.Create((int)value!),
            SettingKind.Text => JsonValue.Create((string)value!),
            SettingKind.Choice when IsList => new JsonArray([.. GetValues(settings).Select(item => (JsonNode?)JsonValue.Create(NameOf(item)))]),
            SettingKind.Choice => JsonValue.Create(NameOf(value!)),
            _ when IsList => new JsonArray([.. GetValues(settings).Select(item => (JsonNode?)JsonValue.Create((int)item))]),
            _ => value is int id ? JsonValue.Create(id) : null,
        };
    }

    internal bool TryRead(object settings, JsonNode? node)
    {
        var read = IsList ? ReadList(node) : ReadOne(node);

        if (read.Value is not { } value)
            return read.Whole;

        SetValue(settings, value.Value);

        return read.Whole;
    }

    internal SettingProblem? Check(object settings, GameReferences? game)
    {
        var value = GetValue(settings);

        switch (Kind)
        {
            case SettingKind.Flag:
                return null;

            case SettingKind.Number:
                return (int)value! < Minimum || (int)value > Maximum ? Problem(SettingProblemKind.OutOfRange) : null;

            case SettingKind.Text:
                {
                    var text = (string)value!;

                    if (IsRequired && string.IsNullOrWhiteSpace(text))
                        return Problem(SettingProblemKind.Required);

                    return text.Length > MaximumLength ? Problem(SettingProblemKind.TooLong) : null;
                }

            case SettingKind.Choice:
                {
                    IReadOnlyList<object> choices = IsList ? GetValues(settings) : [value!];

                    if (IsRequired && choices.Count == 0)
                        return Problem(SettingProblemKind.Required);

                    if (choices.Any(choice => !Enum.IsDefined(_enum!, choice)))
                        return Problem(SettingProblemKind.Unknown);

                    return HasRepeats(choices) ? Problem(SettingProblemKind.Repeated) : null;
                }

            default:
                return CheckGame(settings, value, game);
        }
    }

    private SettingProblem? CheckGame(object settings, object? value, GameReferences? game)
    {
        IReadOnlyList<object> ids = IsList ? GetValues(settings) : value is int id ? [id] : [];

        if (IsRequired && ids.Count == 0)
            return Problem(SettingProblemKind.Required);

        if (HasRepeats(ids))
            return Problem(SettingProblemKind.Repeated);

        // Without the pack, such as while a module registers its activity, the game's values are not known.
        if (game is null)
            return null;

        if (ids.Any(item => !(IsGathering ? game.IsGathering((int)item) : game.Exists(Kind, (int)item)) || (ItemTypes is { } types && !game.IsItemOf((int)item, types))))
            return Problem(SettingProblemKind.Unknown);

        if (Within?.GetValue(settings) is int within && ids.Any(item => !game.IsWithin(Kind, (int)item, within)))
            return Problem(SettingProblemKind.NotWithin);

        return null;
    }

    private SettingProblem Problem(SettingProblemKind kind)
    {
        return new SettingProblem(this, kind);
    }

    private object? Normalize(object? value)
    {
        if (IsList)
        {
            if (value is not IEnumerable values || value is string)
                throw new ArgumentException($"The setting {Key} holds a list.", nameof(value));

            var distinct = Distinct(values);
            var list = Array.CreateInstance(ElementType, distinct.Count);

            for (var index = 0; index < distinct.Count; index++)
            {
                if (!ElementType.IsInstanceOfType(distinct[index]))
                    throw new ArgumentException($"The setting {Key} holds values of {ElementType.Name}.", nameof(value));

                list.SetValue(distinct[index], index);
            }

            return list;
        }

        return (Kind, value) switch
        {
            (SettingKind.Flag, bool) => value,
            (SettingKind.Number, int) => value,
            (SettingKind.Text, null) => "",
            (SettingKind.Text, string) => value,
            (SettingKind.Choice, not null) when _enum!.IsInstanceOfType(value) => value,
            (SettingKind.Job or SettingKind.Area or SettingKind.SubArea or SettingKind.Resource or SettingKind.Item or SettingKind.Monster or SettingKind.MonsterFamily, null or int) => value,
            _ => throw new ArgumentException($"The setting {Key} does not hold {value ?? "nothing"}.", nameof(value)),
        };
    }

    private string NameOf(object choice)
    {
        return Enum.GetName(_enum!, choice) ?? choice.ToString()!;
    }

    private (StrongBox<object?>? Value, bool Whole) ReadOne(JsonNode? node)
    {
        switch (Kind)
        {
            case SettingKind.Flag when node is JsonValue flag && flag.TryGetValue<bool>(out var on):
                return (new(on), true);

            case SettingKind.Number when node is JsonValue number && number.TryGetValue<int>(out var whole):
                return (new(whole), true);

            case SettingKind.Text when node is JsonValue text && text.TryGetValue<string>(out var words):
                return (new(words), true);

            case SettingKind.Choice when ChoiceOf(node) is { } choice:
                return (new(choice), true);

            case SettingKind.Job or SettingKind.Area or SettingKind.SubArea or SettingKind.Resource or SettingKind.Item or SettingKind.Monster or SettingKind.MonsterFamily when node is null:
                return (new(null), true);

            case SettingKind.Job or SettingKind.Area or SettingKind.SubArea or SettingKind.Resource or SettingKind.Item or SettingKind.Monster or SettingKind.MonsterFamily when node is JsonValue id && id.TryGetValue<int>(out var game):
                return (new(game), true);

            default:
                return (null, false);
        }
    }

    private (StrongBox<object?>? Value, bool Whole) ReadList(JsonNode? node)
    {
        if (node is not JsonArray array)
            return (null, false);

        var values = new List<object>();

        foreach (var item in array)
        {
            object? value = Kind is SettingKind.Choice
                ? ChoiceOf(item)
                : item is JsonValue id && id.TryGetValue<int>(out var game) ? game : null;

            if (value is not null)
                values.Add(value);
        }

        // What still reads is kept: a value dropped is reported, as a whole setting would be.
        return (new(values), values.Count == array.Count);
    }

    private object? ChoiceOf(JsonNode? node)
    {
        // By name, so that an activity may reorder its enumeration: a name it dropped no longer reads.
        if (node is JsonValue value && value.TryGetValue<string>(out var name) && Enum.IsDefined(_enum!, name))
            return Enum.Parse(_enum!, name);

        return null;
    }

    private void ThrowIfFrozen()
    {
        if (_frozen)
            throw new InvalidOperationException("An activity's settings are declared while its module registers it.");
    }
}
