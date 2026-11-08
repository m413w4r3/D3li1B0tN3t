using System.Buffers;
using System.Net.Sockets;
using D3B.Gui.Actions;
using D3B.Gui.Input;
using D3B.Gui.Safety;
using D3B.Gui.Windowing;
using D3B.Network.Framing;
using D3B.Network.Relay;
using D3B.Network.Tests.Relay;

namespace D3B.Bot.Tests.Gui;

public sealed class GuiClientInputRelayContractTests
{
    [Fact]
    public async Task GuiClientInputRelaysTrafficAndRefusesD3BGameplay()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiActionIntent intent = fixture.Intent;
        RecordingInput input = new();
        CapturingInterceptor interceptor = new();

        await using TestRelay relay = await TestRelay.StartAsync(interceptor: interceptor);
        (Socket client, Socket server) = await relay.ConnectAsync();

        using (client)
        using (server)
        {
            IRelayConnection connection = await interceptor.Connection.Task.WaitAsync(TestContext.Current.CancellationToken);
            RelayAdmissionSeam admission = new(client, connection, new RelayRequestClassifier());

            RelayRequest guiRequest = new(GameInputRequestSource.Client, true, [0x10, 0x01, 0xA1], intent);
            byte[] guiRequestFrame = Frame(guiRequest.Payload);

            Assert.True(await admission.SendAsync(guiRequest, TestContext.Current.CancellationToken));
            byte[] relayedGuiRequest = await TestRelay.ReadExactlyAsync(server, guiRequestFrame.Length);
            Assert.Equal(guiRequestFrame, relayedGuiRequest);

            await connection.WaitForObservedFramesAsync(TestContext.Current.CancellationToken);

            RelayRequest generatedGameplay = new(GameInputRequestSource.D3B, true, [0x20, 0x01, 0xB2], null);

            Assert.False(await admission.SendAsync(generatedGameplay, TestContext.Current.CancellationToken));

            RelayRequest d3bControl = new(GameInputRequestSource.D3B, false, [0x30, 0x01, 0xC3], null);
            byte[] d3bControlFrame = Frame(d3bControl.Payload);

            Assert.True(await admission.SendAsync(d3bControl, TestContext.Current.CancellationToken));
            byte[] relayedControl = await TestRelay.ReadExactlyAsync(server, d3bControlFrame.Length);
            Assert.Equal(d3bControlFrame, relayedControl);

            RelayRequest secondGuiRequest = new(GameInputRequestSource.Client, true, [0x10, 0x02, 0xA2], intent);
            byte[] secondGuiRequestFrame = Frame(secondGuiRequest.Payload);

            Assert.True(await admission.SendAsync(secondGuiRequest, TestContext.Current.CancellationToken));
            byte[] relayedSecondGuiRequest = await TestRelay.ReadExactlyAsync(server, secondGuiRequestFrame.Length);
            Assert.Equal(secondGuiRequestFrame, relayedSecondGuiRequest);

            byte[] serverFirst = Frame([0x40, 0x01, 0xD4]);
            byte[] serverSecond = Frame([0x40, 0x02, 0xD5]);
            byte[] serverBytes = [.. serverFirst, .. serverSecond];

            await SendExactlyAsync(server, serverFirst, TestContext.Current.CancellationToken);
            await SendExactlyAsync(server, serverSecond, TestContext.Current.CancellationToken);

            byte[] receivedByClient = await TestRelay.ReadExactlyAsync(client, serverBytes.Length);
            Assert.Equal(serverBytes, receivedByClient);

            client.Shutdown(SocketShutdown.Send);

            byte[] trailingUpstreamBytes = await TestRelay.ReadToEndAsync(server);
            Assert.Empty(trailingUpstreamBytes);

            Assert.Equal(intent.IntentId, guiRequest.Intent?.IntentId);
            Assert.Equal(intent.IntentId, secondGuiRequest.Intent?.IntentId);
            Assert.Equal(0, input.ClickCount);
            Assert.Equal(0, input.ProcessStarts);
        }
    }

    private static byte[] Frame(ReadOnlySpan<byte> payload)
    {
        ArrayBufferWriter<byte> writer = new();
        FrameWriter.Write(writer, payload);
        return writer.WrittenSpan.ToArray();
    }

    private static async Task SendExactlyAsync(Socket socket, byte[] bytes, CancellationToken cancellationToken)
    {
        int sent = 0;

        while (sent < bytes.Length)
        {
            sent += await socket.SendAsync(bytes.AsMemory(sent), cancellationToken);
        }
    }

    private sealed record RelayRequest(
        GameInputRequestSource Source,
        bool IsGameplay,
        byte[] Payload,
        GuiActionIntent? Intent);

    private sealed class RelayRequestClassifier : IGameInputRequestClassifier<RelayRequest>
    {
        public GameInputRequestClassification Classify(RelayRequest request)
        {
            return new GameInputRequestClassification(request.Source, request.IsGameplay);
        }
    }

    /// <summary>Composes the GUI input policy with the fake relay's client socket and D3B admission seam.</summary>
    /// <remarks>Production GameSession admission wiring remains a P2O task; this test leaves relay forwarding unchanged.</remarks>
    private sealed class RelayAdmissionSeam
    {
        private readonly Socket _client;
        private readonly IRelayConnection _connection;
        private readonly RelayRequestClassifier _classifier;

        public RelayAdmissionSeam(Socket client, IRelayConnection connection, RelayRequestClassifier classifier)
        {
            _client = client;
            _connection = connection;
            _classifier = classifier;
        }

        public async Task<bool> SendAsync(RelayRequest request, CancellationToken cancellationToken)
        {
            if (!GuiInputModePolicy.IsAllowed(GameInputMode.GuiClientInput, request, _classifier))
            {
                return false;
            }

            if (request.Source is GameInputRequestSource.Client)
            {
                await SendExactlyAsync(_client, Frame(request.Payload), cancellationToken);
            }
            else
            {
                await _connection.SendToServerAsync(request.Payload, cancellationToken);
            }

            return true;
        }
    }

    private sealed class CapturingInterceptor : IRelayInterceptor, IConnectionInterceptor
    {
        public TaskCompletionSource<IRelayConnection> Connection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IConnectionInterceptor? Intercept(IRelayConnection connection)
        {
            Connection.TrySetResult(connection);
            return this;
        }

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

    private sealed class RecordingInput : IGuiInput
    {
        public bool IsEnabled => true;

        public bool EmitsRealInput => true;

        public int ClickCount { get; private set; }

        public int ProcessStarts { get; private set; }

        public Task<GuiInputResult> ClickAsync(DesktopPixels point, GameWindowSnapshot expectedWindow, CancellationToken cancellationToken)
        {
            ClickCount++;
            ProcessStarts++;
            return Task.FromResult(new GuiInputResult(GuiInputStatus.Sent));
        }
    }
}
