using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;

namespace D3B.Network.Relay;

/// <summary>The server selections whose answer sends the client to D3B's game listener, each waiting for its game connection (3f).</summary>
/// <remarks>
/// <para>
/// Whoever rewrites a server selection's answer adds the real game server here with the ticket the answer gives, and lists
/// the ports it is given in place of the real ones: each stands for the real port at its position. A game connection is
/// matched with its selection by that ticket: the client speaks first on the game connection, and its first frame holds
/// it, measured (client.md). The ticket is looked for among the frame's values by the wire format alone, so no mapping of
/// the game is needed. Any number of clients may select a server at once.
/// </para>
/// <para>
/// A selection is taken once, and forgotten after <see cref="RelaySettings.SelectionLifetime"/>. It holds the ticket the
/// client presents to the game server: it is kept in memory only, never logged.
/// </para>
/// </remarks>
public sealed class GameServerSelections
{
    private readonly RelaySettings _settings;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Selection> _waiting = new(StringComparer.Ordinal);
    private volatile IReadOnlyList<int> _listened;

    /// <summary>Gets the address the client is sent to in place of the game server's host: D3B's.</summary>
    public IPAddress Address => _settings.Address;

    /// <summary>Initializes a new instance of the <see cref="GameServerSelections"/> class.</summary>
    /// <param name="settings">The relay's settings: D3B's address, the game listener's ports, and how long a selection waits.</param>
    /// <param name="time">The clock selections are timed by.</param>
    public GameServerSelections(RelaySettings settings, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);

        _settings = settings;
        _time = time;
        _listened = settings.GamePorts;
    }

    /// <summary>Remembers the game server a server selection's answer named, and gives the ports to list in its place.</summary>
    /// <param name="ticket">The ticket the answer gives the client, which its game connection presents.</param>
    /// <param name="host">The game server's host.</param>
    /// <param name="ports">The game server's ports, in the answer's order.</param>
    /// <returns>
    /// The game listener's ports, one per real port as far as the listener has them, each standing for the real port at
    /// its position; none when the ticket is empty, since no connection could be matched with the selection.
    /// </returns>
    public IReadOnlyList<int> Add(string ticket, string host, IReadOnlyList<int> ports)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(ports);

        if (ticket.Length is 0)
            return [];

        var listened = _listened.Take(ports.Count).ToList();
        var now = _time.GetUtcNow();

        lock (_lock)
        {
            Forget(now);
            _waiting[ticket] = new Selection(Encoding.UTF8.GetByteCount(ticket), host, [.. ports], listened, now);
        }

        return listened;
    }

    /// <summary>Learns the ports the game listener took, which differ from the settings' when these ask for port 0.</summary>
    /// <param name="ports">The ports.</param>
    internal void OnListening(IReadOnlyList<int> ports)
    {
        _listened = ports;
    }

    /// <summary>Takes the selection whose ticket a game connection's first frame presents, with the real port behind the one it chose.</summary>
    /// <param name="frame">The client's first frame, its length already read.</param>
    /// <param name="listenerPort">The game listener's port the client connected to.</param>
    /// <param name="host">The game server's host.</param>
    /// <param name="port">The game server's port standing behind <paramref name="listenerPort"/>.</param>
    /// <returns><see langword="true"/> when a selection waits for the ticket the frame holds.</returns>
    internal bool TryTake(ReadOnlySpan<byte> frame, int listenerPort, [NotNullWhen(true)] out string? host, out int port)
    {
        var values = ProtobufValues.LengthDelimited(frame);
        var now = _time.GetUtcNow();

        lock (_lock)
        {
            Forget(now);

            foreach (var (start, length) in values)
            {
                // Only a value as long as a ticket waiting is decoded: most of the frame's values are not.
                if (!_waiting.Values.Any(selection => selection.TicketLength == length))
                    continue;

                var ticket = Encoding.UTF8.GetString(frame.Slice(start, length));

                if (!_waiting.Remove(ticket, out var selection))
                    continue;

                var position = selection.ListenerPorts.IndexOf(listenerPort);

                host = selection.Host;
                port = selection.Ports[position < 0 ? 0 : position];

                return true;
            }
        }

        host = null;
        port = 0;

        return false;
    }

    private void Forget(DateTimeOffset now)
    {
        foreach (var ticket in _waiting.Where(pair => now - pair.Value.SelectedAt > _settings.SelectionLifetime).Select(pair => pair.Key).ToList())
            _waiting.Remove(ticket);
    }

    private sealed class Selection
    {
        public int TicketLength { get; }

        public string Host { get; }

        public List<int> Ports { get; }

        public List<int> ListenerPorts { get; }

        public DateTimeOffset SelectedAt { get; }

        public Selection(int ticketLength, string host, List<int> ports, List<int> listenerPorts, DateTimeOffset selectedAt)
        {
            TicketLength = ticketLength;
            Host = host;
            Ports = ports;
            ListenerPorts = listenerPorts;
            SelectedAt = selectedAt;
        }
    }
}
