using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace D3B.Scripts.Unity;

/// <summary>Reads the bytes of a serialized object field by field, as its <see cref="UnityTypeTree"/> lays them out.</summary>
/// <remarks>Every typed read checks the field's type first, so that a change in the client fails instead of misreading.</remarks>
internal ref struct UnityReader
{
    private readonly ReadOnlySpan<byte> _data;
    private readonly UnityTypeTree _tree;

    public int Position { get; set; }

    public readonly int Length => _data.Length;

    public readonly UnityTypeTree Tree => _tree;

    public UnityReader(ReadOnlySpan<byte> data, UnityTypeTree tree)
    {
        _data = data;
        _tree = tree;
    }

    /// <summary>Moves past a field without reading it.</summary>
    public void Skip(int node)
    {
        var field = _tree[node];

        // A registry inside a reference type's tree is not serialized there: the object's own registry holds them all.
        if (field.IsRegistry)
            return;

        if (field.IsArray)
        {
            var count = RawInt32();
            var element = field.Children[1];
            var item = _tree[element];

            if (item.Children.Length is 0 && !item.Align && item.ByteSize > 0)
            {
                Advance((long)count * item.ByteSize);
            }
            else
            {
                for (var i = 0; i < count; i++)
                    Skip(element);
            }
        }
        else if (field.Children.Length > 0)
        {
            foreach (var child in field.Children)
                Skip(child);
        }
        else
        {
            Advance(field.ByteSize);
        }

        AlignAfter(node);
    }

    public int ReadInt32(int node)
    {
        Expect(node, "int", "SInt32");

        var value = RawInt32();

        AlignAfter(node);

        return value;
    }

    public uint ReadUInt32(int node)
    {
        Expect(node, "unsigned int", "UInt32");

        var value = BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

        AlignAfter(node);

        return value;
    }

    public long ReadInt64(int node)
    {
        Expect(node, "SInt64", "long long");

        var value = BinaryPrimitives.ReadInt64LittleEndian(Take(8));

        AlignAfter(node);

        return value;
    }

    public ulong ReadUInt64(int node)
    {
        Expect(node, "UInt64", "unsigned long long");

        var value = BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

        AlignAfter(node);

        return value;
    }

    public short ReadInt16(int node)
    {
        Expect(node, "SInt16", "short");

        var value = BinaryPrimitives.ReadInt16LittleEndian(Take(2));

        AlignAfter(node);

        return value;
    }

    public ushort ReadUInt16(int node)
    {
        Expect(node, "UInt16", "unsigned short");

        var value = BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

        AlignAfter(node);

        return value;
    }

    public sbyte ReadSByte(int node)
    {
        Expect(node, "SInt8", "char");

        var value = (sbyte)Take(1)[0];

        AlignAfter(node);

        return value;
    }

    /// <summary>Reads an unsigned byte as a number: the client stores both flags and small numbers as <c>UInt8</c>.</summary>
    public byte ReadByte(int node)
    {
        Expect(node, "UInt8", "unsigned char");

        var value = Take(1)[0];

        AlignAfter(node);

        return value;
    }

    public float ReadSingle(int node)
    {
        Expect(node, "float", "float");

        var value = BinaryPrimitives.ReadSingleLittleEndian(Take(4));

        AlignAfter(node);

        return value;
    }

    public bool ReadBoolean(int node)
    {
        Expect(node, "UInt8", "bool");

        var value = Take(1)[0] is not 0;

        AlignAfter(node);

        return value;
    }

    public string ReadString(int node)
    {
        Expect(node, "string", "string");

        var length = RawInt32();
        var text = Encoding.UTF8.GetString(Take(length));

        AlignAfter(_tree.ArrayOf(node));
        AlignAfter(node);

        return text;
    }

    /// <summary>Reads a string that has no node of its own, such as the type names of a reference registry.</summary>
    public string ReadRawString()
    {
        var length = RawInt32();
        var text = Encoding.UTF8.GetString(Take(length));

        Align();

        return text;
    }

    /// <summary>Reads a struct of two floats named <c>x</c> and <c>y</c>, such as <c>AleVector2</c> or <c>Vector2f</c>.</summary>
    public Vector2 ReadVector2(int node)
    {
        var x = _tree.Child(node, "x");
        var y = _tree.Child(node, "y");

        if (x is -1 || y is -1 || _tree[node].Children.Length is not 2)
            throw _tree.Unexpected(node, "a vector of x and y");

        var value = new Vector2(ReadSingle(x), ReadSingle(y));

        AlignAfter(node);

        return value;
    }

    /// <summary>Reads a struct of four floats named <c>x</c>, <c>y</c>, <c>z</c> and <c>w</c>, such as <c>AleVector4</c>.</summary>
    public Vector4 ReadVector4(int node)
    {
        var x = _tree.Child(node, "x");
        var y = _tree.Child(node, "y");
        var z = _tree.Child(node, "z");
        var w = _tree.Child(node, "w");

        if (x is -1 || y is -1 || z is -1 || w is -1 || _tree[node].Children.Length is not 4)
            throw _tree.Unexpected(node, "a vector of x, y, z and w");

        var value = new Vector4(ReadSingle(x), ReadSingle(y), ReadSingle(z), ReadSingle(w));

        AlignAfter(node);

        return value;
    }

    /// <summary>Reads one managed reference: its identifier in the object's registry.</summary>
    public long ReadReference(int node)
    {
        var rid = _tree.Child(node, "rid");

        if (rid is -1 || _tree[node].Children.Length is not 1)
            throw _tree.Unexpected(node, "a managed reference");

        var value = ReadInt64(rid);

        AlignAfter(node);

        return value;
    }

    /// <summary>Reads a struct holding one unsigned integer, such as <c>AleColor</c>.</summary>
    public uint ReadPackedColor(int node)
    {
        var field = _tree[node];

        if (field.Children.Length is not 1)
            throw _tree.Unexpected(node, "a packed colour");

        var value = ReadUInt32(field.Children[0]);

        AlignAfter(node);

        return value;
    }

    /// <summary>Reads an array, or a vector, of signed or unsigned 32-bit integers.</summary>
    public int[] ReadInt32Array(int node)
    {
        var array = _tree.ArrayOf(node);
        var item = _tree[_tree[array].Children[1]];

        if (item.Type is not ("int" or "SInt32" or "unsigned int" or "UInt32"))
            throw _tree.Unexpected(node, "an array of 32-bit integers");

        var values = new int[Count(4)];

        for (var i = 0; i < values.Length; i++)
            values[i] = RawInt32();

        AlignAfter(array);

        if (array != node)
            AlignAfter(node);

        return values;
    }

    /// <summary>Reads an array, or a vector, of floats.</summary>
    public float[] ReadSingleArray(int node)
    {
        var array = _tree.ArrayOf(node);
        var item = _tree[_tree[array].Children[1]];

        if (item.Type is not "float")
            throw _tree.Unexpected(node, "an array of floats");

        var values = new float[Count(4)];

        for (var i = 0; i < values.Length; i++)
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(Take(4));

        AlignAfter(array);

        if (array != node)
            AlignAfter(node);

        return values;
    }

    /// <summary>Reads an array, or a vector, of strings.</summary>
    public string[] ReadStringArray(int node)
    {
        var array = _tree.ArrayOf(node);
        var item = _tree[array].Children[1];

        if (_tree[item].Type is not "string")
            throw _tree.Unexpected(node, "an array of strings");

        var values = new string[Count(4)];

        for (var i = 0; i < values.Length; i++)
            values[i] = ReadString(item);

        AlignAfter(array);

        if (array != node)
            AlignAfter(node);

        return values;
    }

    /// <summary>Reads an array, or a vector, of signed or unsigned 64-bit integers, such as the keys of a dictionary.</summary>
    public long[] ReadInt64Array(int node)
    {
        var array = _tree.ArrayOf(node);
        var item = _tree[_tree[array].Children[1]];

        if (item.Type is not ("SInt64" or "long long" or "UInt64" or "unsigned long long"))
            throw _tree.Unexpected(node, "an array of 64-bit integers");

        var values = new long[Count(8)];

        for (var i = 0; i < values.Length; i++)
            values[i] = RawInt64();

        AlignAfter(array);

        if (array != node)
            AlignAfter(node);

        return values;
    }

    /// <summary>Reads an array of managed references: their identifiers in the object's registry.</summary>
    public long[] ReadReferenceArray(int node)
    {
        var array = _tree.ArrayOf(node);
        var item = _tree[array].Children[1];
        var rid = _tree.Child(item, "rid");

        if (rid is -1)
            throw _tree.Unexpected(node, "an array of managed references");

        var values = new long[Count(8)];

        for (var i = 0; i < values.Length; i++)
        {
            foreach (var child in _tree[item].Children)
            {
                if (child == rid)
                    values[i] = ReadInt64(child);
                else
                    Skip(child);
            }

            AlignAfter(item);
        }

        AlignAfter(array);

        if (array != node)
            AlignAfter(node);

        return values;
    }

    /// <summary>Reads the count of an array whose items the caller reads, then returns the node of an item.</summary>
    public int BeginArray(int node, out int count)
    {
        var array = _tree.ArrayOf(node);

        count = Count(1);

        return _tree[array].Children[1];
    }

    /// <summary>Finishes an array that <see cref="BeginArray"/> started.</summary>
    public void EndArray(int node)
    {
        var array = _tree.ArrayOf(node);

        AlignAfter(array);

        if (array != node)
            AlignAfter(node);
    }

    public void AlignAfter(int node)
    {
        if (_tree[node].Align)
            Align();
    }

    public void Align()
    {
        Position = (Position + 3) & ~3;
    }

    public readonly void ExpectEnd()
    {
        if (Position != _data.Length)
            throw new InvalidDataException($"{_tree.Name}: {_data.Length - Position} bytes are left after reading the object, so its layout was misread.");
    }

    public int RawInt32()
    {
        return BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    }

    public long RawInt64()
    {
        return BinaryPrimitives.ReadInt64LittleEndian(Take(8));
    }

    /// <summary>Reads the count of an array, checked against the bytes left before anything is allocated for it.</summary>
    private int Count(int itemSize)
    {
        var count = RawInt32();

        if (count < 0 || count > (_data.Length - Position) / itemSize)
            throw new InvalidDataException($"{_tree.Name}: an array claims {count} items of at least {itemSize} bytes, with {_data.Length - Position} bytes left.");

        return count;
    }

    private readonly void Expect(int node, string type, string alternative)
    {
        var actual = _tree[node].Type;

        if (actual != type && actual != alternative)
            throw _tree.Unexpected(node, type);
    }

    private void Advance(long count)
    {
        if (count < 0 || Position + count > _data.Length)
            throw new InvalidDataException($"{_tree.Name}: the object ends early.");

        Position += (int)count;
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || Position + count > _data.Length)
            throw new InvalidDataException($"{_tree.Name}: the object ends early.");

        var slice = _data.Slice(Position, count);

        Position += count;

        return slice;
    }
}
