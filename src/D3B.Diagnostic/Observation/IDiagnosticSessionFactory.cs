namespace D3B.Diagnostic.Observation;

/// <summary>Creates an independent diagnostic session for each game connection.</summary>
public interface IDiagnosticSessionFactory
{
    /// <summary>Creates and starts a bounded diagnostic session.</summary>
    /// <returns>The session's nonblocking sink.</returns>
    IDiagnosticSink CreateSession();
}
