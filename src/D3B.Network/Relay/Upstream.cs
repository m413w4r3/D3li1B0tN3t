using System.Net;

namespace D3B.Network.Relay;

/// <summary>The real server behind a connection the relay accepted: its host's name, and where to reach it.</summary>
/// <param name="Host">The name the client asked for.</param>
/// <param name="EndPoints">The server's addresses, with the port, tried in order.</param>
internal sealed record Upstream(string Host, IReadOnlyList<IPEndPoint> EndPoints);
