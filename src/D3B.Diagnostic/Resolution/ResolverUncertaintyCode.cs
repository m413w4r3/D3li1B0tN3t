using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>Stable uncertainty codes emitted by the resolver.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ResolverUncertaintyCode>))]
public enum ResolverUncertaintyCode
{
    /// <summary>The readable descriptor intentionally omits fields.</summary>
    [JsonStringEnumMemberName("PARTIAL_READABLE")]
    PartialReadable,
    /// <summary>A reference is not exact to the build under analysis.</summary>
    [JsonStringEnumMemberName("BUILD_MISMATCH_REFERENCE")]
    BuildMismatchReference,
    /// <summary>Several readable descriptors have the same exact shape.</summary>
    [JsonStringEnumMemberName("AMBIGUOUS_READABLE_SHAPE")]
    AmbiguousReadableShape,
    /// <summary>Several schema tokens share one exact readable shape.</summary>
    [JsonStringEnumMemberName("AMBIGUOUS_SCHEMA_TOKEN")]
    AmbiguousSchemaToken,
    /// <summary>There is no reproducible valid observation.</summary>
    [JsonStringEnumMemberName("MISSING_VALID_OBSERVATION")]
    MissingValidObservation,
    /// <summary>Observation coverage is partial.</summary>
    [JsonStringEnumMemberName("PARTIAL_OBSERVATION")]
    PartialObservation,
    /// <summary>Several observation statuses are present.</summary>
    [JsonStringEnumMemberName("MIXED_OBSERVATION_STATUS")]
    MixedObservationStatus,
    /// <summary>Observation detail is truncated or carries a soft limit condition.</summary>
    [JsonStringEnumMemberName("OBSERVATION_TRUNCATED")]
    ObservationTruncated,
    /// <summary>Several known observation directions are present.</summary>
    [JsonStringEnumMemberName("AMBIGUOUS_OBSERVATION_DIRECTION")]
    AmbiguousObservationDirection,
    /// <summary>A build-exact pin names a different readable message.</summary>
    [JsonStringEnumMemberName("CONFLICTING_PRIOR_MAPPING")]
    ConflictingPriorMapping,
    /// <summary>No semantic identity proof was supplied for the candidate.</summary>
    [JsonStringEnumMemberName("SEMANTIC_PROOF_MISSING")]
    SemanticProofMissing,
    /// <summary>A supplied semantic identity proof is malformed or bound to another identity.</summary>
    [JsonStringEnumMemberName("SEMANTIC_PROOF_INVALID")]
    SemanticProofInvalid
}
