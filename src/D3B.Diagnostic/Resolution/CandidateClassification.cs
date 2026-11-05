using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>Resolver outcome independent of heuristic ranking.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CandidateClassification>))]
public enum CandidateClassification
{
    /// <summary>Unique build-exact correspondence has reproducible validation.</summary>
    [JsonStringEnumMemberName("VERIFIED")]
    Verified,
    /// <summary>A plausible correspondence is missing proof or is ambiguous.</summary>
    [JsonStringEnumMemberName("CANDIDATE")]
    Candidate,
    /// <summary>At least one hard contradiction exists.</summary>
    [JsonStringEnumMemberName("CONFLICT")]
    Conflict,
    /// <summary>No readable message can be named.</summary>
    [JsonStringEnumMemberName("UNKNOWN")]
    Unknown
}
