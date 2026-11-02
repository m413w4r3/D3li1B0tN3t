namespace D3B.App.Updates;

/// <summary>Where the app's update stands (D173).</summary>
internal enum UpdateStatus
{
    /// <summary>The app runs from its code, not installed: it does not update itself.</summary>
    NotInstalled,

    /// <summary>Installed, and not checked yet.</summary>
    Idle,

    /// <summary>Asks its releases whether a newer version is published.</summary>
    Checking,

    /// <summary>No newer version is published.</summary>
    UpToDate,

    /// <summary>Downloads the newer version found.</summary>
    Downloading,

    /// <summary>The newer version is downloaded: it installs when the app restarts or closes.</summary>
    Ready,

    /// <summary>The last check or download failed: <see cref="AppUpdates.Failure"/> says why.</summary>
    Failed,
}
