using D3B.Data.Characteristics;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the characteristics, and the Lua formulas they name to scale a monster's.</summary>
/// <remarks>
/// Of the client's 38 formulas, of experience, guilds, alliances and seasons among others, the pack keeps those a
/// characteristic names; the extraction fails when a characteristic names one the client lacks (client.md). One
/// characteristic of client 3.7.12.12 names a text the client lacks: it is kept unnamed.
/// </remarks>
internal static class CharacteristicsExtractor
{
    private static readonly string[] _tables = ["characteristics", "luaformulas"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static CharacteristicCatalog Read(ClientInstall client, IReadOnlySet<int> texts)
    {
        try
        {
            var records = UnityDataTable.ReadById(client, "characteristics").OrderBy(record => record.Int32("id")).ToArray();
            var formulas = UnityDataTable.Read(client, "luaformulas").ToDictionary(formula => checked((int)formula.Key), formula => formula.Record.String("formula"));
            var characteristics = records.Select(record => new Characteristic(
                record.Int32("id"),
                texts.Contains(record.Int32("nameId")) ? record.Int32("nameId") : 0,
                record.Int32("categoryId"),
                record.Int32("order"),
                record.Int32("scaleFormulaId"),
                record.Boolean("visible"),
                record.Boolean("upgradable"))).ToArray();
            var named = characteristics.Select(characteristic => characteristic.ScaleFormulaId).Where(id => id is not Characteristic.NoFormula).Distinct().Order().ToArray();

            foreach (var id in named)
            {
                if (!formulas.ContainsKey(id))
                    throw new InvalidDataException($"A characteristic is scaled by formula {id}, which the client does not hold.");
            }

            var catalog = new CharacteristicCatalog(characteristics, records.Select(record => record.String("keyword")).ToArray(), named, named.Select(id => formulas[id]).ToArray());
            var unnamed = records.Count(record => record.Int32("nameId") is not 0 && !texts.Contains(record.Int32("nameId")));

            Console.WriteLine($"Characteristics: {characteristics.Length:N0}, {characteristics.Count(characteristic => characteristic.ScaleFormulaId is not Characteristic.NoFormula):N0} scaled by {named.Length:N0} of the client's {formulas.Count:N0} formulas; "
                + $"naming a text the client lacks, kept unnamed: {unnamed:N0}");

            return catalog;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The characteristics' tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }
}
