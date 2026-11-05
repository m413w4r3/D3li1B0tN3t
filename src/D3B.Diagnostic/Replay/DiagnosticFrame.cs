using System.Text;

namespace D3B.Diagnostic.Replay;

/// <summary>A redacted replay frame and its payload bytes.</summary>
public sealed record DiagnosticFrame
{
    /// <summary>Creates a replay frame.</summary>
    public DiagnosticFrame(long index, string direction, string? typeUrl, string? carriedType, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(direction);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (direction is not ("client" or "server" or "d3b"))
        {
            throw new ArgumentException("The frame direction is invalid.", nameof(direction));
        }

        if (payload.Length > 1_048_576)
        {
            throw new ArgumentException("The frame payload exceeds the protocol limit.", nameof(payload));
        }

        if (typeUrl is not null && Encoding.UTF8.GetByteCount(typeUrl) > 256)
        {
            throw new ArgumentException("The frame type URL exceeds the protocol limit.", nameof(typeUrl));
        }

        if (carriedType is not null
            && (carriedType.Length is 0 or > 128
                || !carriedType.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_')))
        {
            throw new ArgumentException("The frame carried type is invalid.", nameof(carriedType));
        }

        Index = index;
        Direction = direction;
        TypeUrl = typeUrl;
        CarriedType = carriedType;
        Payload = payload;
    }

    /// <summary>The unique, increasing replay index.</summary>
    public long Index { get; }

    /// <summary>The frame direction.</summary>
    public string Direction { get; }

    /// <summary>The observed envelope type URL, if present.</summary>
    public string? TypeUrl { get; }

    /// <summary>The carried message token, if present.</summary>
    public string? CarriedType { get; }

    /// <summary>The Any value bytes.</summary>
    public byte[] Payload { get; }
}
