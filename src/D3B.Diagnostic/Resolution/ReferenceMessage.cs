namespace D3B.Diagnostic.Resolution;

/// <summary>A reference from an obfuscated token to a protocol message name.</summary>
public sealed record ReferenceMessage
{
    /// <summary>The obfuscated message token.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>The referenced message name, possibly including an external namespace.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Evidence source name.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Build associated with the reference.</summary>
    public string Build { get; init; } = "other/unknown";

    /// <summary>Whether the reference is exact to one build.</summary>
    public bool BuildExact { get; init; }

    /// <summary>Known field structure, when supplied by a normalized corpus.</summary>
    public IReadOnlyList<ReferenceField> Fields { get; init; } = Array.Empty<ReferenceField>();

    /// <summary>Optional semantic identity proof carried by a normalized corpus.</summary>
    public SemanticProof? SemanticProof { get; init; }
}
