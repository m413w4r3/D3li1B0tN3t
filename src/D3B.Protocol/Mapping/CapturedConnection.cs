namespace D3B.Protocol.Mapping;

/// <summary>The frames of one captured connection, each side in the order it sent them (D37, D38).</summary>
public sealed class CapturedConnection
{
    /// <summary>Gets the frames the client sent.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> ClientFrames { get; }

    /// <summary>Gets the frames the server sent.</summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> ServerFrames { get; }

    /// <summary>Initializes a new instance of the <see cref="CapturedConnection"/> class.</summary>
    /// <param name="clientFrames">The frames the client sent, in order.</param>
    /// <param name="serverFrames">The frames the server sent, in order.</param>
    public CapturedConnection(IReadOnlyList<ReadOnlyMemory<byte>> clientFrames, IReadOnlyList<ReadOnlyMemory<byte>> serverFrames)
    {
        ArgumentNullException.ThrowIfNull(clientFrames);
        ArgumentNullException.ThrowIfNull(serverFrames);

        ClientFrames = clientFrames;
        ServerFrames = serverFrames;
    }
}
