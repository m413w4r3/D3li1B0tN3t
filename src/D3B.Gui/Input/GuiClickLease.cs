namespace D3B.Gui.Input;

/// <summary>Linearizes actor revocation against the start of one GUI click command.</summary>
public sealed class GuiClickLease
{
    private readonly Lock _sync = new();
    private bool _cancelled;
    private bool _launchStarted;

    /// <summary>Gets whether a revocation has won the lease lock.</summary>
    public bool IsCancelled
    {
        get
        {
            lock (_sync)
                return _cancelled;
        }
    }

    /// <summary>Gets whether the click launch gate has won and input may have been emitted.</summary>
    public bool LaunchStarted
    {
        get
        {
            lock (_sync)
                return _launchStarted;
        }
    }

    /// <summary>Tries to claim the single click launch point; a prior cancellation or claim refuses it.</summary>
    public bool TryBeginClick()
    {
        lock (_sync)
        {
            if (_cancelled || _launchStarted)
                return false;

            _launchStarted = true;
            return true;
        }
    }

    /// <summary>Revokes a click that has not won the launch point.</summary>
    public void Cancel()
    {
        lock (_sync)
            _cancelled = true;
    }
}
