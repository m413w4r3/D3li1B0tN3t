using System.Globalization;
using D3B.Data.Maps;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Extract;

/// <summary>Reads a map of either of the client's two layouts into a <see cref="MapData"/>, through its type tree.</summary>
/// <remarks>
/// <para>
/// <c>ClientMapData</c> holds the map's id, its cells, and its elements as managed references: an interactive element
/// stands on the cell of the middleground group that lists it, or, when animated, on a cell of its own. A few drawn in
/// the background or foreground have no cell. The interactive identifier is a property's backing field,
/// <c>&lt;interactiveId&gt;k__BackingField</c>, which <see cref="UnityTypeNode.Field"/> names <c>interactiveId</c>.
/// Every element names the graphic it is drawn with, <c>gfxId</c>, in both layouts.
/// </para>
/// <para>
/// <c>MapMetadata</c> nests a map in its <c>mapData</c>, without an id: the object's name, <c>map_&lt;id&gt;</c>, gives
/// it. Its cells add <c>nonWalkableDuringFight</c> and <c>nonWalkableDuringRP</c> and call the altitude <c>floor</c>;
/// its interactive elements name their own cell and an <c>m_interactionId</c>, and so do its bounding boxes.
/// </para>
/// <para>
/// Everything else, the maps' own graphics, materials, effects and sounds, is skipped. A field the client adds changes nothing,
/// and one it removes or retypes fails the extraction (client.md).
/// </para>
/// </remarks>
internal sealed class MapReader
{
    private const string MapNamePrefix = "map_";

    private readonly IReadOnlyDictionary<string, UnityTypeTree> _references;
    private readonly HashSet<string> _interactiveClasses;
    private readonly Dictionary<long, int> _groupCells = [];
    private readonly Dictionary<long, (int ElementId, int CellId, int GfxId)> _elements = [];
    private readonly List<(int ElementId, int CellId, int GfxId)> _interactives = [];

    /// <summary>How many elements the maps read so far draw twice on one cell with two graphics, each kept with the lowest.</summary>
    public int TwoGraphics { get; private set; }

    /// <param name="references">The type trees of the serialized file's managed reference classes.</param>
    public MapReader(IReadOnlyDictionary<string, UnityTypeTree> references)
    {
        _references = references;

        // Interactive elements are the referenced objects that carry an interactive identifier, whatever their class.
        _interactiveClasses = new HashSet<string>(
            references.Where(reference => reference.Value[0].Children.Any(child => IsInteractiveField(reference.Value[child].Field))).Select(reference => reference.Key),
            StringComparer.Ordinal);
    }

    /// <summary>Tells which layout a type tree is, or <see langword="null"/> for an object that is not a map.</summary>
    public static ClientMapLayout? LayoutOf(UnityTypeTree tree)
    {
        if (tree.HasFields("id", "cellsData", "middlegroundMapElements", "references"))
            return ClientMapLayout.ClientMapData;

        var mapData = tree.Child(0, "mapData");

        return mapData is not -1 && tree.Child(mapData, "cellsData") is not -1 && tree.HasFields("references")
            ? ClientMapLayout.MapMetadata
            : null;
    }

    private static bool IsInteractiveField(string field)
    {
        return field is "interactiveId" or "m_interactionId";
    }

    /// <summary>What each field of a cell is: a value the reader keeps, a flag, or a field it skips.</summary>
    private static (int[] Fields, MapCellProperties[] Flags) CellFields(UnityTypeTree tree, int item)
    {
        var children = tree[item].Children;
        var fields = new int[children.Length];
        var flags = new MapCellProperties[children.Length];
        var found = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < children.Length; i++)
        {
            var name = tree[children[i]].Field;

            flags[i] = name switch
            {
                "mov" => MapCellProperties.Walkable,
                "los" => MapCellProperties.LineOfSight,
                "nonWalkableDuringFight" => MapCellProperties.NonWalkableDuringFight,
                "nonWalkableDuringRP" => MapCellProperties.NonWalkableDuringRoleplay,
                "farmCell" => MapCellProperties.Farm,
                "visible" => MapCellProperties.Visible,
                "havenbagCell" => MapCellProperties.Havenbag,
                "roleplayMonstersMovementBlocked" => MapCellProperties.MonstersBlocked,
                "red" => MapCellProperties.Red,
                "blue" => MapCellProperties.Blue,
                _ => MapCellProperties.None,
            };
            fields[i] = name switch
            {
                "cellNumber" => CellField.Number,
                "speed" => CellField.Speed,
                "mapChangeData" => CellField.MapChange,
                "moveZone" => CellField.MoveZone,
                "linkedZone" => CellField.LinkedZone,
                "altitude" or "floor" => CellField.Floor,
                "arrow" => CellField.Arrow,
                _ => flags[i] is MapCellProperties.None ? CellField.Skip : CellField.Flag,
            };

            if (fields[i] is not CellField.Skip)
                found.Add(name is "floor" ? "altitude" : name);
        }

