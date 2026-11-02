using D3B.App.Localization;
using D3B.Bot.Localization;
using D3B.Data;
using D3B.Data.Texts;
using D3B.Data.World;

namespace D3B.App.ViewModels;

/// <summary>Says a module's <see cref="GameText"/> in the app's language, the game's values named from the pack (D112, D131).</summary>
/// <remarks>An item, a profession and a sub-area by their names, a map by its coordinates; what the pack does not name, by its id.</remarks>
internal sealed class GameTextFormatter
{
    private readonly Localizer _localizer;
    private readonly GameWorld? _world;
    private readonly GameTexts? _texts;
    private readonly GameTables? _tables;

    /// <summary>Initializes a new instance of the <see cref="GameTextFormatter"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="world">The pack's world, which places the maps, if the host has one.</param>
    /// <param name="texts">The pack's texts, if the host has them.</param>
    /// <param name="tables">The pack's tables, which name the items and professions, if the host has them.</param>
    public GameTextFormatter(Localizer localizer, GameWorld? world, GameTexts? texts, GameTables? tables)
    {
        _localizer = localizer;
        _world = world;
        _texts = texts;
        _tables = tables;
    }

    /// <summary>Says a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>It, in the app's language.</returns>
    public string Say(GameText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var culture = _localizer.Culture;

        // French counts 0 as singular, English as plural, as the app's own texts do.
        var one = culture.Equals(Localizer.French) ? text.Count is 0 or 1 : text.Count is 1;
        var format = text.Many is { } many && !one ? many : text.Format;

        try
        {
            return string.Format(culture, format.In(culture), [.. text.Values.Select(Name)]);
        }
        catch (FormatException)
        {
            // A module's format whose placeholders its values do not fill must not throw in the app (D91).
            return format.In(culture);
        }
    }

    /// <summary>Names a value of a text.</summary>
    /// <param name="value">The value.</param>
    /// <returns>Its name, or the number written as the app's language writes it.</returns>
    public string Name(GameTextValue value)
    {
        var culture = _localizer.Culture;

        return value.Kind switch
        {
            GameTextKind.Number => value.Id.ToString("N0", culture),
            GameTextKind.Text => value.Text ?? "",
            GameTextKind.Item => Item((int)value.Id),
            GameTextKind.Job => Job((int)value.Id),
            GameTextKind.SubArea => SubArea((int)value.Id),
            _ => Map(value.Id),
        };
    }

    /// <summary>Names an item.</summary>
    /// <param name="itemId">The item.</param>
    /// <returns>Its name in the pack's texts, else its id.</returns>
    public string Item(int itemId)
    {
        return _tables is not null && _tables.Items.TryGetItem(itemId, out var item) && Text(item.NameId) is { } name ? name : _localizer.Format("ResourceNamed", itemId);
    }

    private string Job(int jobId)
    {
        return _tables is not null && _tables.Jobs.TryGetJob(jobId, out var job) && Text(job.NameId) is { } name ? name : _localizer.Format("JobNamed", jobId);
    }

    private string SubArea(int subAreaId)
    {
        return _world is not null && _world.Atlas.TryGetSubArea(subAreaId, out var subArea) && Text(subArea.NameId) is { } name ? name : _localizer.Format("SubArea", subAreaId);
    }

    private string Map(long mapId)
    {
        return _world is not null && _world.Atlas.TryGetPosition(mapId, out var position)
            ? _localizer.Format("Coordinates", position.X, position.Y)
            : _localizer.Format("MapNumber", mapId);
    }

    private string? Text(int nameId)
    {
        return _texts is not null && nameId != 0 && _texts.TryGet(_localizer.GameLanguage, nameId, out var text) ? text : null;
    }
}
