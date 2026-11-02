using D3B.Data.Banks;
using D3B.Data.Interactives;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Texts;
using D3B.Data.World;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads, in each language of <see cref="TextLanguages.All"/>, the numbered texts the pack's tables name.</summary>
/// <remarks>
/// Game data names texts by number; of the client's third of a million, the pack keeps those its tables name (D25). The
/// keyed texts, found by a hash of a key the client builds at run time, belong to its interface and are left out.
/// </remarks>
internal static class TextsExtractor
{
    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [.. TextLanguages.All.Select(language => PathOf(client, language))];
    }

    /// <summary>Reads a language's text file.</summary>
    /// <exception cref="FileNotFoundException">The client has no texts in that language.</exception>
    public static I18nFile Open(ClientInstall client, string language)
    {
        var path = PathOf(client, language);

        if (!File.Exists(path))
            throw new FileNotFoundException($"The client has no {language} texts: {path} does not exist.", path);

        return I18nFile.Read(path);
    }

    /// <summary>Gets the texts every language holds, for a table that names a text the client lacks.</summary>
    public static IReadOnlySet<int> Held(ClientInstall client)
    {
        HashSet<int>? held = null;

        foreach (var language in TextLanguages.All)
        {
            var ids = Open(client, language).Texts.Select(text => text.Id);

            if (held is null)
                held = [.. ids];
            else
                held.IntersectWith(ids);
        }

        return held ?? [];
    }

    /// <summary>Gets the texts the tables name.</summary>
    public static IReadOnlySet<int> Named(WorldAtlas atlas, ItemCatalog items, JobCatalog jobs, InteractiveCatalog interactives, BankCatalog banks, FightTables fights)
    {
        var named = new HashSet<int>();

        foreach (var position in atlas.Positions.Span)
            named.Add(position.NameId);

        foreach (var subArea in atlas.SubAreas.Span)
            named.Add(subArea.NameId);

        foreach (var area in atlas.Areas.Span)
            named.Add(area.NameId);

        foreach (var item in items.Items.Span)
            named.Add(item.NameId);

        foreach (var type in items.Types.Span)
            named.Add(type.NameId);

        foreach (var job in jobs.Jobs.Span)
            named.Add(job.NameId);

        foreach (var skill in jobs.Skills.Span)
            named.Add(skill.NameId);

        foreach (var interactive in interactives.Interactives.Span)
            named.Add(interactive.NameId);

        foreach (var banker in banks.Bankers.Span)
            named.Add(banker.NameId);

        named.UnionWith(fights.Named());

        // 12,274 maps of client 3.7.12.12 name text 0, which no text file holds, and so do a spell, 27 effects and two
        // characteristics: they have no name.
        named.Remove(0);

        return named;
    }

    /// <summary>Reads the named texts of every language, failing when one lacks a text the tables name.</summary>
    public static IReadOnlyList<TextTable> Read(ClientInstall client, IReadOnlySet<int> named)
    {
        var ids = named.Order().ToArray();
        var tables = new List<TextTable>(TextLanguages.All.Count);

        foreach (var language in TextLanguages.All)
        {
            var file = Open(client, language);
            var offsets = new Dictionary<int, int>(file.Texts.Count);

            foreach (var (id, offset) in file.Texts)
            {
                if (!offsets.TryAdd(id, offset))
                    throw new InvalidDataException($"The {language} texts hold text {id} twice.");
            }

            var texts = new string[ids.Length];

            for (var i = 0; i < ids.Length; i++)
            {
                if (!offsets.TryGetValue(ids[i], out var offset))
                    throw new InvalidDataException($"The {language} texts lack text {ids[i]}, which the extracted tables name.");

                texts[i] = file.TextAt(offset);
            }

            tables.Add(new TextTable(language, ids, texts));
            Console.WriteLine($"Texts ({language}): {ids.Length:N0} of {offsets.Count:N0}");
        }

        return tables;
    }

    private static string PathOf(ClientInstall client, string language)
    {
        return Path.Combine(client.StreamingAssets, "Content", "I18n", $"{language}.bin");
    }
}
