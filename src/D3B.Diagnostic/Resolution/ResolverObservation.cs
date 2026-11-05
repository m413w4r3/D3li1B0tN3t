using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Resolution;

/// <summary>One redacted aggregate observation for a build schema token.</summary>
public sealed record ResolverObservation
{
    /// <summary>Build schema token associated with the observation.</summary>
    public string Token { get; init; } = string.Empty;
    /// <summary>Number of occurrences represented by the aggregate.</summary>
    public long Occurrences { get; init; }
    /// <summary>Direction counts represented by the aggregate.</summary>
    public IReadOnlyList<ResolverObservationDirection> Directions { get; init; } = Array.Empty<ResolverObservationDirection>();
    /// <summary>Status counts represented by the aggregate.</summary>
    public IReadOnlyList<ResolverObservationStatus> Statuses { get; init; } = Array.Empty<ResolverObservationStatus>();
    /// <summary>Occurrences with a valid type URL.</summary>
    public long TypeUrlOkOccurrences { get; init; }
    /// <summary>Aggregated schema problem counts.</summary>
    public IReadOnlyList<ResolverObservationProblem> Problems { get; init; } = Array.Empty<ResolverObservationProblem>();
    /// <summary>Whether report or analysis detail was truncated.</summary>
    public bool Truncated { get; init; }
    /// <summary>Whether every occurrence is a complete valid observation.</summary>
    public bool FullyValid
    {
        get
        {
            ResolverObservationDirection[] directions = Directions.Where(direction => direction.Occurrences > 0).ToArray();
            ResolverObservationStatus[] statuses = Statuses.Where(status => status.Occurrences > 0).ToArray();
            return Occurrences > 0
                && TypeUrlOkOccurrences == Occurrences
                && !Truncated
                && directions.Length == 1
                && directions[0].Direction is "client" or "server" or "d3b"
                && directions[0].Occurrences == Occurrences
                && statuses.Length == 1
                && statuses[0].Status == DiagnosticStatus.Valid
                && statuses[0].Occurrences == Occurrences
                && !Problems.Any(problem => IsHardProblem(problem.Code) || IsSoftProblem(problem.Code));
        }
    }

    private static bool IsHardProblem(DiagnosticProblemCode code)
    {
        return code is DiagnosticProblemCode.UnknownType
            or DiagnosticProblemCode.UnknownFieldNumber
            or DiagnosticProblemCode.WireTypeMismatch
            or DiagnosticProblemCode.PackedNotAllowed
            or DiagnosticProblemCode.InvalidUtf8
            or DiagnosticProblemCode.GroupUnsupported
            or DiagnosticProblemCode.LengthOverflow
            or DiagnosticProblemCode.VarintOverflow
            or DiagnosticProblemCode.Malformed
            or DiagnosticProblemCode.SchemaMessageMissing
            or DiagnosticProblemCode.SchemaInvalid;
    }

    private static bool IsSoftProblem(DiagnosticProblemCode code)
    {
        return code is DiagnosticProblemCode.DepthExceeded
            or DiagnosticProblemCode.Truncated
            or DiagnosticProblemCode.LimitExceeded;
    }
}

/// <summary>Count of observations for one supported direction.</summary>
public sealed record ResolverObservationDirection
{
    /// <summary>Observed direction: client, server, d3b or unknown.</summary>
    public string Direction { get; init; } = "unknown";
    /// <summary>Number of occurrences in this direction.</summary>
    public long Occurrences { get; init; }
}

/// <summary>Count of observations with one diagnostic status.</summary>
public sealed record ResolverObservationStatus
{
    /// <summary>Observed diagnostic status.</summary>
    public DiagnosticStatus Status { get; init; }
    /// <summary>Number of occurrences with this status.</summary>
    public long Occurrences { get; init; }
}

/// <summary>Count of one redacted schema problem, optionally at a safe field path.</summary>
public sealed record ResolverObservationProblem
{
    /// <summary>Observed problem code.</summary>
    public DiagnosticProblemCode Code { get; init; }
    /// <summary>Sanitized field path, when present.</summary>
    public string? FieldPath { get; init; }
    /// <summary>Number of occurrences with this problem.</summary>
    public long Occurrences { get; init; }
}
