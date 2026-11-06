using System.Text.Json;
using System.Text.Json.Serialization;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Reporting;

/// <summary>Source-generated serializer metadata for deterministic report JSON.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(DiagnosticReport))]
[JsonSerializable(typeof(DiagnosticReportEngine))]
[JsonSerializable(typeof(DiagnosticReportProvenance))]
[JsonSerializable(typeof(DiagnosticReportSession))]
[JsonSerializable(typeof(DiagnosticReportType))]
[JsonSerializable(typeof(DiagnosticReportDirectionCount))]
[JsonSerializable(typeof(DiagnosticReportStatusCount))]
[JsonSerializable(typeof(DiagnosticReportProblemCount))]
[JsonSerializable(typeof(DiagnosticReportField))]
[JsonSerializable(typeof(DiagnosticReportFieldProblemCount))]
[JsonSerializable(typeof(DiagnosticReportVariant))]
[JsonSerializable(typeof(DiagnosticReportContradiction))]
[JsonSerializable(typeof(DiagnosticReportTruncation))]
internal sealed partial class DiagnosticReportJsonContext : JsonSerializerContext
{
    /// <summary>Gets or initializes the ReportOptions value.</summary>
    internal static JsonSerializerOptions ReportOptions { get; } = CreateOptions();

    /// <summary>Gets or initializes the Protocol value.</summary>
    internal static DiagnosticReportJsonContext Protocol { get; } = new(ReportOptions);

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticStatus>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticProblemCode>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticErrorCode>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticFieldKind>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticWireType>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticReportSessionState>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticReportContradictionCode>(allowIntegerValues: false));
        return options;
    }
}
