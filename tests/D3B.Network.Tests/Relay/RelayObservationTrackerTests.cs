using System.Buffers;
using D3B.Network.Framing;
using D3B.Network.Relay;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Network.Tests.Relay;

public sealed class RelayObservationTrackerTests
{
    private const int MaxHeldLength = 64 * 1024;

    [Fact]
    public async Task AReadWithSeveralFramesIsObservedOnlyAfterItsLastFrameReturns()
    {
        var tracker = new RelayObservationTracker();
        var interceptor = new FrameGate(2);
        var side = new InterceptedSide("test", RelayDirection.FromClient, interceptor, MaxHeldLength, NullLogger<RelayConnectionHandler>.Instance);
        var output = new ArrayBufferWriter<byte>();
        var bytes = new ArrayBufferWriter<byte>();

        FrameWriter.Write(bytes, [0x01]);
        FrameWriter.Write(bytes, [0x02]);
        var ticket = tracker.Admit();
        var read = Task.Run(() =>
        {
            var returned = false;

            try
            {
                side.Read(new ReadOnlySequence<byte>(bytes.WrittenMemory), output);
                returned = true;
            }
            finally
            {
                tracker.Observe(ticket, returned && side.IsWatched);
            }
        }, TestContext.Current.CancellationToken);

        await interceptor.BlockedFrameEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        var wait = tracker.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.False(wait.IsCompleted);
        }
        finally
        {
            interceptor.ReleaseBlockedFrame.TrySetResult();
        }

        await read;
        await wait;

        Assert.Equal(2, interceptor.FramesSeen);
    }

    [Fact]
    public async Task TheSharedWatermarkWaitsForOutOfOrderReadsFromBothDirections()
    {
        var tracker = new RelayObservationTracker();
        var first = tracker.Admit();
        var second = tracker.Admit();
        var wait = tracker.WaitAsync(TestContext.Current.CancellationToken);

        tracker.Observe(second, watched: true);

        Assert.False(wait.IsCompleted);

        tracker.Observe(first, watched: true);
        await wait;
    }

    [Fact]
    public async Task ALaterAdmissionDoesNotExtendTheCapturedWatermark()
    {
        var tracker = new RelayObservationTracker();
        var included = tracker.Admit();
        var wait = tracker.WaitAsync(TestContext.Current.CancellationToken);
        var concurrent = tracker.Admit();
        var concurrentWait = tracker.WaitAsync(TestContext.Current.CancellationToken);

        tracker.Observe(included, watched: true);
        await wait;

        Assert.False(concurrentWait.IsCompleted);

        tracker.Observe(concurrent, watched: true);
        await concurrentWait;
    }

    [Fact]
    public async Task AClosedConnectionFailsTheObservationWait()
    {
        var tracker = new RelayObservationTracker();
        tracker.Admit();
        var wait = tracker.WaitAsync(TestContext.Current.CancellationToken);

        tracker.Fail("the test connection closed");

        var exception = await Assert.ThrowsAsync<RelayInjectionException>(() => wait);

        Assert.Contains("connection closed", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFragmentIsObservedAsAReadAndItsCompletingReadWaitsForTheFrame()
    {
        var tracker = new RelayObservationTracker();
        var interceptor = new FrameGate(1);
        var side = new InterceptedSide("test", RelayDirection.FromClient, interceptor, MaxHeldLength, NullLogger<RelayConnectionHandler>.Instance);
        var output = new ArrayBufferWriter<byte>();
        var frame = new ArrayBufferWriter<byte>();

        FrameWriter.Write(frame, [0x01, 0x02]);
        var firstTicket = tracker.Admit();
        side.Read(new ReadOnlySequence<byte>(frame.WrittenMemory[..^1]), output);
        tracker.Observe(firstTicket, watched: side.IsWatched);

        await tracker.WaitAsync(TestContext.Current.CancellationToken);

        var finalByte = frame.WrittenMemory[^1..].ToArray();
        var completingTicket = tracker.Admit();
        var completingRead = Task.Run(() =>
        {
            var returned = false;

            try
            {
                side.Read(new ReadOnlySequence<byte>(finalByte), output);
                returned = true;
            }
            finally
            {
                tracker.Observe(completingTicket, returned && side.IsWatched);
            }
        }, TestContext.Current.CancellationToken);

        await interceptor.BlockedFrameEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        var completingWait = tracker.WaitAsync(TestContext.Current.CancellationToken);

        try
        {
            Assert.False(completingWait.IsCompleted);
        }
        finally
        {
            interceptor.ReleaseBlockedFrame.TrySetResult();
        }

        await completingRead;
        await completingWait;

        Assert.Equal(1, interceptor.FramesSeen);
    }

    [Fact]
    public async Task ABlockedConnectionDoesNotHoldAnotherConnectionsObservationWait()
    {
        var blockedConnection = new RelayObservationTracker();
        var independentConnection = new RelayObservationTracker();
        var blockedTicket = blockedConnection.Admit();
        var independentTicket = independentConnection.Admit();
        var blockedWait = blockedConnection.WaitAsync(TestContext.Current.CancellationToken);
        var independentWait = independentConnection.WaitAsync(TestContext.Current.CancellationToken);

        independentConnection.Observe(independentTicket, watched: true);
        await independentWait;

        Assert.False(blockedWait.IsCompleted);

        blockedConnection.Observe(blockedTicket, watched: true);
        await blockedWait;
    }

    [Fact]
    public async Task CancellingOneWaitDoesNotChangeTheConnectionFence()
    {
        var tracker = new RelayObservationTracker();
        var ticket = tracker.Admit();
        using var cancellation = new CancellationTokenSource();
        var wait = tracker.WaitAsync(cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);

        tracker.Observe(ticket, watched: true);
        await tracker.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AnUnframedReadFailsTheObservationFence()
    {
        var tracker = new RelayObservationTracker();
        var interceptor = new FrameGate(2);
        var side = new InterceptedSide("test", RelayDirection.FromClient, interceptor, MaxHeldLength, NullLogger<RelayConnectionHandler>.Instance);
        var ticket = tracker.Admit();
        byte[] invalid = [0xFF, 0xFF, 0xFF, 0xFF, 0x7F];

        side.Read(new ReadOnlySequence<byte>(invalid), new ArrayBufferWriter<byte>());
        tracker.Observe(ticket, side.IsWatched);

        await Assert.ThrowsAsync<RelayInjectionException>(() => tracker.WaitAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FrameGate : IConnectionInterceptor
    {
        private readonly int _frameToBlock;
        private int _framesSeen;

        public TaskCompletionSource BlockedFrameEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseBlockedFrame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int FramesSeen => Volatile.Read(ref _framesSeen);

        public FrameGate(int frameToBlock)
        {
            _frameToBlock = frameToBlock;
        }

        public bool Holds(RelayDirection direction)
        {
            return false;
        }

        public byte[]? OnFrame(RelayDirection direction, ReadOnlySequence<byte> frame, bool held)
        {
            if (Interlocked.Increment(ref _framesSeen) == _frameToBlock)
            {
                BlockedFrameEntered.TrySetResult();
                ReleaseBlockedFrame.Task.GetAwaiter().GetResult();
            }

            return null;
        }

        public void OnHoldAbandoned(RelayDirection direction, string reason)
        {
        }
    }
}
