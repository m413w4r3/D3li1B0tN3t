using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Resolution;

/// <summary>Source-generated serializer metadata for deterministic candidate reports.</summary>
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Serialization,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CandidateReport))]
internal partial class CandidateReportJsonContext : JsonSerializerContext
{
}
