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
    AmbiguousObservationDirection
}
