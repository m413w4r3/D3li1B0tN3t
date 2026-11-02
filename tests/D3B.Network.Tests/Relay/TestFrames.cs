using System.Buffers;
using System.Text;
using D3B.Network.Framing;

namespace D3B.Network.Tests.Relay;

/// <summary>Protobuf messages written by hand, as a game connection's first frame is shaped: its ticket nested in fields.</summary>
internal static class TestFrames
{
    /// <summary>A length-delimited field.</summary>
    public static byte[] Field(int number, byte[] value)
    {
        return [.. Varint((ulong)(number << 3 | 2)), .. Varint((ulong)value.Length), .. value];
    }

    /// <summary>A varint field.</summary>
    public static byte[] Number(int number, ulong value)
    {
        return [.. Varint((ulong)(number << 3)), .. Varint(value)];
    }

    /// <summary>A frame holding a ticket four levels down, beside other values, as measured: field 4.2.2.1.</summary>
    public static byte[] Presenting(string ticket)
    {
        var inner = Field(1, Encoding.UTF8.GetBytes(ticket));

        return [.. Number(1, 7), .. Field(4, [.. Field(1, Encoding.UTF8.GetBytes("type.test/abc")), .. Field(2, Field(2, [.. inner, .. Number(2, 3)]))])];
    }

    /// <summary>A frame with its varint length, as it goes on the wire.</summary>
    public static byte[] Framed(byte[] payload)
    {
        var writer = new ArrayBufferWriter<byte>();

        FrameWriter.Write(writer, payload);

        return writer.WrittenSpan.ToArray();
    }

    private static byte[] Varint(ulong value)
    {
        var bytes = new List<byte>();

        while (value >= 0x80)
        {
            bytes.Add((byte)(value | 0x80));
            value >>= 7;
        }

        bytes.Add((byte)value);

        return [.. bytes];
    }
}
