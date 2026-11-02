namespace D3B.Network.Relay;

/// <summary>Which of a client's two connections a relayed connection is: the connection phase, or the game.</summary>
public enum RelayRoute
{
    /// <summary>The connection phase, with the connection host the client's configuration names (D35).</summary>
    Connection,

    /// <summary>The game, with the game server the connection phase's server selection named (3f).</summary>
    Game,
}
