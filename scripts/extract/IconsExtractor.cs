using System.Globalization;
using AssetRipper.TextureDecoder.Bc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using D3B.Data.Breeds;
using D3B.Data.Icons;
using D3B.Data.Items;
using D3B.Data.Jobs;
using D3B.Data.Monsters;
using D3B.Data.Spells;
using D3B.Data.World;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>
/// Reads the icons of every item, of the professions, of the classes' spells and of the monsters a hunter meets, decoded
/// from the client's BC7 textures (D135, D136, D247, D265).
/// </summary>
/// <remarks>
/// <para>
/// <c>Picto/Items</c>, <c>Picto/UI</c>, <c>Picto/Spells</c> and <c>Picto/Monsters</c> hold one texture and one sprite
/// per icon, named by the icon's identifier, a spell's after <c>sort_</c>, a monster's its <c>gfxId</c>, in a bundle per
/// scale. Items' icons are 64 pixels at 1x, professions' at 2x, spells' 48 at 1x, monsters' 64 at 1x: each kind's table
/// has its own <see cref="IconTable.Size"/>, the items' halved to 32 (D265). The extractor keeps every item's icon, those
/// of the breeds' spells and of both spells of every variant, the spells every character has included, and those of the
/// monsters the sub-areas list, but bombs and the monsters hidden from the bestiary.
/// </para>
/// <para>
/// A texture is BC7, one level, its texels in the bundle's <c>.resS</c>, bottom row first, as Unity stores them; its
/// sprite's rectangle covers it whole, an items' sprite drawing it through a mesh trimmed to its opaque texels. The
/// extraction fails on anything else, rather than keep an icon cut or upside down. It fails too on an item's, a
/// profession's or a spell's icon the bundle lacks, or that of an item a harvest names, the sub-areas listing it as
/// harvestable or a skill gathering it, Base's included: the pack is made once a build by its maintainer, who should hear
/// of it then, rather than the app drawing a neutral square nobody notices. A monster's it lacks, or another item's, is
/// counted and left out, as an item's icon of another size or format is: 4 monsters of client 3.7.12.12 have no picture,
/// 4 items' icons are missing and 3 are not of 64 pixels in BC7, and nothing but a name needs one.
/// </para>
/// </remarks>
internal static class IconsExtractor
{
    // UnityEngine.TextureFormat.BC7.
    private const int Bc7Format = 25;

    // Each bundle's icons at the scale it is read at (D136): a texture of another size fails the extraction.
    private const int ItemSize = 64;
    private const int JobSize = 64;
    private const int SpellSize = 48;
    private const int MonsterSize = 64;

    // A hunter meets neither. UseSummonSlot is no such flag: set on 4,655 of the 5,129 monsters of client 3.7.12.12,
    // Astrub's larvae included, it says what a monster does once summoned.
    private const MonsterProperties Unmet = MonsterProperties.UseBombSlot | MonsterProperties.HideInBestiary;

    public static IReadOnlyList<string> Inputs(ClientInstall client)
    {
        return [ItemsBundle(client), JobsBundle(client), SpellsBundle(client), MonstersBundle(client), UnityDataTable.PathOf(client, "subareas")];
    }