        foreach (var required in (string[])["cellNumber", "mapChangeData", "linkedZone", "altitude", "mov", "los"])
        {
            if (!found.Contains(required))
                throw new InvalidDataException($"{tree.Name}: a cell has no {required}: the client changed this type, so the extractor needs updating.");
        }

        return (fields, flags);
    }

    private static MapCell[] ReadCells(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        var item = reader.BeginArray(node, out var count);
        var children = tree[item].Children;
        var (fields, flags) = CellFields(tree, item);
        var cells = new MapCell[count];

        for (var i = 0; i < count; i++)
        {
            int number = -1, speed = 0, change = 0, moveZone = 0, linkedZone = 0, floor = 0, arrow = 0;
            var properties = MapCellProperties.None;

            for (var k = 0; k < children.Length; k++)
            {
                var child = children[k];

                switch (fields[k])
                {
                    case CellField.Number:
                        number = checked((int)reader.ReadUInt32(child));
                        break;
                    case CellField.Speed:
                        speed = reader.ReadInt32(child);
                        break;
                    case CellField.MapChange:
                        change = reader.ReadInt32(child);
                        break;
                    case CellField.MoveZone:
                        moveZone = reader.ReadInt32(child);
                        break;
                    case CellField.LinkedZone:
                        linkedZone = reader.ReadInt32(child);
                        break;
                    case CellField.Floor:
                        floor = reader.ReadInt32(child);
                        break;
                    case CellField.Arrow:
                        arrow = reader.ReadInt32(child);
                        break;
                    case CellField.Flag:
                        if (reader.ReadBoolean(child))
                            properties |= flags[k];

                        break;
                    default:
                        reader.Skip(child);
                        break;
                }
            }

            reader.AlignAfter(item);

            if (number != i)
                throw new InvalidDataException($"{tree.Name}: cell {i} is numbered {number}; the extractor expects cells in order.");

            cells[i] = new MapCell(properties, floor, speed, change, moveZone, linkedZone, arrow);
        }

        reader.EndArray(node);

        return cells;
    }

    /// <summary>Reads an element's interactive identifier, its graphic and its own cell, if it has one, skipping its other fields.</summary>
    private static (int? ElementId, int CellId, int GfxId) ReadInteractive(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        int? elementId = null;
        int? gfxId = null;
        var cell = MapInteractive.NoCell;

        foreach (var child in tree[node].Children)
        {
            var field = tree[child].Field;

            if (IsInteractiveField(field))
                elementId = checked((int)reader.ReadUInt32(child));
            else if (field is "cellId")
                cell = reader.ReadInt32(child);
            else if (field is "gfxId")
                gfxId = reader.ReadInt32(child);
            else
                reader.Skip(child);
        }

        // A tour tells a map's resources by its elements' graphics (D138): an element drawn with none is a layout the
        // extractor does not know.
        if (elementId is { } id && gfxId is null)
            throw new InvalidDataException($"{tree.Name}: interactive element {id} names no graphic: the client changed this type, so the extractor needs updating.");

        return (elementId, cell, gfxId ?? 0);
    }

    public MapData Read(byte[] data, UnityTypeTree tree, ClientMapLayout layout)
    {
        _groupCells.Clear();
        _elements.Clear();
        _interactives.Clear();

        var reader = new UnityReader(data, tree);
        var map = new MapRecord();

        foreach (var child in tree[0].Children)
        {
            switch (tree[child].Field)
            {
                case "m_Name":
                    map.Name = reader.ReadString(child);
                    break;
                case "mapData" when layout is ClientMapLayout.MapMetadata:
                    foreach (var part in tree[child].Children)
                        ReadMapField(ref reader, part, map);

                    reader.AlignAfter(child);
                    break;
                case "references":
                    UnityReferences.ReadRegistry(ref reader, data, child, _references, ReadReference);
                    break;
                default:
                    ReadMapField(ref reader, child, map);
                    break;
            }
        }

        reader.ExpectEnd();

        return Build(map, layout, tree);
    }

    private void ReadMapField(ref UnityReader reader, int node, MapRecord map)
    {
        var tree = reader.Tree;

        switch (tree[node].Field)
        {
            case "id":
                map.Id = reader.ReadInt32(node);
                break;
            case "topNeighbourId":
                map.Top = reader.ReadInt32(node);
                break;
            case "bottomNeighbourId":
                map.Bottom = reader.ReadInt32(node);
                break;
            case "leftNeighbourId":
                map.Left = reader.ReadInt32(node);
                break;
            case "rightNeighbourId":
                map.Right = reader.ReadInt32(node);
                break;
            case "cellsData":
                map.Cells = ReadCells(ref reader, node);
                break;
            case "middlegroundMapElements":
                ReadGroupCells(ref reader, node);
                break;
            case "interactiveElements":
                map.InteractiveReferences = reader.ReadReferenceArray(node);
                break;
            case "boundingBoxes":
                ReadBoundingBoxes(ref reader, node);
                break;
            default:
                reader.Skip(node);
                break;
        }
    }

    /// <summary>Records the cell of every element a middleground group lists: the cell the group is drawn on.</summary>
    private void ReadGroupCells(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        var item = reader.BeginArray(node, out var count);

        for (var i = 0; i < count; i++)
        {
            long[] elements = [];
            var cell = MapInteractive.NoCell;

            foreach (var child in tree[item].Children)
            {
                switch (tree[child].Field)
                {
                    case "mapElements":
                        elements = reader.ReadReferenceArray(child);
                        break;
                    case "cellId":
                        cell = reader.ReadInt32(child);
                        break;
                    default:
                        reader.Skip(child);
                        break;
                }
            }

            reader.AlignAfter(item);

            foreach (var element in elements)
                _groupCells[element] = cell;
        }

        reader.EndArray(node);
    }

    private void ReadBoundingBoxes(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        var item = reader.BeginArray(node, out var count);

        for (var i = 0; i < count; i++)
        {
            var (elementId, cell, gfxId) = ReadInteractive(ref reader, item);

            reader.AlignAfter(item);

            if (elementId is { } id)
                _interactives.Add((id, cell, gfxId));
        }

        reader.EndArray(node);
    }

    private bool ReadReference(long id, string className, ref UnityReader reader)
    {
        if (!_interactiveClasses.Contains(className))
            return false;

        var (elementId, cell, gfxId) = ReadInteractive(ref reader, 0);

        if (elementId is { } element)
            _elements[id] = (element, cell, gfxId);

        return true;
    }

    private MapData Build(MapRecord map, ClientMapLayout layout, UnityTypeTree tree)
    {
        long id = map.Id;

        if (layout is ClientMapLayout.MapMetadata)
        {
            if (!map.Name.StartsWith(MapNamePrefix, StringComparison.Ordinal)
                || !long.TryParse(map.Name.AsSpan(MapNamePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out id))
                throw new InvalidDataException($"{tree.Name} '{map.Name}' is not named map_<id>, which is where its id comes from.");

            foreach (var reference in map.InteractiveReferences)
            {
                if (!_elements.TryGetValue(reference, out var element))
                    throw new InvalidDataException($"Map {id} lists interactive element {reference}, which its registry does not hold.");

                _interactives.Add(element);
            }
        }
        else
        {
            foreach (var (reference, element) in _elements)
            {
                var hasGroup = _groupCells.TryGetValue(reference, out var groupCell);

                if (element.CellId is not MapInteractive.NoCell && hasGroup && groupCell != element.CellId)
                    throw new InvalidDataException($"Map {id}: interactive element {element.ElementId} names cell {element.CellId}, its group cell {groupCell}.");

                _interactives.Add((element.ElementId, element.CellId is not MapInteractive.NoCell ? element.CellId : hasGroup ? groupCell : MapInteractive.NoCell, element.GfxId));
            }
        }

        if (map.Cells is null)
            throw new InvalidDataException($"Map {id} has no cells.");

        return new MapData(id, map.Top, map.Bottom, map.Left, map.Right, map.Cells, Interactives(id));
    }

    /// <summary>The map's interactive elements, one per identifier, in ascending order.</summary>
    private MapInteractive[] Interactives(long mapId)
    {
        _interactives.Sort();

        var result = new List<MapInteractive>(_interactives.Count);

        for (var i = 0; i < _interactives.Count; i++)
        {
            var (elementId, cell, gfxId) = _interactives[i];

            if (i > 0 && _interactives[i - 1].ElementId == elementId)
            {
                // An element drawn twice on one cell is one element; on two cells, the map is not what the extractor knows.
                if (_interactives[i - 1].CellId != cell)
                    throw new InvalidDataException($"Map {mapId} places interactive element {elementId} on cells {_interactives[i - 1].CellId} and {cell}.");

                if (_interactives[i - 1].GfxId != gfxId)
                    TwoGraphics++;

                continue;
            }

            result.Add(new MapInteractive(elementId, cell, gfxId));
        }

        return [.. result];
    }

    /// <summary>The cell fields the reader keeps.</summary>
    private static class CellField
    {
        public const int Skip = 0;
        public const int Number = 1;
        public const int Speed = 2;
        public const int MapChange = 3;
        public const int MoveZone = 4;
        public const int LinkedZone = 5;
        public const int Floor = 6;
        public const int Arrow = 7;
        public const int Flag = 8;
    }

    private sealed class MapRecord
    {
        public string Name { get; set; } = string.Empty;

        public int Id { get; set; }

        public int Top { get; set; }

        public int Bottom { get; set; }

        public int Left { get; set; }

        public int Right { get; set; }

        public MapCell[]? Cells { get; set; }

        public long[] InteractiveReferences { get; set; } = [];
    }
}
