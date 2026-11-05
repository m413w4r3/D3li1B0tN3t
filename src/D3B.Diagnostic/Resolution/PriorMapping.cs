using D3B.Protocol.Mapping;

namespace D3B.Diagnostic.Resolution;

/// <summary>A build-pinned readable-to-obfuscated pair, used as corroboration only.</summary>
public sealed record PriorMapping
{
    /// <summary>Readable protobuf full name.</summary>
    public string ReadableFullName { get; init; } = string.Empty;

    /// <summary>Obfuscated schema token or full name.</summary>
    public string ObfuscatedToken { get; init; } = string.Empty;

    /// <summary>Build associated with the pin.</summary>
    public string Build { get; init; } = string.Empty;

    /// <summary>Whether the pin names one exact build.</summary>
    public bool BuildExact { get; init; }

    /// <summary>Converts pins to corroborating prior mappings.</summary>
    public static IReadOnlyList<PriorMapping> From(PinnedPairs pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        return pins.Messages.Select(pin => new PriorMapping
        {
            ReadableFullName = pin.Readable,
            ObfuscatedToken = pin.Obfuscated,
            Build = pins.ClientVersion,
            BuildExact = true
        }).ToArray();
    }
}
