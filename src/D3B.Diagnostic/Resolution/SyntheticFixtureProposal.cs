using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>A minimal payload proposal made entirely from zero and empty values.</summary>
public sealed record SyntheticFixtureProposal
{
    /// <summary>Schema message for the fixture.</summary>
    public string SchemaMessage { get; init; } = string.Empty;

    /// <summary>Status expected from the diagnostic engine.</summary>
    public string ExpectedStatus { get; init; } = "VALID";

    /// <summary>Expected top-level field numbers.</summary>
    public IReadOnlyList<int> ExpectedFieldNumbers { get; init; } = Array.Empty<int>();

    /// <summary>True because this proposal contains only constructed synthetic bytes.</summary>
    public bool Synthetic { get; init; }

    /// <summary>Lowercase hex for the generated synthetic payload.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string PayloadHex { get; init; } = string.Empty;
}
