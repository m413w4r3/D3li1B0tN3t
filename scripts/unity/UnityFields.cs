using System.Numerics;

namespace D3B.Scripts.Unity;

/// <summary>The plain fields of a serialized object, read by name through its type tree, each with its type checked.</summary>
/// <remarks>
/// Game data records are many small classes: reading their numbers, flags, strings, references and arrays of such
/// structs into one bag spares a reader per class, while a getter still fails, with the field's name, when the client
/// removes or retypes a field. Structs nest bags of their own.
/// </remarks>
internal sealed class UnityFields
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);
    private readonly string _owner;

    private UnityFields(string owner)
    {
        _owner = owner;
    }

    /// <summary>Reads the fields of a node, the object itself for node 0.</summary>
    /// <param name="reader">The reader, at the node's first field.</param>
    /// <param name="node">The node.</param>
    /// <param name="owner">What the fields belong to, for errors: a class name.</param>
    public static UnityFields Read(ref UnityReader reader, int node, string owner)
    {
        var tree = reader.Tree;
        var fields = new UnityFields(owner);

        foreach (var child in tree[node].Children)
        {
            var field = tree[child];

            // A registry inside a reference type's tree is never written: the object's own registry holds them all.
            if (field.IsRegistry)
            {
                reader.Skip(child);

                continue;
            }

            switch (field.Type)
            {
                case "float":
                    fields._values[field.Name] = reader.ReadSingle(child);
                    break;
                case "int" or "SInt32":
                    fields._values[field.Name] = reader.ReadInt32(child);
                    break;
                case "SInt64" or "long long":
                    fields._values[field.Name] = reader.ReadInt64(child);
                    break;
                case "unsigned int" or "UInt32":
                    fields._values[field.Name] = reader.ReadUInt32(child);
                    break;
                case "UInt64" or "unsigned long long":
                    fields._values[field.Name] = reader.ReadUInt64(child);
                    break;
                case "SInt16" or "short":
                    fields._values[field.Name] = reader.ReadInt16(child);
                    break;
                case "UInt16" or "unsigned short":
                    fields._values[field.Name] = reader.ReadUInt16(child);
                    break;
                case "SInt8":
                    fields._values[field.Name] = reader.ReadSByte(child);
                    break;

                // The client stores flags and small numbers alike as UInt8: the getter says which it is.
                case "UInt8":
                    fields._values[field.Name] = reader.ReadByte(child);
                    break;
                case "bool":
                    fields._values[field.Name] = reader.ReadBoolean(child);
                    break;
                case "AleVector2" or "Vector2f":
                    fields._values[field.Name] = reader.ReadVector2(child);
                    break;
                case "AleVector4" or "Vector4f":
                    fields._values[field.Name] = reader.ReadVector4(child);
                    break;
                case "AleColor":
                    fields._values[field.Name] = reader.ReadPackedColor(child);
                    break;
                case "AleMatrix4x4":
                    fields._values[field.Name] = ReadMatrix(ref reader, child);
                    break;
                case "managedReference":
                    fields._values[field.Name] = reader.ReadReference(child);
                    break;
                case "string":
                    fields._values[field.Name] = reader.ReadString(child);
                    break;
                default:
                    if (IsArray(tree, child))
                    {
                        if (ItemType(tree, child) is "string")
                            fields._values[field.Name] = reader.ReadStringArray(child);
                        else if (ItemType(tree, child) is "float")
                            fields._values[field.Name] = reader.ReadSingleArray(child);
                        else if (IsStructArray(tree, child))
                            fields._values[field.Name] = ReadItems(ref reader, child, $"{owner}.{field.Name}");
                        else if (ItemType(tree, child) is "int" or "SInt32" or "unsigned int" or "UInt32")
                            fields._values[field.Name] = reader.ReadInt32Array(child);
                        else
                            reader.Skip(child);
                    }
                    else if (field.Children.Length > 0)
                    {
                        fields._values[field.Name] = Read(ref reader, child, $"{owner}.{field.Name}");
                    }
                    else
                    {
                        reader.Skip(child);
                    }

                    break;
            }
        }

        reader.AlignAfter(node);

        return fields;
    }

    /// <summary>The array node of a field: the field itself, or its child for vectors; -1 for a field that is no array.</summary>
    private static int ArrayNode(UnityTypeTree tree, int node)
    {
        if (tree[node].IsArray)
            return node;

        foreach (var child in tree[node].Children)
        {
            if (tree[child].IsArray)
                return child;
        }

        return -1;
    }

    private static bool IsArray(UnityTypeTree tree, int node)
    {
        return ArrayNode(tree, node) is not -1;
    }

    private static bool IsStructArray(UnityTypeTree tree, int node)
    {
        var array = ArrayNode(tree, node);

        return array is not -1 && tree[tree[array].Children[1]].Children.Length > 0;
    }

    private static string ItemType(UnityTypeTree tree, int node)
    {
        return tree[tree[ArrayNode(tree, node)].Children[1]].Type;
    }

    private static List<UnityFields> ReadItems(ref UnityReader reader, int node, string owner)
    {
        var item = reader.BeginArray(node, out var count);
        var items = new List<UnityFields>(count);

        for (var i = 0; i < count; i++)
            items.Add(Read(ref reader, item, owner));

        reader.EndArray(node);

        return items;
    }

    private static Matrix4x4 ReadMatrix(ref UnityReader reader, int node)
    {
        var tree = reader.Tree;
        var columns = new Vector4[4];

        for (var i = 0; i < columns.Length; i++)
        {
            var column = tree.Child(node, $"column{i}");

            if (column is -1)
                throw tree.Unexpected(node, "a matrix of four columns");

            columns[i] = reader.ReadVector4(column);
        }

        reader.AlignAfter(node);

        return new Matrix4x4(
            columns[0].X, columns[0].Y, columns[0].Z, columns[0].W,
            columns[1].X, columns[1].Y, columns[1].Z, columns[1].W,
            columns[2].X, columns[2].Y, columns[2].Z, columns[2].W,
            columns[3].X, columns[3].Y, columns[3].Z, columns[3].W);
    }

    /// <summary>Gets whether the object has a field, which some layouts of a class lack.</summary>
    public bool Has(string name)
    {
        return _values.ContainsKey(name);
    }

    /// <summary>Gets an integer of any width, signed or not, such as a key the client stores in 16 bits in one table and 32 in another.</summary>
    public long Integer(string name)
    {
        if (!_values.TryGetValue(name, out var value))
            throw new InvalidDataException($"{_owner} has no field {name}: the client changed this type, so the extractor needs updating.");

        return value switch
        {
            int number => number,
            uint number => number,
            short number => number,
            ushort number => number,
            sbyte number => number,
            byte number => number,
            long number => number,
            _ => throw new InvalidDataException($"{_owner}.{name} is a {value.GetType().Name}, where the extractor expects an integer."),
        };
    }

    public float Single(string name)
    {
        return Get<float>(name);
    }

    /// <summary>Gets a float the client added to a later layout of the class, or a fallback when this one lacks it.</summary>
    public float Single(string name, float fallback)
    {
        return Has(name) ? Get<float>(name) : fallback;
    }

    public int Int32(string name)
    {
        return Get<int>(name);
    }

    public uint UInt32(string name)
    {
        return Get<uint>(name);
    }

    public long Int64(string name)
    {
        return Get<long>(name);
    }

    public ulong UInt64(string name)
    {
        return Get<ulong>(name);
    }

    public short Int16(string name)
    {
        return Get<short>(name);
    }

    public ushort UInt16(string name)
    {
        return Get<ushort>(name);
    }

    public sbyte SByte(string name)
    {
        return Get<sbyte>(name);
    }

    /// <summary>Gets a <c>UInt8</c> read as a number.</summary>
    public byte Byte(string name)
    {
        return Get<byte>(name);
    }

    /// <summary>Gets an array of 32-bit integers, signed or not.</summary>
    public int[] Int32Array(string name)
    {
        return Get<int[]>(name);
    }

    /// <summary>Gets an array of floats.</summary>
    public float[] SingleArray(string name)
    {
        return Get<float[]>(name);
    }

    /// <summary>Gets an array of strings.</summary>
    public string[] StringArray(string name)
    {
        return Get<string[]>(name);
    }

    public string String(string name)
    {
        return Get<string>(name);
    }

    /// <summary>Gets a vector of three floats, which the client stores as a struct of x, y and z.</summary>
    public System.Numerics.Vector3 Vector3(string name)
    {
        var vector = Nested(name);

        return new System.Numerics.Vector3(vector.Single("x"), vector.Single("y"), vector.Single("z"));
    }

    /// <summary>Gets a flag, which the client stores as a <c>bool</c> or as a <c>UInt8</c>.</summary>
    public bool Boolean(string name)
    {
        return _values.TryGetValue(name, out var value) && value is byte flag ? flag is not 0 : Get<bool>(name);
    }

    public Vector2 Vector2(string name)
    {
        return Get<Vector2>(name);
    }

    /// <summary>Gets a vector the client added to a later layout of the class, or zero when this one lacks it.</summary>
    public Vector2 Vector2OrZero(string name)
    {
        return Has(name) ? Get<Vector2>(name) : default;
    }

    public Vector4 Vector4(string name)
    {
        return Get<Vector4>(name);
    }

    /// <summary>Gets a packed ARGB colour.</summary>
    public uint Color(string name)
    {
        return Get<uint>(name);
    }

    /// <summary>Gets a Unity matrix, its rows those of the client's columns: <see cref="System.Numerics"/> multiplies row vectors.</summary>
    public Matrix4x4 Matrix(string name)
    {
        return Get<Matrix4x4>(name);
    }

    /// <summary>Gets the identifier of a managed reference in the object's registry.</summary>
    public long Reference(string name)
    {
        return Get<long>(name);
    }

    public IReadOnlyList<UnityFields> Items(string name)
    {
        return Get<IReadOnlyList<UnityFields>>(name);
    }

    /// <summary>Gets the fields of a struct field, such as the parameters a configuration wraps.</summary>
    public UnityFields Nested(string name)
    {
        return Get<UnityFields>(name);
    }

    private T Get<T>(string name)
    {
        if (!_values.TryGetValue(name, out var value))
            throw new InvalidDataException($"{_owner} has no field {name}: the client changed this type, so the extractor needs updating.");

        return value is T typed
            ? typed
            : throw new InvalidDataException($"{_owner}.{name} is a {value.GetType().Name}, where the extractor expects a {typeof(T).Name}.");
    }
}
