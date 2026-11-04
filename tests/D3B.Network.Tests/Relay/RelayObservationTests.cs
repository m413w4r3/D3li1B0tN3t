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

    private static async Task AssertWaitsForObservedFrameAsync(RelayDirection direction)
    {
        var interceptor = new GatedInterceptor();
        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();
        Task? wait = null;

        try
        {
            var source = direction is RelayDirection.FromClient ? client : server;
            var frame = TestFrames.Framed([0x0A, 0x01, 0x01]);

            await source.SendAsync(frame, TestContext.Current.CancellationToken);
            await interceptor.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);

            wait = interceptor.Connection.WaitForObservedFramesAsync(TestContext.Current.CancellationToken);

            Assert.False(wait.IsCompleted);
        }
        finally
        {
            interceptor.Release.TrySetResult();
            client.Dispose();
            server.Dispose();
        }

        await wait!;
    }

    private sealed class GatedInterceptor : IRelayInterceptor, IConnectionInterceptor
    {
        public TaskCompletionSource<IRelayConnection> Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            Release.Task.GetAwaiter().GetResult();

            return null;
        }

        public void OnHoldAbandoned(RelayDirection direction, string reason)
        {
        }
    }
}
