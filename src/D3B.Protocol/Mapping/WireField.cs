namespace D3B.Protocol.Mapping;

/// <summary>A field of a protobuf message as its bytes show it: its number, its wire type and where its value lies.</summary>
public readonly struct WireField
{
    /// <summary>Gets the field's number.</summary>
    public int Number { get; }

    /// <summary>Gets the field's wire type: 0 varint, 1 fixed 64 bits, 2 length-delimited, 5 fixed 32 bits.</summary>
    public int WireType { get; }

    /// <summary>Gets where the value starts, after its length for a length-delimited field.</summary>
    public int Start { get; }

    /// <summary>Gets the value's length in bytes.</summary>
    public int Length { get; }

    /// <summary>Initializes a new instance of the <see cref="WireField"/> struct.</summary>
    /// <param name="number">The field's number.</param>
    /// <param name="wireType">Its wire type.</param>
    /// <param name="start">Where its value starts.</param>
    /// <param name="length">The value's length in bytes.</param>
    public WireField(int number, int wireType, int start, int length)
    {
        Number = number;
        WireType = wireType;
        Start = start;
        Length = length;
    }
}
