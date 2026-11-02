namespace D3B.App.ViewModels;

/// <summary>A module's page that failed, shown in its place: what its code threw (D96).</summary>
internal sealed class PageFailureViewModel
{
    /// <summary>Gets what went wrong: each exception's type and message, the inner ones after, in English as the host's own messages are (D94).</summary>
    public string Message { get; }

    /// <summary>Initializes a new instance of the <see cref="PageFailureViewModel"/> class.</summary>
    /// <param name="message">What went wrong.</param>
    public PageFailureViewModel(string message)
    {
        Message = message;
    }
}
