namespace D3B.App.ViewModels;

/// <summary>What the app says briefly in a corner of the window: what was just done, and what a bot met, with a way to it (D159).</summary>
/// <remarks>
/// The window shows each through ShadUI's toasts; the view models only say it, so that they are tested without a window.
/// The last ones are kept, a few, for the tests to read.
/// </remarks>
internal sealed class Toasts
{
    private const int Kept = 20;

    private readonly List<Toast> _recent = [];

    /// <summary>Gets the toasts said last, the latest last.</summary>
    public IReadOnlyList<Toast> Recent => _recent;

    /// <summary>Occurs when a toast is to show.</summary>
    public event Action<Toast>? Shown;

    /// <summary>Says a toast.</summary>
    /// <param name="toast">What to say.</param>
    public void Show(Toast toast)
    {
        ArgumentNullException.ThrowIfNull(toast);

        if (_recent.Count == Kept)
            _recent.RemoveAt(0);

        _recent.Add(toast);
        Shown?.Invoke(toast);
    }
}
