using D3B.Bot.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Mapping;
using D3B.Protocol.Tests.Mapping;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging.Abstractions;

namespace D3B.Bot.Tests.Network;

public sealed class NonPositionTranslationTests
{
    private static (FakeRelayConnection Relay, GameSession Session, TestConnection Connection) Session(string clientVersion, Action<Obfuscation>? change = null)
    {
        var obfuscation = new Obfuscation(ReadableProtocol.Files);

        change?.Invoke(obfuscation);

        var schemaBytes = obfuscation.SchemaBytes();
        var schema = ProtocolSchema.Load(schemaBytes);
        var mapping = new ProtocolMatcher(ReadableProtocol.Files, schema, ReadableProtocol.Scopes)
            .Match(obfuscation.Pins(Obfuscation.GamePayloads()), [])
            .ToMapping(clientVersion, schema.Sha256, ReadableProtocol.Digest, "test");
        var translation = ProtocolTranslation.Create(schema, ProtocolMapping.Parser.ParseFrom(mapping.ToByteArray()));
        var relay = new FakeRelayConnection("p2k", RelayRoute.Game);
        var session = GameSession.Start(relay, translation, new GameAccounts(TimeSpan.FromMinutes(5), TimeProvider.System), [], NullLogger.Instance);

        session.SendGate = _ => true;

        return (relay, session, new TestConnection(relay, session, translation));
    }

    private static byte[] Event(string carriedType, params byte[] value)
    {
        return new GameMessage
        {
            Event = new GameEvent
            {
                Content = new Any
                {
                    TypeUrl = ProtocolTranslation.TypeUrlPrefix + carriedType,
                    Value = ByteString.CopyFrom(value),
                },
            },
        }.ToByteArray();
    }

    private static byte[] Request(string carriedType, params byte[] value)
    {
        return new GameMessage
        {
            Request = new GameRequest
            {
                Id = ProtocolTranslation.UncountedRequestId,
                Content = new Any
                {
                    TypeUrl = ProtocolTranslation.TypeUrlPrefix + carriedType,
                    Value = ByteString.CopyFrom(value),
                },
            },
        }.ToByteArray();
    }

    private static void AddBoolMessage(Obfuscation obfuscation, string name)
    {
        obfuscation.AddSchemaMessage(name, (1, FieldDescriptorProto.Types.Type.Bool));
    }

    private static void AddInt32Message(Obfuscation obfuscation, string name)
    {
        obfuscation.AddSchemaMessage(name, (1, FieldDescriptorProto.Types.Type.Int32));
    }

