using D3B.Network.Relay;

namespace D3B.Network.Tests.Relay;

public sealed class RelayObservationTests
{
    [Fact]
    public async Task TheObservationWaitCoversAnAdmittedClientReadUntilItsFrameIsShown()
    {
        await AssertWaitsForObservedFrameAsync(RelayDirection.FromClient);
    }

    [Fact]
    public async Task TheObservationWaitCoversAnAdmittedServerReadUntilItsFrameIsShown()
    {
        await AssertWaitsForObservedFrameAsync(RelayDirection.FromServer);
    }

    [Fact]
    public async Task TheConnectionWaitPropagatesCallerCancellation()
    {
        var interceptor = new GatedInterceptor();
        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        try
        {
            await client.SendAsync(TestFrames.Framed([0x0A]), TestContext.Current.CancellationToken);
            await interceptor.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            var wait = interceptor.Connection.WaitForObservedFramesAsync(cancellation.Token);

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        }
        finally
        {
            interceptor.Release.TrySetResult();
            client.Dispose();
            server.Dispose();
        }
    }

    [Fact]
    public async Task TheConnectionWaitFailsWhenItsSocketClosesDuringObservation()
    {
        var interceptor = new GatedInterceptor();
        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        try
        {
            await client.SendAsync(TestFrames.Framed([0x0B]), TestContext.Current.CancellationToken);
            await interceptor.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            var wait = interceptor.Connection.WaitForObservedFramesAsync(TestContext.Current.CancellationToken);

            client.Dispose();

            var result = await Task.WhenAny(wait, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

            Assert.Same(wait, result);
            await Assert.ThrowsAsync<RelayInjectionException>(() => wait);
        }
        finally
        {
            interceptor.Release.TrySetResult();
            client.Dispose();
            server.Dispose();
        }
    }

    [Fact]
    public async Task AnInterceptorFailureFailsTheConnectionWait()
    {
        var interceptor = new GatedInterceptor { FailsOnFrame = true };
        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        try
        {
            var frame = TestFrames.Framed([0x0C]);

            await client.SendAsync(frame, TestContext.Current.CancellationToken);
            await interceptor.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<RelayInjectionException>(() => interceptor.Connection.WaitForObservedFramesAsync(TestContext.Current.CancellationToken));
            Assert.Equal(frame, await TestRelay.ReadExactlyAsync(server, frame.Length));
        }
        finally
        {
            interceptor.Release.TrySetResult();
            client.Dispose();
            server.Dispose();
        }
    }

    private static async Task AssertWaitsForObservedFrameAsync(RelayDirection direction)
    {
        var interceptor = new GatedInterceptor();
        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        try
        {
            var source = direction is RelayDirection.FromClient ? client : server;
            var frame = TestFrames.Framed([0x0A, 0x01, 0x01]);

            await source.SendAsync(frame, TestContext.Current.CancellationToken);
            await interceptor.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            var wait = interceptor.Connection.WaitForObservedFramesAsync(TestContext.Current.CancellationToken);

            Assert.False(wait.IsCompleted);

            interceptor.Release.TrySetResult();
            await wait;
        }
        finally
        {
            interceptor.Release.TrySetResult();
            client.Dispose();
            server.Dispose();
        }
    }

    private sealed class GatedInterceptor : IRelayInterceptor, IConnectionInterceptor
    {
        public TaskCompletionSource<IRelayConnection> Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool FailsOnFrame { get; init; }

        public IRelayConnection Connection => Captured.Task.GetAwaiter().GetResult();

        public IConnectionInterceptor? Intercept(IRelayConnection connection)
        {
            Captured.TrySetResult(connection);

            return this;
        }

        public bool Holds(RelayDirection direction)
        {
            return false;
        }

        public byte[]? OnFrame(RelayDirection direction, System.Buffers.ReadOnlySequence<byte> frame, bool held)
        {
            Entered.TrySetResult();

            if (FailsOnFrame)
                throw new InvalidOperationException("The test interceptor failed.");

            Release.Task.GetAwaiter().GetResult();

            return null;
        }

        public void OnHoldAbandoned(RelayDirection direction, string reason)
        {
        }
    }
}