    private static string ItemsBundle(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "Items", "item_assets_1x.bundle");
    }

    private static string JobsBundle(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "UI", "job_assets_2x.bundle");
    }

    private static string SpellsBundle(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "Spells", "spell_assets_1x.bundle");
    }

    private static string MonstersBundle(ClientInstall client)
    {
        return Path.Combine(client.StreamingAssets, "Content", "Picto", "Monsters", "monster_assets_1x.bundle");
    }

    public static IconCatalog Read(ClientInstall client, ItemCatalog items, JobCatalog jobs, SpellCatalog spells, BreedCatalog breeds, MonsterCatalog monsters, WorldAtlas atlas)
    {
        var named = new SortedSet<int>(UnityDataTable.ReadById(client, "subareas").SelectMany(record => record.Int32Array("harvestables")));

        foreach (var skill in jobs.Skills.Span)
        {
            if (skill.GatheredItemId is not Skill.NoItem)
                named.Add(skill.GatheredItemId);
        }

        var itemIcons = new SortedSet<int>();

        foreach (var id in named)
        {
            if (!items.TryGetItem(id, out var item))
                throw new InvalidDataException($"Item {id}, which a harvest names, is no item of the client.");

            itemIcons.Add(item.IconId);
        }

        var jobIcons = new SortedSet<int>();

        foreach (var job in jobs.Jobs.Span)
        {
            if (job.IconId is not Job.NoIcon)
                jobIcons.Add(job.IconId);
        }

        var classSpells = ClassSpells(spells, breeds);
        var spellIcons = new SortedSet<int>();

        foreach (var id in classSpells)
        {
            if (!spells.TryGetSpell(id, out var spell))
                throw new InvalidDataException($"Spell {id}, which a breed or a variant names, is no spell of the client.");

            spellIcons.Add(spell.IconId);
        }

        var met = new SortedSet<int>();
        var monsterIcons = new SortedSet<int>();

        foreach (var id in atlas.Monsters.Span)
        {
            if (!monsters.TryGetMonster(id, out var monster))
                throw new InvalidDataException($"Monster {id}, which a sub-area lists, is no monster of the client.");

            if ((monster.Properties & Unmet) is MonsterProperties.None && met.Add(id))
                monsterIcons.Add(monster.GfxId);
        }

        var allIcons = new SortedSet<int>(itemIcons);

        foreach (ref readonly var item in items.Items.Span)
        {
            if (item.IconId > 0)
                allIcons.Add(item.IconId);
        }

        var (read, itemsLacking) = Decode(ItemsBundle(client), string.Empty, ItemSize, allIcons, leaveOdd: true);

        if (itemsLacking.Where(itemIcons.Contains).ToList() is { Count: > 0 } harvestLacking)
            throw new InvalidDataException($"{Path.GetFileName(ItemsBundle(client))} lacks {Missing(harvestLacking)}.");

        var itemTable = Halved(read);

        var (monsterTable, lacking) = Decode(MonstersBundle(client), string.Empty, MonsterSize, monsterIcons);
        var unpictured = met.Where(id => monsters.TryGetMonster(id, out var monster) && lacking.Contains(monster.GfxId)).ToList();
        var catalog = new IconCatalog(
            itemTable,
            Whole(JobsBundle(client), string.Empty, JobSize, jobIcons, Missing),
            Whole(SpellsBundle(client), "sort_", SpellSize, spellIcons, ids => MissingSpells(ids, spells, breeds, classSpells)),
            monsterTable);

        Console.WriteLine($"Icons: {catalog.Items.Count:N0} of the items' {allIcons.Count:N0} at {catalog.Items.Size} pixels, {itemsLacking.Count:N0} lacking or not of {ItemSize} pixels in BC7; {catalog.Jobs.Count:N0} of the professions, {catalog.Spells.Count:N0} of the {classSpells.Count:N0} spells of the breeds and their variants");
        Console.WriteLine($"  {catalog.Monsters.Count:N0} pictures of the {met.Count:N0} monsters a hunter meets; monsters whose picture the client lacks, drawn without: {unpictured.Count:N0}{(unpictured.Count > 0 ? $" ({List(unpictured)})" : string.Empty)}");

        return catalog;
    }

    private static SortedSet<int> ClassSpells(SpellCatalog spells, BreedCatalog breeds)
    {
        var ids = new SortedSet<int>();

        foreach (ref readonly var breed in breeds.Breeds.Span)
        {
            foreach (var id in breeds.SpellsOf(breed))
                ids.Add(id);
        }

        foreach (ref readonly var variant in spells.Variants.Span)
        {
            ids.Add(variant.SpellId);
            ids.Add(variant.VariantSpellId);
        }

        return ids;
    }

    private static string Missing(IReadOnlyList<int> ids)
    {
        return $"the icon, or its sprite, of {List(ids)}";
    }

    // By breed, the spells every character has apart: the maintainer sees whose spells would go without.
    private static string MissingSpells(IReadOnlyList<int> ids, SpellCatalog spells, BreedCatalog breeds, SortedSet<int> classSpells)
    {
        var lacking = new HashSet<int>(ids);
        var breedOf = new Dictionary<int, int>();

        foreach (ref readonly var breed in breeds.Breeds.Span)
        {
            foreach (var id in breeds.SpellsOf(breed))
                breedOf.TryAdd(id, breed.Id);
        }

        foreach (ref readonly var variant in spells.Variants.Span)
        {
            breedOf.TryAdd(variant.SpellId, variant.BreedId);
            breedOf.TryAdd(variant.VariantSpellId, variant.BreedId);
        }

        var without = classSpells
            .Where(id => spells.TryGetSpell(id, out var spell) && lacking.Contains(spell.IconId))
            .GroupBy(id => breedOf[id])
            .OrderBy(group => group.Key)
            .Select(group => $"{(group.Key == SpellVariant.CommonBreed ? "every character's" : $"breed {group.Key}'s")} {List([.. group])}");

        return $"the icon, or its sprite, of {ids.Count:N0} icons, those of spells {string.Join("; ", without)}";
    }

    private static string List(IReadOnlyList<int> ids)
    {
        return $"{string.Join(", ", ids.Take(10))}{(ids.Count > 10 ? ", …" : string.Empty)}";
    }

    private static IconTable Whole(string path, string prefix, int size, SortedSet<int> wanted, Func<IReadOnlyList<int>, string> describeMissing)
    {
        var (table, missing) = Decode(path, prefix, size, wanted);

        if (missing.Count > 0)
            throw new InvalidDataException($"{Path.GetFileName(path)} lacks {describeMissing(missing)}.");

        return table;
    }

    private static (IconTable Table, IReadOnlyList<int> Missing) Decode(string path, string prefix, int size, SortedSet<int> wanted, bool leaveOdd = false)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"The client has no icons there: {path} does not exist.", path);

        using var bundle = new UnityBundle(path);
        var textures = new Dictionary<long, (int IconId, byte[] Pixels)>();
        var odd = new HashSet<long>();
        var sprites = new HashSet<int>();

        foreach (var file in bundle.Files)
        {
            var metadata = file.file.Metadata;

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.Texture2D))
            {
                var texture = ReadFields(file, info, "Texture2D");

                if (IconOf(texture.String("m_Name"), prefix) is not { } iconId || !wanted.Contains(iconId))
                    continue;

                // Measured on client 3.7.12.12: 3 of the 11,751 items' icons are 60 or 62 pixels, one of them not BC7.
                if (leaveOdd && !IsIcon(texture, size))
                    odd.Add(info.PathId);
                else
                    textures.Add(info.PathId, (iconId, DecodeTexture(bundle, texture, iconId, size)));
            }

            foreach (var info in metadata.GetAssetsOfType(AssetClassID.Sprite))
            {
                var sprite = ReadFields(file, info, "Sprite");

                if (IconOf(sprite.String("m_Name"), prefix) is not { } iconId || !wanted.Contains(iconId))
                    continue;

                var data = sprite.Nested("m_RD");

                if (odd.Contains(data.Nested("texture").Int64("m_PathID")))
                    continue;

                if (!IsWhole(sprite.Nested("m_Rect"), size)
                    || !textures.TryGetValue(data.Nested("texture").Int64("m_PathID"), out var texture) || texture.IconId != iconId)
                    throw new InvalidDataException($"{Path.GetFileName(path)}: the sprite of icon {iconId} does not cover its own texture whole.");

                sprites.Add(iconId);
            }
        }

        var found = textures.Values.ToDictionary(texture => texture.IconId, texture => texture.Pixels);
        var held = wanted.Where(id => found.ContainsKey(id) && sprites.Contains(id)).ToArray();
        var iconLength = size * size * 4;
        var pixels = new byte[held.Length * iconLength];

        for (var i = 0; i < held.Length; i++)
            found[held[i]].CopyTo(pixels, i * iconLength);

        return (new IconTable(size, held, pixels), wanted.Except(held).ToList());
    }

    private static UnityFields ReadFields(AssetsFileInstance file, AssetFileInfo info, string owner)
    {
        var tree = UnityTypeTree.From(file.file.Metadata.TypeTreeTypes[info.TypeIdOrIndex], owner);
        var reader = new UnityReader(UnityBundle.ReadObject(file, info), tree);

        return UnityFields.Read(ref reader, 0, owner);
    }

    private static int? IconOf(string name, string prefix)
    {
        return name.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private static bool IsWhole(UnityFields rectangle, int size)
    {
        return rectangle.Single("x") == 0 && rectangle.Single("y") == 0
            && rectangle.Single("width") == size && rectangle.Single("height") == size;
    }

    private static bool IsIcon(UnityFields texture, int size)
    {
        // BC7 packs a block of 4 by 4 texels in 16 bytes: one byte a texel.
        return texture.Int32("m_Width") == size && texture.Int32("m_Height") == size && texture.Int32("m_TextureFormat") == Bc7Format
            && texture.Int32("m_MipCount") == 1 && texture.Nested("m_StreamData").UInt32("size") == size * size;
    }

    // An item's icon is drawn at 24 pixels (D265): halved, each pixel the mean of four, weighed by their alpha so that the
    // transparent texels' colour does not darken the edges.
    private static IconTable Halved(IconTable table)
    {
        var from = table.Size;
        var to = from / 2;
        var pixels = new byte[table.Count * to * to * 4];
        var source = table.Pixels.Span;

        for (var icon = 0; icon < table.Count; icon++)
        {
            var read = icon * from * from * 4;
            var written = icon * to * to * 4;

            for (var y = 0; y < to; y++)
            {
                for (var x = 0; x < to; x++)
                {
                    int red = 0, green = 0, blue = 0, alpha = 0;

                    for (var texel = 0; texel < 4; texel++)
                    {
                        var at = read + ((((2 * y) + (texel / 2)) * from) + (2 * x) + (texel % 2)) * 4;
                        var weight = source[at + 3];

                        red += source[at] * weight;
                        green += source[at + 1] * weight;
                        blue += source[at + 2] * weight;
                        alpha += weight;
                    }

                    var pixel = written + (((y * to) + x) * 4);

                    if (alpha > 0)
                    {
                        pixels[pixel] = (byte)((red + (alpha / 2)) / alpha);
                        pixels[pixel + 1] = (byte)((green + (alpha / 2)) / alpha);
                        pixels[pixel + 2] = (byte)((blue + (alpha / 2)) / alpha);
                    }

                    pixels[pixel + 3] = (byte)((alpha + 2) / 4);
                }
            }
        }

        return new IconTable(to, table.Ids, pixels);
    }

    private static byte[] DecodeTexture(UnityBundle bundle, UnityFields texture, int iconId, int size)
    {
        var (width, height) = (texture.Int32("m_Width"), texture.Int32("m_Height"));
        var format = texture.Int32("m_TextureFormat");
        var stream = texture.Nested("m_StreamData");
        var length = checked((int)stream.UInt32("size"));

        if (!IsIcon(texture, size))
            throw new InvalidDataException($"Icon {iconId} is {width}x{height}, format {format}, {texture.Int32("m_MipCount")} levels and {length} bytes, not {size}x{size} BC7 in one level.");

        var blocks = bundle.ReadStream(stream.String("path"), checked((long)stream.UInt64("offset")), length);

        Bc7.Decompress<ColorRGBA<byte>, byte>(blocks, width, height, out var decoded);

        // Unity's texels start at the bottom row; an icon's rows start at the top (IconTable).
        var stride = width * 4;
        var pixels = new byte[decoded.Length];

        for (var row = 0; row < height; row++)
            decoded.AsSpan(row * stride, stride).CopyTo(pixels.AsSpan((height - 1 - row) * stride));

        return pixels;
    }
}