    [Fact]
    public async Task AListedMessageWithAValidSchemaDoesNotCloseMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddBoolMessage(obfuscation, "knb"));

        connection.ServerRaw(Event("knb", 0x08, 0x01));

        Assert.False(session.IsMovementTranslationBlocked);

        await session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);

        Assert.Single(relay.Sent);
    }

    [Fact]
    public async Task TheServersTimeIsAClassifiedNonPositionMessage()
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation => obfuscation.AddSchemaMessage("lok", (1, FieldDescriptorProto.Types.Type.Int64), (2, FieldDescriptorProto.Types.Type.Int32)));

        connection.ServerRaw(Event("lok", 0x08, 0x01, 0x10, 0x02));

        Assert.False(session.IsMovementTranslationBlocked);

        await session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);

        Assert.Single(relay.Sent);
    }

    [Fact]
    public async Task TheUnprovenAccountRoleStatusEventWithAValidThreeFieldSchemaClosesMovement()
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation => obfuscation.AddSchemaMessage(
                "mem",
                (1, FieldDescriptorProto.Types.Type.Bool),
                (2, FieldDescriptorProto.Types.Type.Int32),
                (3, FieldDescriptorProto.Types.Type.Int32)));

        // int32 is wire-compatible with the two enum fields (kyc and mem.mek), but ggpy and ggqa remain semantically unknown.
        connection.ServerRaw(Event("mem", 0x08, 0x01, 0x10, 0x02, 0x18, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken));
        Assert.Empty(relay.Sent);
    }

    [Theory]
    [InlineData("lnu")]
    [InlineData("ktc")]
    public async Task AnEmptyServerEventWithAnEmptySchemaLeavesMovementOpen(string carriedType)
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation => obfuscation.AddSchemaMessage(carriedType));

        connection.ServerRaw(Event(carriedType));

        Assert.False(session.IsMovementTranslationBlocked);
        await session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);
        Assert.Single(relay.Sent);
    }

    [Theory]
    [InlineData("lnu")]
    [InlineData("ktc")]
    public async Task AnEmptyServerEventWithAFieldItsSchemaLacksClosesMovement(string carriedType)
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation => obfuscation.AddSchemaMessage(carriedType));

        connection.ServerRaw(Event(carriedType, 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken));
        Assert.Empty(relay.Sent);
    }

    [Theory]
    [InlineData("kmk")]
    [InlineData("kmu")]
    public async Task AnEmptyClientRequestWithAnEmptySchemaLeavesMovementOpen(string carriedType)
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation => obfuscation.AddSchemaMessage(carriedType));

        connection.ClientRaw(Request(carriedType));

        Assert.False(session.IsMovementTranslationBlocked);
        await session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);
        Assert.Single(relay.Sent);
    }

    [Fact]
    public async Task AnUnprovenAccountStatusTypeWithAValidSchemaStillClosesMovement()
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation => obfuscation.AddSchemaMessage(
                "kpx",
                (1, FieldDescriptorProto.Types.Type.Bool),
                (2, FieldDescriptorProto.Types.Type.Int32),
                (3, FieldDescriptorProto.Types.Type.Bool)));

        connection.ServerRaw(Event("kpx", 0x08, 0x01, 0x10, 0x02, 0x18, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken));
        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task CorrectedAccountAndEmptyEventsLeaveMovementOpenForTheNextRequest()
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation =>
            {
                obfuscation.AddSchemaMessage("lnu");
                obfuscation.AddSchemaMessage("ktc");
            });

        connection.ServerRaw(Event("lnu"));
        connection.ServerRaw(Event("ktc"));

        Assert.False(session.IsMovementTranslationBlocked);
        await session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);
        Assert.Single(relay.Sent);
    }

    [Fact]
    public async Task AListedClientRequestFromTheClientDoesNotCloseMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddInt32Message(obfuscation, "hlf"));

        connection.ClientRaw(Request("hlf", 0x08, 0x01));

        Assert.False(session.IsMovementTranslationBlocked);

        await session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);

        Assert.Single(relay.Sent);
    }

    [Fact]
    public async Task AListedClientRequestFromTheServerClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddInt32Message(obfuscation, "hlf"));

        connection.ServerRaw(Event("hlf", 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Theory]
    [InlineData("lnu")]
    [InlineData("ktc")]
    public async Task AListedServerMessageFromTheClientClosesMovement(string carriedType)
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddInt32Message(obfuscation, carriedType));

        connection.ClientRaw(Event(carriedType, 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));
        Assert.Empty(relay.Sent);
    }

    [Theory]
    [InlineData("kmk")]
    [InlineData("kmu")]
    public async Task AListedClientMessageTypeSentFromTheServerClosesMovement(string carriedType)
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddInt32Message(obfuscation, carriedType));

        connection.ServerRaw(Event(carriedType, 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));
        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AListedServerMessageWithAForeignTypeUrlClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddInt32Message(obfuscation, "lnu"));
        var envelope = GameMessage.Parser.ParseFrom(Event("lnu", 0x08, 0x01));
        envelope.Event.Content.TypeUrl = "type.ankama.com/x/lnu";

        connection.ServerRaw(envelope.ToByteArray());

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));
        Assert.Empty(relay.Sent);
    }

    [Theory]
    [InlineData("kmk")]
    [InlineData("kmu")]
    public async Task AnEmptyClientRequestWithAFieldItsSchemaLacksClosesMovement(string carriedType)
    {
        var (relay, session, connection) = Session(
            NonPositionMessages.ClientVersion,
            obfuscation => obfuscation.AddSchemaMessage(carriedType));

        connection.ClientRaw(Request(carriedType, 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));
        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AnUnlistedMessageWithAValidSchemaClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddBoolMessage(obfuscation, "hmj"));

        connection.ServerRaw(Event("hmj", 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AListedMessageWithAForeignTypeUrlClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddBoolMessage(obfuscation, "knb"));
        var envelope = GameMessage.Parser.ParseFrom(Event("knb", 0x08, 0x01));
        envelope.Event.Content.TypeUrl = "type.ankama.com/x/knb";

        connection.ServerRaw(envelope.ToByteArray());

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AListedMessageHoldingWhatItsSchemaLacksClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddBoolMessage(obfuscation, "knb"));

        // Field 2 is not in the build's message: the payload is not the schema's shape, whatever its type URL says.
        connection.ServerRaw(Event("knb", 0x08, 0x01, 0x10, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AListedMessageWhosePayloadDoesNotParseClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddBoolMessage(obfuscation, "knb"));

        // Field 1 is a varint; its continuation bit is set without a following byte.
        connection.ServerRaw(Event("knb", 0x08, 0x80));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AnUnlistedPositionMessageWithAValidSchemaClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddInt32Message(obfuscation, "jpe"));

        connection.ServerRaw(Event("jpe", 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AnUnlistedFightEventWithAValidSchemaClosesMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddInt32Message(obfuscation, "jxh"));

        connection.ServerRaw(Event("jxh", 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AListedMessageOfAnotherBuildClosesMovement()
    {
        var (relay, session, connection) = Session("3.7.12.12", obfuscation => AddBoolMessage(obfuscation, "knb"));

        connection.ServerRaw(Event("knb", 0x08, 0x01));

        Assert.True(session.IsMovementTranslationBlocked);
        await Assert.ThrowsAsync<RelayInjectionException>(() => session.SendAsync(
            new MapMovementRequest { MapId = (int)TestPlay.MapId },
            TestContext.Current.CancellationToken));

        Assert.Empty(relay.Sent);
    }

    [Fact]
    public async Task AListedMessageDoesNotWithdrawAQueuedMovementSend()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddBoolMessage(obfuscation, "knb"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        relay.SendWait = async cancellationToken =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        };

        var sending = session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        // The frame arrives while the request waits in the relay: it is proven non-position, so it must not cancel it.
        connection.ServerRaw(Event("knb", 0x08, 0x01));
        release.TrySetResult();

        await sending;

        Assert.False(session.IsMovementTranslationBlocked);
        Assert.Single(relay.Sent);
    }

    [Fact]
    public async Task AListedMessageDuringTheInboxBarrierDoesNotCloseMovement()
    {
        var (relay, session, connection) = Session(NonPositionMessages.ClientVersion, obfuscation => AddBoolMessage(obfuscation, "knb"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        session.SendBarrier = async () =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        };

        var sending = session.SendAsync(new MapMovementRequest { MapId = (int)TestPlay.MapId }, TestContext.Current.CancellationToken);

        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        connection.ServerRaw(Event("knb", 0x08, 0x01));
        release.TrySetResult();

        await sending;

        Assert.False(session.IsMovementTranslationBlocked);
        Assert.Single(relay.Sent);
    }
}
