namespace D3B.Diagnostic.Contract;

/// <summary>Constants that define diagnostic protocol version 1.</summary>
public static class DiagnosticProtocol
{
    /// <summary>The supported protocol version.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>The prefix used by Ankama Any type URLs.</summary>
    public const string TypeUrlPrefix = "type.ankama.com/";

    /// <summary>The maximum frame payload size, in bytes.</summary>
    public const int FramePayloadLimit = 1_048_576;

    /// <summary>The maximum JSON line size, in bytes.</summary>
    public const int MessageLimit = 1_048_576;

    /// <summary>The maximum number of schema messages.</summary>
    public const int MaxSchemaMessages = 20_000;

    /// <summary>The maximum number of fields in a message.</summary>
    public const int MaxFieldsPerMessage = 4_096;

    /// <summary>The maximum nested message depth.</summary>
    public const int MaxDepth = 32;
}
