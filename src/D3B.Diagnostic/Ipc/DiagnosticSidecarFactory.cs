using System.Net.Sockets;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Ipc;

/// <summary>Starts the configured engine and connects a protocol client.</summary>
public sealed class DiagnosticSidecarFactory : IDiagnosticSidecarFactory
{
    /// <inheritdoc />
    public async Task<SidecarStartResult> StartAsync(
        SidecarOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        SidecarProcessStartResult started = await SidecarProcess.StartAsync(options, cancellationToken).ConfigureAwait(false);
        if (started.Process is null)
        {
            return new SidecarStartResult(null, started.ErrorCode ?? DiagnosticErrorCode.IoError);
        }

        SidecarProcess process = started.Process;
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.ConnectTimeout);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(process.SocketPath), timeout.Token).ConfigureAwait(false);
            var stream = new NetworkStream(socket, ownsSocket: true);
            return new SidecarStartResult(new DiagnosticSidecarClient(stream, options, process), null);
        }
        catch (OperationCanceledException)
        {
            socket.Dispose();
            await process.DisposeAsync().ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new SidecarStartResult(null, DiagnosticErrorCode.SocketFailed);
        }
        catch (Exception)
        {
            socket.Dispose();
            await process.DisposeAsync().ConfigureAwait(false);
            return new SidecarStartResult(null, DiagnosticErrorCode.SocketFailed);
        }
    }
}
