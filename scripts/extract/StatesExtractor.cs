using D3B.Data.Spells;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the states fighters may be in, and counts the states that spells name and the client lacks.</summary>
/// <remarks>A state keeps its name, its booleans as one set of flags, its effects' numbers and its spell level; its icon is left out.</remarks>
internal static class StatesExtractor
{
    private static readonly (string Field, SpellStateProperties Property)[] _properties =
    [
        ("preventsSpellCast", SpellStateProperties.PreventsSpellCast),
        ("preventsFight", SpellStateProperties.PreventsFight),
        ("isSilent", SpellStateProperties.IsSilent),
        ("cantBeMoved", SpellStateProperties.CantBeMoved),
        ("cantBePushed", SpellStateProperties.CantBePushed),
        ("cantDealDamage", SpellStateProperties.CantDealDamage),
        ("invulnerable", SpellStateProperties.Invulnerable),
        ("cantSwitchPosition", SpellStateProperties.CantSwitchPosition),
        ("incurable", SpellStateProperties.Incurable),
        ("invulnerableMelee", SpellStateProperties.InvulnerableMelee),
        ("invulnerableRange", SpellStateProperties.InvulnerableRange),
        ("cantTackle", SpellStateProperties.CantTackle),
        ("cantBeTackled", SpellStateProperties.CantBeTackled),
        ("displayTurnRemaining", SpellStateProperties.DisplayTurnRemaining),
        ("isMainState", SpellStateProperties.IsMainState),
    ];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [UnityDataTable.PathOf(client, "spellstates")];
    }

    public static SpellStateCatalog Read(ClientInstall client, SpellCatalog spells)
    {
        try
        {
            var states = new List<SpellState>();
            var effects = new List<int>();

            foreach (var record in UnityDataTable.ReadById(client, "spellstates").OrderBy(record => record.Int32("id")))
            {
                var properties = SpellStateProperties.None;

                foreach (var (field, property) in _properties)
                {
                    if (record.Boolean(field))
                        properties |= property;
                }

                var stateEffects = record.Items("effects").Select(effect => effect.Int32("id")).ToArray();

                states.Add(new SpellState(record.Int32("id"), record.Int32("nameId"), properties, effects.Count, checked((byte)stateEffects.Length), record.Int32("spellLevelId")));
                effects.AddRange(stateEffects);
            }

            var catalog = new SpellStateCatalog(states.ToArray(), effects.ToArray());
            var levels = states.Count(state => state.SpellLevelId is not 0 && !spells.TryGetLevel(state.SpellLevelId, out _));
            var named = Named(spells, catalog);

            Console.WriteLine($"Spell states: {states.Count:N0}, {effects.Count:N0} effects; naming a spell level the pack lacks: {levels:N0}; "
                + $"named by the spells' masks, criteria and effects: {named.Named:N0}, of which the client lacks {named.Missing:N0}");

            return catalog;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The states' table does not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static (int Named, int Missing) Named(SpellCatalog spells, SpellStateCatalog states)
    {
        var named = 0;
        var missing = 0;

        void Count(int state)
        {
            named++;

            if (!states.TryGetState(state, out _))
                missing++;
        }

        foreach (var level in spells.Levels.Span)
        {
            foreach (var state in Numbers(spells.StatesCriterionOf(level), "HS"))
                Count(state);
        }

        foreach (var effect in spells.Effects.Span)
        {
            foreach (var state in Numbers(spells.TargetMaskOf(effect), "E").Concat(Numbers(spells.TargetMaskOf(effect), "e")))
                Count(state);

            // Setting, removing and disabling a state name it by its dice, measured on every such effect (client.md).
            if (effect.EffectId is 950 or 951 or 952)
                Count(effect.DiceNum);
        }

        return (named, missing);
    }

    /// <summary>The numbers that follow a prefix in a mask or criterion, such as the states of <c>a,E516</c> or <c>HS!4</c>.</summary>
    private static IEnumerable<int> Numbers(string? text, string prefix)
    {
        if (string.IsNullOrEmpty(text))
            yield break;

        var index = 0;

        while ((index = text.IndexOf(prefix, index, StringComparison.Ordinal)) >= 0)
        {
            index += prefix.Length;

            if (index < text.Length && text[index] is '=' or '!')
                index++;

            var start = index;

            while (index < text.Length && char.IsAsciiDigit(text[index]))
                index++;

            if (index > start)
                yield return int.Parse(text.AsSpan(start, index - start), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
