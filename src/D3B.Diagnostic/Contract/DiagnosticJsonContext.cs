using System.Text.Json;
using System.Text.Json.Serialization;

namespace D3B.Diagnostic.Contract;

/// <summary>Source-generated serializers for protocol-v1 DTOs.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(DiagnosticSchema))]
[JsonSerializable(typeof(DiagnosticMessage))]
[JsonSerializable(typeof(DiagnosticField))]
[JsonSerializable(typeof(DiagnosticHello))]
[JsonSerializable(typeof(DiagnosticEngineHello))]
[JsonSerializable(typeof(DiagnosticAnalysis))]
[JsonSerializable(typeof(DiagnosticFieldAnalysis))]
[JsonSerializable(typeof(DiagnosticObservedShape))]
[JsonSerializable(typeof(DiagnosticProblem))]
[JsonSerializable(typeof(DiagnosticSummary))]
[JsonSerializable(typeof(DiagnosticError))]
[JsonSerializable(typeof(DiagnosticEnd))]
public sealed partial class DiagnosticJsonContext : JsonSerializerContext
{
    /// <summary>Options used for compact, strict protocol serialization.</summary>
    public static JsonSerializerOptions ProtocolOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.General);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.WriteIndented = false;
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticStatus>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticProblemCode>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticErrorCode>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticFieldKind>(allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<DiagnosticWireType>(allowIntegerValues: false));
        return options;
    }

    /// <summary>Shared source-generated context instance.</summary>
    public static DiagnosticJsonContext Protocol { get; } = new(ProtocolOptions);
}
