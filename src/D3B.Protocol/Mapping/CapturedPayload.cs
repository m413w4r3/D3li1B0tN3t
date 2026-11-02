using Google.Protobuf.Reflection;

namespace D3B.Protocol.Mapping;

/// <summary>A message of a build's schema that captures carry in an <c>Any</c>, and how many frames of each side carry it.</summary>
public sealed class CapturedPayload
{
    /// <summary>Gets the obfuscated message the type URLs name.</summary>
    public MessageDescriptor Message { get; }

    /// <summary>Gets the number of the client's frames carrying it.</summary>
    public int FromClient { get; }

    /// <summary>Gets the number of the server's frames carrying it.</summary>
    public int FromServer { get; }

    internal CapturedPayload(MessageDescriptor message, int fromClient, int fromServer)
    {
        ArgumentNullException.ThrowIfNull(message);

        Message = message;
        FromClient = fromClient;
        FromServer = fromServer;
    }
}
