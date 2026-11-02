using System.Globalization;
using D3B.Data.Breeds;
using D3B.Data.Characteristics;
using D3B.Data.Spells;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads the breeds, their spells, roles and the cost of their characteristics.</summary>
/// <remarks>
/// A breed's spells must be spells the client holds, each in a variant of the breed's, since a character plays one of a
/// variant's two (client.md): the extraction fails otherwise.
/// </remarks>
internal static class BreedsExtractor
{
    // The client names each list of costs by its characteristic; the pack names it by the characteristic's identifier, found by keyword.
    private static readonly (string Field, string Keyword)[] _costs =
    [
        ("statsPointsForStrength", "strength"),
        ("statsPointsForIntelligence", "intelligence"),
        ("statsPointsForChance", "chance"),
        ("statsPointsForAgility", "agility"),
        ("statsPointsForVitality", "vitality"),
        ("statsPointsForWisdom", "wisdom"),
    ];

    private static readonly string[] _tables = ["breeds", "breedroles"];

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. _tables.Select(table => UnityDataTable.PathOf(client, table))];
    }

    public static BreedCatalog Read(ClientInstall client, SpellCatalog spells, CharacteristicCatalog characteristics)
    {
        try
        {
            var characteristicIds = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < characteristics.Characteristics.Length; i++)
                characteristicIds[characteristics.Keywords.Span[i]] = characteristics.Characteristics.Span[i].Id;

            var breeds = new List<Breed>();
            var breedSpells = new List<int>();
            var breedRoles = new List<BreedRoleValue>();
            var costs = new List<BreedStatCost>();

            foreach (var record in UnityDataTable.ReadById(client, "breeds").OrderBy(record => record.Int32("id")))
            {
                var id = record.Int32("id");
                var ids = record.Int32Array("breedSpellsId");
                var roles = record.Items("breedRoles");
                var firstCost = costs.Count;

                if (!int.TryParse(record.String("shortNameId"), NumberStyles.None, CultureInfo.InvariantCulture, out var nameId))
                    throw new InvalidDataException($"Breed {id} is named by '{record.String("shortNameId")}', where the extractor measured a text's number.");

                foreach (var spell in ids)
                {
                    if (!spells.TryGetVariantOf(spell, out var variant) || variant.BreedId != id)
                        throw new InvalidDataException($"Breed {id} has spell {spell}, which is in no variant of the breed's.");
                }

                foreach (var (field, keyword) in _costs)
                {
                    if (!characteristicIds.TryGetValue(keyword, out var characteristic))
                        throw new InvalidDataException($"The client has no characteristic {keyword}, whose cost the breeds list.");

                    foreach (var step in record.Items(field))
                    {
                        var values = step.Int32Array("values");

                        if (values.Length is not 2)
                            throw new InvalidDataException($"Breed {id} lists a cost of {keyword} in {values.Length} numbers, where the extractor measured a threshold and a cost.");

                        costs.Add(new BreedStatCost(characteristic, values[0], values[1]));
                    }
                }

                breeds.Add(new Breed
                {
                    Id = id,
                    NameId = nameId,
                    FirstSpell = breedSpells.Count,
                    SpellCount = checked((byte)ids.Length),
                    FirstRole = breedRoles.Count,
                    RoleCount = checked((byte)roles.Count),
                    FirstStatCost = firstCost,
                    StatCostCount = checked((byte)(costs.Count - firstCost)),
                });
                breedSpells.AddRange(ids);
                breedRoles.AddRange(roles.Select(role => new BreedRoleValue(role.Int32("roleId"), role.Int32("value"), role.Int32("order"))));
            }

            var catalogRoles = UnityDataTable.ReadById(client, "breedroles").Select(record => new BreedRole(record.Int32("id"), record.Int32("nameId"))).OrderBy(role => role.Id).ToArray();
            var catalog = new BreedCatalog(breeds.ToArray(), breedSpells.ToArray(), catalogRoles, breedRoles.ToArray(), costs.ToArray());
            var unknownRoles = breedRoles.Count(role => Array.FindIndex(catalogRoles, known => known.Id == role.RoleId) < 0);

            Console.WriteLine($"Breeds: {breeds.Count:N0}, {breedSpells.Count:N0} spells, {catalogRoles.Length:N0} roles, {costs.Count:N0} costs; roles the client lacks: {unknownRoles:N0}");

            return catalog;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"The breeds' tables do not fit what the extractor measured: {exception.Message}", exception);
        }
    }
}
