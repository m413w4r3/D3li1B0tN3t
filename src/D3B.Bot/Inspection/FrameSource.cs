namespace D3B.Bot.Inspection;

/// <summary>Who sent a frame of the game connection.</summary>
public enum FrameSource
{
    /// <summary>The client: its requests, the player's and those it sends after an action.</summary>
    Client,

    /// <summary>The server: its events and responses.</summary>
    Server,

    /// <summary>D3B itself, to the server, between the client's frames (D61).</summary>
    D3B,
}
