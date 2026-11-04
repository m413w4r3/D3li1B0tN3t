using System.Buffers;
using System.IO.Pipelines;
using D3B.Network.Relay;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Network.Tests.Relay;

public sealed class InjectedFramesTests
{
    [Fact]
    public async Task AQueuedFrameIsRejectedWhenItsConnectionIsNoLongerObservable()
    {
        var pipe = new Pipe();
        var injected = new InjectedFrames("test", pipe.Writer, capture: null, NullLogger<RelayConnectionHandler>.Instance);
        var side = new InterceptedSide("test", RelayDirection.FromClient, new PassiveInterceptor(), 64 * 1024, NullLogger<RelayConnectionHandler>.Instance);
        var sideOutput = new ArrayBufferWriter<byte>();
        var valid = true;

        try
        {
            side.Read(new ReadOnlySequence<byte>(new byte[] { 0x01 }), sideOutput);
            await injected.WatchAsync(side);

            var sending = injected.SendAsync(new byte[] { 0x22 }, TestContext.Current.CancellationToken, () => valid);

            Assert.False(sending.IsCompleted);
            valid = false;

            await injected.EnterAsync();

            try
            {
                side.Read(new ReadOnlySequence<byte>(new byte[] { 0x22 }), sideOutput);
                injected.Drain();
                injected.Flushed(await pipe.Writer.FlushAsync(TestContext.Current.CancellationToken));
            }
            finally
            {
                injected.Exit();
            }

            await Assert.ThrowsAsync<RelayInjectionException>(() => sending);
            Assert.Equal(0, injected.Sent);

            var read = await pipe.Reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.True(read.Buffer.IsEmpty);
            pipe.Reader.AdvanceTo(read.Buffer.End);
        }
        finally
        {
            await pipe.Reader.CompleteAsync();
            await pipe.Writer.CompleteAsync();
        }
    }

    private sealed class PassiveInterceptor : IConnectionInterceptor
    {
        public bool Holds(RelayDirection direction)
        {
            return false;
        }

        public byte[]? OnFrame(RelayDirection direction, ReadOnlySequence<byte> frame, bool held)
        {
            return null;
        }

        public void OnHoldAbandoned(RelayDirection direction, string reason)
        {
        }
    }
}
