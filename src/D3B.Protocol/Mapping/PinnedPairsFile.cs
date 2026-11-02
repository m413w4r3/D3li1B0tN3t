using Google.Protobuf;

namespace D3B.Protocol.Mapping;

/// <summary>The pinned pairs of client builds: one committed file per build, in protobuf's JSON form (D43).</summary>
public static class PinnedPairsFile
{
    /// <summary>Gets the path of the pins of a client build in a directory of pins.</summary>
    public static string PathOf(string directory, string clientVersion)
    {
        return Path.Combine(directory, clientVersion + ".json");
    }

    /// <summary>Reads the pins of a build, checking they name it.</summary>
    /// <exception cref="InvalidProtocolBufferException">The file is not pins.</exception>
    /// <exception cref="InvalidDataException">The pins name another build.</exception>
    public static PinnedPairs Read(string path, string clientVersion)
    {
        var pins = JsonParser.Default.Parse<PinnedPairs>(File.ReadAllText(path));

        if (pins.ClientVersion != clientVersion)
            throw new InvalidDataException($"{path} pins client {pins.ClientVersion}, not {clientVersion}.");

        return pins;
    }
}
