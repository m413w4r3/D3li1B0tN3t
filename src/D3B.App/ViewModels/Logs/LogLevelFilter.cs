namespace D3B.App.ViewModels.Logs;

/// <summary>Which lines the logs page shows, by their tone (D111).</summary>
internal enum LogLevelFilter
{
    /// <summary>Every line.</summary>
    All,

    /// <summary>Warnings and errors: an action that did not end as asked, a problem of the host.</summary>
    Problems,

    /// <summary>Errors alone: an activity that failed, an error of the host.</summary>
    Errors,
}
