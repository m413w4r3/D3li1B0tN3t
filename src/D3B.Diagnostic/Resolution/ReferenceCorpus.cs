namespace D3B.Diagnostic.Resolution;

/// <summary>A read-only collection of named protocol message references.</summary>
public sealed record ReferenceCorpus
{
    /// <summary>Source label used for provenance.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Build named by this corpus, or an explicit unknown marker.</summary>
    public string Build { get; init; } = "other/unknown";

    /// <summary>Whether this corpus is known to describe exactly one client build.</summary>
    public bool BuildExact { get; init; }

    /// <summary>Messages contained by this corpus.</summary>
    public IReadOnlyList<ReferenceMessage> Messages { get; init; } = Array.Empty<ReferenceMessage>();
}
