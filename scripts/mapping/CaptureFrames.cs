using System.Buffers;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Protocol.Mapping;

namespace D3B.Scripts.Mapping;

/// <summary>Reads the frames of the captures the host recorded of a build's connections (D37, D38).</summary>
/// <remarks>
/// The host records the game connection in the <c>game</c> directory of its captures, apart from the connection phase
/// (D53): a directory's own files are read as the connection phase's, its <c>game</c> directory's as the game's, and a
/// file named alone by the directory it is in.
/// </remarks>
internal static class CaptureFrames
{
    private const string GameDirectory = "game";

    /// <summary>Reads the captures of a build, both sides of each, framed.</summary>
    /// <param name="paths">Directories of captures as the host writes them, or capture files.</param>
    /// <param name="clientVersion">The build whose captures to read; the others are skipped.</param>
    /// <param name="skipped">The number of captures of other builds, or that could not be read.</param>
    /// <returns>The connection phase's captured connections, and the game's.</returns>
    public static (List<CapturedConnection> Connection, List<CapturedConnection> Game) Read(IEnumerable<string> paths, string clientVersion, out int skipped)
    {
        var connection = new List<CapturedConnection>();
        var game = new List<CapturedConnection>();

        skipped = 0;

        foreach (var (file, isGame) in paths.SelectMany(Files))
        {
            try
            {
                using var reader = CaptureReader.Open(file);

                var records = reader.ReadAll().ToList();

                if (reader.Header.ClientVersion != clientVersion)
                {
                    skipped++;

                    continue;
                }

                (isGame ? game : connection).Add(new CapturedConnection(Frames(records, CaptureRecordKind.FromClient), Frames(records, CaptureRecordKind.FromServer)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FrameFormatException)
            {
                skipped++;
            }
        }

        return (connection, game);
    }

    private static IEnumerable<(string File, bool IsGame)> Files(string path)
    {
        if (Directory.Exists(path))
        {
            var game = Path.Combine(path, GameDirectory);
            var games = Directory.Exists(game) ? Captures(game).Select(file => (file, true)) : [];

            return Captures(path).Select(file => (file, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) == GameDirectory)).Concat(games);
        }

        return File.Exists(path) ? [(path, Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path))) == GameDirectory)] : [];
    }

    private static IEnumerable<string> Captures(string directory)
    {
        return Directory.GetFiles(directory, "*" + CaptureWriter.FileExtension).Order(StringComparer.Ordinal);
    }

    private static List<ReadOnlyMemory<byte>> Frames(List<CaptureRecord> records, CaptureRecordKind kind)
    {
        var bytes = records.Where(record => record.Kind == kind).SelectMany(record => record.Data.ToArray()).ToArray();
        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(bytes));
        var frames = new List<ReadOnlyMemory<byte>>();

        while (FrameReader.TryReadFrame(ref reader, out var frame))
            frames.Add(frame.ToArray());

        return frames;
    }
}
