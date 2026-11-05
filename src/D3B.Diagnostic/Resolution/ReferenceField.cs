using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Resolution;

/// <summary>A field shape attached to a reference message.</summary>
public sealed record ReferenceField
{
    /// <summary>Protobuf field number.</summary>
    public int Number { get; init; }

    /// <summary>Exact protobuf kind.</summary>
    public DiagnosticFieldKind Kind { get; init; }

    /// <summary>Whether the field is repeated.</summary>
    public bool Repeated { get; init; }

    /// <summary>Referenced message full name for message fields.</summary>
    public string? MessageType { get; init; }
}
