namespace D3B.App.Hosting;

/// <summary>Where the bots' host stands in the app's process (D91).</summary>
internal enum HostStatus
{
    /// <summary>Gets ready: builds its modules, finds the pack and the mapping, starts the relay.</summary>
    Starting,

    /// <summary>Runs: clients started from the launcher go through its relay, and their characters get bots.</summary>
    Running,

    /// <summary>Did not start: <see cref="HostRunner.Failure"/> says why.</summary>
    Failed,

    /// <summary>Stops its bots and its relay, and gives the clients their configuration back.</summary>
    Stopping,

    /// <summary>Stopped.</summary>
    Stopped,
}
