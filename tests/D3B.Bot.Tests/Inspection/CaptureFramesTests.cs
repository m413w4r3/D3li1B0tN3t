using System.Buffers;
using D3B.Bot.Inspection;
using D3B.Bot.Tests.Network;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;

namespace D3B.Bot.Tests.Inspection;

public sealed class CaptureFramesTests : IDisposable
{
    private static readonly ProtocolTranslation _translation = TestGame.Translation();

    private readonly TemporaryDirectory _directory = new();

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static byte[] Framed(byte[] frame)
    {
        var writer = new ArrayBufferWriter<byte>();

        FrameWriter.Write(writer, frame);

        return writer.WrittenSpan.ToArray();
    }

    private string Capture(string version, Action<CaptureWriter> write)
    {
        var path = _directory.Combine("20261029-031619-0HNPLDBVDE8EE" + CaptureWriter.FileExtension);
        var header = new CaptureHeader { StartedAt = DateTimeOffset.UnixEpoch, ClientVersion = version, Listener = string.Empty, ServerHost = string.Empty, Server = string.Empty };

        using (var stream = File.Create(path))
        using (var writer = new CaptureWriter(stream, header, TimeProvider.System))
            write(writer);

        return path;
    }

    [Fact]
    public void ACapturesFramesAreFramedAgainAcrossReadsAndDecodedEachFromItsSide()
    {
        var authentication = Framed(TestGame.Request(new AuthenticationRequest { Ticket = "0123456789abcdef0123456789abcdef", Language = "fr" }));
        var path = Capture(_translation.ClientVersion, writer =>
        {
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(authentication[..7]));
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(authentication[7..]));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>([.. Framed(TestGame.Event(new AuthenticationAcceptedEvent())), .. Framed(TestGame.Event(new MapCurrentEvent { MapId = 188_744_706 }))]));
            writer.Write(CaptureRecordKind.Injected, new ReadOnlySequence<byte>(Framed(TestGame.Request(new MapMovementRequest { MapId = 188_744_706, KeyMovements = { 433, 431 } }))));
            writer.WriteClosed(CaptureRecordKind.ClientClosed);
        });

        var game = CaptureFrames.Read(path, _translation);

        Assert.Equal(
            [
                (FrameSource.Client, nameof(AuthenticationRequest)),
                (FrameSource.Server, nameof(AuthenticationAcceptedEvent)),
                (FrameSource.Server, nameof(MapCurrentEvent)),
                (FrameSource.D3B, nameof(MapMovementRequest)),
            ],
            game.Frames.Select(frame => (frame.Source, frame.Frame.ReadableType!.Name)));
        Assert.All(game.Frames, frame => Assert.Equal("20261029-031619-0HNPLDBVDE8EE", frame.ConnectionId));
        Assert.False(game.IsTruncated);
    }

    [Fact]
    public void ACaptureOfAnotherBuildIsRefused()
    {
        var path = Capture("3.0.0.0", writer => writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(TestGame.Request(new MapCurrentEvent())))));

        var refused = Assert.Throws<InvalidDataException>(() => CaptureFrames.Read(path, _translation));

        Assert.Contains("3.0.0.0", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BytesThatStopBeingFramesEndTheirSide()
    {
        var path = Capture(_translation.ClientVersion, writer =>
        {
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(Framed(TestGame.Event(new AuthenticationAcceptedEvent()))));
        });

        Assert.Equal([FrameSource.Server], CaptureFrames.Read(path, _translation).Frames.Select(frame => frame.Source));
    }
}
