namespace D3B.Diagnostic.Contract;

/// <summary>A protobuf field descriptor in the exported schema.</summary>
public sealed record DiagnosticField
{
    /// <summary>The protobuf field number.</summary>
    public int Number { get; init; }
    /// <summary>The obfuscated protobuf field name.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>The protobuf field kind.</summary>
    public DiagnosticFieldKind Kind { get; init; }
    /// <summary>Whether the field is repeated.</summary>
    public bool Repeated { get; init; }
    /// <summary>Whether the descriptor declares packed encoding.</summary>
    public bool Packed { get; init; }
    /// <summary>Full name of the message type, when applicable.</summary>
    public string? MessageType { get; init; }
    /// <summary>Full name of the enum type, when applicable.</summary>
    public string? EnumType { get; init; }
    /// <summary>Name of the real oneof, when applicable.</summary>
    public string? Oneof { get; init; }
    /// <summary>Whether the field has presence semantics.</summary>
    public bool HasPresence { get; init; }
}
