namespace D3B.App.Updates;

/// <summary>What finds, downloads and installs the app's newer versions: Velopack's, or a test's own.</summary>
internal interface IUpdateClient
{
    /// <summary>Gets whether the app runs installed, which alone updates itself.</summary>
    bool IsInstalled { get; }

    /// <summary>Asks the releases for a version newer than the one running.</summary>
    /// <returns>The newer version, or <see langword="null"/> when none is published.</returns>
    Task<string?> CheckAsync();

    /// <summary>Downloads the version the last check found.</summary>
    /// <param name="progress">Told the share downloaded, from 0 to 100.</param>
    /// <returns>A task that completes once it is downloaded and checked.</returns>
    Task DownloadAsync(Action<int> progress);

    /// <summary>Ends the process, installs the version downloaded and starts the app again.</summary>
    void ApplyAndRestart();

    /// <summary>Has the version downloaded installed once the process has ended, without starting the app again.</summary>
    void ApplyOnExit();
}
