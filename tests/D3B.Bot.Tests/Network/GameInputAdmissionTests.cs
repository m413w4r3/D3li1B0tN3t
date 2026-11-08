using System.Buffers;
using D3B.Bot.Network;
using D3B.Gui.Safety;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Network;

public sealed class GameInputAdmissionTests : IDisposable
{
    private static readonly RelaySettings _settings = new()
    {
        Address = RelaySettings.DefaultAddress,
        Ports = RelaySettings.DefaultPorts,
        GamePorts = RelaySettings.DefaultGamePorts,
        ClientDirectory = "no-client",
        RedirectDirectory = "no-redirect",
    };

    private readonly ProtocolTranslation _translation = TestGame.Translation();
    private readonly FakeRelayConnection _connection = new("admission", RelayRoute.Game);
    private readonly RecordingObserver _observer = new();
    private readonly GameSession _session;

    public GameInputAdmissionTests()
    {
        var accounts = new GameAccounts(_settings.SelectionLifetime, TimeProvider.System);
        var decoder = new RelayDecoder(_translation, new GameServerSelections(_settings, TimeProvider.System), accounts, [_observer], NullLoggerFactory.Instance);

        _session = (GameSession)decoder.Intercept(_connection)!;
        _session.SendGate = _ => true;
    }

    [Fact]
    public async Task GuiModeRefusesGameplayRequestsBeforeMovementBarriersOrEncoding()
    {
        var requests = GameplayRequests();
        var observationWaits = 0;
        var barriers = 0;

        _session.InputMode = GameInputMode.GuiClientInput;
        _connection.ObservationWait = _ =>
        {
            observationWaits++;

            return Task.CompletedTask;
        };
        _session.SendBarrier = () =>
        {
            barriers++;

            return Task.CompletedTask;
        };

        foreach (var request in requests)
            await Assert.ThrowsAsync<InvalidOperationException>(() => _session.SendAsync(request, TestContext.Current.CancellationToken));

        Assert.Empty(_connection.Sent);
        Assert.Equal(0, observationWaits);
        Assert.Equal(0, barriers);
    }

    [Fact]
    public async Task AnUnknownOrUnclassifiedDescriptorIsRefused()
    {
        _session.InputMode = GameInputMode.GuiClientInput;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.SendAsync(
            new MapInformationRequest { MapId = 1 },
            TestContext.Current.CancellationToken));

        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task AnExplicitlyAllowlistedNonGameplayDescriptorCanBeSent()
    {
        _session.InputMode = GameInputMode.GuiClientInput;
        _session.GuiClassifier = new DescriptorGameInputRequestClassifier([MapInformationRequest.Descriptor.FullName]);

        await _session.SendAsync(new MapInformationRequest { MapId = 1 }, TestContext.Current.CancellationToken);

        Assert.Single(_connection.Sent);
    }

    [Fact]
    public async Task AnUndefinedInputModeRefusesTheRequest()
    {
        _session.InputMode = (GameInputMode)int.MaxValue;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.SendAsync(
            new DialogLeaveRequest(),
            TestContext.Current.CancellationToken));

        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task ProtocolInjectionStillAdmitsExistingD3BGameplayRequests()
    {
        Assert.Equal(GameInputMode.ProtocolInjection, _session.InputMode);

        await _session.SendAsync(new MapMovementRequest { MapId = 1 }, TestContext.Current.CancellationToken);
        await _session.SendAsync(new GameActionFightCastRequest { SpellId = 1, CellId = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(2, _connection.Sent.Count);
    }

    [Fact]
    public async Task AModeChangeWhileAFrameWaitsAtTheRelayInvalidatesIt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _connection.SendWait = async cancellationToken =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        };

        var sending = _session.SendAsync(new DialogLeaveRequest(), TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        _session.InputMode = GameInputMode.GuiClientInput;
        release.TrySetResult();

        await Assert.ThrowsAsync<RelayInjectionException>(() => sending);
        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task ChangingTheModeAwayAndBackWhileQueuedStillInvalidatesTheFrame()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _connection.SendWait = async cancellationToken =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        };

        var sending = _session.SendAsync(new DialogLeaveRequest(), TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        _session.InputMode = GameInputMode.GuiClientInput;
        _session.InputMode = GameInputMode.ProtocolInjection;
        release.TrySetResult();

        await Assert.ThrowsAsync<RelayInjectionException>(() => sending);
        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task AClosedConnectionFailsAFrameThatWasAlreadyQueued()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _connection.SendWait = async cancellationToken =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        };

        var sending = _session.SendAsync(new DialogLeaveRequest(), TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        _connection.Close();
        release.TrySetResult();

        await Assert.ThrowsAsync<RelayInjectionException>(() => sending);
        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task AClosedTranslationLatchDoesNotPermitMovementInGuiModeAndFramesRemainObservable()
    {
        _session.InputMode = GameInputMode.GuiClientInput;
        Show(RelayDirection.FromServer, TestGame.UnknownCarriedEvent());

        Assert.True(_session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.SendAsync(
            new MapMovementRequest { MapId = 1 },
            TestContext.Current.CancellationToken));

        Show(RelayDirection.FromClient, TestGame.Request(new AuthenticationRequest { Ticket = "test", Language = "en" }));
        Show(RelayDirection.FromServer, TestGame.Event(new MapCurrentEvent { MapId = 2 }));

        Assert.Equal(3, _observer.Frames.Count);
        Assert.Empty(_connection.Sent);
    }

    [Fact]
    public async Task AThrowingClassifierRefusesD3BAndDoesNotBreakFrameObservation()
    {
        _session.InputMode = GameInputMode.GuiClientInput;
        _session.GuiClassifier = new ThrowingClassifier();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _session.SendAsync(
            new DialogLeaveRequest(),
            TestContext.Current.CancellationToken));

        Show(RelayDirection.FromClient, TestGame.Request(new AuthenticationRequest { Ticket = "test", Language = "en" }));
        Show(RelayDirection.FromServer, TestGame.Event(new MapCurrentEvent { MapId = 3 }));

        Assert.Equal(2, _observer.Frames.Count);
        Assert.Empty(_connection.Sent);
    }

    private static IMessage[] GameplayRequests()
    {
        return
        [
            new MapMovementRequest { MapId = 1 },
            new InteractiveUseRequest { ElementId = 1, SkillInstanceId = 1 },
            new GameActionFightCastRequest { SpellId = 1, CellId = 2 },
            new TeleportRequest { MapId = 1 },
            new ObjectUseRequest { Uid = 1 },
            new ExchangeTransferFromInventoryRequest { Uids = { 1 } },
            new NpcDialogReplyRequest { ReplyId = 1 },
        ];
    }

    private void Show(RelayDirection direction, byte[] frame)
    {
        ((IConnectionInterceptor)_session).OnFrame(direction, new ReadOnlySequence<byte>(frame), held: false);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class ThrowingClassifier : IGameInputRequestClassifier<IMessage>
    {
        public GameInputRequestClassification Classify(IMessage request)
        {
            throw new InvalidOperationException("Classifier failed.");
        }
    }
}
