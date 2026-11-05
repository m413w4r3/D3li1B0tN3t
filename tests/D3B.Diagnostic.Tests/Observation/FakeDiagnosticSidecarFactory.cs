using D3B.Diagnostic.Ipc;

namespace D3B.Diagnostic.Tests.Observation;

internal sealed class FakeDiagnosticSidecarFactory : IDiagnosticSidecarFactory
{
    private readonly IDiagnosticSidecar? _sidecar;
    private readonly D3B.Diagnostic.Contract.DiagnosticErrorCode? _errorCode;

    public FakeDiagnosticSidecarFactory(IDiagnosticSidecar sidecar)
    {
        _sidecar = sidecar;
    }

    public FakeDiagnosticSidecarFactory(D3B.Diagnostic.Contract.DiagnosticErrorCode errorCode)
    {
        _errorCode = errorCode;
    }

    public Task<SidecarStartResult> StartAsync(SidecarOptions options, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SidecarStartResult(_sidecar, _errorCode));
    }
}
