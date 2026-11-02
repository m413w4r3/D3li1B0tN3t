using System.Buffers;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Translation;

namespace D3B.Bot.Inspection;

/// <summary>Reads a game connection's capture into its frames, decoded through the installed build's translation (D63, D115).</summary>
/// <remarks>
/// <para>
/// A capture records reads, not frames (D37): each side's bytes are framed again as a reader of the connection would, a
/// frame cut across reads coming whole once its last bytes do. D3B's own frames are records of their own, each a frame
/// with its length (D61). A side whose bytes stop being frames is read no further, as the relay would watch it no more.
/// </para>
/// <para>
/// Only a capture of the installed build is read: its frames name the build's obfuscated messages, which only that
/// build's mapping turns into readable ones (D11). The capture holds the account's ticket, which the inspector hides as it
/// does live (D65).
/// </para>
/// </remarks>
public static class CaptureFrames
{
    /// <summary>Reads a capture of a game connection.</summary>
    /// <param name="path">The capture's file, such as one of the captures' <c>game</c> folder (D53).</param>
    /// <param name="translation">The installed build's translation.</param>
    /// <returns>The capture's frames.</returns>
    /// <exception cref="CaptureFormatException">The file is no capture, or a record of it is damaged.</exception>
    /// <exception cref="InvalidDataException">The capture is of another client build than the translation's.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    public static CapturedGame Read(string path, ProtocolTranslation translation)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(translation);

        using var reader = CaptureReader.Open(path);
        var header = reader.Header;

        if (header.ClientVersion != translation.ClientVersion)
            throw new InvalidDataException($"The capture is of client {header.ClientVersion}, and the installed client is {translation.ClientVersion}: only a capture of the installed build reads through its mapping.");

        var id = Path.GetFileNameWithoutExtension(path);
        var frames = new List<InspectedFrame>();
        var client = new Side();
        var server = new Side();

        foreach (var record in reader.ReadAll())
        {
            var at = header.StartedAt + record.Offset;
            var (side, source) = record.Kind switch
            {
                CaptureRecordKind.FromClient => (client, FrameSource.Client),
                CaptureRecordKind.FromServer => (server, FrameSource.Server),
                CaptureRecordKind.Injected => (new Side(), FrameSource.D3B),
                _ => (null, FrameSource.Client),
            };

            if (side is null)
                continue;

            foreach (var frame in side.Read(record.Data))
                frames.Add(new InspectedFrame(at, id, source, translation.DecodeGame(new ReadOnlySequence<byte>(frame))));
        }

        return new CapturedGame(header, frames, reader.IsTruncated);
    }

    /// <summary>One side's bytes, framed as they come.</summary>
    private sealed class Side
    {
        private byte[] _pending = [];
        private bool _broken;

        public List<byte[]> Read(ReadOnlyMemory<byte> data)
        {
            var frames = new List<byte[]>();

            if (_broken)
                return frames;

            var bytes = new byte[_pending.Length + data.Length];

            _pending.CopyTo(bytes, 0);
            data.CopyTo(bytes.AsMemory(_pending.Length));

            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));

            try
            {
                while (FrameReader.TryReadFrame(ref reader, out var frame))
                    frames.Add(frame.ToArray());
            }
            catch (FrameFormatException)
            {
                _broken = true;
            }

            _pending = bytes[(int)reader.Consumed..];

            return frames;
        }
    }
}
