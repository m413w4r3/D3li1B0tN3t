using Avalonia.Media;

namespace D3B.App.Controls;

/// <summary>The colours the map is drawn in, one palette for each of the app's themes (D122).</summary>
/// <remarks>Each theme's is a resource of <c>App.axaml</c>, <c>MapPalette</c>, which the map and its legend take dynamically.</remarks>
internal sealed class MapPalette
{
    /// <summary>Gets or sets the fill of a cell the character may walk on.</summary>
    public IBrush Walkable { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the outline of each cell walked on, the grid.</summary>
    public IBrush Grid { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of a cell that neither lets a character nor the view through.</summary>
    public IBrush Obstacle { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the outline of a cell that leaves the map, and the exits' labels.</summary>
    public IBrush Exit { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of a cell that leaves the map.</summary>
    public IBrush ExitFill { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the character's disc and its path.</summary>
    public IBrush Character { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the ring around the character's disc.</summary>
    public IBrush CharacterRing { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets a resource whose harvest is offered.</summary>
    public IBrush Resource { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets a resource harvested.</summary>
    public IBrush Harvested { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets an element that is no resource.</summary>
    public IBrush Element { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets a door.</summary>
    public IBrush Door { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets an element the server did not describe.</summary>
    public IBrush Unknown { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets a non-player character.</summary>
    public IBrush Npc { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets another bot's ring.</summary>
    public IBrush Bot { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets an ally in the character's fight.</summary>
    public IBrush Ally { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets an enemy in the character's fight.</summary>
    public IBrush Enemy { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of a challengers' placement cell, red as the client's.</summary>
    public IBrush ChallengerCell { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of a defenders' placement cell, blue as the client's.</summary>
    public IBrush DefenderCell { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of a stair's cell.</summary>
    public IBrush Stairs { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the outline of the cell under the pointer.</summary>
    public IBrush Hover { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of the cells of a map's first zone, and of every fourth after it.</summary>
    public IBrush Zone1 { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of the cells of a map's second zone.</summary>
    public IBrush Zone2 { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of the cells of a map's third zone.</summary>
    public IBrush Zone3 { get; set; } = Brushes.Transparent;

    /// <summary>Gets or sets the fill of the cells of a map's fourth zone.</summary>
    public IBrush Zone4 { get; set; } = Brushes.Transparent;

    /// <summary>Gets the fill of a zone's cells.</summary>
    /// <param name="zone">The zone, from 1.</param>
    /// <returns>Its fill: four in turn, zones beyond the fourth sharing them.</returns>
    public IBrush ZoneOf(int zone)
    {
        return ((zone - 1) % 4) switch
        {
            0 => Zone1,
            1 => Zone2,
            2 => Zone3,
            _ => Zone4,
        };
    }

    /// <summary>Gets the colour of a mark.</summary>
    /// <param name="mark">What is drawn.</param>
    /// <returns>Its colour.</returns>
    public IBrush Of(MapMark mark)
    {
        return mark switch
        {
            MapMark.Character => Character,
            MapMark.Resource => Resource,
            MapMark.Harvested => Harvested,
            MapMark.Element => Element,
            MapMark.Door => Door,
            MapMark.Unknown => Unknown,
            MapMark.Npc => Npc,
            MapMark.Bot => Bot,
            MapMark.Ally => Ally,
            MapMark.Enemy => Enemy,
            MapMark.ChallengerCell => ChallengerCell,
            MapMark.DefenderCell => DefenderCell,
            _ => Exit,
        };
    }
}
