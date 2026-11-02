namespace D3B.App.ViewModels.Logs;

/// <summary>Whose lines the logs page shows.</summary>
internal enum LogSourceKind
{
    /// <summary>Every bot's and the host's, by time.</summary>
    All,

    /// <summary>The host's problems.</summary>
    Host,

    /// <summary>A bot's journal.</summary>
    Bot,
}
