using System.Net.Sockets;

namespace D3B.Bot.Tests.Network;

/// <summary>Reads everything a socket receives in the background, so that a test waits for as many bytes as it expects.</summary>
internal sealed class SocketReceiver
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly Socket _socket;
    private readonly List<byte> _received = [];
    private readonly Lock _lock = new();
    private readonly Task _reading;

    public byte[] Received
    {
        get
        {
            lock (_lock)
                return [.. _received];
        }
    }

    public SocketReceiver(Socket socket)
    {
        _socket = socket;
        _reading = Task.Run(ReadAsync);
    }

    /// <summary>Waits until the socket has received at least so many bytes.</summary>
    public async Task WaitForAsync(long count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(_timeout);

        while (Received.Length < count)
            await Task.Delay(5, timeout.Token);
    }

    /// <summary>Waits until the other side closed, and returns everything received.</summary>
    public async Task<byte[]> ToEndAsync()
    {
        await _reading.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        return Received;
    }

    private async Task ReadAsync()
    {
        var buffer = new byte[8_192];

        while (true)
        {
            int read;

            try
            {
                read = await _socket.ReceiveAsync(buffer, TestContext.Current.CancellationToken);
            }
            catch (SocketException)
            {
                return;
            }

            if (read is 0)
                return;

            lock (_lock)
                _received.AddRange(buffer.AsSpan(0, read));
        }
    }
}
