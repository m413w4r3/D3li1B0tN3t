using System.Net.Sockets;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Relay;

namespace D3B.Network.Tests.Relay;

public sealed class RelayInjectionTests
{
    private static byte[] Payload(int length, int seed)
    {
        var bytes = new byte[length];

        new Random(seed).NextBytes(bytes);

        return bytes;
    }

    private static Task SendAdmittedAsync(IRelayConnection connection, byte[] frame, Func<bool> stillAdmitted)
    {
        var admission = new RelaySendAdmission(1, frame, stillAdmitted);

        return ((IRelayAdmissionConnection)connection).SendAdmittedAsync(frame, admission, TestContext.Current.CancellationToken);
    }

    private static async Task<IRelayConnection> ConnectionAsync(RecordingInterceptor interceptor)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        while (interceptor.Connections.Count is 0)
            await Task.Delay(10, timeout.Token);

        return interceptor.Connections[0];
    }

    [Fact]
    public async Task PublicSendCannotSupplyAnAlwaysTrueAdmissionAndClientFramesStillPass()
    {
        var interceptor = new RecordingInterceptor();
        var first = TestFrames.Framed(Payload(18, 18));
        var next = TestFrames.Framed(Payload(16, 19));

        Assert.DoesNotContain(typeof(IRelayConnection).GetMethods(), method => method.GetParameters()
            .Any(parameter => parameter.ParameterType == typeof(Func<bool>)));

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);

            await client.SendAsync(first, TestContext.Current.CancellationToken);
            Assert.Equal(first, await TestRelay.ReadExactlyAsync(server, first.Length));

            ((IRelayAdmissionModeController)connection).SetAdmissionMode(1, requiresTypedAdmission: true);

            await Assert.ThrowsAsync<RelayInjectionException>(() => connection.SendToServerAsync(
                Payload(10, 20),
                TestContext.Current.CancellationToken));

            await client.SendAsync(next, TestContext.Current.CancellationToken);
            Assert.Equal(next, await TestRelay.ReadExactlyAsync(server, next.Length));
        }
    }

    [Fact]
    public async Task AnAdmissionCapabilityCannotBeUsedForAnotherFrame()
    {
        var interceptor = new RecordingInterceptor();
        var allowed = Payload(14, 21);
        var other = Payload(14, 22);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);
            ((IRelayAdmissionModeController)connection).SetAdmissionMode(1, requiresTypedAdmission: true);
            var admission = new RelaySendAdmission(1, allowed, () => true);

            await Assert.ThrowsAsync<RelayInjectionException>(() => ((IRelayAdmissionConnection)connection)
                .SendAdmittedAsync(other, admission, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task AFrameOfD3BsGoesToTheServerBetweenTheClientsFrames()
    {
        var interceptor = new RecordingInterceptor();
        var first = TestFrames.Framed(Payload(30, 1));
        var injected = Payload(40, 2);
        var second = TestFrames.Framed(Payload(50, 3));

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(first, TestContext.Current.CancellationToken);
            Assert.Equal(first, await TestRelay.ReadExactlyAsync(server, first.Length));

            await (await ConnectionAsync(interceptor)).SendToServerAsync(injected, TestContext.Current.CancellationToken);
            await client.SendAsync(second, TestContext.Current.CancellationToken);

            byte[] expected = [.. TestFrames.Framed(injected), .. second];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }

        // The interceptor sees what the client sent, not what D3B did.
        Assert.Equal(2, interceptor.Frames.Count);
    }

    [Fact]
    public async Task AFrameSentWhileTheClientsFrameIsHalfGoneWaitsForItsEnd()
    {
        var interceptor = new RecordingInterceptor();
        var frame = TestFrames.Framed(Payload(300, 4));
        var injected = Payload(20, 5);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(frame.AsMemory(0, 100), TestContext.Current.CancellationToken);
            Assert.Equal(frame[..100], await TestRelay.ReadExactlyAsync(server, 100));

            var sending = (await ConnectionAsync(interceptor)).SendToServerAsync(injected, TestContext.Current.CancellationToken);

            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(sending.IsCompleted);

            await client.SendAsync(frame.AsMemory(100), TestContext.Current.CancellationToken);
            await sending;

            byte[] expected = [.. frame[100..], .. TestFrames.Framed(injected)];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }
    }

    [Fact]
    public async Task FramesSentOneAfterAnotherGoInThatOrder()
    {
        var interceptor = new RecordingInterceptor();
        var frames = Enumerable.Range(0, 20).Select(index => Payload(10 + index, 100 + index)).ToList();

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);

            foreach (var frame in frames)
                await connection.SendToServerAsync(frame, TestContext.Current.CancellationToken);

            byte[] expected = [.. frames.SelectMany(TestFrames.Framed)];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }
    }

    [Fact]
    public async Task GuiAdmissionPreservesClientAndServerBytesAndRejectsRawInjection()
    {
        var interceptor = new RecordingInterceptor();
        var first = TestFrames.Framed(Payload(24, 20));
        var injected = Payload(18, 21);
        var second = TestFrames.Framed(Payload(31, 22));
        var reply = Payload(27, 23);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);

            await client.SendAsync(first, TestContext.Current.CancellationToken);
            Assert.Equal(first, await TestRelay.ReadExactlyAsync(server, first.Length));

            ((IRelayAdmissionModeController)connection).SetAdmissionMode(1, requiresTypedAdmission: true);

            await Assert.ThrowsAsync<RelayInjectionException>(() => connection.SendToServerAsync(
                Payload(12, 24),
                TestContext.Current.CancellationToken));

            await SendAdmittedAsync(connection, injected, () => true);
            Assert.Equal(TestFrames.Framed(injected), await TestRelay.ReadExactlyAsync(server, TestFrames.Framed(injected).Length));

            await client.SendAsync(second, TestContext.Current.CancellationToken);
            var forwardedSecond = await TestRelay.ReadExactlyAsync(server, second.Length);
            Assert.Equal(second, forwardedSecond);

            await server.SendAsync(reply, TestContext.Current.CancellationToken);
            Assert.Equal(reply, await TestRelay.ReadExactlyAsync(client, reply.Length));
        }
    }

    [Fact]
    public async Task AQueuedTypedFrameIsRejectedWhenAdmissionChangesBeforeTheClientFrameEnds()
    {
        var interceptor = new RecordingInterceptor();
        var clientFrame = TestFrames.Framed(Payload(250, 25));
        var secondFrame = TestFrames.Framed(Payload(22, 26));
        var injected = Payload(19, 27);
        var admitted = true;

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);

            ((IRelayAdmissionModeController)connection).SetAdmissionMode(1, requiresTypedAdmission: true);
            await client.SendAsync(clientFrame.AsMemory(0, 80), TestContext.Current.CancellationToken);
            Assert.Equal(clientFrame[..80], await TestRelay.ReadExactlyAsync(server, 80));

            var sending = SendAdmittedAsync(connection, injected, () => admitted);

            Assert.False(sending.IsCompleted);
            admitted = false;
            await client.SendAsync(clientFrame.AsMemory(80), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<RelayInjectionException>(() => sending);

            await client.SendAsync(secondFrame, TestContext.Current.CancellationToken);
            var forwardedFrames = await TestRelay.ReadExactlyAsync(server, clientFrame.Length - 80 + secondFrame.Length);
            byte[] expected = [.. clientFrame[80..], .. secondFrame];
            Assert.Equal(expected, forwardedFrames);
        }
    }

    [Fact]
    public async Task AModeChangeThatWinsTheFinalAdmissionLockRejectsTheQueuedFrame()
    {
        var interceptor = new RecordingInterceptor();
        var clientFrame = TestFrames.Framed(Payload(190, 31));
        var injected = Payload(17, 32);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);
            var relayConnection = Assert.IsType<RelayConnection>(connection);
            var mode = (IRelayAdmissionModeController)connection;
            mode.SetAdmissionMode(1, requiresTypedAdmission: true);

            await client.SendAsync(clientFrame.AsMemory(0, 70), TestContext.Current.CancellationToken);
            Assert.Equal(clientFrame[..70], await TestRelay.ReadExactlyAsync(server, 70));

            relayConnection.BeforeFinalAdmissionCheck = () =>
            {
                reached.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            };

            var admission = new RelaySendAdmission(1, injected, () => true);
            var sending = ((IRelayAdmissionConnection)connection).SendAdmittedAsync(
                injected,
                admission,
                TestContext.Current.CancellationToken);

            await client.SendAsync(clientFrame.AsMemory(70), TestContext.Current.CancellationToken);
            await reached.Task.WaitAsync(TestContext.Current.CancellationToken);
            mode.SetAdmissionMode(2, requiresTypedAdmission: true);
            release.TrySetResult();

            await Assert.ThrowsAsync<RelayInjectionException>(() => sending);
            Assert.Equal(clientFrame[70..], await TestRelay.ReadExactlyAsync(server, clientFrame.Length - 70));
        }
    }

    [Fact]
    public async Task AModeChangeAfterTheFinalCheckWaitsUntilTheFrameWriteCompletes()
    {
        var interceptor = new RecordingInterceptor();
        var clientFrame = TestFrames.Framed(Payload(180, 33));
        var injected = Payload(16, 34);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var updateStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);
            var relayConnection = Assert.IsType<RelayConnection>(connection);
            var mode = (IRelayAdmissionModeController)connection;
            mode.SetAdmissionMode(1, requiresTypedAdmission: true);

            await client.SendAsync(clientFrame.AsMemory(0, 64), TestContext.Current.CancellationToken);
            Assert.Equal(clientFrame[..64], await TestRelay.ReadExactlyAsync(server, 64));

            relayConnection.AfterFinalAdmissionCheck = () =>
            {
                reached.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            };

            var admission = new RelaySendAdmission(1, injected, () => true);
            var sending = ((IRelayAdmissionConnection)connection).SendAdmittedAsync(
                injected,
                admission,
                TestContext.Current.CancellationToken);

            await client.SendAsync(clientFrame.AsMemory(64), TestContext.Current.CancellationToken);
            await reached.Task.WaitAsync(TestContext.Current.CancellationToken);

            var updating = Task.Run(() =>
            {
                updateStarted.TrySetResult();
                mode.SetAdmissionMode(2, requiresTypedAdmission: true);
            }, TestContext.Current.CancellationToken);

            await updateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.False(updating.IsCompleted);
            release.TrySetResult();

            await sending;
            await updating;

            byte[] expected = [.. clientFrame[64..], .. TestFrames.Framed(injected)];
            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }
    }

    [Fact]
    public async Task AThrowingAdmissionPredicateFailsClosedWithoutStoppingClientForwarding()
    {
        var interceptor = new RecordingInterceptor();
        var clientFrame = TestFrames.Framed(Payload(240, 28));
        var laterFrame = TestFrames.Framed(Payload(17, 29));
        var injected = Payload(21, 30);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            var connection = await ConnectionAsync(interceptor);

            ((IRelayAdmissionModeController)connection).SetAdmissionMode(1, requiresTypedAdmission: true);
            await client.SendAsync(clientFrame.AsMemory(0, 75), TestContext.Current.CancellationToken);
            Assert.Equal(clientFrame[..75], await TestRelay.ReadExactlyAsync(server, 75));

            var sending = SendAdmittedAsync(
                connection,
                injected,
                () => throw new InvalidOperationException("Admission classifier failed."));

            Assert.False(sending.IsCompleted);
            await client.SendAsync(clientFrame.AsMemory(75), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<RelayInjectionException>(() => sending);

            await client.SendAsync(laterFrame, TestContext.Current.CancellationToken);
            var forwardedFrames = await TestRelay.ReadExactlyAsync(server, clientFrame.Length - 75 + laterFrame.Length);
            byte[] expected = [.. clientFrame[75..], .. laterFrame];
            Assert.Equal(expected, forwardedFrames);
        }
    }

    [Fact]
    public async Task AFrameWithdrawnBeforeItWentNeverGoes()
    {
        var interceptor = new RecordingInterceptor();
        var frame = TestFrames.Framed(Payload(200, 6));
        var after = Payload(15, 7);

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(frame.AsMemory(0, 50), TestContext.Current.CancellationToken);
            Assert.Equal(frame[..50], await TestRelay.ReadExactlyAsync(server, 50));

            var connection = await ConnectionAsync(interceptor);

            using (var withdrawn = new CancellationTokenSource())
            {
                var sending = connection.SendToServerAsync(Payload(30, 8), withdrawn.Token);

                await withdrawn.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sending);
            }

            await client.SendAsync(frame.AsMemory(50), TestContext.Current.CancellationToken);
            await connection.SendToServerAsync(after, TestContext.Current.CancellationToken);

            byte[] expected = [.. frame[50..], .. TestFrames.Framed(after)];

            Assert.Equal(expected, await TestRelay.ReadExactlyAsync(server, expected.Length));
        }
    }

    [Fact]
    public async Task AFrameCannotGoBetweenBytesThatAreNoFrames()
    {
        var interceptor = new RecordingInterceptor();

        // A length of five bytes above 32 bits: no frame can start so.
        byte[] bytes = [0xFF, 0xFF, 0xFF, 0xFF, 0x7F, .. Payload(100, 9)];

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            await client.SendAsync(bytes, TestContext.Current.CancellationToken);
            Assert.Equal(bytes, await TestRelay.ReadExactlyAsync(server, bytes.Length));

            var exception = await Assert.ThrowsAsync<RelayInjectionException>(async () => await (await ConnectionAsync(interceptor)).SendToServerAsync(Payload(10, 10), TestContext.Current.CancellationToken));

            Assert.Contains("no frames", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AFrameSentOnceTheConnectionClosedFails()
    {
        var interceptor = new RecordingInterceptor();

        await using var relay = await TestRelay.StartAsync(interceptor: interceptor);
        var (client, server) = await relay.ConnectAsync();
        var connection = await ConnectionAsync(interceptor);

        using (server)
        {
            using (client)
                client.Shutdown(SocketShutdown.Both);

            await TestRelay.ReadToEndAsync(server);
        }

        await relay.WaitForLogAsync(record => record.Message.Contains("closed after", StringComparison.Ordinal));

        Assert.True(connection.Closed.IsCancellationRequested);
        await Assert.ThrowsAsync<RelayInjectionException>(() => connection.SendToServerAsync(Payload(10, 11), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCaptureRecordsWhatD3BSentWhereItWent()
    {
        using var directory = new TemporaryDirectory();
        var interceptor = new RecordingInterceptor();
        var first = TestFrames.Framed(Payload(30, 12));
        var injected = Payload(25, 13);
        var second = TestFrames.Framed(Payload(35, 14));
        byte[] received;

        await using (var relay = await TestRelay.StartAsync(capturesDirectory: directory.Path, interceptor: interceptor))
        {
            var (client, server) = await relay.ConnectAsync();

            using (server)
            {
                using (client)
                {
                    await client.SendAsync(first, TestContext.Current.CancellationToken);
                    await TestRelay.ReadExactlyAsync(server, first.Length);
                    await (await ConnectionAsync(interceptor)).SendToServerAsync(injected, TestContext.Current.CancellationToken);
                    await client.SendAsync(second, TestContext.Current.CancellationToken);
                    client.Shutdown(SocketShutdown.Send);
                }

                received = [.. first, .. await TestRelay.ReadToEndAsync(server)];
            }

            await relay.WaitForLogAsync(record => record.Message.Contains("1 frames from D3B", StringComparison.Ordinal));
        }

        using var reader = CaptureReader.Open(Assert.Single(Directory.GetFiles(directory.Path, "*" + CaptureWriter.FileExtension)));
        var records = reader.ReadAll().ToList();

        Assert.Equal(2, reader.FormatVersion);
        Assert.Equal(TestFrames.Framed(injected), Assert.Single(records, record => record.Kind is CaptureRecordKind.Injected).Data.ToArray());

        // What the server got is what the client sent and what D3B did, in the order of their records.
        Assert.Equal(received, records.Where(record => record.Kind is CaptureRecordKind.FromClient or CaptureRecordKind.Injected).SelectMany(record => record.Data.ToArray()));
    }
}
