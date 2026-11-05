using D3B.Diagnostic.Observation;

namespace D3B.Bot.Tests.Network;

internal sealed class FakeDiagnosticSessionFactory : IDiagnosticSessionFactory
{
    public Func<IDiagnosticSink> Create { get; init; } = () => new FakeDiagnosticSink();

    public IDiagnosticSink? LastSink { get; private set; }

    public IDiagnosticSink CreateSession()
    {
        IDiagnosticSink sink = Create();
        LastSink = sink;
        return sink;
    }
}
