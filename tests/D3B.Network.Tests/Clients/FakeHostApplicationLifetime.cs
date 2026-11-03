using Microsoft.Extensions.Hosting;

namespace D3B.Network.Tests.Clients;

/// <summary>A host lifetime a test watches, which records the stop a redirect asks for.</summary>
internal sealed class FakeHostApplicationLifetime : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private readonly CancellationTokenSource _stopped = new();
    private volatile bool _stopApplicationCalled;

    public bool StopApplicationCalled => _stopApplicationCalled;

    public CancellationToken ApplicationStarted => CancellationToken.None;

    public CancellationToken ApplicationStopping => _stopping.Token;

    public CancellationToken ApplicationStopped => _stopped.Token;

    public void StopApplication()
    {
        _stopApplicationCalled = true;
        _stopping.Cancel();
        _stopped.Cancel();
    }

    public void Dispose()
    {
        _stopping.Dispose();
        _stopped.Dispose();
    }
}
