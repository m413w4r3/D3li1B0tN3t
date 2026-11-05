using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Reporting;

/// <summary>Terminal state of a diagnostic observation session.</summary>
public enum DiagnosticReportSessionState
{
    /// <summary>The session completed normally.</summary>
    [JsonStringEnumMemberName("COMPLETED")]
    Completed,
    /// <summary>The session failed.</summary>
    [JsonStringEnumMemberName("FAILED")]
    Failed,
    /// <summary>The session was cancelled.</summary>
    [JsonStringEnumMemberName("CANCELLED")]
    Cancelled
}
