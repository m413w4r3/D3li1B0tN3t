using D3B.Data.Effects;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the effects spells and items name.</summary>
/// <remarks>
/// An effect keeps what bears on fights: its characteristic and how it changes it, its category, element and flags. Its
/// icons, tooltips and theoretical descriptions are left out, and so is the client's <c>effectId</c>, -1 on most effects
/// and of no measured meaning (client.md).
/// </remarks>
internal static class EffectsExtractor
{
    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [UnityDataTable.PathOf(client, "effects")];
    }

    public static EffectCatalog Read(ClientInstall client)
    {
        try
        {
            var effects = UnityDataTable.ReadById(client, "effects").Select(record => new Effect(
                record.Int32("id"),
                record.Int32("descriptionId"),
                record.Int32("characteristic"),
                Operator(record.String("characteristicOperator")),
                checked((byte)record.Int32("category")),
                checked((sbyte)record.Int32("elementId")),
                record.SByte("bonusType"),
                record.Int32("effectPriority"),
                record.Single("effectPowerRate"),
                record.Int32("oppositeId"),
                Properties(record))).OrderBy(effect => effect.Id).ToArray();
            var catalog = new EffectCatalog(effects);
            var opposites = effects.Count(effect => effect.OppositeId is not 0 && !catalog.TryGetEffect(effect.OppositeId, out _));

            Console.WriteLine($"Effects: {effects.Length:N0}; undone by an effect the client lacks: {opposites:N0}");

            return catalog;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The effects' table does not fit what the extractor measured: {exception.Message}", exception);
        }
    }

    private static char Operator(string text)
    {
        // Client 3.7.12.12 writes none, +, - or /: another operator would change a characteristic in a way not measured.
        return text switch
        {
            "" => '\0',
            "+" or "-" or "/" => text[0],
            _ => throw new InvalidDataException($"An effect changes its characteristic by '{text}', which the extractor does not know."),
        };
    }

    private static EffectProperties Properties(UnityFields record)
    {
        var properties = EffectProperties.None;

        if (record.Boolean("boost"))
            properties |= EffectProperties.Boost;

        if (record.Boolean("active"))
            properties |= EffectProperties.Active;

        if (record.Boolean("useInFight"))
            properties |= EffectProperties.UseInFight;

        if (record.Boolean("useDice"))
            properties |= EffectProperties.UseDice;

        if (record.Boolean("forceMinMax"))
            properties |= EffectProperties.ForceMinMax;

        if (record.Boolean("isInPercent"))
            properties |= EffectProperties.IsInPercent;

        return properties;
    }
}
