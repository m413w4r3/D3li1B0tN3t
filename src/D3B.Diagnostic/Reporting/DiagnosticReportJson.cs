namespace D3B.Diagnostic.Reporting;

/// <summary>Deterministic, bounded JSON operations for diagnostic reports.</summary>
public static class DiagnosticReportJson
{
/// <summary>Provides the SerializeToUtf8Bytes report operation.</summary>
    public static byte[] SerializeToUtf8Bytes(DiagnosticReport report, DiagnosticReportOptions options)
    {
        throw new NotImplementedException();
    }

/// <summary>Provides the Serialize report operation.</summary>
    public static string Serialize(DiagnosticReport report, DiagnosticReportOptions options)
    {
        throw new NotImplementedException();
    }

/// <summary>Provides the LoadFile report operation.</summary>
    public static LoadedDiagnosticReport LoadFile(string path, DiagnosticReportOptions? options = null)
    {
        throw new NotImplementedException();
    }

/// <summary>Provides the Load report operation.</summary>
    public static LoadedDiagnosticReport Load(byte[] bytes, DiagnosticReportOptions? options = null)
    {
        throw new NotImplementedException();
    }
}
