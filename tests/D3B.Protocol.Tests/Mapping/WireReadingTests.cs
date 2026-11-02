using D3B.Protocol.Connection;
using D3B.Protocol.Mapping;
using Google.Protobuf;

namespace D3B.Protocol.Tests.Mapping;

public sealed class WireReadingTests
{
    [Fact]
    public void FieldsAreReadWithTheirNumbersWireTypesAndPlaces()
    {
        var bytes = new GameServer { Id = 300, Type = ServerType.Value3 }.ToByteArray();

        Assert.True(WireReading.TryFields(bytes, out var fields));
        Assert.Equal([(1, 0, 1, 2), (3, 0, 4, 1)], fields.Select(field => (field.Number, field.WireType, field.Start, field.Length)));
    }

    [Fact]
    public void BytesCutShortAreNoMessage()
    {
        var bytes = new ServerSelectionResponse.Types.Success { Ticket = "ticket" }.ToByteArray();

        Assert.False(WireReading.TryFields(bytes.AsSpan(0, bytes.Length - 1), out _));
    }

    [Fact]
    public void AMessageTypesEveryFieldAtEveryDepth()
    {
        var update = new ServerUpdate
        {
            Server = new ServerInformation { Server = new GameServer { Id = 1 }, Characters = { new CharacterInformation { Name = "name", Level = 3 } } },
        };

        Assert.Equal(6, WireReading.Typed(ServerUpdate.Descriptor, update.ToByteArray()));
    }

    [Fact]
    public void AMessageLackingAFieldDoesNotReadTheBytes()
    {
        var bytes = new GameServer { Id = 1, Status = GameServer.Types.Status.Maintenance }.ToByteArray();

        Assert.Equal(-1, WireReading.Typed(ServerSelectionRequest.Descriptor, bytes));
    }

    [Fact]
    public void AListOfScalarsIsReadPackedOrNot()
    {
        var packed = new ServerSelectionResponse.Types.Success { Ports = { 5555, 443 } }.ToByteArray();
        byte[] unpacked = [3 << 3, 0xB3, 0x2B, 3 << 3, 0xBB, 0x03];

        Assert.Equal(1, WireReading.Typed(ServerSelectionResponse.Types.Success.Descriptor, packed));
        Assert.Equal(2, WireReading.Typed(ServerSelectionResponse.Types.Success.Descriptor, unpacked));
    }
}
