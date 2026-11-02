using D3B.App.Updates;

namespace D3B.App.Tests.Updates;

/// <summary>Releases of a test's own: the newer version they publish, if any, a check that fails, and what was installed.</summary>
internal sealed class FakeUpdateClient : IUpdateClient
{
    public bool IsInstalled { get; init; } = true;

    /// <summary>Gets or sets the newer version published, or <see langword="null"/> for none.</summary>
    public string? Published { get; set; }

    /// <summary>Gets or sets what a check throws, as the releases unreachable would.</summary>
    public Exception? CheckFails { get; set; }

    public int Checks { get; private set; }

    public bool Downloaded { get; private set; }

    public bool AppliedAndRestarted { get; private set; }

    public bool AppliedOnExit { get; private set; }

    public Task<string?> CheckAsync()
    {
        Checks++;

        return CheckFails is { } failure ? Task.FromException<string?>(failure) : Task.FromResult(Published);
    }

    public Task DownloadAsync(Action<int> progress)
    {
        progress(50);
        progress(100);
        Downloaded = true;

        return Task.CompletedTask;
    }

    public void ApplyAndRestart()
    {
        AppliedAndRestarted = true;
    }

    public void ApplyOnExit()
    {
        AppliedOnExit = true;
    }
}
